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
        private const int HEIGHT = 114;
        private const int MARGIN = 8;
        private const int ROW_HEIGHT = 22;
        private const int LABEL_COL = 65;
        private const ushort WHITE = 0xFFFF;

        private readonly Label _modeLabel;
        private readonly Label _micStatusLabel;
        private readonly Label _proxLabel;

        private volatile bool _dirty;
        // Track last-seen state to avoid redundant label updates
        private bool _lastMuted;
        private bool _lastProx;
        private bool _lastSpeechEnabled;
        private bool _lastPttActive;
        private int _lastSpeechMode;

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

            y += ROW_HEIGHT;

            // ── Bindings shortcut row — opens the rebind panel ──
            Add(new Label("Keys:", true, WHITE, 0, 1) { X = MARGIN, Y = y });
            Add(new Label("PTT / Mute", true, WHITE, 0, 1) { X = LABEL_COL, Y = y });

            var bindBtn = new NiceButton(WIDTH - MARGIN - 50, y - 2, 50, 20, ButtonAction.Activate, "Rebind")
            { IsSelectable = false };
            bindBtn.MouseUp += (sender, e) => OpenBindings();
            Add(bindBtn);

            // Subscribe to voice state changes
            if (world.VivoxManager != null)
            {
                world.VivoxManager.VoiceStateChanged += OnVoiceStateChanged;
            }

            // Seed last-seen state so first Update() doesn't double-update
            _lastMuted  = world.VivoxManager?.IsMicMuted ?? false;
            _lastProx   = ProfileManager.CurrentProfile?.EnableProximityChat ?? true;
            _lastSpeechEnabled = Settings.GlobalSettings.SpeechRecognitionEnabled;
            _lastSpeechMode    = ProfileManager.CurrentProfile?.SpeechCmdMode ?? 0;
            _lastPttActive     = world.VoiceInteractionManager?.SpeechCmdPttActive ?? false;
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
            Settings.GlobalSettings.Save();
            _modeLabel.Text = GetCurrentModeLabel();
        }

        private void OnToggleProximity()
        {
            var profile = ProfileManager.CurrentProfile;
            if (profile == null) return;

            profile.EnableProximityChat = !profile.EnableProximityChat;
            _proxLabel.Text = profile.EnableProximityChat ? "● ON" : "● OFF";
            _proxLabel.Hue = profile.EnableProximityChat ? (ushort)0x004F : (ushort)0x0026;
        }

        private void OpenBindings()
        {
            // One-at-a-time. Close any existing instance before opening a new
            // one so repeated clicks don't stack duplicates on the desktop.
            Game.Managers.UIManager.GetGump<VoiceBindingsGump>()?.Dispose();
            Game.Managers.UIManager.Add(new VoiceBindingsGump(World));
        }

        private void OnVoiceStateChanged()
        {
            _dirty = true;
        }

        public override void Update()
        {
            base.Update();

            // ── Mic row — driven by VivoxManager event + poll ─────────────────
            bool muted = World.VivoxManager?.IsMicMuted ?? false;
            if (_dirty || muted != _lastMuted)
            {
                _dirty = false;
                _lastMuted = muted;
                _micStatusLabel.Text = muted ? "● OFF" : "● ON";
                _micStatusLabel.Hue  = muted ? (ushort)0x0026 : (ushort)0x004F;
            }

            // ── Prox row — poll profile ───────────────────────────────────────
            bool proxOn = ProfileManager.CurrentProfile?.EnableProximityChat ?? false;
            if (proxOn != _lastProx)
            {
                _lastProx = proxOn;
                _proxLabel.Text = proxOn ? "● ON" : "● OFF";
                _proxLabel.Hue  = proxOn ? (ushort)0x004F : (ushort)0x0026;
            }

            // ── Speech row — poll mode + PTT state ────────────────────────────
            bool speechOn  = Settings.GlobalSettings.SpeechRecognitionEnabled;
            int  cmdMode   = ProfileManager.CurrentProfile?.SpeechCmdMode ?? 0;
            bool pttActive = World.VoiceInteractionManager?.SpeechCmdPttActive ?? false;

            if (speechOn != _lastSpeechEnabled || cmdMode != _lastSpeechMode || pttActive != _lastPttActive)
            {
                _lastSpeechEnabled = speechOn;
                _lastSpeechMode    = cmdMode;
                _lastPttActive     = pttActive;
                _modeLabel.Text    = GetModeLabel(speechOn, cmdMode, pttActive);
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
            bool speechOn  = Settings.GlobalSettings.SpeechRecognitionEnabled;
            int  cmdMode   = ProfileManager.CurrentProfile?.SpeechCmdMode ?? 0;
            // PTT active state is unknown at construction — default to false (off) for PTT mode
            bool pttActive = false;
            return GetModeLabel(speechOn, cmdMode, pttActive);
        }

        private static string GetModeLabel(bool speechEnabled, int cmdMode, bool pttActive)
        {
            if (!speechEnabled) return "Off";
            if (cmdMode == 1) return pttActive ? "PTT: ON" : "PTT: Off";
            string mode = Settings.GlobalSettings.VoiceCommandMode ?? "basic";
            return mode.ToLowerInvariant() switch
            {
                "basic"    => "Basic",
                "advanced" => "Advanced",
                "off"      => "Off",
                _          => "Simple"
            };
        }
    }
}
