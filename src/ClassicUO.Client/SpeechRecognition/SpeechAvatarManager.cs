// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Game;
using ClassicUO.SpeechRecognition;

namespace ClassicUO.SpeechRecognition
{
    internal class SpeechAvatarManager
    {
        private readonly Random _random = new Random();
        private World _world;
        private volatile bool _chatMode;
        private LLMClient _llmClient;
        private GenerativeSpeech _generativeSpeech; // null when TTS is disabled
        // Cancellation for any in-flight LLM request — lets barge-in cancel it cleanly
        private CancellationTokenSource _llmCts = new CancellationTokenSource();

        public bool ChatMode => _chatMode;

        /// <param name="generativeSpeech">Optional — when non-null, Avatar speaks responses aloud via TTS.</param>
        public SpeechAvatarManager(World world, LLMClient llmClient, GenerativeSpeech generativeSpeech = null)
        {
            _world = world;
            _llmClient = llmClient;
            _generativeSpeech = generativeSpeech;
        }

        public SpeechAvatarManager() { }

        /// <summary>
        /// Process speech text for Avatar interactions.
        /// Returns true if the text was consumed by the Avatar system (stop processing further).
        /// Non-blocking — LLM calls are fire-and-forget.
        /// </summary>
        public bool HandleLlmPrompt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return _chatMode;

            string lower = text.ToLowerInvariant();

            // Activation
            if (lower.Contains("hey avatar") || lower.StartsWith("avatar"))
            {
                text = text.Replace("hey avatar", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("avatar", "", StringComparison.OrdinalIgnoreCase)
                           .Trim();
                _chatMode = true;
                PlayerSay(GetRandomAttentionPhrase());
            }

            // Deactivation
            if (lower.StartsWith("goodbye"))
            {
                _chatMode = false;
                PlayerSay(GetRandomAvatarFarewellPhrase());
                return true;
            }

            if (!_chatMode) return false;

            // Skip trivially short prompts (single word / noise)
            if (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
                return true;

            // Cancel any previous in-flight LLM request
            _llmCts.Cancel();
            _llmCts = new CancellationTokenSource();
            var ct = _llmCts.Token;

            // Fire-and-forget — no .Result blocking
            _ = SendToLlmAsync(text, ct);
            return true;
        }

        private async Task SendToLlmAsync(string text, CancellationToken ct)
        {
            try
            {
                if (_generativeSpeech != null)
                {
                    // Streaming TTS: Avatar speaks while LLM still generates
                    _generativeSpeech.SpeakStreaming(text);
                }
                else
                {
                    // Text-only fallback: full response → chat text
                    string response = await _llmClient.ChatWithOllamaAsync(text, ct);
                    if (ct.IsCancellationRequested || string.IsNullOrEmpty(response)) return;
                    SpeakResponse(response);
                }
            }
            catch (OperationCanceledException) { /* barge-in or shutdown — ignore */ }
            catch (Exception ex)
            {
                Console.WriteLine($"[Avatar] LLM error: {ex.Message}");
            }
        }

        private void SpeakResponse(string response)
        {
            if (response.Length < 100)
            {
                PlayerSay(response);
                return;
            }

            // Split long responses into ≤100-char chunks at sentence/word boundaries
            int chunkSize = 100;
            int lastSplit = 0;

            for (int i = 0; i < response.Length; i += chunkSize)
            {
                int end = Math.Min(i + chunkSize, response.Length - 1);
                int splitIndex = response.LastIndexOfAny(new[] { '.', '!', '?', ' ' }, end);
                if (splitIndex <= lastSplit)
                    splitIndex = Math.Min(i + chunkSize, response.Length);

                string chunk = response[lastSplit..splitIndex].Trim();
                lastSplit = splitIndex + 1;

                if (!string.IsNullOrEmpty(chunk))
                    PlayerSay(chunk);
            }
        }

        private string GetRandomAvatarFarewellPhrase()
        {
            int index = _random.Next(SpeechRecognitionStrings.AvatarFarewells.Length);
            return SpeechRecognitionStrings.AvatarFarewells[index];
        }

        private string GetRandomAttentionPhrase()
        {
            int index = _random.Next(SpeechRecognitionStrings.AttentionPhrases.Length);
            return SpeechRecognitionStrings.AttentionPhrases[index];
        }

        private void PlayerSay(string message)
        {
            if (_world?.Player != null)
                GameActions.Say(message);
        }
    }
}
