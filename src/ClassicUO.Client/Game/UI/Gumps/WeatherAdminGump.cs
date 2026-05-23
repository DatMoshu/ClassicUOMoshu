// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — admin UI for Weather3DSystem.
// Pick a weather type, scrub intensity & wind, fire lightning manually.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Audio; // FootstepMaterial / FootwearKind (migrated to Audio domain)
using ClassicUO.Renderer.Core;
// Disambiguate WeatherKind / WindGustMode — the legacy parallel enums in
// Renderer3D namespace would otherwise collide with the domain enums.
using WeatherKind = ClassicUO.Renderer.Atmosphere.WeatherKind;
using WindGustMode = ClassicUO.Renderer.Atmosphere.WindGustMode;
using ClassicUO.Renderer.Environment;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WeatherAdminGump : Gump
    {
        public static WeatherAdminGump Instance;

        // ===== Constructor-injected services (ADR-012 §6 / playbook §Q) =====
        private readonly IWeatherService _weather;
        private readonly IWindService _wind;
        private readonly IWeatherAudioService _weatherAudio;
        private readonly IFootstepAudioService _footstepAudio;
        private readonly ClassicUO.Renderer.Effects.IParticleService _particle;
        private readonly IGroundEffectService _ground;
        private readonly IFoliageSeasonService _foliage;
        private readonly IMulti3DConfigService _multi;
        private readonly IFoliage3DConfigService _foliage3D;
        private readonly IGroundOverlayService _groundOverlay;

        private const int W = 360;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        // Type buttons share an ID range; intensity/wind/lightning use distinct IDs.
        private static class BtnId
        {
            public const int TypeNone      = 100;
            public const int TypeRain      = 101;
            public const int TypeSnow      = 102;
            public const int TypeStorm     = 103;
            public const int TypeSandstorm = 104;
            public const int TypeEmbers    = 105;
            public const int TypeFog       = 106;
            public const int TypeBloodMoon = 107;
            public const int TypeBlizzard  = 108;
            public const int TypeTornado   = 109;

            public const int LightningNow  = 200;
            public const int Reset         = 201;
            public const int ClearParticles = 202;
            public const int DumpState     = 203;
            public const int OpenConfig    = 204;

            public const int GroundNone = 300;
            public const int GroundWet  = 301;
            public const int GroundSnow = 302;

            public const int SeasonNone = 400;
            public const int SeasonFall = 401;

            public const int GustNone     = 500;
            public const int GustSteady   = 501;
            public const int GustVariable = 502;
            public const int GustStorm    = 503;
        }

        // Audio toggles get their own id namespace so they can't collide
        // with the existing weather buttons.
        private static class BtnIdAudio
        {
            public const int CycleFootwear = 600;
            public const int CycleMaterial = 601;
        }

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _statusLabel;
        private Label _typeLabel;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        /// <summary>
        /// Convenience overload that resolves the four migrated services from the active
        /// renderer service container. Existing call sites continue to use
        /// <c>new WeatherAdminGump(World)</c> until the launcher migrates this opener.
        /// </summary>
        public WeatherAdminGump(World world)
            : this(world,
                Renderer3DHost.Services.Weather,
                Renderer3DHost.Services.Wind,
                Renderer3DHost.Services.WeatherAudio,
                Renderer3DHost.Services.FootstepAudio,
                Renderer3DHost.Services.Particle,
                Renderer3DHost.Services.GroundEffect,
                Renderer3DHost.Services.FoliageSeason,
                Renderer3DHost.Services.Multi3DConfig,
                Renderer3DHost.Services.Foliage3DConfig,
                Renderer3DHost.Services.GroundOverlay) { }

        public WeatherAdminGump(World world,
                                 IWeatherService weather,
                                 IWindService wind,
                                 IWeatherAudioService weatherAudio,
                                 IFootstepAudioService footstepAudio,
                                 ClassicUO.Renderer.Effects.IParticleService particle,
                                 IGroundEffectService ground,
                                 IFoliageSeasonService foliage,
                                 IMulti3DConfigService multi,
                                 IFoliage3DConfigService foliage3D,
                                 IGroundOverlayService groundOverlay) : base(world, 0, 0)
        {
            _weather       = weather       ?? throw new ArgumentNullException(nameof(weather));
            _wind          = wind          ?? throw new ArgumentNullException(nameof(wind));
            _weatherAudio  = weatherAudio  ?? throw new ArgumentNullException(nameof(weatherAudio));
            _footstepAudio = footstepAudio ?? throw new ArgumentNullException(nameof(footstepAudio));
            _particle      = particle      ?? throw new ArgumentNullException(nameof(particle));
            _ground        = ground        ?? throw new ArgumentNullException(nameof(ground));
            _foliage       = foliage       ?? throw new ArgumentNullException(nameof(foliage));
            _multi         = multi         ?? throw new ArgumentNullException(nameof(multi));
            _foliage3D     = foliage3D     ?? throw new ArgumentNullException(nameof(foliage3D));
            _groundOverlay = groundOverlay ?? throw new ArgumentNullException(nameof(groundOverlay));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "WEATHER  ADMIN",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2;

            // ---------- TYPE ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WEATHER TYPE", y);
            // 4-cell grid (2 rows × 4 cols ≈ 7 buttons + label).
            int colW = (innerW - 6) / 4;
            int btnH = 22;
            AddTypeButton("Clear",     BtnId.TypeNone,      contentX + colW * 0, y, colW, btnH);
            AddTypeButton("Rain",      BtnId.TypeRain,      contentX + colW * 1, y, colW, btnH);
            AddTypeButton("Snow",      BtnId.TypeSnow,      contentX + colW * 2, y, colW, btnH);
            AddTypeButton("Storm",     BtnId.TypeStorm,     contentX + colW * 3, y, colW, btnH);
            y += btnH + 4;
            AddTypeButton("Sandstorm", BtnId.TypeSandstorm, contentX + colW * 0, y, colW, btnH);
            AddTypeButton("Embers",    BtnId.TypeEmbers,    contentX + colW * 1, y, colW, btnH);
            AddTypeButton("Fog",       BtnId.TypeFog,       contentX + colW * 2, y, colW, btnH);
            AddTypeButton("Blood Moon",BtnId.TypeBloodMoon, contentX + colW * 3, y, colW, btnH);
            y += btnH + 4;
            AddTypeButton("Blizzard",  BtnId.TypeBlizzard,  contentX + colW * 0, y, colW, btnH);
            AddTypeButton("Tornado",   BtnId.TypeTornado,   contentX + colW * 1, y, colW, btnH);
            y += btnH + 6;

            _typeLabel = new Label("Active: None", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_typeLabel);
            y += ROW_H;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- INTENSITY ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "INTENSITY  +  COVERAGE", y);
            // Slider stores int 0..100; map to 0..1.
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Intensity %", 0, 100,
                (int)(_weather.Intensity * 100f), y,
                v => _weather.SetIntensity(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Radius",      80, 1200,
                (int)_weather.Radius, y,
                v => _weather.SetRadius(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Spawn height", 80, 1200,
                (int)_weather.Height, y,
                v => _weather.SetHeight(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- WIND ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WIND  (world u/s²)", y);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Wind X", -400, 400,
                (int)_weather.WindX, y,
                v => _weather.SetWindX(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Wind Z", -400, 400,
                (int)_weather.WindZ, y,
                v => _weather.SetWindZ(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- GLOBAL WIND  (drives trees, particle drift, etc.) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "GLOBAL WIND  (trees + drift)", y);
            // Mode selector — None / Steady / Variable / Storm.
            int gmw = (innerW - 6) / 4;
            Add(new NiceButton(contentX + gmw * 0,         y, gmw, btnH, ButtonAction.Activate, "None")
                { ButtonParameter = BtnId.GustNone });
            Add(new NiceButton(contentX + gmw * 1 + 2,     y, gmw, btnH, ButtonAction.Activate, "Steady")
                { ButtonParameter = BtnId.GustSteady });
            Add(new NiceButton(contentX + gmw * 2 + 4,     y, gmw, btnH, ButtonAction.Activate, "Variable")
                { ButtonParameter = BtnId.GustVariable });
            Add(new NiceButton(contentX + gmw * 3 + 6,     y, gmw, btnH, ButtonAction.Activate, "Storm")
                { ButtonParameter = BtnId.GustStorm });
            y += btnH + 4;
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Strength %", 0, 100,
                (int)(_wind.Strength * 100f), y,
                v => _wind.SetStrength(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Direction (deg)", 0, 359,
                (int)_wind.DirectionDeg, y,
                v => _wind.SetDirectionDeg(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Frequency (cHz)", 1, 200,
                (int)(_wind.Frequency * 100f), y,
                v => _wind.SetFrequency(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust change min (ds)", 1, 200,
                (int)(_wind.GustChangeMin * 10f), y,
                v => _wind.SetGustChangeMin(v / 10f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust change max (ds)", 1, 200,
                (int)(_wind.GustChangeMax * 10f), y,
                v => _wind.SetGustChangeMax(v / 10f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust strength min %", 0, 100,
                (int)(_wind.GustStrengthMin * 100f), y,
                v => _wind.SetGustStrengthMin(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust strength max %", 0, 100,
                (int)(_wind.GustStrengthMax * 100f), y,
                v => _wind.SetGustStrengthMax(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust dir range (deg)", 0, 180,
                (int)_wind.GustDirectionRangeDeg, y,
                v => _wind.SetGustDirectionRangeDeg(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Gust ease speed (x10)", 1, 60,
                (int)(_wind.GustLerpSpeed * 10f), y,
                v => _wind.SetGustLerpSpeed(v / 10f));
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Wind drives weather", _wind.LinkToWeather,
                v => _wind.SetLinkToWeather(v),
                "Apply profile on weather", _weather.ApplyProfileOnSetType,
                v => _weather.SetApplyProfileOnSetType(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Tree sway (deg)", 0, 30,
                (int)_foliage3D.LeafPlaneWindAmpDeg, y,
                v => _foliage3D.SetLeafPlaneWindAmpDeg(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- GROUND EFFECT ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "GROUND EFFECT", y);
            int gw = (innerW - 4) / 3;
            Add(new NiceButton(contentX + gw * 0, y, gw, btnH, ButtonAction.Activate, "Dry")
                { ButtonParameter = BtnId.GroundNone });
            Add(new NiceButton(contentX + gw * 1 + 2, y, gw, btnH, ButtonAction.Activate, "Wet")
                { ButtonParameter = BtnId.GroundWet });
            Add(new NiceButton(contentX + gw * 2 + 4, y, gw, btnH, ButtonAction.Activate, "Snow")
                { ButtonParameter = BtnId.GroundSnow });
            y += btnH + 4;
            // Slider drives the TARGET — current intensity eases toward it.
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Target strength %", 0, 100,
                (int)(_ground.TargetIntensity * 100f), y,
                v => _ground.SetTargetIntensity(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Ease speed", 1, 100,
                (int)(_ground.EaseSpeed * 10f), y,
                v => _ground.SetEaseSpeed(v / 10f));
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Link to weather", _ground.LinkToWeather,
                v => _ground.SetLinkToWeather(v),
                null, false, null);
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Link strength %", 0, 100,
                (int)(_ground.LinkStrength * 100f), y,
                v => _ground.SetLinkStrength(v / 100f));
            // Shader tunables (live; ApplyTunables runs every frame).
            // NoiseScale stored as 1/N — slider exposes N (32..512).
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Noise scale (1/N)", 32, 512,
                (int)(1f / _groundOverlay.NoiseScale), y,
                v => _groundOverlay.SetNoiseScale(1f / System.Math.Max(1, v)));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Puddle scale (1/N)", 64, 800,
                (int)(1f / _groundOverlay.PuddleScale), y,
                v => _groundOverlay.SetPuddleScale(1f / System.Math.Max(1, v)));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Puddle threshold %", 20, 90,
                (int)(_groundOverlay.PuddleThresh * 100f), y,
                v => _groundOverlay.SetPuddleThresh(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Z-bias (Y lift)", 0, 50,
                (int)(_groundOverlay.HeightBias * 10f), y,
                v => _groundOverlay.SetHeightBias(v / 10f));
            // Puddle presence multiplier — 0 = damp tint only, 100 = full puddles.
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Puddle amount %", 0, 100,
                (int)(_groundOverlay.PuddleAmount * 100f), y,
                v => _groundOverlay.SetPuddleAmount(v / 100f));
            // Snow de-tile: 0 = visible repeating tile pattern when zoomed out,
            // 100 = full per-cell rotation+offset randomisation eliminates the period.
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Snow de-tile %", 0, 100,
                (int)(_groundOverlay.SnowDetile * 100f), y,
                v => _groundOverlay.SetSnowDetile(v / 100f));
            // Snow macro-cell size in world units (~10 UO tiles = 220).
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Snow cell size", 60, 480,
                (int)_groundOverlay.SnowCellSize, y,
                v => _groundOverlay.SetSnowCellSize(v));
            // Foliage overlay toggles — wet/snow on tree leaves and trunks.
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Apply to leaves", _foliage3D.ApplyOverlayToFoliage,
                v => _foliage3D.SetApplyOverlayToFoliage(v),
                "Apply to trunks (wet)", _foliage3D.ApplyOverlayToTrunks,
                v => _foliage3D.SetApplyOverlayToTrunks(v));
            // Sky-cover gating for falling weather + roof snow accumulation.
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Sky-cover gating (rain/snow)", _weather.OcclusionCheckEnabled,
                v => _weather.SetOcclusionCheckEnabled(v),
                "Snow on roof tops", _multi.RoofSnowOverlay,
                v => _multi.SetRoofSnowOverlay(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- FOLIAGE SEASON (FALL) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "FOLIAGE SEASON", y);
            int sw = (innerW - 2) / 2;
            Add(new NiceButton(contentX + sw * 0, y, sw, btnH, ButtonAction.Activate, "Off")
                { ButtonParameter = BtnId.SeasonNone });
            Add(new NiceButton(contentX + sw * 1 + 2, y, sw, btnH, ButtonAction.Activate, "Fall")
                { ButtonParameter = BtnId.SeasonFall });
            y += btnH + 4;
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Fall strength %", 0, 100,
                (int)(_foliage.Intensity * 100f), y,
                v => _foliage.SetIntensity(v / 100f));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Fall tree cell size", 8, 80,
                (int)_groundOverlay.FallTreeUnit, y,
                v => _groundOverlay.SetFallTreeUnit(v));
            y = Debug3DStyle.AddSlider(this, contentX, innerW, 100, "Fall saturation %", 0, 100,
                (int)(_groundOverlay.FallSaturation * 100f), y,
                v => _groundOverlay.SetFallSaturation(v / 100f));

            y += Debug3DStyle.SECTION_GAP;

            // ---------- LIGHTNING ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "LIGHTNING", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Auto (storm only)", _weather.AutoLightning,
                v => _weather.SetAutoLightning(v),
                null, false, null);
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Strike Now")
                { ButtonParameter = BtnId.LightningNow });
            y += ROW_H + 2;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- AUDIO ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "AUDIO", y);
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Weather ambience", _weatherAudio.Enabled,
                v => { _weatherAudio.SetEnabled(v); if (v) _weatherAudio.Refresh(); else _weatherAudio.StopAll(); },
                "Verbose log",      _weatherAudio.VerboseLog,
                v => _weatherAudio.SetVerboseLog(v));
            y = Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                "Custom footsteps", _footstepAudio.Enabled,
                v => _footstepAudio.SetEnabled(v),
                "Snow in winter",   _footstepAudio.AutoUseSnowInWinter,
                v => _footstepAudio.SetAutoUseSnowInWinter(v));
            // Material override row — cycles through the supported materials.
            Add(new NiceButton(contentX, y, innerW / 2 - 2, 22, ButtonAction.Activate,
                    $"Footwear: {_footstepAudio.Footwear}")
                { ButtonParameter = BtnIdAudio.CycleFootwear });
            Add(new NiceButton(contentX + innerW / 2 + 2, y, innerW / 2 - 2, 22, ButtonAction.Activate,
                    $"Mat: {_footstepAudio.OverrideMaterial}")
                { ButtonParameter = BtnIdAudio.CycleMaterial });
            y += ROW_H + 4;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- ACTIONS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ACTIONS", y);
            Add(new NiceButton(contentX, y, innerW / 2 - 2, 22, ButtonAction.Activate, "Reset wind & intensity")
                { ButtonParameter = BtnId.Reset });
            Add(new NiceButton(contentX + innerW / 2 + 2, y, innerW / 2 - 2, 22, ButtonAction.Activate, "Clear particles")
                { ButtonParameter = BtnId.ClearParticles });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = BtnId.DumpState });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate,
                    "Weather Config — pin defaults per type")
                { ButtonParameter = BtnId.OpenConfig });
            y += ROW_H + 4;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- STATUS ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statusLabel = new Label("(idle)", true, Debug3DStyle.HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H + 4;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        private void AddTypeButton(string label, int id, int x, int y, int w, int h)
        {
            Add(new NiceButton(x, y, w, h, ButtonAction.Activate, label)
                { ButtonParameter = id });
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel != null && !_statusLabel.IsDisposed)
            {
                _statusLabel.Text =
                    $"alive={_particle.AliveParticles}  drawn={_particle.LastDrawnParticles}\n" +
                    $"wx/wz=({_weather.WindX:0},{_weather.WindZ:0})  " +
                    $"gust={_wind.GustMode} str={_wind.Strength:0.00} dir={_wind.DirectionDeg:0}°  sway={_foliage3D.LeafPlaneWindAmpDeg:0.0}°\n" +
                    $"ground={_ground.Mode} cur={_ground.CurrentIntensity:0.00}→tgt={_ground.TargetIntensity:0.00}";
            }
            if (_typeLabel != null && !_typeLabel.IsDisposed)
            {
                _typeLabel.Text = $"Active: {_weather.Type}  intensity={_weather.Intensity:0.00}";
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:    Dispose(); break;

                case BtnId.TypeNone:      _weather.SetType(WeatherKind.None);      break;
                case BtnId.TypeRain:      _weather.SetType(WeatherKind.Rain);      break;
                case BtnId.TypeSnow:      _weather.SetType(WeatherKind.Snow);      break;
                case BtnId.TypeStorm:     _weather.SetType(WeatherKind.Storm);     break;
                case BtnId.TypeSandstorm: _weather.SetType(WeatherKind.Sandstorm); break;
                case BtnId.TypeEmbers:    _weather.SetType(WeatherKind.Embers);    break;
                case BtnId.TypeFog:       _weather.SetType(WeatherKind.Fog);       break;
                case BtnId.TypeBloodMoon: _weather.SetType(WeatherKind.BloodMoon); break;
                case BtnId.TypeBlizzard:  _weather.SetType(WeatherKind.Blizzard);  break;
                case BtnId.TypeTornado:   _weather.SetType(WeatherKind.Tornado);   break;

                case BtnId.GustNone:     _wind.SetGustMode(WindGustMode.None);     break;
                case BtnId.GustSteady:   _wind.SetGustMode(WindGustMode.Steady);   break;
                case BtnId.GustVariable: _wind.SetGustMode(WindGustMode.Variable); break;
                case BtnId.GustStorm:    _wind.SetGustMode(WindGustMode.Storm);    break;

                case BtnId.LightningNow:  _weather.TriggerLightning();             break;
                case BtnId.Reset:
                    _weather.SetWindX(0f);
                    _weather.SetWindZ(0f);
                    _weather.SetIntensity(0.5f);
                    // Rebuild gump so sliders snap back to the reset values.
                    var w = World;
                    Dispose();
                    var g = new WeatherAdminGump(w, _weather, _wind, _weatherAudio, _footstepAudio, _particle, _ground, _foliage, _multi, _foliage3D, _groundOverlay);
                    Instance = g;
                    Game.Managers.UIManager.Add(g);
                    break;
                case BtnId.ClearParticles: _particle.Clear(); break;

                case BtnId.DumpState: DumpFullState(); break;

                case BtnId.OpenConfig:
                    if (WeatherConfigGump.Instance == null)
                    {
                        WeatherConfigGump.Instance = new WeatherConfigGump(World);
                        Game.Managers.UIManager.Add(WeatherConfigGump.Instance);
                    }
                    else
                    {
                        WeatherConfigGump.Instance.SetInScreen();
                        WeatherConfigGump.Instance.BringOnTop();
                    }
                    break;

                case BtnId.GroundNone: _ground.SetMode(GroundEffectMode.None); break;
                case BtnId.GroundWet:  _ground.SetMode(GroundEffectMode.Wet);  break;
                case BtnId.GroundSnow: _ground.SetMode(GroundEffectMode.Snow); break;

                case BtnId.SeasonNone: _foliage.SetMode(FoliageSeasonMode.None); break;
                case BtnId.SeasonFall: _foliage.SetMode(FoliageSeasonMode.Fall); break;

                case BtnIdAudio.CycleFootwear:
                    _footstepAudio.SetFootwear(
                        _footstepAudio.Footwear == FootwearKind.Bare
                            ? FootwearKind.Shoe : FootwearKind.Bare);
                    RebuildSelf();
                    break;
                case BtnIdAudio.CycleMaterial:
                {
                    var values = (FootstepMaterial[])System.Enum.GetValues(typeof(FootstepMaterial));
                    int idx = System.Array.IndexOf(values, _footstepAudio.OverrideMaterial);
                    _footstepAudio.SetOverrideMaterial(values[(idx + 1) % values.Length]);
                    RebuildSelf();
                    break;
                }
            }
        }

        private void RebuildSelf()
        {
            int x = X, y = Y;
            var w = World;
            Dispose();
            var g = new WeatherAdminGump(w, _weather, _wind, _weatherAudio, _footstepAudio, _particle, _ground, _foliage, _multi, _foliage3D, _groundOverlay) { X = x, Y = y };
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        // Comprehensive console dump of every weather/ground subsystem field —
        // captures the full state of Weather3DSystem, World3DRenderer ground/
        // foliage effects, and GroundOverlayEffect tunables in one block.
        // Non-static so it can read injected _ground.
        private void DumpFullState()
        {
            System.Console.WriteLine("==================== [3DCUO] WEATHER — FULL STATE ====================");

            System.Console.WriteLine("[Weather3DSystem]");
            System.Console.WriteLine($"  Type                 = {_weather.Type}");
            System.Console.WriteLine($"  Intensity            = {_weather.Intensity:F2}");
            System.Console.WriteLine($"  WindX                = {_weather.WindX:F1}");
            System.Console.WriteLine($"  WindZ                = {_weather.WindZ:F1}");
            System.Console.WriteLine($"  Radius               = {_weather.Radius:F0}");
            System.Console.WriteLine($"  Height               = {_weather.Height:F0}");
            System.Console.WriteLine($"  AutoLightning        = {_weather.AutoLightning}");
            System.Console.WriteLine($"  LightningEveryMin/Max= {_weather.LightningEveryMin:F1} / {_weather.LightningEveryMax:F1}");
            // (Reads above flow through IWeatherService — Weather3DSystem facade delegates.)

            System.Console.WriteLine("[World3DRenderer (ground)]");
            System.Console.WriteLine($"  GroundEffectMode             = {_ground.Mode}");
            System.Console.WriteLine($"  GroundEffectIntensity        = {_ground.CurrentIntensity:F2}");
            System.Console.WriteLine($"  TargetGroundEffectIntensity  = {_ground.TargetIntensity:F2}");
            System.Console.WriteLine($"  GroundEffectEaseSpeed        = {_ground.EaseSpeed:F2}");
            System.Console.WriteLine($"  LinkToWeather                = {_ground.LinkToWeather}");
            System.Console.WriteLine($"  LinkStrength                 = {_ground.LinkStrength:F2}");

            System.Console.WriteLine("[World3DRenderer (foliage)]");
            System.Console.WriteLine($"  FoliageSeason                = {_foliage.Mode}");
            System.Console.WriteLine($"  FoliageSeasonIntensity       = {_foliage.Intensity:F2}");

            System.Console.WriteLine("[Static3DRenderer (overlay routing)]");
            System.Console.WriteLine($"  ApplyOverlayToFoliage        = {_foliage3D.ApplyOverlayToFoliage}");
            System.Console.WriteLine($"  ApplyOverlayToTrunks         = {_foliage3D.ApplyOverlayToTrunks}");

            System.Console.WriteLine("[GroundOverlayEffect tunables]");
            System.Console.WriteLine($"  NoiseScale                   = {_groundOverlay.NoiseScale:F5}  (1/N where N={1f / _groundOverlay.NoiseScale:F0})");
            System.Console.WriteLine($"  PuddleScale                  = {_groundOverlay.PuddleScale:F5}  (1/N where N={1f / _groundOverlay.PuddleScale:F0})");
            System.Console.WriteLine($"  FlakeScale                   = {_groundOverlay.FlakeScale:F5}  (1/N where N={1f / _groundOverlay.FlakeScale:F0})");
            System.Console.WriteLine($"  PuddleThresh                 = {_groundOverlay.PuddleThresh:F2}");
            System.Console.WriteLine($"  PuddleAmount                 = {_groundOverlay.PuddleAmount:F2}");
            System.Console.WriteLine($"  HeightBias                   = {_groundOverlay.HeightBias:F2}");
            System.Console.WriteLine($"  FallTreeUnit                 = {_groundOverlay.FallTreeUnit:F1}");
            System.Console.WriteLine($"  FallSaturation               = {_groundOverlay.FallSaturation:F2}");
            System.Console.WriteLine($"  SnowDetile                   = {_groundOverlay.SnowDetile:F2}");
            System.Console.WriteLine($"  SnowCellSize                 = {_groundOverlay.SnowCellSize:F0}");

            System.Console.WriteLine("======================================================================");
        }
    }
}
