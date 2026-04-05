// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using SDL3;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>States reported to <see cref="ActionInferenceEngine.StateChanged"/>.</summary>
    internal enum SpeechInferenceState
    {
        Idle,       // mic on, waiting
        Listening,  // STT partial result arriving
        Thinking,   // LLM request in-flight
        Result,     // command matched / HUD showing
        Error       // LLM or STT error
    }

    /// <summary>
    /// Orchestrates voice-to-action inference.
    ///
    /// Two entry points:
    ///   HandlePartialResult — runs TokenScorer for eager commands (instant, no HUD)
    ///   HandleFinalResult   — runs LlmScorer or TokenScorer, shows ActionHudGump
    ///
    /// Shared singleton accessible via VoiceInteractionManager.InferenceEngine.
    /// </summary>
    internal sealed class ActionInferenceEngine : IDisposable
    {
        private const float EAGER_THRESHOLD = 0.85f;

        private readonly World _world;
        private readonly TokenScorer _tokenScorer = new TokenScorer();
        private readonly DedupGuard _eagerDedup = new DedupGuard();
        private LlmScorer _llmScorer;
        private IReadOnlyList<CommandRegistryEntry> _registry;

        // Active HUD gump — null when hidden
        private ActionHudGump _activeHud;
        private readonly object _hudLock = new object();

        private bool _disposed;

        /// <summary>Fired (from any thread) when inference state changes. Arg is label text (e.g. partial text).</summary>
        public event Action<SpeechInferenceState, string> StateChanged;

        private void SetState(SpeechInferenceState state, string label = "")
            => StateChanged?.Invoke(state, label ?? string.Empty);

        public ActionInferenceEngine(World world)
        {
            _world = world;
        }

        public void Initialize(IReadOnlyList<CommandRegistryEntry> registry)
        {
            _registry = registry;

            var settings = Settings.GlobalSettings;
            if (settings.InferenceBackend == "llm")
            {
                _llmScorer = new LlmScorer(settings.LlmBaseUrl, settings.LlmModel, registry);
            }
        }

        // ── Public entry points ───────────────────────────────────────────────

        /// <summary>
        /// Called on every Vosk partial result. Runs TokenScorer for eager command detection.
        /// If a high-confidence eager command is matched, it fires immediately with no HUD.
        /// Also handles voice confirmation/cancellation while HUD is open.
        /// </summary>
        public void HandlePartialResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _registry == null) return;

            // Voice confirm/cancel while HUD is showing
            if (_activeHud != null)
            {
                string trimmed = text.Trim();
                if (VoicePhrases.Confirm.Contains(trimmed))
                {
                    _activeHud?.ExecuteTop();
                    return;
                }
                if (VoicePhrases.Cancel.Contains(trimmed))
                {
                    _activeHud?.Cancel();
                    return;
                }
            }

            // Show listening state with partial text
            SetState(SpeechInferenceState.Listening, text);

            // Eager scoring — fast path
            var (entry, confidence) = _tokenScorer.ScoreEager(text, _registry);
            if (entry == null || !entry.IsEager) return;
            if (confidence < EAGER_THRESHOLD) return;

            // Combat context check — skip if command doesn't match current combat state
            if (!IsAllowedByCombatContext(entry)) return;

            // Dedup: don't fire the same command twice in 2 seconds from partial alone
            if (_eagerDedup.CheckAndRecord(entry.Command))
                return;

            // Close any open HUD and execute immediately
            CloseHud();
            ExecuteCommand(entry.Command);

            SetState(SpeechInferenceState.Result, entry.Label);

            // Flash indicator shown by the HUD system — post a brief toast
            ShowEagerToast(entry.Label);
        }

        /// <summary>
        /// Called on each Vosk final result. Runs inference (LLM or TokenScorer)
        /// and shows the ActionHudGump with the ranked top-3.
        /// </summary>
        public void HandleFinalResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _registry == null) return;

            // If an eager command fired recently, suppress the HUD entirely for this utterance.
            if (_eagerDedup.FiredRecently())
                return;

            SetState(SpeechInferenceState.Thinking, text);

            // Run inference (async to not block the audio callback thread)
            _ = Task.Run(async () => await RunInferenceAndShowHud(text));
        }

        /// <summary>
        /// Called by GameSceneInputHandler while the HUD is visible.
        /// Returns true if the key was consumed.
        /// </summary>
        public bool HandleKey(SDL.SDL_Keycode key)
        {
            if (_activeHud == null) return false;

            switch (key)
            {
                case SDL.SDL_Keycode.SDLK_2:
                    _activeHud.ExecuteAlternative(1);
                    return true;
                case SDL.SDL_Keycode.SDLK_3:
                    _activeHud.ExecuteAlternative(2);
                    return true;
                case SDL.SDL_Keycode.SDLK_ESCAPE:
                    _activeHud.Cancel();
                    return true;
            }
            return false;
        }

        public bool IsHudVisible => _activeHud != null;

        // ── Private ───────────────────────────────────────────────────────────

        private async Task RunInferenceAndShowHud(string transcript)
        {
            InferenceResult result = null;

            // Run TokenScorer in parallel regardless of backend (used as fallback)
            var tokenTask = Task.Run(() => _tokenScorer.Score(transcript, _registry));

            if (_llmScorer != null && Settings.GlobalSettings.InferenceBackend == "llm")
            {
                using var cts = new CancellationTokenSource(800);
                result = await _llmScorer.ScoreAsync(transcript, _registry, cts.Token);
            }

            // Fallback to TokenScorer if LLM failed or not configured
            if (result == null || !result.HasResults)
                result = await tokenTask;

            if (result == null || !result.HasResults)
            {
                SetState(SpeechInferenceState.Error, "No match");
                return;
            }

            SetState(SpeechInferenceState.Result, result.Actions[0].Label);

            // Show HUD on the main game thread via the game's action queue
            Client.Game.EnqueueAction(0, () => ShowHud(result));
        }

        private void ShowHud(InferenceResult result)
        {
            CloseHud();

            var hud = new ActionHudGump(_world, result,
                onExecute: (index) =>
                {
                    var action = result.Actions[index];
                    _tokenScorer.RecordExecuted(action.Command);
                    ExecuteCommand(action.Command);
                    ClearHudRef();
                },
                onCancel: () => ClearHudRef());

            _activeHud = hud;
            UIManager.Add(hud);
        }

        private void CloseHud()
        {
            if (_activeHud == null) return;
            _activeHud.Dispose();
            _activeHud = null;
        }

        private void ClearHudRef() => _activeHud = null;

        private void ShowEagerToast(string label)
        {
            // Brief journal message as toast — a dedicated toast gump can replace this later
            _world.Journal?.Add($"[Voice] ⚡ {label}", 0x03B2, "System", null,
                ClassicUO.Game.Data.TextType.SYSTEM, false, MessageType.Regular);
        }

        private bool IsAllowedByCombatContext(CommandRegistryEntry entry)
        {
            if (entry.Combat == CombatContext.Any) return true;
            bool inCombat = _world?.Player?.InWarMode == true;
            return entry.Combat == CombatContext.CombatOnly ? inCombat : !inCombat;
        }

        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;

            // Heal commands: "heal:self", "heal:closest", "gheal:self", etc.
            if (command.StartsWith("heal:", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith("gheal:", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith("cure:", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteHealCommand(command);
                return;
            }

            if (command.StartsWith("saferecall:", StringComparison.OrdinalIgnoreCase))
            {
                uint runeSerial = Settings.GlobalSettings.RecallRuneSerial;
                if (runeSerial == 0 || _world?.Player == null) return;

                var recallMacro = new MacroObject(MacroType.CastSpell, MacroSubType.Recall);
                _world.Macros.SetMacroToExecute(recallMacro);
                _world.Macros.Update();

                _ = Task.Delay(150).ContinueWith(_ =>
                    Client.Game.EnqueueAction(0, () => GameActions.DoubleClick(_world, runeSerial)));
                return;
            }

            if (command.StartsWith("macro:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = command.Split(':');
                if (parts.Length == 3
                    && Enum.TryParse<MacroType>(parts[1], out var mt)
                    && Enum.TryParse<MacroSubType>(parts[2], out var mst))
                {
                    var macro = new MacroObject(mt, mst);
                    _world.Macros.SetMacroToExecute(macro);
                    _world.Macros.Update();
                }
            }
            else if (_world?.Player != null)
            {
                GameActions.Say(command);
            }
        }

        private void ExecuteHealCommand(string command)
        {
            if (_world?.Player == null) return;

            // Parse "heal:self", "gheal:closest", "cure:self", etc.
            var parts = command.Split(':');
            if (parts.Length != 2) return;

            string spellType = parts[0].ToLowerInvariant();
            string target = parts[1].ToLowerInvariant();

            // Determine which spell to cast
            MacroSubType spell = spellType switch
            {
                "heal" => MacroSubType.Heal,
                "gheal" => MacroSubType.GreaterHeal,
                "cure" => MacroSubType.Cure,
                _ => MacroSubType.Heal
            };

            // Cast the spell
            var macro = new MacroObject(MacroType.CastSpell, spell);
            _world.Macros.SetMacroToExecute(macro);
            _world.Macros.Update();

            // Auto-target after spell cast delay
            _ = Task.Delay(150).ContinueWith(_ =>
                Client.Game.EnqueueAction(0, () => AutoTarget(target)));
        }

        private void AutoTarget(string target)
        {
            if (_world?.Player == null) return;

            if (target == "self")
            {
                _world.TargetManager.Target(_world.Player.Serial);
            }
            else if (target == "closest")
            {
                // Find nearest friendly mobile (party member or pet)
                uint nearestSerial = 0;
                int nearestDist = int.MaxValue;

                foreach (var mobile in _world.Mobiles.Values)
                {
                    if (mobile.Serial == _world.Player.Serial) continue;
                    if (mobile.Distance >= nearestDist) continue;

                    // Accept party members, pets, and non-hostile NPCs
                    if (mobile.NotorietyFlag == Game.Data.NotorietyFlag.Ally
                        || mobile.NotorietyFlag == Game.Data.NotorietyFlag.Innocent)
                    {
                        nearestDist = mobile.Distance;
                        nearestSerial = mobile.Serial;
                    }
                }

                if (nearestSerial != 0)
                    _world.TargetManager.Target(nearestSerial);
                else
                    _world.TargetManager.Target(_world.Player.Serial); // fallback to self
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseHud();
            _llmScorer?.Dispose();
        }
    }
}
