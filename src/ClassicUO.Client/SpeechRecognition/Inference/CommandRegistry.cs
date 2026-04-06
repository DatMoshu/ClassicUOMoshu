// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition.Diagnostics;

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

        /// <summary>Build the full registry from all known command sources.</summary>
        public static IReadOnlyList<CommandRegistryEntry> Build()
        {
            var entries = new List<CommandRegistryEntry>();

            AddUowwCommands(entries);
            AddMacroCommands(entries);
            AddHealCommands(entries);
            AddInventoryCommands(entries);
            AddSafeWordRecall(entries);

            return entries.AsReadOnly();
        }

        /// <summary>
        /// Filter registry entries by voice command mode.
        /// Simple mode only includes Core commands; Advanced includes everything.
        /// </summary>
        public static IReadOnlyList<CommandRegistryEntry> FilterByMode(
            IReadOnlyList<CommandRegistryEntry> registry, string mode)
        {
            if (string.Equals(mode, "advanced", StringComparison.OrdinalIgnoreCase))
                return registry;

            // Simple mode: Core commands only
            return registry.Where(e => e.Category == VoiceCommandCategory.Core).ToList().AsReadOnly();
        }

        private static void AddSafeWordRecall(List<CommandRegistryEntry> entries)
        {
            string raw = Settings.GlobalSettings.RecallSafeWord?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return;

            string safeWord = raw.ToLowerInvariant();

            if (ReservedPhrases.Contains(safeWord))
            {
                SpeechLog.Warn(SpeechLogChannel.Voice, $"Safe word \"{safeWord}\" conflicts with a reserved phrase — ignoring.");
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
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "open war map", "show war map", "war map", "show map", "open map", "warmap"));

            entries.Add(Entry("[qi]", "Territory Query", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
                "who controls", "who owns", "what faction controls", "control point", "quadrant info",
                "territory query", "who controls this area", "who controls this quadrant"));

            entries.Add(Entry("[factioninfo]", "Faction Info", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
                "what faction", "my faction", "which faction am i", "faction info",
                "my rank", "what is my rank", "what rank am i",
                "my war points", "how many war points", "my points"));

            entries.Add(Entry("[ArmyStatus]", "Army Status", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
                "army status", "squad status", "troop status", "how many troops",
                "army report", "my army", "check army", "check troops", "troop report"));

            entries.Add(Entry("[DispatchSquad infantry_standard]", "Dispatch Infantry", isEager: false,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "dispatch infantry", "send infantry", "deploy infantry",
                "send troops", "dispatch troops", "send squad"));

            entries.Add(Entry("[DispatchSquad archer_squad]", "Dispatch Archers", isEager: false,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "dispatch archers", "send archers", "deploy archers", "send ranged"));

            entries.Add(Entry("[DispatchSquad cavalry_squad]", "Dispatch Cavalry", isEager: false,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "dispatch cavalry", "send cavalry", "deploy cavalry", "send mounted"));

            entries.Add(Entry("[DispatchSquad support_squad]", "Dispatch Support", isEager: false,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "dispatch medics", "send medics", "deploy support", "send healers"));

            entries.Add(Entry("[DispatchSquad assault_squad]", "Dispatch Assault", isEager: false,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Advanced,
                "dispatch assault", "send assault", "deploy heavy", "send siege"));

            entries.Add(Entry("[voice proximity]", "Proximity Channel", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
                "proximity voice", "proximity channel", "local chat", "switch to proximity",
                "local voice", "area voice"));

            entries.Add(Entry("[voice faction]", "Faction Channel", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
                "switch to faction", "faction channel", "join faction chat",
                "faction voice", "faction chat"));

            entries.Add(Entry("[voice guild]", "Guild Channel", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Advanced,
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

        // ── Heal commands (with auto-targeting) ──────────────────────────

        private static void AddHealCommands(List<CommandRegistryEntry> entries)
        {
            // "heal" alone or "heal self" / "heal me" → Cast Heal, auto-target self
            entries.Add(Entry("heal:self", "Heal Self", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "heal", "heal self", "heal me", "heal myself"));

            // "heal closest" → Cast Heal, auto-target nearest friendly
            entries.Add(Entry("heal:closest", "Heal Closest", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "heal closest", "heal nearest", "heal nearby"));

            // "heal <name>" is handled dynamically by HealCommandStrategy at runtime

            // Greater Heal variants
            entries.Add(Entry("gheal:self", "Greater Heal Self", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "greater heal", "greater heal self", "greater heal me", "big heal", "big heal self"));

            entries.Add(Entry("gheal:closest", "Greater Heal Closest", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "greater heal closest", "greater heal nearest", "big heal closest"));

            // Cure variants
            entries.Add(Entry("cure:self", "Cure Self", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "cure", "cure self", "cure me", "cure myself"));

            entries.Add(Entry("cure:closest", "Cure Closest", isEager: true,
                CombatContext.Any, VoiceCommandCategory.Core,
                "cure closest", "cure nearest"));
        }

        // ── Inventory sorting commands ───────────────────────────────────

        private static void AddInventoryCommands(List<CommandRegistryEntry> entries)
        {
            entries.Add(Entry("[sortbackpack]", "Sort Backpack", isEager: true,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Core,
                "sort backpack", "sort inventory", "sort bag", "sort items"));

            entries.Add(Entry("[sortbackpack name]", "Sort By Name", isEager: true,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Core,
                "sort by name", "alphabetical sort", "sort alphabetically"));

            entries.Add(Entry("[sortbackpack weight]", "Sort By Weight", isEager: true,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Core,
                "sort by weight", "sort heaviest", "weight sort"));

            entries.Add(Entry("[organizebackpack]", "Organize Backpack", isEager: true,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Core,
                "organize backpack", "organize inventory", "organize bag",
                "tidy backpack", "tidy inventory"));

            entries.Add(Entry("[cleanbackpack]", "Clean Backpack", isEager: true,
                CombatContext.NonCombatOnly, VoiceCommandCategory.Core,
                "clean backpack", "clean inventory", "tidy up", "clean bag"));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static CommandRegistryEntry Entry(string command, string label, bool isEager, params string[] phrases)
            => Entry(command, label, isEager, CombatContext.Any, VoiceCommandCategory.Core, phrases);

        private static CommandRegistryEntry Entry(string command, string label, bool isEager,
            CombatContext combat, VoiceCommandCategory category, params string[] phrases)
        {
            var filtered = phrases.Where(p => !ReservedPhrases.Contains(p)).ToArray();
            return new CommandRegistryEntry
            {
                Command = command,
                Label = label,
                Phrases = filtered,
                Keywords = ExtractKeywords(filtered),
                IsEager = isEager,
                Combat = combat,
                Category = category
            };
        }

        private static string[] ExtractKeywords(string[] phrases)
        {
            // Stop words to ignore when building keyword set
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "to", "my", "is", "am", "i", "in", "of",
                "at", "it", "how", "what", "who", "show", "open", "close",
                "please", "can", "could", "would", "do", "just", "now",
                "go", "hey", "use", "get", "set"
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
