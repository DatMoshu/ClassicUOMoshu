// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Interfaces
{
    internal readonly struct SttResult
    {
        public string Text { get; init; }
        public float Confidence { get; init; }
        public bool IsFinal { get; init; }

        public SttResult(string text, float confidence, bool isFinal)
        {
            Text = text;
            Confidence = confidence;
            IsFinal = isFinal;
        }
    }

    /// <summary>
    /// Abstraction over speech-to-text engines (Vosk streaming, Whisper batch, etc.)
    /// </summary>
    internal interface ISttEngine : IDisposable
    {
        bool IsLoaded { get; }

        /// <summary>Load the STT model. Must be called before feeding audio.</summary>
        Task LoadModelAsync(string modelPath, float sampleRate);

        /// <summary>
        /// Feed raw PCM bytes from microphone for streaming engines (e.g. Vosk).
        /// Results are reported via <see cref="PartialResultAvailable"/> and <see cref="FinalResultAvailable"/>.
        /// </summary>
        void FeedAudio(byte[] buffer, int bytesRecorded);

        /// <summary>
        /// Transcribe a complete float[] PCM clip for batch engines (e.g. Whisper).
        /// </summary>
        Task<SttResult> TranscribeAsync(float[] pcmAudio, CancellationToken ct = default);

        /// <summary>Fired when a partial (intermediate) result is available with sufficient confidence.</summary>
        event EventHandler<SttResult> PartialResultAvailable;

        /// <summary>Fired when a final transcription result is available.</summary>
        event EventHandler<SttResult> FinalResultAvailable;
    }
}
