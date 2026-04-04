// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition.Interfaces;

namespace ClassicUO.SpeechRecognition.Commands
{
    /// <summary>
    /// Ordered strategy pipeline for routing voice transcripts to game actions.
    /// First matching strategy wins; all others are skipped.
    ///
    /// Pipeline order (by priority):
    ///   1. ListeningToggle  — "speech on/off"
    ///   2. DebugToggle      — "speech debug on/off"
    ///   3. ExactCommand     — speechcommands.json prefix match
    ///   4. FuzzyCommand     — Jaro-Winkler similarity matching
    ///   5. MacroCommand     — 500+ SpeechMacroStrings mappings
    ///   6. PetCommand       — pet/summon control
    ///   7. UowwIntent       — NLP for territory/faction/army
    ///   8. Question         — canned Q&A
    ///   9. AvatarLlm        — LLM fallback (only in chat mode)
    /// </summary>
    internal sealed class CommandRouter
    {
        private readonly World _world;
        private readonly SpeechCommandsManager _commandsManager;
        private readonly SpeechAvatarManager _avatarManager;
        private readonly UowwCommandMap _uowwMap;
        private bool _speechDebug;

        private static readonly Random _random = new Random();

        public CommandRouter(
            World world,
            SpeechCommandsManager commandsManager,
            SpeechAvatarManager avatarManager,
            UowwCommandMap uowwMap)
        {
            _world = world;
            _commandsManager = commandsManager;
            _avatarManager = avatarManager;
            _uowwMap = uowwMap;
        }

        /// <summary>
        /// Route a final (high-confidence) transcript through the strategy pipeline.
        /// </summary>
        public bool RouteFullResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Strip common Vosk prefix artefact
            text = text.TrimStart("the ".ToCharArray()).Trim();

            if (RouteListeningToggle(text)) return true;
            if (_avatarManager.HandleLlmPrompt(text)) return true;

            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) return false;
            if (text.Length <= 1) return false;

            if (_commandsManager.FindSpeechCommand(text)) return true;
            if (RouteFuzzyCommand(text)) return true;
            if (RouteMacroCommand(text)) return true;

            return false;
        }

        /// <summary>
        /// Route a partial (confidence-gated) transcript.
        /// </summary>
        public bool RoutePartialResult(string text, float confidence)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (RouteDebugToggle(text)) return true;
            if (RouteListeningToggle(text)) return true;

            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) return false;

            if (_speechDebug) GameActions.Say("Heard: " + text);
            if (_avatarManager.HandleLlmPrompt(text)) return true;
            if (_commandsManager.FindSpeechCommand(text)) return true;
            if (RoutePetCommand(text)) return true;
            if (RouteUowwIntent(text)) return true;
            if (RouteQuestion(text)) return true;

            return false;
        }

        // ── Individual strategies ─────────────────────────────────────────────

        private bool RouteListeningToggle(string text)
        {
            string lower = text.ToLowerInvariant();
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
            {
                if (SpeechRecognitionStrings.StartListeningCommands.Contains(lower))
                {
                    Settings.GlobalSettings.SpeechRecognitionEnabled = true;
                    PlayerSay($"{GetRandom(SpeechRecognitionStrings.Greetings)}\r\n[Speech On]");
                    return true;
                }
            }
            else
            {
                if (SpeechRecognitionStrings.StopListeningCommands.Contains(lower))
                {
                    Settings.GlobalSettings.SpeechRecognitionEnabled = false;
                    PlayerSay($"{GetRandom(SpeechRecognitionStrings.Farewells)}\r\n[Speech Off]");
                    return true;
                }
            }
            return false;
        }

        private bool RouteDebugToggle(string text)
        {
            if (text.StartsWith("speech debug on", StringComparison.OrdinalIgnoreCase))
            {
                _speechDebug = true;
                PlayerSay("Speech debug on.");
                return true;
            }
            if (text.StartsWith("speech debug off", StringComparison.OrdinalIgnoreCase))
            {
                _speechDebug = false;
                PlayerSay("Speech debug off.");
                return true;
            }
            return false;
        }

        private bool RouteFuzzyCommand(string text)
        {
            if (!Settings.GlobalSettings.NlpIntentEnabled) return false;

            // Build list of known command speech phrases for fuzzy matching
            // This is intentionally lightweight — just tries the top N commands
            // Phase 5+ can expand this with a prebuilt trie or indexed structure
            return false; // placeholder — full impl uses SpeechCommandsManager.GetAllPhrases()
        }

        private bool RouteMacroCommand(string text)
        {
            foreach (var macroType in SpeechMacroStrings.MacroSpeechCommands)
            {
                foreach (var macroSubType in macroType.Value)
                {
                    if (!macroSubType.Value.Any(cmd => text.Equals(cmd, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var macro = new MacroObject(macroType.Key, macroSubType.Key);
                    _world.Macros.SetMacroToExecute(macro);
                    _world.Macros.Update();
                    return true;
                }
            }
            return false;
        }

        private bool RoutePetCommand(string text)
        {
            foreach (var petCommand in SpeechRecognitionStrings.PetCommands)
            {
                foreach (var query in petCommand.Value)
                {
                    if (!text.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

                    string key = petCommand.Key;
                    if (!key.Contains("closest", StringComparison.OrdinalIgnoreCase) &&
                        !key.Contains("nearest", StringComparison.OrdinalIgnoreCase))
                        GameActions.Say(key.ToLowerInvariant());
                    else
                        _commandsManager.FindSpeechCommand(key.Trim());
                    return true;
                }
            }
            return false;
        }

        private bool RouteUowwIntent(string text)
        {
            if (!Settings.GlobalSettings.NlpIntentEnabled) return false;

            var ctx = GameStateContext.Capture(_world);
            var intent = IntentParser.Parse(text, ctx);

            if (intent == ParsedIntent.None) return false;
            return _uowwMap.Execute(intent, ctx);
        }

        private bool RouteQuestion(string text)
        {
            foreach (var question in SpeechRecognitionStrings.PlayerQuestions)
            {
                foreach (var query in question.Value)
                {
                    if (!text.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

                    string key = question.Key;
                    if (key.StartsWith("say ", StringComparison.OrdinalIgnoreCase))
                        GameActions.Say(key[4..]);
                    else if (key.Equals("joke", StringComparison.OrdinalIgnoreCase))
                        GameActions.Say(GetRandom(Jokes.UltimaOnlineJokes));
                    else
                        _commandsManager.FindSpeechCommand(key.Trim());
                    return true;
                }
            }
            return false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void PlayerSay(string message)
        {
            if (_world?.Player != null) GameActions.Say(message);
        }

        private static string GetRandom(string[] array)
        {
            if (array == null || array.Length == 0) return string.Empty;
            return array[_random.Next(array.Length)];
        }
    }
}
