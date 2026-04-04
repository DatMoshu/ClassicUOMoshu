// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using Vosk;

namespace ClassicUO.SpeechRecognition.Engines
{
    /// <summary>
    /// Streaming STT engine backed by Vosk (offline, low-latency, Apache 2.0).
    /// Processes audio chunks in real-time; fires events as results become available.
    /// </summary>
    internal sealed class VoskSttEngine : ISttEngine
    {
        private VoskRecognizer _recognizer;
        private Vosk.Model _model;
        private float _confidenceThreshold;
        private bool _disposed;

        public bool IsLoaded => _recognizer != null;

        public event EventHandler<SttResult> PartialResultAvailable;
        public event EventHandler<SttResult> FinalResultAvailable;

        public VoskSttEngine(float confidenceThreshold = 0.7f)
        {
            _confidenceThreshold = confidenceThreshold;
        }

        public Task LoadModelAsync(string modelPath, float sampleRate)
        {
            return Task.Run(() =>
            {
                Vosk.Vosk.SetLogLevel(0);
                _model = new Vosk.Model(modelPath);
                _recognizer = new VoskRecognizer(_model, sampleRate);
                _recognizer.SetMaxAlternatives(5);
            });
        }

        /// <summary>
        /// Feed a raw PCM byte chunk from NAudio's DataAvailable callback.
        /// This method fires <see cref="FinalResultAvailable"/> or <see cref="PartialResultAvailable"/>.
        /// Must be called from a single thread (the NAudio callback thread).
        /// </summary>
        public void FeedAudio(byte[] buffer, int bytesRecorded)
        {
            if (!IsLoaded) return;

            if (_recognizer.AcceptWaveform(buffer, bytesRecorded))
            {
                ParseFinalResult(_recognizer.Result());
            }
            else
            {
                ParsePartialResult(_recognizer.PartialResult());
            }
        }

        public Task<SttResult> TranscribeAsync(float[] pcmAudio, CancellationToken ct = default)
        {
            // Vosk is a streaming engine — batch transcription is not its strength.
            // Convert float[] to byte[] and feed through the recognizer.
            return Task.Run(() =>
            {
                var bytes = new byte[pcmAudio.Length * 2];
                for (int i = 0; i < pcmAudio.Length; i++)
                {
                    short s = (short)(Math.Clamp(pcmAudio[i], -1f, 1f) * 32767f);
                    bytes[i * 2] = (byte)(s & 0xFF);
                    bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
                _recognizer.AcceptWaveform(bytes, bytes.Length);
                var json = _recognizer.FinalResult();
                return ParseResultJson(json, isFinal: true);
            }, ct);
        }

        private void ParseFinalResult(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textEl))
                {
                    string text = textEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        FinalResultAvailable?.Invoke(this, new SttResult(text, 1.0f, isFinal: true));
                    }
                }
            }
            catch (JsonException) { /* malformed result — skip */ }
        }

        private void ParsePartialResult(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);

                // Partial result with alternatives (confidence scores)
                if (doc.RootElement.TryGetProperty("alternatives", out var alternativesEl))
                {
                    foreach (var alt in alternativesEl.EnumerateArray())
                    {
                        if (!alt.TryGetProperty("text", out var textEl)) continue;
                        if (!alt.TryGetProperty("confidence", out var confEl)) continue;

                        string text = textEl.GetString()?.Trim();
                        float confidence = confEl.GetSingle();

                        if (string.IsNullOrEmpty(text) || confidence < _confidenceThreshold) continue;

                        PartialResultAvailable?.Invoke(this, new SttResult(text, confidence, isFinal: false));
                        break; // Use highest-confidence alternative
                    }
                }
                // Simple partial result
                else if (doc.RootElement.TryGetProperty("partial", out var partialEl))
                {
                    string text = partialEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        PartialResultAvailable?.Invoke(this, new SttResult(text, 0f, isFinal: false));
                    }
                }
            }
            catch (JsonException) { /* malformed result — skip */ }
        }

        private static SttResult ParseResultJson(string json, bool isFinal)
        {
            if (string.IsNullOrEmpty(json)) return default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textEl))
                    return new SttResult(textEl.GetString()?.Trim() ?? string.Empty, 1.0f, isFinal);
            }
            catch (JsonException) { }
            return default;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _recognizer?.Dispose();
            _model?.Dispose();
        }
    }
}
