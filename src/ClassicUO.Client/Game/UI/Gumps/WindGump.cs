// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — global wind controls. Drives IWindService and centralizes
// every tree leaf-sway knob in one place so it can be dialled in.
//
// MIGRATION (ADR-012 §6): WindGump uses constructor-injected IWindService. As of
// session 31 every wind tunable — including LinkToWeather and WeatherWindStrength —
// is reachable through the service contract; no static reads remain in this gump.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WindGump : Gump
    {
        public static WindGump Instance;

        private readonly IWindService _wind;
        private readonly IFoliage3DConfigService _foliage;
        private readonly IWeatherService _weather;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            int x = X, y = Y;
            var w = World;
            Dispose();
            var g = new WindGump(w, _wind, _foliage, _weather) { X = x, Y = y };
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W = 480;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;
        private const int LABEL_W   = 220;

        private Label _statsLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves <see cref="IWindService"/> from the active
        /// renderer service container. Existing call sites (e.g., Render3DLauncherGump)
        /// keep their <c>new WindGump(World)</c> shape until they migrate to
        /// constructor-injected services themselves.
        /// </summary>
        public WindGump(World world)
            : this(world, Renderer3DHost.Services.Wind,
                   Renderer3DHost.Services.Foliage3DConfig,
                   Renderer3DHost.Services.Weather) { }

        public WindGump(World world, IWindService wind, IFoliage3DConfigService foliage, IWeatherService weather)
            : base(world, 0, 0)
        {
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _foliage = foliage ?? throw new ArgumentNullException(nameof(foliage));
            _weather = weather ?? throw new ArgumentNullException(nameof(weather));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "WIND",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Global wind ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "GLOBAL", y);
            y = AddSlider("Strength x100 (0..100)", 0, 100,
                (int)(_wind.Strength * 100f), y,
                v => _wind.SetStrength(v / 100f));
            y = AddSlider("Direction (deg, 0..359)", 0, 359,
                (int)_wind.DirectionDeg, y,
                v => _wind.SetDirectionDeg(v));
            y = AddSlider("Frequency x100 (Hz)", 1, 300,
                (int)(_wind.Frequency * 100f), y,
                v => _wind.SetFrequency(v / 100f));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Tree leaf sway ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TREE LEAF SWAY", y);

            Debug3DStyle.AddCheck(this, contentX, y,
                "Enable leaf sway",
                _foliage.LeafPlaneWindEnabled,
                v => _foliage.SetLeafPlaneWindEnabled(v));
            y += ROW_H;

            // --- Mode picker (3 mutually-exclusive radio-style toggles) ---
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Uniform (combined mesh, all planes lean as one)",
                _foliage.LeafSwayMode == LeafSwayMode.Uniform,
                v => { if (v) { _foliage.SetLeafSwayMode(LeafSwayMode.Uniform); Reopen(); } });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Per-plane phase (each plane sways on its own)",
                _foliage.LeafSwayMode == LeafSwayMode.PerPlanePhase,
                v => { if (v) { _foliage.SetLeafSwayMode(LeafSwayMode.PerPlanePhase); Reopen(); } });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: First plane only (front fronds only)",
                _foliage.LeafSwayMode == LeafSwayMode.FirstPlaneOnly,
                v => { if (v) { _foliage.SetLeafSwayMode(LeafSwayMode.FirstPlaneOnly); Reopen(); } });
            y += ROW_H;

            // --- Amplitudes (sub-degree precision via x10) ---
            y = AddSlider("Sway amplitude x10 (deg, 0..200)", 0, 200,
                (int)(_foliage.LeafPlaneWindAmpDeg * 10f), y,
                v => _foliage.SetLeafPlaneWindAmpDeg(v / 10f));

            y = AddSlider("Per-plane phase x100 (rad, 0..628)", 0, 628,
                (int)(_foliage.LeafSwayPhasePerPlane * 100f), y,
                v => _foliage.SetLeafSwayPhasePerPlane(v / 100f));

            y = AddSlider("Vertical bob x10 (world units, 0..100)", 0, 100,
                (int)(_foliage.LeafSwayBobAmount * 10f), y,
                v => _foliage.SetLeafSwayBobAmount(v / 10f));

            // --- Helpers ---
            Debug3DStyle.AddCheck(this, contentX, y,
                "Smoothstep sample (linger at extremes)",
                _foliage.LeafSwaySmoothstep,
                v => _foliage.SetLeafSwaySmoothstep(v));
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Per-tree phase jitter (decorrelate forest)",
                _foliage.LeafSwayPerTreePhase,
                v => _foliage.SetLeafSwayPerTreePhase(v));
            y += ROW_H;
            y = AddSlider("Per-tree phase amount x10 (0..40)", 0, 40,
                (int)(_foliage.LeafSwayPerTreeAmount * 10f), y,
                v => _foliage.SetLeafSwayPerTreeAmount(v / 10f));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Weather link ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WEATHER LINK", y);
            Debug3DStyle.AddCheck(this, contentX, y,
                "Push wind into Weather3DSystem (rain/snow drift)",
                _wind.LinkToWeather,
                v => _wind.SetLinkToWeather(v));
            y += ROW_H;
            y = AddSlider("Weather wind force (units/s², 0..400)", 0, 400,
                (int)_wind.WeatherWindStrength, y,
                v => _wind.SetWeatherWindStrength(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Diagnostics ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "DIAGNOSTICS", y);
            _statsLabel = new Label("(loading...)", true, Debug3DStyle.HUE_VALUE, innerW, 4)
                { X = contentX, Y = y };
            Add(_statsLabel);
            y += ROW_H * 4;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = 2 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 470;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: Reopen(); break;
                case 2:
                    Console.WriteLine("==================== [3DCUO] WIND STATE ====================");
                    Console.WriteLine($"  Strength               = {_wind.Strength:F2}");
                    Console.WriteLine($"  DirectionDeg           = {_wind.DirectionDeg:F0}");
                    Console.WriteLine($"  Frequency (Hz)         = {_wind.Frequency:F2}");
                    Console.WriteLine($"  Sample (-1..1)         = {_wind.Sample:F2}");
                    Console.WriteLine($"  LeafPlaneWindEnabled   = {_foliage.LeafPlaneWindEnabled}");
                    Console.WriteLine($"  LeafSwayMode           = {_foliage.LeafSwayMode}");
                    Console.WriteLine($"  LeafPlaneWindAmpDeg    = {_foliage.LeafPlaneWindAmpDeg:F1}");
                    Console.WriteLine($"  LeafSwayPhasePerPlane  = {_foliage.LeafSwayPhasePerPlane:F2}");
                    Console.WriteLine($"  LeafSwayBobAmount      = {_foliage.LeafSwayBobAmount:F1}");
                    Console.WriteLine($"  LeafSwaySmoothstep     = {_foliage.LeafSwaySmoothstep}");
                    Console.WriteLine($"  LeafSwayPerTreePhase   = {_foliage.LeafSwayPerTreePhase}");
                    Console.WriteLine($"  LeafSwayPerTreeAmount  = {_foliage.LeafSwayPerTreeAmount:F1}");
                    Console.WriteLine($"  LinkToWeather          = {_wind.LinkToWeather}");
                    Console.WriteLine($"  WeatherWindStrength    = {_wind.WeatherWindStrength:F1}");
                    Console.WriteLine($"  Weather3D WindX/Z      = {_weather.WindX:F1} / {_weather.WindZ:F1}");
                    Console.WriteLine("============================================================");
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_statsLabel != null)
            {
                var v = _wind.VectorXZ;
                _statsLabel.Text =
                    $"sample {_wind.Sample,+5:F2}    strength {_wind.Strength:F2}\n" +
                    $"vector  ({v.X,+6:F2}, {v.Y,+6:F2})    bearing {_wind.DirectionDeg:F0}°\n" +
                    $"sway mode: {_foliage.LeafSwayMode}    amp {_foliage.LeafPlaneWindAmpDeg:F1}°\n" +
                    $"weather wind ({_weather.WindX,+5:F0}, {_weather.WindZ,+5:F0})";
            }
        }

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
        }
    }
}
