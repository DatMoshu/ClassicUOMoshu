// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClassicUO.SpeechRecognition.Engines
{
    /// <summary>
    /// Voice activity detection using Silero VAD v5 (ONNX).
    /// ~2MB model, Apache 2.0. Runs on CPU at &lt;1ms per 32ms frame.
    ///
    /// Download model: https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx
    /// Place at: Models/Voice/silero-vad.onnx
    /// </summary>
    internal sealed class SileroVadEngine : IVadEngine
    {
        private InferenceSession _session;
        private bool _disposed;

        // Silero VAD hidden state — must persist across frames (stateful RNN)
        private float[] _h;
        private float[] _c;

        // VAD state machine
        private readonly float _threshold;
        private readonly int _minSpeechFrames;   // consecutive frames above threshold
        private readonly int _silenceFrames;     // consecutive frames below threshold to end speech
        private int _speechFrameCount;
        private int _silenceFrameCount;
        private bool _isSpeaking;

        // Silero VAD expects exactly 512 samples (32ms at 16kHz)
        public const int FrameSize = 512;
        public const int SampleRate = 16000;

        public bool IsLoaded => _session != null;

        public event EventHandler SpeechStarted;
        public event EventHandler SpeechEnded;

        public SileroVadEngine(float threshold = 0.5f, int minSpeechMs = 250, int silenceMs = 700)
        {
            _threshold = threshold;
            _minSpeechFrames = (int)Math.Ceiling(minSpeechMs / 32.0); // 32ms per frame
            _silenceFrames = (int)Math.Ceiling(silenceMs / 32.0);
            Reset();
        }

        public Task LoadModelAsync(string modelPath)
        {
            return Task.Run(() =>
            {
                var opts = new SessionOptions();
                opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _session = new InferenceSession(modelPath, opts);
                Reset();
            });
        }

        /// <summary>
        /// Process one 32ms frame (512 samples at 16kHz).
        /// Returns speech probability 0.0–1.0 and fires SpeechStarted/SpeechEnded.
        /// </summary>
        public float ProcessFrame(float[] frame)
        {
            if (!IsLoaded || frame.Length != FrameSize) return 0f;

            float prob = RunInference(frame);

            if (prob >= _threshold)
            {
                _silenceFrameCount = 0;
                _speechFrameCount++;

                if (!_isSpeaking && _speechFrameCount >= _minSpeechFrames)
                {
                    _isSpeaking = true;
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _speechFrameCount = 0;
                if (_isSpeaking)
                {
                    _silenceFrameCount++;
                    if (_silenceFrameCount >= _silenceFrames)
                    {
                        _isSpeaking = false;
                        _silenceFrameCount = 0;
                        SpeechEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            return prob;
        }

        private float RunInference(float[] frame)
        {
            // Input tensor: [1, 512] float32
            var inputTensor = new DenseTensor<float>(frame, new[] { 1, FrameSize });

            // Sample rate tensor: [1] int64
            var srTensor = new DenseTensor<long>(new long[] { SampleRate }, new[] { 1 });

            // Hidden state tensors: [2, 1, 64] float32
            var hTensor = new DenseTensor<float>(_h, new[] { 2, 1, 64 });
            var cTensor = new DenseTensor<float>(_c, new[] { 2, 1, 64 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor),
                NamedOnnxValue.CreateFromTensor("h", hTensor),
                NamedOnnxValue.CreateFromTensor("c", cTensor),
            };

            using var results = _session.Run(inputs);

            float prob = 0f;
            foreach (var result in results)
            {
                if (result.Name == "output")
                    prob = result.AsTensor<float>()[0];
                else if (result.Name == "hn")
                {
                    var hn = result.AsTensor<float>();
                    for (int i = 0; i < _h.Length; i++) _h[i] = hn[i];
                }
                else if (result.Name == "cn")
                {
                    var cn = result.AsTensor<float>();
                    for (int i = 0; i < _c.Length; i++) _c[i] = cn[i];
                }
            }

            return prob;
        }

        public void Reset()
        {
            // Silero VAD v5: hidden state is [2, 1, 64] = 128 floats each
            _h = new float[2 * 1 * 64];
            _c = new float[2 * 1 * 64];
            _speechFrameCount = 0;
            _silenceFrameCount = 0;
            _isSpeaking = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session?.Dispose();
        }
    }
}
