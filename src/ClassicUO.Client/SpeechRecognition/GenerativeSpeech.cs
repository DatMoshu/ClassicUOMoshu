// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition;
using ClassicUO.SpeechRecognition.Interfaces;

namespace ClassicUO.Game
{
    /// <summary>
    /// Orchestrates the LLM → TTS pipeline for spoken Avatar responses.
    ///
    /// Pipeline (fully pipelined for minimum latency):
    ///   LLM streams tokens → sentence buffer detects boundaries
    ///   → synthesize sentence N while N+1 is still generating
    ///   → play N while N+1 is synthesizing while N+2 is generating
    ///
    /// Sentence boundaries: '.', '!', '?', or every 30 tokens (safety limit).
    /// </summary>
    internal sealed class GenerativeSpeech
    {
        private readonly ILlmClient _llmClient;
        private readonly TtsPlaybackManager _playback;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private const int MaxTokensPerChunk = 30;

        public GenerativeSpeech(ILlmClient llmClient, TtsPlaybackManager playback)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        }

        /// <summary>
        /// Stream an LLM response and speak it sentence by sentence.
        /// Non-blocking — returns immediately, speaks in background.
        /// Call <see cref="Cancel"/> to stop (barge-in).
        /// </summary>
        public void SpeakStreaming(string prompt)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _playback.Cancel();
            _ = Task.Run(() => StreamAndSpeakAsync(prompt, _cts.Token));
        }

        /// <summary>Stop current streaming response immediately.</summary>
        public void Cancel()
        {
            _cts.Cancel();
            _playback.Cancel();
        }

        private async Task StreamAndSpeakAsync(string prompt, CancellationToken ct)
        {
            var sentenceBuffer = new StringBuilder();
            int tokenCount = 0;

            await foreach (string token in _llmClient.StreamChatAsync(prompt, ct))
            {
                if (ct.IsCancellationRequested) break;

                sentenceBuffer.Append(token);
                tokenCount++;

                // Check for sentence boundary or token limit
                bool isBoundary = token.Contains('.') || token.Contains('!') || token.Contains('?');
                bool isLong = tokenCount >= MaxTokensPerChunk;

                if (isBoundary || isLong)
                {
                    string sentence = sentenceBuffer.ToString().Trim();
                    sentenceBuffer.Clear();
                    tokenCount = 0;

                    if (!string.IsNullOrEmpty(sentence))
                    {
                        // Synthesize and queue — overlaps with continued LLM generation
                        await _playback.SpeakAsync(sentence, ct);
                    }
                }
            }

            // Speak any remaining text
            string remainder = sentenceBuffer.ToString().Trim();
            if (!string.IsNullOrEmpty(remainder) && !ct.IsCancellationRequested)
                await _playback.SpeakAsync(remainder, ct);
        }
    }
}
