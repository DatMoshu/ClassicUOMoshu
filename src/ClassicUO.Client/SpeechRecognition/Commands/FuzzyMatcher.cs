// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;

namespace ClassicUO.SpeechRecognition.Commands
{
    /// <summary>
    /// String similarity metrics for fuzzy voice command matching.
    /// Handles minor ASR errors: "cast fire ball" ≈ "cast fireball".
    /// </summary>
    internal static class FuzzyMatcher
    {
        /// <summary>
        /// Jaro-Winkler similarity (0.0–1.0, higher = more similar).
        /// Good for short strings like command names.
        /// </summary>
        public static float JaroWinkler(string s1, string s2)
        {
            if (s1 == s2) return 1.0f;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0f;

            float jaro = Jaro(s1, s2);

            // Winkler prefix bonus (up to 4 characters)
            int prefix = 0;
            int maxPrefix = Math.Min(4, Math.Min(s1.Length, s2.Length));
            for (int i = 0; i < maxPrefix; i++)
            {
                if (char.ToLowerInvariant(s1[i]) == char.ToLowerInvariant(s2[i]))
                    prefix++;
                else
                    break;
            }

            return jaro + prefix * 0.1f * (1f - jaro);
        }

        private static float Jaro(string s1, string s2)
        {
            int len1 = s1.Length, len2 = s2.Length;
            int matchDist = Math.Max(len1, len2) / 2 - 1;
            if (matchDist < 0) matchDist = 0;

            bool[] s1Matched = new bool[len1];
            bool[] s2Matched = new bool[len2];

            int matches = 0;
            for (int i = 0; i < len1; i++)
            {
                int start = Math.Max(0, i - matchDist);
                int end = Math.Min(i + matchDist + 1, len2);
                for (int j = start; j < end; j++)
                {
                    if (s2Matched[j] || char.ToLowerInvariant(s1[i]) != char.ToLowerInvariant(s2[j])) continue;
                    s1Matched[i] = s2Matched[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0f;

            int transpositions = 0;
            int k = 0;
            for (int i = 0; i < len1; i++)
            {
                if (!s1Matched[i]) continue;
                while (!s2Matched[k]) k++;
                if (char.ToLowerInvariant(s1[i]) != char.ToLowerInvariant(s2[k])) transpositions++;
                k++;
            }

            return (matches / (float)len1 + matches / (float)len2 + (matches - transpositions / 2f) / matches) / 3f;
        }

        /// <summary>
        /// Find the best match in a list of candidates for the given input.
        /// Returns (candidate, score) or (null, 0) if nothing exceeds the threshold.
        /// </summary>
        public static (string best, float score) FindBest(string input, IEnumerable<string> candidates, float threshold = 0.85f)
        {
            string best = null;
            float bestScore = 0f;

            foreach (string candidate in candidates)
            {
                float score = JaroWinkler(input, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= threshold ? (best, bestScore) : (null, 0f);
        }

        /// <summary>
        /// Check if the transcript starts with any candidate (prefix fuzzy match).
        /// Returns (candidate, remainder) or (null, null).
        /// </summary>
        public static (string matched, string remainder) FuzzyPrefixMatch(
            string transcript,
            IEnumerable<string> candidates,
            float threshold = 0.82f)
        {
            string bestMatch = null;
            float bestScore = 0f;
            string bestRemainder = null;

            foreach (string candidate in candidates)
            {
                // Try matching first N words of transcript against candidate
                string[] words = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string[] cmdWords = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (cmdWords.Length > words.Length) continue;

                string transcriptPrefix = string.Join(" ", words[..cmdWords.Length]);
                float score = JaroWinkler(transcriptPrefix, candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = candidate;
                    bestRemainder = string.Join(" ", words[cmdWords.Length..]);
                }
            }

            return bestScore >= threshold ? (bestMatch, bestRemainder) : (null, null);
        }
    }
}
