// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClassicUO.SpeechRecognition.Engines
{
    /// <summary>
    /// Text-to-speech engine backed by Kokoro TTS (ONNX, Apache 2.0, ~82MB).
    /// Near state-of-the-art quality. 24kHz mono output. Runs well on CPU.
    ///
    /// Model download: https://huggingface.co/hexgrad/Kokoro-82M
    /// Place at: Models/Voice/kokoro-tts.onnx
    /// Voice embeddings: Models/Voice/kokoro-voices/{voice_id}.bin
    ///
    /// Supported voices: af_heart, af_bella, am_adam, bf_emma, bm_george, ...
    /// </summary>
    internal sealed class KokoroTtsEngine : ITtsEngine
    {
        private InferenceSession _session;
        private float[] _voiceEmbedding;
        private bool _disposed;

        public bool IsLoaded => _session != null && _voiceEmbedding != null;
        public int SampleRate => 24000;

        private readonly float _speed;

        public KokoroTtsEngine(float speed = 1.0f)
        {
            _speed = speed;
        }

        /// <summary>Load the Kokoro ONNX model and default voice embedding.</summary>
        public async Task LoadModelAsync(string modelPath)
        {
            await Task.Run(() =>
            {
                var opts = new SessionOptions();
                opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _session = new InferenceSession(modelPath, opts);
            });
        }

        /// <summary>Load a voice embedding file (binary float32 array).</summary>
        public async Task LoadVoiceAsync(string voicePath)
        {
            await Task.Run(() =>
            {
                byte[] bytes = System.IO.File.ReadAllBytes(voicePath);
                _voiceEmbedding = new float[bytes.Length / 4];
                for (int i = 0; i < _voiceEmbedding.Length; i++)
                    _voiceEmbedding[i] = BitConverter.ToSingle(bytes, i * 4);
            });
        }

        /// <summary>
        /// Synthesize text to PCM float audio at 24kHz.
        /// Text should be a single sentence (≤200 chars) for best latency.
        /// The caller (TtsPlaybackManager) queues and plays multiple sentences.
        /// </summary>
        public async Task<float[]> SynthesizeAsync(string text, CancellationToken ct = default)
        {
            if (!IsLoaded) return Array.Empty<float>();
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

            // Phonemize: simple character tokenization (placeholder — production should use espeak-ng)
            int[] tokens = SimpleTokenize(text);
            if (tokens.Length == 0) return Array.Empty<float>();

            return await Task.Run(() => RunInference(tokens, ct), ct);
        }

        private float[] RunInference(int[] tokens, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return Array.Empty<float>();

            // Token tensor: [1, token_len] int64
            var tokenTensor = new DenseTensor<long>(
                Array.ConvertAll(tokens, t => (long)t),
                new[] { 1, tokens.Length });

            // Style tensor (voice embedding): [1, 1, 256] float32
            // Kokoro expects voice_embedding of shape [1, 1, 256]
            int embDim = Math.Min(_voiceEmbedding.Length, 256);
            var styleData = new float[256];
            Array.Copy(_voiceEmbedding, styleData, embDim);
            var styleTensor = new DenseTensor<float>(styleData, new[] { 1, 1, 256 });

            // Speed tensor: [1] float32
            var speedTensor = new DenseTensor<float>(new[] { _speed }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("tokens", tokenTensor),
                NamedOnnxValue.CreateFromTensor("style", styleTensor),
                NamedOnnxValue.CreateFromTensor("speed", speedTensor),
            };

            using var outputs = _session.Run(inputs);

            foreach (var output in outputs)
            {
                if (output.Name == "audio" || output.Name == "waveform")
                {
                    var audioTensor = output.AsTensor<float>();
                    var result = new float[audioTensor.Length];
                    for (int i = 0; i < result.Length; i++)
                        result[i] = audioTensor[i];
                    return result;
                }
            }

            return Array.Empty<float>();
        }

        /// <summary>
        /// Simple ASCII → token ID mapping (placeholder).
        /// Production implementation should use espeak-ng phonemization.
        /// This is sufficient for basic testing until full phonemizer is integrated.
        /// </summary>
        private static int[] SimpleTokenize(string text)
        {
            // Basic: map each character to its ASCII value, skip non-printable
            var tokens = new List<int> { 0 }; // start token
            foreach (char c in text.ToLowerInvariant())
            {
                if (c >= 32 && c < 127)
                    tokens.Add(c);
            }
            tokens.Add(0); // end token
            return tokens.ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session?.Dispose();
        }
    }
}
