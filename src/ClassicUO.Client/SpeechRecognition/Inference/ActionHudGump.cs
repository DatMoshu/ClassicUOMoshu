// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// HUD overlay shown when ActionInferenceEngine produces a ranked result.
    ///
    /// Layout:
    ///   ┌─────────────────────────────────────┐
    ///   │ 🎙 "show war map"                    │  ← transcript echo
    ///   │                                     │
    ///   │ ▶ Open War Map          ████░░       │  ← #1 + countdown bar
    ///   │   [2] Faction Info      92%          │
    ///   │   [3] Territory Query   44%          │
    ///   │                                     │
    ///   │  [2] [3] [Esc]   say "yes"   1.4s   │
    ///   └─────────────────────────────────────┘
    ///
    /// Positioned bottom-center, above the chat bar.
    /// Auto-executes action[0] after settings.InferenceAutoExecuteMs.
    /// Pressing [2]/[3]/[Esc] or saying "yes"/"cancel" works as described in GDD.
    /// </summary>
    internal sealed class ActionHudGump : Gump
    {
        private const int WIDTH  = 400;
        private const int HEIGHT = 130;
        private const int MARGIN = 10;

        private readonly InferenceResult _result;
        private readonly Action<int> _onExecute;
        private readonly Action _onCancel;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _duration;

        // Dynamic controls updated each frame
        private readonly Label _timerLabel;
        private readonly AlphaBlendControl _barFill;
        private readonly AlphaBlendControl _barBg;
        private bool _fired;

        public ActionHudGump(
            World world,
            InferenceResult result,
            Action<int> onExecute,
            Action onCancel)
            : base(world, 0, 0)
        {
            _result   = result;
            _onExecute = onExecute;
            _onCancel  = onCancel;
            _duration  = Math.Max(500, Settings.GlobalSettings.InferenceAutoExecuteMs);

            CanMove             = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc     = false;
            AcceptKeyboardInput  = false; // keys handled by ActionInferenceEngine via HandleKey()
            IsVisible           = true;

            PositionUpperLeft();
            BuildControls(out _timerLabel, out _barBg, out _barFill);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ExecuteTop() => Execute(0);

        public void ExecuteAlternative(int index)
        {
            if (index < _result.Actions.Count && !string.IsNullOrEmpty(_result.Actions[index].Command))
                Execute(index);
        }

        public void Cancel()
        {
            if (_fired) return;
            _fired = true;
            _onCancel?.Invoke();
            Dispose();
        }

        // ── Update loop ───────────────────────────────────────────────────────

        public override void Update()
        {
            base.Update();

            if (_fired) return;

            double elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

            // Update countdown bar width
            float fill = Math.Clamp(1f - (float)(elapsedMs / _duration), 0f, 1f);
            int barWidth = (int)(_barBg.Width * fill);
            _barFill.Width = Math.Max(0, barWidth);

            _barFill.Alpha = 200;

            // Update remaining time label
            double remaining = Math.Max(0, (_duration - elapsedMs) / 1000.0);
            if (_timerLabel != null)
                _timerLabel.Text = $"{remaining:F1}s";

            // Auto-execute when timer expires
            if (elapsedMs >= _duration)
                Execute(0);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void Execute(int index)
        {
            if (_fired) return;
            _fired = true;
            _onExecute?.Invoke(index);
            Dispose();
        }

        private void PositionUpperLeft()
        {
            X = 12;
            Y = 56; // Below SpeechRecognitionStatusGump (12 + 36 + 8)
        }

        private void BuildControls(out Label timerLbl, out AlphaBlendControl barBg, out AlphaBlendControl barFill)
        {
            int y = MARGIN;

            // ── Panel background ──────────────────────────────────────────────
            var bg = new AlphaBlendControl(0.82f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT };
            Add(bg);

            // ── Transcript echo ───────────────────────────────────────────────
            string echo = _result.Transcript?.Length > 38
                ? _result.Transcript[..38] + "…"
                : _result.Transcript ?? string.Empty;

            Add(new Label($"🎙 \"{echo}\"", false, 0x03B2, WIDTH - MARGIN * 2, 1) { X = MARGIN, Y = y });
            y += 18;

            // ── Countdown bar ─────────────────────────────────────────────────
            barBg = new AlphaBlendControl(0.4f)
            {
                X = MARGIN, Y = y, Width = WIDTH - MARGIN * 2, Height = 4
            };
            Add(barBg);

            barFill = new AlphaBlendControl(0.9f)
            {
                X = MARGIN, Y = y, Width = WIDTH - MARGIN * 2, Height = 4
            };
            Add(barFill);
            y += 8;

            // ── Action rows ───────────────────────────────────────────────────
            var actions = _result.Actions;
            for (int i = 0; i < Math.Min(3, actions.Count); i++)
            {
                var action = actions[i];
                if (string.IsNullOrEmpty(action.Command)) continue;

                string prefix = i == 0 ? "▶ " : $"[{i + 1}] ";
                ushort hue = i == 0 ? (ushort)0xFFFF : (ushort)0x0386;

                // Low confidence tints top row amber
                if (i == 0 && action.Confidence < 0.5f) hue = 0x0035;

                string pct    = $"{(int)(action.Confidence * 100)}%";
                string rowTxt = $"{prefix}{action.Label}";

                Add(new Label(rowTxt, false, hue, WIDTH - 60 - MARGIN * 2, 1)
                    { X = MARGIN, Y = y });
                Add(new Label(pct, false, hue, 40, 1, align: TEXT_ALIGN_TYPE.TS_RIGHT)
                    { X = WIDTH - 44, Y = y });
                y += 16;
            }

            // ── Footer: hints + timer ─────────────────────────────────────────
            y += 2;
            Add(new Label("[2] [3] [Esc]   say \"yes\"", false, 0x0386, WIDTH - 60, 1)
                { X = MARGIN, Y = y });

            timerLbl = new Label("1.5s", false, 0x03B2, 40, 1, align: TEXT_ALIGN_TYPE.TS_RIGHT)
                { X = WIDTH - 44, Y = y };
            Add(timerLbl);
        }
    }
}
