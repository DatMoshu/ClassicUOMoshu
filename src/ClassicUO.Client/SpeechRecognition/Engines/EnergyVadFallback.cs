// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;

namespace ClassicUO.SpeechRecognition.Engines
{
    /// <summary>
    /// Minimal VAD based on RMS energy threshold.
    /// Used when Silero ONNX model is unavailable. Always loaded (no model file needed).
    /// </summary>
    internal sealed class EnergyVadFallback : IVadEngine
    {
        private readonly float _threshold;
        private readonly int _minSpeechFrames;
        private readonly int _silenceFrames;

        private int _speechCount;
        private int _silenceCount;
        private bool _isSpeaking;

        public bool IsLoaded => true; // always available

        public event EventHandler SpeechStarted;
        public event EventHandler SpeechEnded;

        public EnergyVadFallback(float threshold = 0.02f, int minSpeechMs = 250, int silenceMs = 700)
        {
            _threshold = threshold;
            // Assume 512-sample frames at 16kHz = 32ms
            _minSpeechFrames = (int)Math.Ceiling(minSpeechMs / 32.0);
            _silenceFrames = (int)Math.Ceiling(silenceMs / 32.0);
        }

        public Task LoadModelAsync(string modelPath) => Task.CompletedTask; // no-op

        public float ProcessFrame(float[] frame)
        {
            float rms = ComputeRms(frame);

            if (rms >= _threshold)
            {
                _silenceCount = 0;
                _speechCount++;
                if (!_isSpeaking && _speechCount >= _minSpeechFrames)
                {
                    _isSpeaking = true;
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _speechCount = 0;
                if (_isSpeaking)
                {
                    _silenceCount++;
                    if (_silenceCount >= _silenceFrames)
                    {
                        _isSpeaking = false;
                        _silenceCount = 0;
                        SpeechEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            // Normalize RMS as a probability proxy
            return Math.Min(rms / _threshold, 1.0f);
        }

        public void Reset()
        {
            _speechCount = 0;
            _silenceCount = 0;
            _isSpeaking = false;
        }

        private static float ComputeRms(float[] frame)
        {
            double sum = 0;
            for (int i = 0; i < frame.Length; i++)
                sum += frame[i] * frame[i];
            return (float)Math.Sqrt(sum / frame.Length);
        }

        public void Dispose() { }
    }
}
