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
    /// Ordered strategy pipeline for routing final voice transcripts to game actions.
    /// Partials are used only for control toggles (listening on/off, debug) — never
    /// for command execution, which prevents duplicate firing as Vosk streams results.
    ///
    /// Pipeline order for final results (by priority):
    ///   1. ListeningToggle  — "speech on/off"
    ///   2. AvatarLlm        — "hey avatar ..."
    ///   3. ExactCommand     — speechcommands.json prefix match
    ///   4. MacroCommand     — SpeechMacroStrings mappings
    ///   5. PetCommand       — pet/summon control
    ///   6. UowwIntent       — NLP for territory/faction/army
    ///   7. Question         — canned Q&A
    /// </summary>
    internal sealed class CommandRouter
    {
        private readonly World _world;
        private readonly SpeechCommandsManager _commandsManager;
        private readonly SpeechAvatarManager _avatarManager;
        private readonly UowwCommandMap _uowwMap;
        private volatile bool _speechDebug;

        // Dedup guard: prevent re-executing the same command within this window
        private string _lastExecutedCommand = string.Empty;
        private long _lastExecutedTickMs;
        private const long DEDUP_WINDOW_MS = 2000;

        private static readonly Random _random = new Random();

        // Phrases that confirm or cancel the ActionHud while it is showing — never route as commands
        private static readonly HashSet<string> ConfirmationPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "accept", "confirm", "do it", "go", "ok", "okay"
        };
        private static readonly HashSet<string> CancellationPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            "no", "cancel", "stop", "abort", "never mind", "nevermind"
        };

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

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Route a final (high-confidence) transcript through the full strategy pipeline.
        /// This is the only path that executes game commands.
        /// </summary>
        public bool RouteFullResult(string text)
        {
            Console.WriteLine($"[Route:Full] text='{text}'  SpeechEnabled={Settings.GlobalSettings.SpeechRecognitionEnabled}");
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (RouteListeningToggle(text)) return true;

            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) { Console.WriteLine("[Route:Full] Blocked — SpeechRecognitionEnabled=false"); return false; }
            if (text.Length <= 1) return false;

            // Ignore confirmation/cancellation phrases — handled by ActionHud
            if (ConfirmationPhrases.Contains(text) || CancellationPhrases.Contains(text)) return false;

            if (IsDuplicate(text)) return false;

            if (_avatarManager.HandleLlmPrompt(text)) { RecordExecuted(text); return true; }
            if (_commandsManager.FindSpeechCommand(text)) { RecordExecuted(text); return true; }
            if (RouteMacroCommand(text)) { RecordExecuted(text); return true; }
            if (RoutePetCommand(text)) return true; // RoutePetCommand manages its own dedup per key
            if (RouteUowwIntent(text)) { RecordExecuted(text); return true; }
            if (RouteQuestion(text)) { RecordExecuted(text); return true; }

            return false;
        }

        /// <summary>
        /// Route a partial transcript through the full strategy pipeline.
        /// Partials are the primary execution path for Vosk streaming mode —
        /// final results only arrive after a silence gap, so commands must
        /// be routable from partials for low-latency response.
        /// </summary>
        public bool RoutePartialResult(string text, float confidence)
        {
            Console.WriteLine($"[Route:Partial] text='{text}' conf={confidence:F2}  SpeechEnabled={Settings.GlobalSettings.SpeechRecognitionEnabled}");
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (RouteDebugToggle(text)) return true;
            if (RouteListeningToggle(text)) return true;

            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) { Console.WriteLine("[Route:Partial] Blocked — SpeechRecognitionEnabled=false"); return false; }

            if (_speechDebug) GameActions.Say("Heard: " + text);
            if (_avatarManager.HandleLlmPrompt(text)) { Console.WriteLine($"[Route:Partial] → AvatarLlm matched"); return true; }
            if (_commandsManager.FindSpeechCommand(text)) { Console.WriteLine($"[Route:Partial] → SpeechCommand matched"); return true; }
            if (RoutePetCommand(text)) { Console.WriteLine($"[Route:Partial] → PetCommand matched"); return true; }
            if (RouteUowwIntent(text)) { Console.WriteLine($"[Route:Partial] → UowwIntent matched"); return true; }
            if (RouteQuestion(text)) { Console.WriteLine($"[Route:Partial] → Question matched"); return true; }

            Console.WriteLine($"[Route:Partial] No match for '{text}'");
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

                    // Per-command dedup: use the matched key, not the full transcript.
                    // Different Vosk transcriptions of the same pet command (e.g. "attack" vs
                    // "attack him") both map to the same key — dedup on the key prevents double-fire.
                    string dedupKey = key.ToLowerInvariant();
                    if (IsDuplicate(dedupKey)) return true; // suppress silently
                    RecordExecuted(dedupKey);

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

        // ── Dedup guard ───────────────────────────────────────────────────────

        private bool IsDuplicate(string text)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (string.Equals(text, _lastExecutedCommand, StringComparison.OrdinalIgnoreCase)
                && nowMs - _lastExecutedTickMs < DEDUP_WINDOW_MS)
                return true;
            return false;
        }

        private void RecordExecuted(string text)
        {
            _lastExecutedCommand = text;
            _lastExecutedTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
