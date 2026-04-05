// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// Sends the transcript to a local Ollama LLM and parses its ranked top-3 action JSON.
    /// Falls back to the TokenScorer result when Ollama is unreachable or times out.
    ///
    /// System prompt is built once from the commandRegistry at startup and cached.
    /// Uses a dedicated HttpClient with the LLM_TIMEOUT_MS hard cap — completely
    /// separate from the Avatar LLM client (different system prompt, no history).
    /// </summary>
    internal sealed class LlmScorer : IDisposable
    {
        private const int LLM_TIMEOUT_MS = 800;
        private const string ENDPOINT = "/api/chat";

        private readonly HttpClient _http;
        private readonly Uri _chatUri;
        private readonly string _model;
        private readonly string _systemPrompt;
        private bool _disposed;

        public LlmScorer(string baseUrl, string model, IReadOnlyList<CommandRegistryEntry> registry)
        {
            baseUrl = NormalizeBaseUrl(baseUrl);
            _chatUri = new Uri(baseUrl + ENDPOINT);
            _model = model;
            _systemPrompt = BuildSystemPrompt(registry);
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(LLM_TIMEOUT_MS + 200) };
        }

        /// <summary>
        /// Call Ollama and return top-3 actions. Returns null on failure (caller uses TokenScorer fallback).
        /// </summary>
        public async Task<InferenceResult> ScoreAsync(
            string transcript,
            IReadOnlyList<CommandRegistryEntry> registry,
            CancellationToken ct = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(LLM_TIMEOUT_MS);

            try
            {
                // Build JSON manually — NativeAOT disables reflection-based JsonSerializer.Serialize
                // on complex types. Serializing a primitive string is AOT-safe.
                string modelJson = JsonSerializer.Serialize(_model);
                string sysJson   = JsonSerializer.Serialize(_systemPrompt);
                string userJson  = JsonSerializer.Serialize(transcript);
                string json =
                    $"{{\"model\":{modelJson}," +
                    $"\"messages\":[{{\"role\":\"system\",\"content\":{sysJson}}}," +
                    $"{{\"role\":\"user\",\"content\":{userJson}}}]," +
                    $"\"stream\":false,\"options\":{{\"temperature\":0.0}}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(_chatUri, content, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Inference] LLM {(int)response.StatusCode}: {errBody}");
                    return null;
                }

                string body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                return ParseResponse(body, transcript, registry);
            }
            catch (OperationCanceledException)
            {
                return null; // timeout — caller uses TokenScorer fallback
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Inference] LLM error: {ex.Message}");
                return null;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private InferenceResult ParseResponse(
            string body, string transcript, IReadOnlyList<CommandRegistryEntry> registry)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);

                // Ollama wraps in {"message":{"content":"..."}}
                string content = doc.RootElement
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                using var inner = JsonDocument.Parse(content);
                var actionsEl = inner.RootElement.GetProperty("actions");

                var validCommands = new HashSet<string>(
                    registry.Select(r => r.Command), StringComparer.OrdinalIgnoreCase);

                var actions = new List<InferenceAction>(3);

                foreach (var el in actionsEl.EnumerateArray())
                {
                    string cmd   = el.TryGetProperty("command",    out var c) ? c.GetString() : null;
                    string label = el.TryGetProperty("label",      out var l) ? l.GetString() : cmd;
                    float  conf  = el.TryGetProperty("confidence", out var f) ? f.GetSingle() : 0f;

                    // Validate command exists in registry — LLM must not invent commands
                    if (string.IsNullOrEmpty(cmd) || !validCommands.Contains(cmd)) continue;

                    bool isEager = registry.FirstOrDefault(r =>
                        string.Equals(r.Command, cmd, StringComparison.OrdinalIgnoreCase))?.IsEager ?? false;

                    actions.Add(new InferenceAction
                    {
                        Command    = cmd,
                        Label      = label ?? cmd,
                        Confidence = Math.Clamp(conf, 0f, 1f),
                        IsEager    = isEager
                    });

                    if (actions.Count == 3) break;
                }

                return actions.Count > 0
                    ? new InferenceResult(transcript, actions)
                    : null;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Inference] LLM JSON parse error: {ex.Message}");
                return null;
            }
        }

        private static string BuildSystemPrompt(IReadOnlyList<CommandRegistryEntry> registry)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a voice command parser for a medieval war game (Ultima Online World War).");
            sb.AppendLine("Return ONLY valid JSON — no prose, no markdown, no code blocks.");
            sb.AppendLine();
            sb.AppendLine("Available commands:");

            foreach (var entry in registry)
            {
                string examples = entry.Phrases.Length > 0
                    ? $" (e.g. \"{entry.Phrases[0]}\")"
                    : string.Empty;
                sb.AppendLine($"  {entry.Command} — {entry.Label}{examples}");
            }

            sb.AppendLine();
            sb.AppendLine("Return format:");
            sb.AppendLine("{\"actions\":[");
            sb.AppendLine("  {\"command\":\"[warmap]\",\"label\":\"Open War Map\",\"confidence\":0.92},");
            sb.AppendLine("  {\"command\":\"[factioninfo]\",\"label\":\"Faction Info\",\"confidence\":0.61},");
            sb.AppendLine("  {\"command\":\"[qi]\",\"label\":\"Territory Query\",\"confidence\":0.44}");
            sb.AppendLine("]}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Return exactly 3 actions (use confidence:0 for low-quality matches).");
            sb.AppendLine("- command MUST be a valid command from the list above — no invention.");
            sb.AppendLine("- confidence is 0.0–1.0.");

            return sb.ToString();
        }

        private static string NormalizeBaseUrl(string url)
        {
            url = url.TrimEnd('/');
            if (url.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
                url = url[..^"/api/generate".Length];
            if (url.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
                url = url[..^"/api/chat".Length];
            return url;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http.Dispose();
        }
    }
}
