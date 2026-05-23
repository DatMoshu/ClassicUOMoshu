// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — per-weather profile pinning gump.
//
// Workflow: scrub the existing Weather Admin gump until a weather looks the
// way you want, then pop open this gump, pick the type, and click "Save
// current as default for {Type}". The override goes into
// WeatherDefaultsStore.Overrides and is persisted to weather-defaults.json.
// Re-launching the game re-applies it automatically the next time
// SetType(type) fires.
//
// Per project convention (.claude/skills/team-gump): WarGumpStyle theme,
// admin-style naming, Refresh + Dump State buttons in the footer.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.EnvRender;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WeatherConfigGump : Gump
    {
        public static WeatherConfigGump Instance;

        private const int W = 420;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;

        // Picker state — which weather we're pinning. Starts at the active
        // weather so "Save current" feels intuitive.
        private static Weather3DType _pickType = Weather3DType.None;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _statusLabel;
        private Label _activeLabel;

        private static class BtnId
        {
            public const int Refresh   = 1;
            public const int DumpState = 2;
            // Type picker block — one button per weather (100..108)
            public const int PickBase  = 100;
            // Action buttons.
            public const int CaptureCurrent = 200;
            public const int Reset          = 201;
            public const int ResetAll       = 202;
            public const int SaveJson       = 203;
            public const int LoadJson       = 204;
            public const int ApplyToWorld   = 205;  // SetType(_pickType) — preview the merged profile
            public const int ToggleSwayOn   = 210;  // pin TreeSwayEnabled = true for picked weather
            public const int ToggleSwayOff  = 211;  // pin TreeSwayEnabled = false
            public const int ToggleSwayClr  = 212;  // remove the override (use default)
        }

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            var w = World;
            int x = X, y = Y;
            Dispose();
            var g = new WeatherConfigGump(w) { X = x, Y = y };
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private readonly IEnvironmentService _env;
        private readonly IWeatherService _weather;

        public WeatherConfigGump(World world)
            : this(world, Renderer3DHost.Services.Environment, Renderer3DHost.Services.Weather) { }

        public WeatherConfigGump(World world, IEnvironmentService env, IWeatherService weather) : base(world, 0, 0)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _weather = weather ?? throw new ArgumentNullException(nameof(weather));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            // Default the picker to whatever weather is currently active so
            // "Save current as default" maps to what the user is looking at.
            // _pickType / WeatherDefaultsStore are still keyed by legacy Weather3DType
            // (separate migration); cast at the boundary.
            var activeType = (Weather3DType)(int)_weather.Type;
            if (activeType != Weather3DType.None || _pickType == Weather3DType.None)
                _pickType = activeType;

            int y = Debug3DStyle.BuildShell(this, W, "WEATHER  CONFIG",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- TYPE PICKER ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW,
                $"PICK WEATHER (selected: {_pickType})", y);

            int btnH = 22;
            int colW = (innerW - 9) / 4;
            // 9 weather types laid out 4-per-row (3 rows).
            var types = new[]
            {
                Weather3DType.None,      Weather3DType.Rain,      Weather3DType.Snow,      Weather3DType.Storm,
                Weather3DType.Sandstorm, Weather3DType.Embers,    Weather3DType.Fog,       Weather3DType.BloodMoon,
                Weather3DType.Blizzard,  Weather3DType.Tornado,
            };
            for (int i = 0; i < types.Length; i++)
            {
                int row = i / 4, col = i % 4;
                int bx = contentX + col * (colW + 3);
                int by = y + row * (btnH + 3);
                Add(new NiceButton(bx, by, colW, btnH, ButtonAction.Activate,
                        types[i].ToString().Length > 9 ? types[i].ToString().Substring(0, 9) : types[i].ToString())
                    { ButtonParameter = BtnId.PickBase + i });
            }
            y += ((types.Length + 3) / 4) * (btnH + 3);
            y += SECTION_GAP;

            // ---------- ACTIVE / OVERRIDE STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _activeLabel = new Label("(loading...)", true, HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_activeLabel);
            y += ROW_H * 4;
            y += SECTION_GAP;

            // ---------- ACTIONS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ACTIONS", y);
            int half = (innerW - 8) / 2;
            Add(new NiceButton(contentX, y, innerW, btnH, ButtonAction.Activate,
                    $"Save current world state as default for {_pickType}")
                { ButtonParameter = BtnId.CaptureCurrent });
            y += btnH + 4;
            Add(new NiceButton(contentX, y, half, btnH, ButtonAction.Activate, $"Apply (SetType {_pickType})")
                { ButtonParameter = BtnId.ApplyToWorld });
            Add(new NiceButton(contentX + half + 8, y, half, btnH, ButtonAction.Activate, $"Reset {_pickType} override")
                { ButtonParameter = BtnId.Reset });
            y += btnH + 4;
            Add(new NiceButton(contentX, y, half, btnH, ButtonAction.Activate, "Save JSON")
                { ButtonParameter = BtnId.SaveJson });
            Add(new NiceButton(contentX + half + 8, y, half, btnH, ButtonAction.Activate, "Load JSON")
                { ButtonParameter = BtnId.LoadJson });
            y += btnH + 4;
            Add(new NiceButton(contentX, y, innerW, btnH, ButtonAction.Activate, "Reset ALL overrides")
                { ButtonParameter = BtnId.ResetAll });
            y += btnH + 4;
            y += SECTION_GAP;

            // ---------- TREE SWAY OVERRIDE (per weather) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW,
                $"TREE SWAY for {_pickType}", y);
            int third = (innerW - 16) / 3;
            Add(new NiceButton(contentX, y, third, btnH, ButtonAction.Activate, "Pin: ON")
                { ButtonParameter = BtnId.ToggleSwayOn });
            Add(new NiceButton(contentX + third + 8, y, third, btnH, ButtonAction.Activate, "Pin: OFF")
                { ButtonParameter = BtnId.ToggleSwayOff });
            Add(new NiceButton(contentX + (third + 8) * 2, y, third, btnH, ButtonAction.Activate, "Use default")
                { ButtonParameter = BtnId.ToggleSwayClr });
            y += btnH + 4;
            y += SECTION_GAP;

            // ---------- DIAGNOSTICS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "INFO", y);
            _statusLabel = new Label("(loading...)", true, HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = BtnId.Refresh });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = BtnId.DumpState });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case BtnId.Refresh: Reopen(); break;
                case BtnId.DumpState:
                    Console.WriteLine("==================== [3DCUO] WEATHER CONFIG ====================");
                    Console.WriteLine($"  Picked type     = {_pickType}");
                    Console.WriteLine($"  Override count  = {WeatherDefaultsStore.Count}");
                    Console.WriteLine($"  LastSavedPath   = {WeatherDefaultsStore.LastSavedPath}");
                    foreach (var kv in WeatherDefaultsStore.Overrides)
                    {
                        Console.WriteLine($"  override[{kv.Key}] WindStr={kv.Value.WindStrength} " +
                                          $"BG=({kv.Value.BgR},{kv.Value.BgG},{kv.Value.BgB}) " +
                                          $"Intensity={kv.Value.Intensity}");
                    }
                    Console.WriteLine("================================================================");
                    break;

                case BtnId.CaptureCurrent:
                    WeatherDefaultsStore.CaptureCurrent(_pickType);
                    Console.WriteLine($"[WeatherConfig] Captured current state as default for {_pickType}.");
                    break;

                case BtnId.Reset:
                    WeatherDefaultsStore.Reset(_pickType);
                    Console.WriteLine($"[WeatherConfig] Reset override for {_pickType}.");
                    break;

                case BtnId.ResetAll:
                    WeatherDefaultsStore.ResetAll();
                    Console.WriteLine("[WeatherConfig] Cleared ALL overrides.");
                    break;

                case BtnId.SaveJson:
                    if (WeatherDefaultsStore.Save())
                        Console.WriteLine($"[WeatherConfig] Saved → {WeatherDefaultsStore.LastSavedPath}");
                    break;

                case BtnId.LoadJson:
                    WeatherDefaultsStore.Load();
                    Reopen();
                    break;

                case BtnId.ApplyToWorld:
                    _weather.SetType((WeatherKind)(int)_pickType);
                    break;

                case BtnId.ToggleSwayOn:
                case BtnId.ToggleSwayOff:
                case BtnId.ToggleSwayClr:
                {
                    // Make sure an override row exists; create a thin one if
                    // not so we have somewhere to land the bool.
                    if (!WeatherDefaultsStore.Overrides.TryGetValue(_pickType, out var dto))
                    {
                        dto = new WeatherDefaultsStore.ProfileDto();
                        WeatherDefaultsStore.SetOverride(_pickType, dto);
                    }
                    dto.TreeSwayEnabled =
                        buttonID == BtnId.ToggleSwayOn  ? true  :
                        buttonID == BtnId.ToggleSwayOff ? false :
                        (bool?)null;
                    Console.WriteLine($"[WeatherConfig] {_pickType} sway pinned → {(dto.TreeSwayEnabled?.ToString() ?? "default")}");
                    break;
                }

                default:
                    if (buttonID >= BtnId.PickBase && buttonID < BtnId.PickBase + 16)
                    {
                        int idx = buttonID - BtnId.PickBase;
                        var all = (Weather3DType[])Enum.GetValues(typeof(Weather3DType));
                        if (idx >= 0 && idx < all.Length) _pickType = all[idx];
                        Reopen();
                    }
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            bool hasOverride = WeatherDefaultsStore.Overrides.ContainsKey(_pickType);
            if (_activeLabel != null)
            {
                _activeLabel.Text =
                    $"active world : {_weather.Type}  intensity={_weather.Intensity:F2}\n" +
                    $"selected     : {_pickType}\n" +
                    $"override     : {(hasOverride ? "PINNED" : "(default)")}\n" +
                    $"world wind   : str={WindManager.Strength:F2} dir={WindManager.DirectionDeg:F0}°";
            }
            if (_statusLabel != null)
            {
                _statusLabel.Text =
                    $"overrides stored: {WeatherDefaultsStore.Count}\n" +
                    $"file: {WeatherDefaultsStore.LastSavedPath ?? "(not saved yet)"}\n" +
                    $"BG=({_env.BackgroundColor.R},{_env.BackgroundColor.G},{_env.BackgroundColor.B})";
            }
        }
    }
}
