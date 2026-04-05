// SPDX-License-Identifier: BSD-2-Clause

using System.Diagnostics;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// Small corner widget showing the current voice inference state.
    ///
    ///   ● Idle      — hidden
    ///   ● Listening — cyan,  partial STT text
    ///   ● Thinking  — amber, "LLM…"
    ///   ● Result    — green, command label (briefly)
    ///   ● Error     — red,   "No match"
    ///
    /// Positioned bottom-left, above the chat bar.
    /// Auto-hides IDLE_TIMEOUT_MS after the last state update.
    ///
    /// Thread-safe: SetState may be called from any thread;
    /// rendering happens on the game thread via Update().
    /// </summary>
    internal sealed class SpeechRecognitionStatusGump : Gump
    {
        private const int WIDTH          = 320;
        private const int HEIGHT         = 36;
        private const int MARGIN         = 8;
        private const int IDLE_TIMEOUT_MS = 8000;

        private Label _statusLabel;

        private volatile int _pendingState = (int)SpeechInferenceState.Idle;
        private string       _pendingLabel  = string.Empty;
        private bool         _dirty         = false;

        private SpeechInferenceState _shownState = SpeechInferenceState.Idle;
        private readonly Stopwatch   _idleTimer  = Stopwatch.StartNew();

        public SpeechRecognitionStatusGump(World world) : base(world, 0, 0)
        {
            CanMove               = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc       = false;
            AcceptKeyboardInput   = false;
            IsVisible             = false; // hidden until first non-Idle state

            PositionUpperLeft();

            var bg = new AlphaBlendControl(0.72f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT };
            Add(bg);

            _statusLabel = new Label(string.Empty, false, 0x0386, WIDTH - MARGIN * 2, 1)
                { X = MARGIN, Y = 10 };
            Add(_statusLabel);
        }

        // ── Public API — thread-safe ──────────────────────────────────────────

        /// <summary>
        /// Update the displayed state. May be called from any thread.
        /// The gump redraws on the next game-thread Update() tick.
        /// </summary>
        public void SetState(SpeechInferenceState state, string label)
        {
            _pendingState = (int)state;
            _pendingLabel = label ?? string.Empty;
            _dirty = true;
        }

        // ── Update loop (game thread) ─────────────────────────────────────────

        public override void Update()
        {
            base.Update();

            if (_dirty)
            {
                _dirty      = false;
                _shownState = (SpeechInferenceState)_pendingState;
                ApplyState(_shownState, _pendingLabel);
                _idleTimer.Restart();
            }

            // Auto-hide after timeout for non-Idle states
            if (_shownState != SpeechInferenceState.Idle
                && _idleTimer.ElapsedMilliseconds > IDLE_TIMEOUT_MS)
            {
                _shownState = SpeechInferenceState.Idle;
                IsVisible   = false;
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyState(SpeechInferenceState state, string label)
        {
            if (state == SpeechInferenceState.Idle)
            {
                IsVisible = false;
                return;
            }

            string truncated = label.Length > 40 ? label[..40] + "…" : label;

            var (bullet, hue, displayText) = state switch
            {
                SpeechInferenceState.Listening => ("●", (ushort)0x0085, truncated), // cyan
                SpeechInferenceState.Thinking  => ("●", (ushort)0x0035, "Thinking…"), // amber
                SpeechInferenceState.Result    => ("●", (ushort)0x004F, truncated), // green
                SpeechInferenceState.Error     => ("●", (ushort)0x0026, truncated), // red
                _                              => ("●", (ushort)0x0386, truncated)
            };

            _statusLabel.Text = $"{bullet} {displayText}";
            _statusLabel.Hue  = hue;
            IsVisible         = true;
        }

        private void PositionUpperLeft()
        {
            X = 12;
            Y = 12;
        }
    }
}
