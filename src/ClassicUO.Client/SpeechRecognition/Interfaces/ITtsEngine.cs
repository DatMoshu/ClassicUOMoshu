// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Interfaces
{
    /// <summary>
    /// Abstraction over text-to-speech synthesis engines (Kokoro, Piper, System.Speech, etc.)
    /// </summary>
    internal interface ITtsEngine : IDisposable
    {
        bool IsLoaded { get; }

        /// <summary>Output sample rate in Hz (typically 22050 or 24000).</summary>
        int SampleRate { get; }

        /// <summary>Load the TTS model. Must be called before synthesizing.</summary>
        Task LoadModelAsync(string modelPath);

        /// <summary>
        /// Synthesize text into PCM float audio.
        /// Returns mono float[] samples at <see cref="SampleRate"/>.
        /// </summary>
        Task<float[]> SynthesizeAsync(string text, CancellationToken ct = default);
    }
}
