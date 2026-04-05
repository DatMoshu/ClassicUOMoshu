// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// Pure C# command scorer. Sub-millisecond, no external dependencies.
    ///
    /// Composite score formula (max theoretical ≈ 8.3):
    ///   exact_match     × 3.0  — any registered phrase matches exactly (after article strip)
    ///   keyword_overlap × 2.0  — % of command's key terms present in transcript
    ///   fuzzy_token     × 1.5  — best Jaro-Winkler score across token pairs
    ///   position_bonus  × 0.5  — first 2 transcript words hit command keywords
    ///   recency_bonus   × 0.3  — command was executed in last 60 seconds
    ///   synonym_bonus   × 1.0  — transcript contains a known synonym for a command keyword
    /// </summary>
    internal sealed class TokenScorer
    {
        private const float MIN_SCORE = 0.4f;
        private const float MAX_THEORETICAL = 8.3f;
        private const long RECENCY_WINDOW_MS = 60_000;

        // Articles and filler words stripped from transcripts before scoring
        private static readonly HashSet<string> ArticlesAndFiller = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "please", "can", "could", "would", "do", "just", "now", "hey", "um", "uh"
        };

        // Synonym map: common alternate words → canonical command words
        private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bag"] = "backpack",
            ["satchel"] = "backpack",
            ["pack"] = "backpack",
            ["hp"] = "heal",
            ["health"] = "heal",
            ["mend"] = "heal",
            ["map"] = "warmap",
            ["enemies"] = "hostile",
            ["foes"] = "hostile",
            ["troops"] = "army",
            ["soldiers"] = "infantry",
            ["horse"] = "cavalry",
            ["mounted"] = "cavalry",
            ["medics"] = "support",
            ["healers"] = "support",
            ["heavy"] = "assault",
            ["siege"] = "assault",
        };

        // Track recently executed commands for recency bonus (thread-safe: written from game thread, read from Task.Run)
        private readonly ConcurrentDictionary<string, long> _recentlyExecuted = new(StringComparer.OrdinalIgnoreCase);

        public void RecordExecuted(string command)
        {
            _recentlyExecuted[command] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Score all registry entries against the transcript and return top 3.
        /// Safe to call from any thread — no shared mutable state during scoring.
        /// </summary>
        public InferenceResult Score(string transcript, IReadOnlyList<CommandRegistryEntry> registry)
        {
            if (string.IsNullOrWhiteSpace(transcript) || registry.Count == 0)
                return InferenceResult.Empty;

            string t = NormalizeTranscript(transcript);
            string[] tTokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var scored = new List<(CommandRegistryEntry Entry, float Score)>(registry.Count);

            foreach (var entry in registry)
            {
                float score = ComputeScore(t, tTokens, entry, nowMs);
                if (score >= MIN_SCORE)
                    scored.Add((entry, score));
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            var actions = scored
                .Take(3)
                .Select(s => new InferenceAction
                {
                    Command = s.Entry.Command,
                    Label = s.Entry.Label,
                    Confidence = Math.Clamp(s.Score / MAX_THEORETICAL, 0f, 1f),
                    IsEager = s.Entry.IsEager
                })
                .ToList();

            return new InferenceResult(transcript, actions);
        }

        /// <summary>
        /// Score for eager evaluation (partial transcript). Returns normalized confidence.
        /// </summary>
        public (CommandRegistryEntry Entry, float Confidence) ScoreEager(
            string partial, IReadOnlyList<CommandRegistryEntry> registry)
        {
            if (string.IsNullOrWhiteSpace(partial) || registry.Count == 0)
                return (null, 0f);

            string t = NormalizeTranscript(partial);
            string[] tTokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            CommandRegistryEntry best = null;
            float bestScore = 0f;

            foreach (var entry in registry)
            {
                float score = ComputeScore(t, tTokens, entry, nowMs);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            float confidence = Math.Clamp(bestScore / MAX_THEORETICAL, 0f, 1f);
            return (best, confidence);
        }

        // ── Transcript normalization ──────────────────────────────────────────

        /// <summary>
        /// Strip articles, filler words, and apply synonym expansion.
        /// "please cast a fireball" → "cast fireball"
        /// "open the bag" → "open backpack"
        /// </summary>
        private static string NormalizeTranscript(string raw)
        {
            string lower = raw.ToLowerInvariant().Trim();
            var tokens = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(tokens.Length);

            foreach (var token in tokens)
            {
                if (ArticlesAndFiller.Contains(token)) continue;

                // Apply synonym if known
                result.Add(Synonyms.TryGetValue(token, out string syn) ? syn : token);
            }

            return string.Join(' ', result);
        }

        // ── Scoring components ────────────────────────────────────────────────

        private float ComputeScore(string transcript, string[] tTokens,
            CommandRegistryEntry entry, long nowMs)
        {
            float exact   = ExactMatchBonus(transcript, entry.Phrases)  * 3.0f;
            float overlap = KeywordOverlap(transcript, entry.Keywords)  * 2.0f;
            float fuzzy   = BestFuzzyToken(tTokens, entry.Phrases)     * 1.5f;
            float pos     = PositionBonus(tTokens, entry.Keywords)      * 0.5f;
            float recency = RecencyBonus(entry.Command, nowMs)          * 0.3f;
            float synonym = SynonymBonus(tTokens, entry.Keywords)       * 1.0f;
            return exact + overlap + fuzzy + pos + recency + synonym;
        }

        private static float ExactMatchBonus(string transcript, string[] phrases)
        {
            foreach (var p in phrases)
                if (transcript.Equals(p, StringComparison.OrdinalIgnoreCase)) return 1.0f;
            // Partial prefix match (transcript starts with phrase)
            foreach (var p in phrases)
                if (transcript.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return 0.6f;
            return 0f;
        }

        private static float KeywordOverlap(string transcript, string[] keywords)
        {
            if (keywords.Length == 0) return 0f;
            int hits = 0;
            foreach (var kw in keywords)
                if (transcript.Contains(kw, StringComparison.OrdinalIgnoreCase)) hits++;
            return (float)hits / keywords.Length;
        }

        private static float BestFuzzyToken(string[] tTokens, string[] phrases)
        {
            float best = 0f;
            foreach (var phrase in phrases)
            {
                string[] pTokens = phrase.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tt in tTokens)
                    foreach (var pt in pTokens)
                    {
                        float jw = JaroWinkler(tt, pt);
                        if (jw > best) best = jw;
                    }
            }
            return best;
        }

        /// <summary>
        /// Position bonus: reward when the first 2 transcript words match command keywords.
        /// 1.0 if both match, 0.5 if only first matches, 0 otherwise.
        /// </summary>
        private static float PositionBonus(string[] tTokens, string[] keywords)
        {
            if (tTokens.Length == 0 || keywords.Length == 0) return 0f;

            int matched = 0;
            int check = Math.Min(2, tTokens.Length);
            for (int i = 0; i < check; i++)
            {
                foreach (var kw in keywords)
                {
                    if (tTokens[i].Equals(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        matched++;
                        break;
                    }
                }
            }

            return matched switch
            {
                2 => 1.0f,
                1 => 0.5f,
                _ => 0f
            };
        }

        /// <summary>
        /// Bonus when a synonym-expanded transcript token matches a command keyword
        /// that the original transcript token did not match directly.
        /// </summary>
        private static float SynonymBonus(string[] tTokens, string[] keywords)
        {
            if (keywords.Length == 0) return 0f;
            foreach (var token in tTokens)
            {
                // Only count if this token was a synonym expansion
                if (!Synonyms.ContainsValue(token)) continue;
                foreach (var kw in keywords)
                {
                    if (token.Equals(kw, StringComparison.OrdinalIgnoreCase))
                        return 1.0f;
                }
            }
            return 0f;
        }

        private float RecencyBonus(string command, long nowMs)
        {
            if (_recentlyExecuted.TryGetValue(command, out long ts))
                return nowMs - ts < RECENCY_WINDOW_MS ? 1.0f : 0f;
            return 0f;
        }

        // ── Jaro-Winkler similarity ───────────────────────────────────────────

        private static float JaroWinkler(string s1, string s2)
        {
            if (s1 == s2) return 1.0f;
            if (s1.Length == 0 || s2.Length == 0) return 0f;

            int matchDist = Math.Max(s1.Length, s2.Length) / 2 - 1;
            if (matchDist < 0) matchDist = 0;

            bool[] s1Matched = new bool[s1.Length];
            bool[] s2Matched = new bool[s2.Length];

            int matches = 0;
            int transpositions = 0;

            for (int i = 0; i < s1.Length; i++)
            {
                int start = Math.Max(0, i - matchDist);
                int end = Math.Min(i + matchDist + 1, s2.Length);

                for (int j = start; j < end; j++)
                {
                    if (s2Matched[j] || s1[i] != s2[j]) continue;
                    s1Matched[i] = true;
                    s2Matched[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0f;

            int k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!s1Matched[i]) continue;
                while (!s2Matched[k]) k++;
                if (s1[i] != s2[k]) transpositions++;
                k++;
            }

            float jaro = (matches / (float)s1.Length
                        + matches / (float)s2.Length
                        + (matches - transpositions / 2f) / matches) / 3f;

            // Winkler prefix bonus (up to 4 characters)
            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
            {
                if (s1[i] == s2[i]) prefix++;
                else break;
            }

            return jaro + prefix * 0.1f * (1f - jaro);
        }
    }
}
