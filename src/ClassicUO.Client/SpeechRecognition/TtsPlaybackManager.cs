// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using NAudio.Wave;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Queues synthesized audio chunks and plays them back via NAudio.
    /// Supports immediate cancellation for barge-in.
    /// Thread-safe — synthesize and play can run concurrently.
    /// </summary>
    internal sealed class TtsPlaybackManager : IDisposable
    {
        private readonly ITtsEngine _ttsEngine;
        private readonly ConcurrentQueue<float[]> _audioQueue = new ConcurrentQueue<float[]>();
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _buffer;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private volatile bool _isPlaying;
        private bool _disposed;

        public bool IsPlaying => _isPlaying;

        /// <summary>Fired when all queued audio has finished playing.</summary>
        public event EventHandler PlaybackComplete;

        public TtsPlaybackManager(ITtsEngine ttsEngine, float volumeScale = 0.8f)
        {
            _ttsEngine = ttsEngine;

            // NAudio WaveOut for audio playback
            _waveOut = new WaveOutEvent { Volume = volumeScale };
            _buffer = new BufferedWaveProvider(
                new WaveFormat(_ttsEngine.SampleRate, 32, 1) // 32-bit float, mono
            )
            {
                BufferDuration = TimeSpan.FromSeconds(30),
                DiscardOnBufferOverflow = false
            };
            _waveOut.Init(_buffer);
        }

        /// <summary>
        /// Synthesize text and queue for playback.
        /// Called as LLM tokens stream in — sentence by sentence.
        /// </summary>
        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            if (!_ttsEngine.IsLoaded || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                float[] pcm = await _ttsEngine.SynthesizeAsync(text, ct);
                if (ct.IsCancellationRequested || pcm.Length == 0) return;

                _audioQueue.Enqueue(pcm);
                if (!_isPlaying)
                    _ = Task.Run(() => DrainQueue(), _cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Immediately stop playback, clear queue, and prepare for new speech.
        /// Called by BargeInController when user interrupts.
        /// </summary>
        public void Cancel()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            _waveOut.Stop();
            _buffer.ClearBuffer();

            while (_audioQueue.TryDequeue(out _)) { } // drain queue

            _isPlaying = false;
        }

        private void DrainQueue()
        {
            _isPlaying = true;
            try
            {
                var ct = _cts.Token;
                while (_audioQueue.TryDequeue(out float[] chunk) && !ct.IsCancellationRequested)
                {
                    // Convert float[] to 32-bit IEEE float PCM bytes
                    byte[] bytes = new byte[chunk.Length * 4];
                    Buffer.BlockCopy(chunk, 0, bytes, 0, bytes.Length);
                    _buffer.AddSamples(bytes, 0, bytes.Length);
                }

                if (!_cts.Token.IsCancellationRequested)
                {
                    _waveOut.Play();
                    // Wait for playback to drain
                    while (_waveOut.PlaybackState == PlaybackState.Playing && !_cts.Token.IsCancellationRequested)
                        Thread.Sleep(20);
                }
            }
            finally
            {
                _isPlaying = false;
                PlaybackComplete?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetVolume(float volume)
        {
            if (_waveOut != null) _waveOut.Volume = Math.Clamp(volume, 0f, 1f);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _ttsEngine?.Dispose();
        }
    }
}
