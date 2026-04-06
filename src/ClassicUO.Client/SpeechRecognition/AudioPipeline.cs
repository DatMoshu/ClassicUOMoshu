// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Diagnostics;
using ClassicUO.SpeechRecognition.Engines;
using ClassicUO.SpeechRecognition.Interfaces;
using NAudio.Wave;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Owns the NAudio microphone input and routes audio to the configured STT engine.
    ///
    /// Two modes (chosen automatically based on engine type):
    ///   • Streaming (Vosk): feed bytes directly → engine fires events
    ///   • VAD-gated batch (Whisper): accumulate speech frames → TranscribeAsync when speech ends
    ///
    /// In VAD-gated mode, a SileroVadEngine (or EnergyVadFallback) gates transcription
    /// so Whisper only processes real utterances, not silence.
    /// </summary>
    internal sealed class AudioPipeline : IDisposable
    {
        private readonly ISttEngine _sttEngine;
        private readonly IVadEngine _vadEngine; // null in streaming mode
        private WaveInEvent _waveIn;
        private bool _disposed;

        // Audio debug logging
        private int  _audioChunkCount;
        private long _audioRmsAccum;

        // Capture format (device native) vs STT format (16kHz mono)
        private int  _captureChannels = 2;
        private int  _captureRate = 48000;
        private int  _sttRate = 16000;

        // RMS gate — chunks below this threshold are treated as silence and not fed to STT.
        // Prevents Vosk hallucinations from near-silent noise (e.g. RMS=1 from idle mic).
        // Value tuned for 16-bit PCM; >500 is speech, <50 is noise floor.
        private const double MIN_SPEECH_RMS = 50.0;

        // VAD-gated accumulation buffer
        private readonly List<float> _speechBuffer = new List<float>(16000 * 10); // up to 10s
        private bool _isAccumulating;
        private CancellationTokenSource _transcribeCts;

        /// <summary>Fired (on the NAudio or ThreadPool thread) when STT produces a partial result.</summary>
        public event EventHandler<SttResult> PartialResultAvailable;

        /// <summary>Fired (on the NAudio or ThreadPool thread) when STT produces a final result.</summary>
        public event EventHandler<SttResult> FinalResultAvailable;

        // Optional barge-in controller — receives audio frames while TTS is playing
        private BargeInController _bargeIn;

        /// <summary>Register a barge-in controller that receives frames during TTS playback.</summary>
        public void SetBargeInController(BargeInController bargeIn) => _bargeIn = bargeIn;

        /// <param name="sttEngine">STT engine to use.</param>
        /// <param name="vadEngine">
        ///   Optional VAD engine. If provided, enables VAD-gated batch transcription (Whisper mode).
        ///   If null, audio is fed directly to the STT engine (streaming mode for Vosk).
        /// </param>
        public AudioPipeline(ISttEngine sttEngine, IVadEngine vadEngine = null)
        {
            _sttEngine = sttEngine ?? throw new ArgumentNullException(nameof(sttEngine));
            _vadEngine = vadEngine;

            // In streaming mode, forward engine events directly
            if (_vadEngine == null)
            {
                _sttEngine.PartialResultAvailable += (s, e) => PartialResultAvailable?.Invoke(this, e);
                _sttEngine.FinalResultAvailable += (s, e) => FinalResultAvailable?.Invoke(this, e);
            }

            // Wire VAD events for batch mode
            if (_vadEngine != null)
            {
                _vadEngine.SpeechStarted += OnVadSpeechStarted;
                _vadEngine.SpeechEnded += OnVadSpeechEnded;
            }
        }

        /// <summary>Initialize NAudio microphone capture.</summary>
        /// <param name="sttSampleRate">STT engine sample rate in Hz (e.g. 16000).</param>
        /// <param name="deviceNumber">WaveIn device index. -1 = OS default (WAVE_MAPPER).</param>
        /// <param name="captureRate">Mic capture rate in Hz (e.g. 48000 for Scarlett Solo).</param>
        /// <param name="captureChannels">Mic capture channels (e.g. 2 for stereo interfaces).</param>
        public void Initialize(float sttSampleRate, int deviceNumber = -1, int captureRate = 48000, int captureChannels = 2)
        {
            _sttRate = (int)sttSampleRate;
            _captureRate = captureRate;
            _captureChannels = captureChannels;

            // Log all available capture devices
            int deviceCount = WaveInEvent.DeviceCount;
            SpeechLog.Debug(SpeechLogChannel.Audio, $"Available capture devices ({deviceCount} total):");
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                SpeechLog.Debug(SpeechLogChannel.Audio, $"  [{i}] {caps.ProductName} (ch={caps.Channels})");
            }

            // Capture at the device's native rate and channel count.
            // Pro audio interfaces (e.g. Scarlett Solo) produce silence when asked for
            // 16kHz mono — they only support their native rate (typically 44.1/48kHz stereo).
            // We resample + downmix to STT rate in OnDataAvailable.
            _waveIn = new WaveInEvent
            {
                WaveFormat   = new WaveFormat(_captureRate, 16, _captureChannels),
                DeviceNumber = deviceNumber
            };

            string selectedName = deviceNumber >= 0 && deviceNumber < deviceCount
                ? WaveInEvent.GetCapabilities(deviceNumber).ProductName
                : "OS default (WAVE_MAPPER)";
            SpeechLog.Info(SpeechLogChannel.Audio, $"Using device [{deviceNumber}]: {selectedName} ({_captureChannels}ch 16-bit PCM at {_captureRate}Hz → STT at {_sttRate}Hz mono)");

            _waveIn.DataAvailable += OnDataAvailable;
        }

        public void Start()
        {
            if (_waveIn == null) throw new InvalidOperationException("Call Initialize first.");
            _vadEngine?.Reset();
            _waveIn.StartRecording();
        }

        public void Stop()
        {
            _waveIn?.StopRecording();
        }

        // ── NAudio callback (fast path — no blocking) ─────────────────────────

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_sttEngine.IsLoaded) return;
            if (e.BytesRecorded <= 0) return;

            // ── Step 1: Downmix to mono float at capture rate ────────────────
            int bytesPerSample = 2 * _captureChannels;
            int frameCount = e.BytesRecorded / bytesPerSample;
            var monoFloat = new float[frameCount];

            if (_captureChannels >= 2)
            {
                for (int i = 0; i < frameCount; i++)
                {
                    short l = (short)(e.Buffer[i * bytesPerSample]     | (e.Buffer[i * bytesPerSample + 1] << 8));
                    short r = (short)(e.Buffer[i * bytesPerSample + 2] | (e.Buffer[i * bytesPerSample + 3] << 8));
                    monoFloat[i] = (l + r) / 65536f;
                }
            }
            else
            {
                for (int i = 0; i < frameCount; i++)
                {
                    short s = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                    monoFloat[i] = s / 32768f;
                }
            }

            // ── Step 2: Resample to STT rate (e.g. 48kHz → 16kHz) ───────────
            float[] sttFloat;
            if (_captureRate != _sttRate)
            {
                int ratio = _captureRate / _sttRate; // e.g. 3 for 48k→16k
                int outLen = frameCount / ratio;
                sttFloat = new float[outLen];
                for (int i = 0; i < outLen; i++)
                {
                    float sum = 0f;
                    for (int j = 0; j < ratio; j++)
                        sum += monoFloat[i * ratio + j];
                    sttFloat[i] = sum / ratio;
                }
            }
            else
            {
                sttFloat = monoFloat;
            }

            // ── Step 3: Convert to 16-bit PCM bytes for Vosk ─────────────────
            int monoBytes = sttFloat.Length * 2;
            var monoBuffer = new byte[monoBytes];
            for (int i = 0; i < sttFloat.Length; i++)
            {
                short s = (short)(Math.Clamp(sttFloat[i], -1f, 1f) * 32767f);
                monoBuffer[i * 2]     = (byte)(s & 0xFF);
                monoBuffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            // ── RMS — compute per-chunk for gate, log every 20 chunks ──────────
            double chunkRms;
            {
                int samples = monoBytes / 2;
                long rmsSum = 0;
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(monoBuffer[i * 2] | (monoBuffer[i * 2 + 1] << 8));
                    rmsSum += (long)s * s;
                }
                long perSample = samples > 0 ? rmsSum / samples : 0;
                chunkRms = Math.Sqrt((double)perSample);

                _audioRmsAccum += perSample;
                _audioChunkCount++;
                if (_audioChunkCount >= 20)
                {
                    double avgRms = Math.Sqrt((double)_audioRmsAccum / _audioChunkCount);
                    SpeechLog.Trace(SpeechLogChannel.Audio, $"chunks=20 avgRMS={avgRms:F0} (0=silence, >500=speech)");
                    _audioChunkCount = 0;
                    _audioRmsAccum   = 0;
                }
            }

            if (_vadEngine == null)
            {
                // Streaming mode: feed all audio to Vosk continuously.
                // Vosk is a streaming HMM — it requires uninterrupted audio to maintain
                // acoustic state and detect utterance boundaries via internal silence detection.
                _sttEngine.FeedAudio(monoBuffer, monoBytes);
                if (_bargeIn != null)
                    FeedBargeIn(monoBuffer, monoBytes);
            }
            else
            {
                // VAD batch mode: gate on RMS to avoid feeding silence to Whisper.
                if (chunkRms < MIN_SPEECH_RMS) return;
                ProcessVadFrame(monoBuffer, monoBytes);
            }
        }

        private void FeedBargeIn(byte[] buffer, int bytesRecorded)
        {
            int sampleCount = bytesRecorded / 2;
            int offset = 0;
            while (offset < sampleCount)
            {
                int chunk = Math.Min(SileroVadEngine.FrameSize, sampleCount - offset);
                var frame = new float[chunk];
                for (int i = 0; i < chunk; i++)
                {
                    int idx = (offset + i) * 2;
                    short s = (short)(buffer[idx] | (buffer[idx + 1] << 8));
                    frame[i] = s / 32768f;
                }
                _bargeIn.ProcessFrame(frame);
                offset += chunk;
            }
        }

        private void ProcessVadFrame(byte[] buffer, int bytesRecorded)
        {
            // Convert 16-bit PCM to float
            int sampleCount = bytesRecorded / 2;
            Span<float> floats = stackalloc float[Math.Min(sampleCount, 4096)];
            int processed = 0;

            while (processed < sampleCount)
            {
                int chunk = Math.Min(SileroVadEngine.FrameSize, sampleCount - processed);
                var frame = new float[chunk];

                for (int i = 0; i < chunk; i++)
                {
                    int idx = (processed + i) * 2;
                    short sample = (short)(buffer[idx] | (buffer[idx + 1] << 8));
                    frame[i] = sample / 32768f;
                }

                _vadEngine.ProcessFrame(frame);

                if (_isAccumulating)
                    _speechBuffer.AddRange(frame);

                // Always feed barge-in regardless of accumulation state
                if (_bargeIn != null && chunk == SileroVadEngine.FrameSize)
                    _bargeIn.ProcessFrame(frame);

                processed += chunk;
            }
        }

        // ── VAD events ────────────────────────────────────────────────────────

        private void OnVadSpeechStarted(object sender, EventArgs e)
        {
            _isAccumulating = true;
            _speechBuffer.Clear();
        }

        private void OnVadSpeechEnded(object sender, EventArgs e)
        {
            _isAccumulating = false;
            var audioClip = _speechBuffer.ToArray();
            _speechBuffer.Clear();

            if (audioClip.Length < 3200) return; // < 200ms — ignore noise burst

            // Cancel any previous pending transcription and start a new one
            _transcribeCts?.Cancel();
            _transcribeCts = new CancellationTokenSource();
            var ct = _transcribeCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _sttEngine.TranscribeAsync(audioClip, ct);
                    if (!ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(result.Text))
                        FinalResultAvailable?.Invoke(this, result);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { SpeechLog.Error(SpeechLogChannel.Stt, $"Transcription error: {ex.Message}"); }
            }, ct);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transcribeCts?.Cancel();
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _sttEngine?.Dispose();
            _vadEngine?.Dispose();
        }
    }
}
