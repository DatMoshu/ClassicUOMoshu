// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// Builds and owns the flat list of all voice-activatable commands.
    /// Sources: SpeechMacroStrings (macro commands), IntentParser UOWW commands,
    /// and SpeechCommandsManager (speechcommands.json custom commands).
    ///
    /// Call Build() once at startup. The result is immutable and shared across
    /// TokenScorer and LlmScorer.
    /// </summary>
    internal static class CommandRegistry
    {
        // Confirmation/cancellation phrases are never valid commands
        private static readonly HashSet<string> ReservedPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "no", "accept", "cancel", "confirm", "stop", "abort",
            "do it", "go", "ok", "okay", "never mind", "nevermind"
        };

        /// <summary>Build the registry from all known command sources.</summary>
        public static IReadOnlyList<CommandRegistryEntry> Build()
        {
            var entries = new List<CommandRegistryEntry>();

            AddUowwCommands(entries);
            AddMacroCommands(entries);
            AddSafeWordRecall(entries);

            return entries.AsReadOnly();
        }

        private static void AddSafeWordRecall(List<CommandRegistryEntry> entries)
        {
            string raw = Settings.GlobalSettings.RecallSafeWord?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return;

            string safeWord = raw.ToLowerInvariant();

            if (ReservedPhrases.Contains(safeWord))
            {
                Console.WriteLine($"[Voice] Safe word \"{safeWord}\" conflicts with a reserved phrase — ignoring.");
                return;
            }

            entries.Add(new CommandRegistryEntry
            {
                Command  = "saferecall:",
                Label    = "Emergency Recall",
                Phrases  = new[] { safeWord },
                Keywords = new[] { safeWord },
                IsEager  = true
            });
        }

        // ── UOWW intent commands ──────────────────────────────────────────────

        private static void AddUowwCommands(List<CommandRegistryEntry> entries)
        {
            entries.Add(Entry("[warmap]", "Open War Map", isEager: true,
                "open war map", "show war map", "war map", "show map", "open map", "warmap"));

            entries.Add(Entry("[qi]", "Territory Query", isEager: true,
                "who controls", "who owns", "what faction controls", "control point", "quadrant info",
                "territory query", "who controls this area", "who controls this quadrant"));

            entries.Add(Entry("[factioninfo]", "Faction Info", isEager: true,
                "what faction", "my faction", "which faction am i", "faction info",
                "my rank", "what is my rank", "what rank am i",
                "my war points", "how many war points", "my points"));

            entries.Add(Entry("[ArmyStatus]", "Army Status", isEager: true,
                "army status", "squad status", "troop status", "how many troops",
                "army report", "my army", "check army", "check troops", "troop report"));

            entries.Add(Entry("[DispatchSquad infantry_standard]", "Dispatch Infantry", isEager: false,
                "dispatch infantry", "send infantry", "deploy infantry",
                "send troops", "dispatch troops", "send squad"));

            entries.Add(Entry("[DispatchSquad archer_squad]", "Dispatch Archers", isEager: false,
                "dispatch archers", "send archers", "deploy archers", "send ranged"));

            entries.Add(Entry("[DispatchSquad cavalry_squad]", "Dispatch Cavalry", isEager: false,
                "dispatch cavalry", "send cavalry", "deploy cavalry", "send mounted"));

            entries.Add(Entry("[DispatchSquad support_squad]", "Dispatch Support", isEager: false,
                "dispatch medics", "send medics", "deploy support", "send healers"));

            entries.Add(Entry("[DispatchSquad assault_squad]", "Dispatch Assault", isEager: false,
                "dispatch assault", "send assault", "deploy heavy", "send siege"));

            entries.Add(Entry("[voice proximity]", "Proximity Channel", isEager: true,
                "proximity voice", "proximity channel", "local chat", "switch to proximity",
                "local voice", "area voice"));

            entries.Add(Entry("[voice faction]", "Faction Channel", isEager: true,
                "switch to faction", "faction channel", "join faction chat",
                "faction voice", "faction chat"));

            entries.Add(Entry("[voice guild]", "Guild Channel", isEager: true,
                "switch to guild", "guild channel", "join guild chat",
                "guild voice", "guild chat"));
        }

        // ── Macro commands from SpeechMacroStrings ────────────────────────────

        private static void AddMacroCommands(List<CommandRegistryEntry> entries)
        {
            foreach (var macroType in SpeechMacroStrings.MacroSpeechCommands)
            {
                foreach (var macroSubType in macroType.Value)
                {
                    var phrases = macroSubType.Value
                        .Where(p => !ReservedPhrases.Contains(p))
                        .ToArray();

                    if (phrases.Length == 0) continue;

                    string command = $"macro:{macroType.Key}:{macroSubType.Key}";
                    string label = FormatMacroLabel(macroType.Key.ToString(), macroSubType.Key.ToString());

                    entries.Add(new CommandRegistryEntry
                    {
                        Command = command,
                        Label = label,
                        Phrases = phrases,
                        Keywords = ExtractKeywords(phrases),
                        IsEager = true // macros are always eager — instant player reflex
                    });
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static CommandRegistryEntry Entry(string command, string label, bool isEager, params string[] phrases)
        {
            var filtered = phrases.Where(p => !ReservedPhrases.Contains(p)).ToArray();
            return new CommandRegistryEntry
            {
                Command = command,
                Label = label,
                Phrases = filtered,
                Keywords = ExtractKeywords(filtered),
                IsEager = isEager
            };
        }

        private static string[] ExtractKeywords(string[] phrases)
        {
            // Stop words to ignore when building keyword set
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "to", "my", "is", "am", "i", "in", "of",
                "at", "it", "how", "what", "who", "show", "open", "close"
            };

            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var phrase in phrases)
            {
                foreach (var word in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!stopWords.Contains(word) && word.Length > 2)
                        keywords.Add(word.ToLowerInvariant());
                }
            }
            return keywords.ToArray();
        }

        private static string FormatMacroLabel(string macroType, string macroSubType)
        {
            // Convert "Open" + "Paperdoll" → "Open Paperdoll"
            // Convert "CastSpell" + "Fireball" → "Cast Fireball"
            string type = System.Text.RegularExpressions.Regex.Replace(macroType, "([A-Z])", " $1").Trim();
            string sub = System.Text.RegularExpressions.Regex.Replace(macroSubType, "([A-Z])", " $1").Trim();
            return $"{type} {sub}".Trim();
        }
    }
}
