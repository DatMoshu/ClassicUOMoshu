// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.SpeechRecognition.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Game
{
    // ── AOT-safe JSON source generation context ──────────────────────────────
    // All types that cross the JsonSerializer boundary must be listed here.

    [JsonSerializable(typeof(LLMClient.ChatRequest))]
    [JsonSerializable(typeof(LLMClient.GenerateRequest))]
    [JsonSerializable(typeof(LLMClient.ChatReply))]
    [JsonSerializable(typeof(LLMClient.StreamChunk))]
    [JsonSerializable(typeof(LLMClient.ResponsePayload))]
    [JsonSerializable(typeof(LLMClient.GenResponse))]
    [JsonSerializable(typeof(List<LLMClient.Message>))]
    [JsonSerializable(typeof(List<LLMClient.GenResponse>))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal partial class LlmJsonContext : JsonSerializerContext { }

    public class LLMClient : ILlmClient
    {
        private readonly Uri _generateUri;
        private readonly Uri _chatUri;
        private readonly HttpClient _httpClient;
        private readonly string _systemPrompt = "You are the Avatar from Ultima Online. Stay in character. Provide in-game information and lore. Keep responses under 250 characters. Plain text only.";

        private readonly List<Message> _conversationHistory = new List<Message>();
        private readonly int _maxHistorySize;
        private string _model;

        public LLMClient(string baseUrl, string model, int maxHistorySize = 20)
        {
            baseUrl = baseUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl[..^"/api/generate".Length];
            if (baseUrl.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl[..^"/api/chat".Length];

            _generateUri = new Uri(baseUrl + "/api/generate");
            _chatUri = new Uri(baseUrl + "/api/chat");
            _model = model;
            _maxHistorySize = maxHistorySize;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) }; // 5 min for long responses
        }

        // ── ILlmClient implementation ───────────────────────────────────────

        public async Task<string> ChatAsync(string prompt, CancellationToken ct = default)
            => await ChatWithOllamaAsync(prompt, ct);

        public async IAsyncEnumerable<string> StreamChatAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            _conversationHistory.Add(new Message { Role = "user", Content = prompt });
            TrimHistory();

            var payload = new ChatRequest
            {
                Model = _model,
                Messages = _conversationHistory,
                Stream = true,
                Options = new ChatOptions { Temperature = 0.7 }
            };

            string json = JsonSerializer.Serialize(payload, LlmJsonContext.Default.ChatRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, _chatUri) { Content = content };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM] Stream error: {ex.Message}");
                yield break;
            }

            var fullResponse = new StringBuilder();
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            string line;
            while ((line = await reader.ReadLineAsync()) != null && !ct.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                StreamChunk chunk;
                try { chunk = JsonSerializer.Deserialize(line, LlmJsonContext.Default.StreamChunk); }
                catch (JsonException) { continue; }

                string token = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(token))
                {
                    fullResponse.Append(token);
                    yield return token;
                }
                if (chunk?.Done == true) break;
            }

            if (fullResponse.Length > 0)
                _conversationHistory.Add(new Message { Role = "assistant", Content = fullResponse.ToString() });
        }

        public void ClearHistory() => _conversationHistory.Clear();

        // ── Chat endpoint (non-streaming) ───────────────────────────────────

        public async Task<string> ChatWithOllamaAsync(string prompt, CancellationToken ct = default)
        {
            _conversationHistory.Add(new Message { Role = "user", Content = prompt });
            TrimHistory();

            var payload = new ChatRequest
            {
                Model = _model,
                Messages = _conversationHistory,
                Stream = false,
                Options = new ChatOptions { Temperature = 0.7 }
            };

            string json = JsonSerializer.Serialize(payload, LlmJsonContext.Default.ChatRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine($"[LLM] POST {_chatUri} model={_model} prompt_len={prompt.Length}");
            try
            {
                var response = await _httpClient.PostAsync(_chatUri, content, ct);
                Console.WriteLine($"[LLM] HTTP {(int)response.StatusCode} {response.StatusCode}");
                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[LLM] Raw response ({responseContent.Length} chars): {responseContent[..Math.Min(200, responseContent.Length)]}");

                var responseObject = JsonSerializer.Deserialize(responseContent, LlmJsonContext.Default.ChatReply);
                string msg = responseObject?.Message?.Content ?? "No response from Ollama.";
                Console.WriteLine($"[LLM] Parsed message ({msg.Length} chars): {msg[..Math.Min(100, msg.Length)]}");

                _conversationHistory.Add(new Message { Role = "assistant", Content = msg });
                return msg;
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"[LLM] CANCELLED: {e.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM] EXCEPTION {ex.GetType().Name}: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        // ── Generate endpoint (kept for backward compat) ────────────────────

        public async Task<string> CallLlamaAsync(string prompt, bool raw = false, bool stream = false)
        {
            var payload = new GenerateRequest
            {
                Model = _model,
                Prompt = $"{_systemPrompt} {prompt}",
                Raw = raw,
                Stream = stream
            };

            string json = JsonSerializer.Serialize(payload, LlmJsonContext.Default.GenerateRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(_generateUri, content);

            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize(body, LlmJsonContext.Default.ResponsePayload);
                string text = result?.Response ?? string.Empty;
                return text.Length > 250 ? text[..250] : text;
            }

            throw new Exception($"LLM error: {response.StatusCode}");
        }

        public async Task<List<GenResponse>> PromptAvatarJsonAsync(string prompt, bool raw = false, bool stream = false)
        {
            var payload = new GenerateRequest
            {
                Model = _model,
                Prompt = $"{_systemPrompt} Respond in JSON format with a single 'response' field. {prompt}",
                Raw = raw,
                Stream = stream
            };

            string json = JsonSerializer.Serialize(payload, LlmJsonContext.Default.GenerateRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(_generateUri, content);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"LLM error: {response.StatusCode}");

            string body = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize(body, LlmJsonContext.Default.ResponsePayload);

            string responseResult = parsed?.Response?.Trim()
                .Replace("```json", "[").Replace("```", "]") ?? "[]";

            int start = responseResult.IndexOf('[');
            int end = responseResult.LastIndexOf(']');
            if (start < 0 || end < 0) return new List<GenResponse>();

            responseResult = responseResult[start..(end + 1)];
            return JsonSerializer.Deserialize(responseResult, LlmJsonContext.Default.ListGenResponse)
                   ?? new List<GenResponse>();
        }

        // ── Private helpers ─────────────────────────────────────────────────

        private void TrimHistory()
        {
            while (_conversationHistory.Count > _maxHistorySize)
                _conversationHistory.RemoveAt(0);
        }

        // ── Request / response types (named — required for AOT) ──────────────

        public class ChatRequest
        {
            [JsonPropertyName("model")]   public string Model { get; set; }
            [JsonPropertyName("messages")] public List<Message> Messages { get; set; }
            [JsonPropertyName("stream")]  public bool Stream { get; set; }
            [JsonPropertyName("options")] public ChatOptions Options { get; set; }
        }

        public class ChatOptions
        {
            [JsonPropertyName("temperature")] public double Temperature { get; set; }
            [JsonPropertyName("seed")]        public int Seed { get; set; }
        }

        public class GenerateRequest
        {
            [JsonPropertyName("model")]  public string Model { get; set; }
            [JsonPropertyName("prompt")] public string Prompt { get; set; }
            [JsonPropertyName("raw")]    public bool Raw { get; set; }
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        public class ResponsePayload
        {
            [JsonPropertyName("response")] public string Response { get; set; }
        }

        public class StreamChunk
        {
            [JsonPropertyName("message")] public Message Message { get; set; }
            [JsonPropertyName("done")]    public bool Done { get; set; }
        }

        public class GenResponse
        {
            [JsonPropertyName("response")] public string Response { get; set; }
        }

        public class ChatReply
        {
            [JsonPropertyName("message")] public Message Message { get; set; }
        }

        public class Message
        {
            [JsonPropertyName("role")]    public string Role { get; set; }
            [JsonPropertyName("content")] public string Content { get; set; }
        }
    }
}
