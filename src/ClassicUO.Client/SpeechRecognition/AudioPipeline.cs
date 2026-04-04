// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>Initialize NAudio microphone at the given sample rate.</summary>
        public void Initialize(float sampleRate)
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat((int)sampleRate, 1)
            };
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

            if (_vadEngine == null)
            {
                // Streaming mode: feed bytes directly to Vosk
                _sttEngine.FeedAudio(e.Buffer, e.BytesRecorded);
                // Still feed barge-in if active (convert bytes for VAD)
                if (_bargeIn != null)
                    FeedBargeIn(e.Buffer, e.BytesRecorded);
            }
            else
            {
                // VAD-gated batch mode: convert to float, run VAD, accumulate if speaking
                ProcessVadFrame(e.Buffer, e.BytesRecorded);
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
                catch (Exception ex) { Console.WriteLine($"[STT] Transcription error: {ex.Message}"); }
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
