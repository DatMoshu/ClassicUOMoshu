// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using Whisper.net;
using Whisper.net.Ggml;

namespace ClassicUO.SpeechRecognition.Engines
{
    /// <summary>
    /// Batch STT engine backed by Whisper.net (whisper.cpp C# bindings).
    /// Higher accuracy than Vosk; works on CPU (base model ~1s latency) or GPU.
    /// Pair with SileroVadEngine to gate transcription to speech segments only.
    /// </summary>
    internal sealed class WhisperSttEngine : ISttEngine
    {
        private WhisperFactory _factory;
        private WhisperProcessor _processor;
        private float _sampleRate;
        private bool _disposed;

        public bool IsLoaded => _processor != null;

        public event EventHandler<SttResult> PartialResultAvailable;
        public event EventHandler<SttResult> FinalResultAvailable;

        /// <summary>
        /// Load a GGUF/GGML whisper model from disk.
        /// Uses CPU by default; set WHISPER_USE_CUDA=1 env var for GPU.
        /// </summary>
        public async Task LoadModelAsync(string modelPath, float sampleRate)
        {
            _sampleRate = sampleRate;

            await Task.Run(() =>
            {
                _factory = WhisperFactory.FromPath(modelPath);
                _processor = _factory.CreateBuilder()
                    .WithLanguage("en")
                    .WithNoContext()
                    .WithSingleSegment()
                    .Build();
            });
        }

        /// <summary>
        /// Whisper is a batch encoder-decoder — streaming is not native.
        /// VAD should gate audio so only complete utterances reach this engine.
        /// This no-op satisfies the ISttEngine interface for AudioPipeline compatibility.
        /// </summary>
        public void FeedAudio(byte[] buffer, int bytesRecorded)
        {
            // Whisper uses TranscribeAsync with accumulated PCM — not the streaming FeedAudio path.
            // The VAD-gated AudioPipeline will call TranscribeAsync when speech ends.
        }

        /// <summary>
        /// Transcribe an accumulated float[] PCM clip.
        /// Called by the AudioPipeline when VAD signals speech has ended.
        /// </summary>
        public async Task<SttResult> TranscribeAsync(float[] pcmAudio, CancellationToken ct = default)
        {
            if (!IsLoaded) return default;
            if (pcmAudio == null || pcmAudio.Length == 0) return default;

            // Whisper expects 16kHz mono float[] — resample if needed
            var samples = _sampleRate == 16000f ? pcmAudio : Resample(pcmAudio, _sampleRate, 16000f);

            string fullText = null;
            await foreach (var segment in _processor.ProcessAsync(samples, ct))
            {
                fullText = (fullText ?? string.Empty) + segment.Text;
            }

            if (string.IsNullOrWhiteSpace(fullText)) return default;

            var result = new SttResult(fullText.Trim(), 1.0f, isFinal: true);
            FinalResultAvailable?.Invoke(this, result);
            return result;
        }

        private static float[] Resample(float[] input, float fromRate, float toRate)
        {
            double ratio = toRate / fromRate;
            int outLen = (int)(input.Length * ratio);
            var output = new float[outLen];
            for (int i = 0; i < outLen; i++)
            {
                double srcPos = i / ratio;
                int lo = (int)srcPos;
                int hi = Math.Min(lo + 1, input.Length - 1);
                double frac = srcPos - lo;
                output[i] = (float)(input[lo] * (1.0 - frac) + input[hi] * frac);
            }
            return output;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _processor?.Dispose();
            _factory?.Dispose();
        }
    }
}
