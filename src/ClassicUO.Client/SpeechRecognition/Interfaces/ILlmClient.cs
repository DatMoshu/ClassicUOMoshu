// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Interfaces
{
    /// <summary>
    /// Abstraction over local LLM backends (Ollama, llama.cpp, etc.)
    /// </summary>
    internal interface ILlmClient
    {
        /// <summary>Send a prompt and receive the full response.</summary>
        Task<string> ChatAsync(string prompt, CancellationToken ct = default);

        /// <summary>
        /// Send a prompt and stream response tokens one at a time.
        /// Enables low-latency TTS pipeline: token → sentence buffer → speak.
        /// </summary>
        IAsyncEnumerable<string> StreamChatAsync(string prompt, CancellationToken ct = default);

        /// <summary>Clear conversation history (start fresh context).</summary>
        void ClearHistory();
    }
}
