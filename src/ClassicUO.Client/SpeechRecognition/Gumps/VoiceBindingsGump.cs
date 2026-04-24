// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using SDL3;

namespace ClassicUO.SpeechRecognition.Gumps
{
    /// <summary>
    /// Voice chat key-binding panel. Rebinds the four voice hotkeys
    /// (Proximity PTT / Faction PTT / Guild PTT / Mic Mute) and toggles
    /// the proximity transmit mode between Push-to-Talk and Always-On.
    ///
    /// Faction and guild are always PTT regardless of the proximity mode —
    /// broadcast channels should never leak accidental mic audio.
    ///
    /// Style matches SpeechSettingsGump: solid black background, small
    /// unicode font, per-row label + value + right-aligned action button.
    /// Click a binding row's Rebind button to arm it, then press a key to
    /// assign it. Press Backspace while armed to clear the binding.
    /// </summary>
    internal sealed class VoiceBindingsGump : Gump
    {
        private const int WIDTH       = 260;
        private const int HEIGHT      = 176;
        private const int MARGIN      = 8;
        private const int ROW_HEIGHT  = 22;
        private const int LABEL_COL   = 8;
        private const int VALUE_COL   = 110;
        private const int BUTTON_W    = 56;
        private const int BUTTON_H    = 20;
        private const int BUTTON_COL  = WIDTH - MARGIN - BUTTON_W;
        private const ushort WHITE    = 0xFFFF;
        private const ushort AMBER    = 0x0035;
        private const ushort GREEN    = 0x004F;
        private const ushort RED      = 0x0026;

        // Which binding field is currently being armed, or null if none.
        private BindingRow _armed;

        private readonly Label _proxModeLabel;

        public VoiceBindingsGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            CanCloseWithEsc = false; // Escape is reserved for clearing an armed binding
            AcceptKeyboardInput = true;

            X = 100;
            Y = 100;
            Width = WIDTH;
            Height = HEIGHT;

            // Solid black background — stack two opaque alpha fills so the
            // panel reads as pure black regardless of what's behind it.
            Add(new AlphaBlendControl(1.0f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT });
            Add(new AlphaBlendControl(1.0f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT });

            int y = MARGIN;

            // Title
            Add(new Label("Voice Bindings", true, WHITE, 0, 1, FontStyle.BlackBorder)
                { X = MARGIN, Y = y });

            // Explicit close X in the top-right — backstop for the right-click path.
            var closeBtn = new NiceButton(WIDTH - MARGIN - 18, y - 2, 18, 20,
                ButtonAction.Activate, "X") { IsSelectable = false };
            closeBtn.MouseUp += (_, __) => Dispose();
            Add(closeBtn);

            y += ROW_HEIGHT + 2;

            // Binding rows
            AddBindingRow("Proximity PTT", y, () => CurrentProfile.VoicePttProximityKey,
                          v => CurrentProfile.VoicePttProximityKey = v);
            y += ROW_HEIGHT;

            AddBindingRow("Faction PTT", y, () => CurrentProfile.VoicePttFactionKey,
                          v => CurrentProfile.VoicePttFactionKey = v);
            y += ROW_HEIGHT;

            AddBindingRow("Guild PTT", y, () => CurrentProfile.VoicePttGuildKey,
                          v => CurrentProfile.VoicePttGuildKey = v);
            y += ROW_HEIGHT;

            AddBindingRow("Mic Mute", y, () => CurrentProfile.VoiceMicMuteKey,
                          v => CurrentProfile.VoiceMicMuteKey = v);
            y += ROW_HEIGHT + 4;

            // Proximity mode toggle row
            Add(new Label("Prox Mode:", true, WHITE, 0, 1) { X = LABEL_COL, Y = y });

            _proxModeLabel = new Label(GetModeText(), true, GetModeHue(), 0, 1)
                { X = VALUE_COL, Y = y };
            Add(_proxModeLabel);

            var modeBtn = new NiceButton(BUTTON_COL, y - 2, BUTTON_W, BUTTON_H,
                ButtonAction.Activate, "Toggle") { IsSelectable = false };
            modeBtn.MouseUp += (_, __) => OnToggleMode();
            Add(modeBtn);

            y += ROW_HEIGHT + 4;

            // Hint row
            Add(new Label("Rebind -> press key.  ESC = clear.",
                true, 0x0481, 0, 1)
                { X = MARGIN, Y = y });
        }

        // Belt-and-braces right-click close. Some child controls (buttons,
        // labels) can consume mouse events before CloseWithRightClick fires,
        // so intercept at the gump level as well.
        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Right)
            {
                Dispose();
                return;
            }

            base.OnMouseUp(x, y, button);
        }

        private static Profile CurrentProfile => ProfileManager.CurrentProfile;

        private void AddBindingRow(string label, int y,
            System.Func<int> getter, System.Action<int> setter)
        {
            Add(new Label(label + ":", true, WHITE, 0, 1) { X = LABEL_COL, Y = y });

            var valueLabel = new Label(KeyDisplay(getter()), true, AMBER, 0, 1)
                { X = VALUE_COL, Y = y };
            Add(valueLabel);

            var row = new BindingRow(valueLabel, getter, setter);

            var btn = new NiceButton(BUTTON_COL, y - 2, BUTTON_W, BUTTON_H,
                ButtonAction.Activate, "Rebind") { IsSelectable = false };
            btn.MouseUp += (_, __) => ArmRow(row);
            Add(btn);
        }

        private void ArmRow(BindingRow row)
        {
            // Disarm any previously-armed row (restore its displayed value).
            if (_armed != null && _armed != row)
                _armed.ValueLabel.Text = KeyDisplay(_armed.Getter());

            _armed = row;
            row.ValueLabel.Text = "<press a key>";
            row.ValueLabel.Hue  = GREEN;
            UIManager.KeyboardFocusControl = this;
        }

        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (_armed == null)
            {
                base.OnKeyDown(key, mod);
                return;
            }

            // Escape clears the binding.
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
            {
                _armed.Setter(0);
                _armed.ValueLabel.Text = "<unbound>";
                _armed.ValueLabel.Hue  = RED;
                _armed = null;
                return;
            }

            // Reject modifier-only keypresses — wait for a real key.
            if (key == SDL.SDL_Keycode.SDLK_LCTRL  || key == SDL.SDL_Keycode.SDLK_RCTRL  ||
                key == SDL.SDL_Keycode.SDLK_LSHIFT || key == SDL.SDL_Keycode.SDLK_RSHIFT ||
                key == SDL.SDL_Keycode.SDLK_LALT   || key == SDL.SDL_Keycode.SDLK_RALT)
            {
                return;
            }

            _armed.Setter((int)key);
            _armed.ValueLabel.Text = KeyDisplay((int)key);
            _armed.ValueLabel.Hue  = AMBER;
            _armed = null;
        }

        private void OnToggleMode()
        {
            var p = CurrentProfile;
            if (p == null) return;

            p.ProximityAlwaysOn = !p.ProximityAlwaysOn;
            _proxModeLabel.Text = GetModeText();
            _proxModeLabel.Hue  = GetModeHue();

            // Apply immediately so the mic state matches the new mode.
            World.VivoxManager?.ApplyProximityMode();
        }

        private string GetModeText()
        {
            var p = CurrentProfile;
            return (p != null && p.ProximityAlwaysOn) ? "Always On" : "Push to Talk";
        }

        private ushort GetModeHue()
        {
            var p = CurrentProfile;
            return (p != null && p.ProximityAlwaysOn) ? GREEN : AMBER;
        }

        private static string KeyDisplay(int sdlKeycode)
        {
            if (sdlKeycode == 0) return "<unbound>";
            string name = KeysTranslator.TryGetKey((SDL.SDL_Keycode)sdlKeycode, SDL.SDL_Keymod.SDL_KMOD_NONE);
            return string.IsNullOrEmpty(name) ? $"Key({sdlKeycode:X})" : name;
        }

        /// <summary>Holds one rebind row's current value label plus getter/setter.</summary>
        private sealed class BindingRow
        {
            public readonly Label ValueLabel;
            public readonly System.Func<int> Getter;
            public readonly System.Action<int> Setter;

            public BindingRow(Label valueLabel,
                System.Func<int> getter, System.Action<int> setter)
            {
                ValueLabel = valueLabel;
                Getter = getter;
                Setter = setter;
            }
        }
    }
}
