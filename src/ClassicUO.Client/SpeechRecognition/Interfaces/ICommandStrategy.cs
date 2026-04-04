// SPDX-License-Identifier: BSD-2-Clause

using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Interfaces
{
    /// <summary>
    /// Single step in the voice command routing pipeline.
    /// Strategies are tried in order; first match wins.
    /// </summary>
    internal interface ICommandStrategy
    {
        /// <summary>
        /// Attempt to handle the transcript.
        /// Returns true if this strategy consumed the command (stop processing further strategies).
        /// </summary>
        Task<bool> TryHandleAsync(string transcript);
    }
}
