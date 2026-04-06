// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.SpeechRecognition.Gumps
{
    /// <summary>
    /// Small floating gump for speech recognition settings.
    /// Opened via -showspeech command or auto-shown on game enter
    /// when speech or proximity chat is enabled.
    /// Shows voice command mode (Off/Basic/Simple/Advanced), mic mute state,
    /// and proximity chat toggle.
    /// </summary>
    internal sealed class SpeechSettingsGump : Gump
    {
        private const int WIDTH = 220;
        private const int HEIGHT = 90;
        private const int MARGIN = 8;
        private const int ROW_HEIGHT = 22;
        private const int LABEL_COL = 65;
        private const ushort WHITE = 0xFFFF;

        private static readonly string[] SPEECH_MODES = { "Off", "Basic", "Simple", "Advanced" };

        private readonly Label _modeLabel;
        private readonly Label _micStatusLabel;
        private readonly Label _proxLabel;

        private volatile bool _dirty;

        public SpeechSettingsGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptKeyboardInput = false;

            X = 12;
            Y = 100;

            var bg = new AlphaBlendControl(0.50f) { X = 0, Y = 0, Width = WIDTH, Height = HEIGHT };
            Add(bg);

            int y = MARGIN;

            // ── Speech mode row: Off / Simple / Advanced ──
            Add(new Label("Speech:", true, WHITE, 0, 1) { X = MARGIN, Y = y });

            _modeLabel = new Label(GetCurrentModeLabel(), true, WHITE, 0, 1)
                { X = LABEL_COL, Y = y };
            Add(_modeLabel);

            var toggleBtn = new NiceButton(WIDTH - MARGIN - 50, y - 2, 50, 20, ButtonAction.Activate, "Toggle")
            { IsSelectable = false };
            toggleBtn.MouseUp += (sender, e) => OnToggleMode();
            Add(toggleBtn);

            y += ROW_HEIGHT;

            // ── Mic status row ──
            Add(new Label("Mic:", true, WHITE, 0, 1) { X = MARGIN, Y = y });

            bool muted = world.VivoxManager?.IsMicMuted ?? false;
            _micStatusLabel = new Label(
                muted ? "● OFF" : "● ON",
                true,
                muted ? (ushort)0x0026 : (ushort)0x004F,
                0, 1)
                { X = LABEL_COL, Y = y };
            Add(_micStatusLabel);

            var micBtn = new NiceButton(WIDTH - MARGIN - 50, y - 2, 50, 20, ButtonAction.Activate, "Mute")
            { IsSelectable = false };
            micBtn.MouseUp += (sender, e) => world.VivoxManager?.ToggleMicMute();
            Add(micBtn);

            y += ROW_HEIGHT;

            // ── Proximity chat row ──
            Add(new Label("Prox:", true, WHITE, 0, 1) { X = MARGIN, Y = y });

            bool proxEnabled = ProfileManager.CurrentProfile?.EnableProximityChat ?? true;
            _proxLabel = new Label(
                proxEnabled ? "● ON" : "● OFF",
                true,
                proxEnabled ? (ushort)0x004F : (ushort)0x0026,
                0, 1)
                { X = LABEL_COL, Y = y };
            Add(_proxLabel);

            var proxBtn = new NiceButton(WIDTH - MARGIN - 50, y - 2, 50, 20, ButtonAction.Activate, "Toggle")
            { IsSelectable = false };
            proxBtn.MouseUp += (sender, e) => OnToggleProximity();
            Add(proxBtn);

            // Subscribe to voice state changes
            if (world.VivoxManager != null)
            {
                world.VivoxManager.VoiceStateChanged += OnVoiceStateChanged;
            }
        }

        private void OnToggleMode()
        {
            string current = (Settings.GlobalSettings.VoiceCommandMode ?? "basic").ToLowerInvariant();

            // Cycle: basic → simple → advanced → off → basic
            string next = current switch
            {
                "basic" => "simple",
                "simple" => "advanced",
                "advanced" => "off",
                _ => "basic"
            };

            Settings.GlobalSettings.VoiceCommandMode = next;
            Settings.GlobalSettings.SpeechRecognitionEnabled = !string.Equals(next, "off", System.StringComparison.OrdinalIgnoreCase);
            _modeLabel.Text = GetModeDisplayName(next);
        }

        private void OnToggleProximity()
        {
            var profile = ProfileManager.CurrentProfile;
            if (profile == null) return;

            profile.EnableProximityChat = !profile.EnableProximityChat;
            _proxLabel.Text = profile.EnableProximityChat ? "● ON" : "● OFF";
            _proxLabel.Hue = profile.EnableProximityChat ? (ushort)0x004F : (ushort)0x0026;
        }

        private void OnVoiceStateChanged()
        {
            _dirty = true;
        }

        public override void Update()
        {
            base.Update();

            if (_dirty)
            {
                _dirty = false;
                bool muted = World.VivoxManager?.IsMicMuted ?? false;
                _micStatusLabel.Text = muted ? "● OFF" : "● ON";
                _micStatusLabel.Hue = muted ? (ushort)0x0026 : (ushort)0x004F;
            }
        }

        public override void Dispose()
        {
            if (World.VivoxManager != null)
            {
                World.VivoxManager.VoiceStateChanged -= OnVoiceStateChanged;
            }

            base.Dispose();
        }

        private static string GetCurrentModeLabel()
        {
            string mode = Settings.GlobalSettings.VoiceCommandMode ?? "basic";
            return GetModeDisplayName(mode);
        }

        private static string GetModeDisplayName(string mode)
        {
            return mode.ToLowerInvariant() switch
            {
                "basic" => "Basic",
                "advanced" => "Advanced",
                "off" => "Off",
                _ => "Simple"
            };
        }
    }
}
