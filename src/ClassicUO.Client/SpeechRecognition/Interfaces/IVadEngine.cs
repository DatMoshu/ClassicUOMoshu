// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Interfaces
{
    /// <summary>
    /// Abstraction over voice activity detection engines (Silero VAD, RMS energy, etc.)
    /// </summary>
    internal interface IVadEngine : IDisposable
    {
        bool IsLoaded { get; }

        /// <summary>Load the VAD model. Must be called before processing frames.</summary>
        Task LoadModelAsync(string modelPath);

        /// <summary>
        /// Process a single audio frame. Returns speech probability 0.0–1.0.
        /// Frame must be exactly 512 samples (32ms at 16kHz) for Silero VAD.
        /// </summary>
        float ProcessFrame(float[] frame);

        /// <summary>Reset internal hidden state (call between utterances).</summary>
        void Reset();

        /// <summary>Fired when speech onset is detected above threshold.</summary>
        event EventHandler SpeechStarted;

        /// <summary>Fired after sustained silence following speech.</summary>
        event EventHandler SpeechEnded;
    }
}
