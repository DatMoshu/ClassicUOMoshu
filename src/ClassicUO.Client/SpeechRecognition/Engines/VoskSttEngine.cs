// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Diagnostics;
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
        private bool _disposed;

        public bool IsLoaded => _recognizer != null;

        public event EventHandler<SttResult> PartialResultAvailable;
        public event EventHandler<SttResult> FinalResultAvailable;

        public VoskSttEngine() { }

        public Task LoadModelAsync(string modelPath, float sampleRate)
        {
            // Validate path BEFORE hitting native code.
            // Vosk's vosk_model_new() returns a null handle on failure (no managed exception),
            // and the subsequent new VoskRecognizer() then crashes with 0xC0000005.
            if (string.IsNullOrWhiteSpace(modelPath) || modelPath == @"path/to/modelfolder")
                throw new ArgumentException($"Vosk model path is not configured. Set 'vosk_model' in settings.json.");

            if (!System.IO.Directory.Exists(modelPath))
                throw new System.IO.DirectoryNotFoundException(
                    $"Vosk model directory not found: '{modelPath}'. " +
                    $"Update 'vosk_model' in settings.json to the folder containing am/, graph/, etc.");

            return Task.Run(() =>
            {
                Vosk.Vosk.SetLogLevel(0);
                _model = new Vosk.Model(modelPath);
                _recognizer = new VoskRecognizer(_model, sampleRate);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.SetWords(true);
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
                string json = _recognizer.Result();
                SpeechLog.Debug(SpeechLogChannel.Stt, $"Vosk final JSON={json}");
                ParseFinalResult(json);
            }
            else
            {
                string json = _recognizer.PartialResult();
                // Only log partial JSON when it contains non-empty text to reduce noise
                if (json != null && json.Length > 20)
                    SpeechLog.Trace(SpeechLogChannel.Stt, $"Vosk partial JSON={json}");
                ParsePartialResult(json);
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

                // Default Vosk format (SetMaxAlternatives(0)): {"text":"hello world"}
                if (doc.RootElement.TryGetProperty("text", out var textEl))
                {
                    string text = textEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        FinalResultAvailable?.Invoke(this, new SttResult(text, 1.0f, isFinal: true));
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

                // Default Vosk partial format: {"partial":"hello wor"}
                if (doc.RootElement.TryGetProperty("partial", out var partialEl))
                {
                    string text = partialEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        PartialResultAvailable?.Invoke(this, new SttResult(text, 0f, isFinal: false));
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
