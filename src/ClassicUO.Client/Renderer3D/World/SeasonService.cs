// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).
//
// Seasonal cycle orchestrator. Drives a continuous Spring→Summer→Autumn→Winter
// loop across multiple subsystems via ISeasonHostBridge, which production-side wraps
// the legacy static singletons (TreeSeasonManager, World3DRenderer, Weather3DSystem,
// Static3DRenderer, TreeDefoliationStagger, NukeShow, WeatherDefaultsStore). As each
// downstream system migrates to its own service, the corresponding bridge method swaps
// to a direct service call without touching this file.
//
// Phase map over Progress 0..1 (loops):
//   0.00..0.05 winter wind-down — snow ground fading, leaves bare
//   0.05..0.20 spring — rain rises/peaks/fades, leaves regrow
//   0.20..0.50 summer — dry, full canopy, calm wind
//   0.50..0.78 autumn — fall foliage tint peaks
//   0.78..1.00 winter — snow ground + particles ramp; leaves drop
//
// Showcase mode doubles to a 2-year cycle with year-2 layered weather events.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Production implementation of <see cref="ISeasonService"/>. Allocation-free in the
    /// steady state. Reads timing from <see cref="FrameTickContext"/> only.
    /// </summary>
    public sealed class SeasonService : ISeasonService, IFrameService
    {
        // Phase boundaries (continuous yearPhase → coarse Season).
        private const float BoundaryWinterToSpring = 0.05f;
        private const float BoundarySpringToSummer = 0.20f;
        private const float BoundarySummerToAutumn = 0.50f;
        private const float BoundaryAutumnToWinter = 0.78f;

        // Dependencies.
        private readonly SeasonServiceConfig _config;
        private readonly IRendererEventBus _bus;
        private readonly IWindService _wind;
        private readonly ISeasonHostBridge _host;

        // Mutable runtime state.
        private bool _enabled;
        private float _progress;
        private float _secondsPerYear;
        private bool _showcaseMode;
        private bool _driveWeatherParticles;
        private bool _regrowInSpring;
        private bool _driveSwayFromWeather;
        private bool _driveFoliageShaderTint;
        private string _lastPhase = "off";
        private bool _lastTickWrote;
        private Season _currentSeason = Season.Winter;

        // Wind-ramp targets — output of phase calc, eased into IWindService each tick.
        private float _targetWindStrength;
        private float _targetWindDirection = 90f;

        // Showcase year-2 state.
        private readonly Random _showcaseRng = new Random(0xCAFE);
        private double _nextLightningT;
        private bool _nukeFiredThisCycle;
        private SeasonWeatherKind _lastShowcaseType = SeasonWeatherKind.None;

        public SeasonService(
            SeasonServiceConfig config,
            IRendererEventBus bus,
            IWindService wind,
            ISeasonHostBridge host)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _host = host ?? throw new ArgumentNullException(nameof(host));

            _enabled = _config.InitialEnabled;
            _secondsPerYear = MathF.Max(_config.SecondsPerYear, 1f);
            _driveWeatherParticles = _config.DriveWeatherParticles;
            _regrowInSpring = _config.RegrowInSpring;
            _driveSwayFromWeather = _config.DriveSwayFromWeather;
            _driveFoliageShaderTint = _config.DriveFoliageShaderTint;
        }

        // ===== ISeasonService — read =====

        public bool Enabled => _enabled;
        public float Progress => _progress;
        public float YearPhase => _progress >= 1f ? _progress - 1f : _progress;
        public Season CurrentSeason => _currentSeason;
        public bool ShowcaseMode => _showcaseMode;
        public float SecondsPerYear => _secondsPerYear;
        public bool DriveWeatherParticles => _driveWeatherParticles;
        public bool RegrowInSpring => _regrowInSpring;
        public bool DriveSwayFromWeather => _driveSwayFromWeather;
        public bool DriveFoliageShaderTint => _driveFoliageShaderTint;
        public string LastPhase => _lastPhase;
        public bool LastTickWrote => _lastTickWrote;

        // ===== ISeasonService — mutate =====

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetSecondsPerYear(float seconds) => _secondsPerYear = MathF.Max(1f, seconds);
        public void SetShowcaseMode(bool showcase) => _showcaseMode = showcase;
        public void SetDriveWeatherParticles(bool drive) => _driveWeatherParticles = drive;
        public void SetRegrowInSpring(bool regrow) => _regrowInSpring = regrow;
        public void SetDriveSwayFromWeather(bool drive) => _driveSwayFromWeather = drive;
        public void SetDriveFoliageShaderTint(bool drive) => _driveFoliageShaderTint = drive;

        public void SnapTo(float progress)
        {
            float p = progress < 0f ? 0f : (progress > 0.999f ? 0.999f : progress);
            _progress = p;
            UpdateSeasonAndMaybePublish(p);
            if (!_enabled) return;
            _host.SetTreeSeasonState(true, true, p);
            ApplyGroundAndWeather(p);
            ApplyFoliageTint(p);
        }

        public void Stop()
        {
            _enabled = false;
            _host.SetGroundEffect(SeasonGroundMode.None, 0f);
            _host.SetFoliageSeason(SeasonFoliageMode.None, 0f);
            if (_driveWeatherParticles)
            {
                _host.SetWeatherType(SeasonWeatherKind.None);
                _host.SetWeatherIntensity(0f);
            }
            _host.DropLeavesWorldwide = false;
            _host.LeafPresence = 1f;
            _host.ConfigureDefoliationStagger(0f);
            _host.SetTreeSeasonState(false, true, _progress);
            _targetWindStrength = 0f;
            _host.LightningFlashIntensity = 0f;
            _nukeFiredThisCycle = false;
            _lastShowcaseType = SeasonWeatherKind.None;
            _lastPhase = "off";
            _lastTickWrote = false;
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            float dt = ctx.DeltaSeconds;

            if (!_enabled)
            {
                _lastPhase = "off";
                _lastTickWrote = false;
                EaseLightningFlash(dt);
                return;
            }

            AdvanceProgress(dt);
            float yearPhase = YearPhase;

            _host.SetTreeSeasonState(true, true, yearPhase);
            _host.SetWeatherLinkSuspended(true);

            ApplyGroundAndWeather(yearPhase);
            ApplyFoliageTint(yearPhase);
            ApplyDefoliation(yearPhase);

            if (_showcaseMode && _progress >= 1f)
                ApplyShowcaseWeather(yearPhase, ctx.TotalSeconds);

            EaseWind(dt);
            EaseLightningFlash(dt);
            ApplyTreeSway(dt);
            UpdateSeasonAndMaybePublish(yearPhase);

            _lastTickWrote = true;
        }

        // ===== Internals: progress + season =====

        private void AdvanceProgress(float dt)
        {
            float spy = MathF.Max(_showcaseMode ? _config.ShowcaseSecondsPerYear : _secondsPerYear, 1f);
            float period = _showcaseMode ? 2f : 1f;
            _progress += dt / spy;
            while (_progress >= period)
            {
                _progress -= period;
                _nukeFiredThisCycle = false;
            }
        }

        private void UpdateSeasonAndMaybePublish(float yearPhase)
        {
            Season newSeason = ClassifySeason(yearPhase);
            if (newSeason == _currentSeason) return;
            Season prev = _currentSeason;
            _currentSeason = newSeason;
            _bus.Publish(new SeasonChangedEvent(newSeason, prev, yearPhase));
        }

        /// <summary>Classify a yearPhase in [0,1) into the coarse <see cref="Season"/> enum.</summary>
        public static Season ClassifySeason(float yearPhase)
        {
            if (yearPhase < BoundaryWinterToSpring) return Season.Winter;
            if (yearPhase < BoundarySpringToSummer) return Season.Spring;
            if (yearPhase < BoundarySummerToAutumn) return Season.Summer;
            if (yearPhase < BoundaryAutumnToWinter) return Season.Autumn;
            return Season.Winter;
        }

        // ===== Internals: wind ease =====

        private void EaseWind(float dt)
        {
            if (_config.WindEaseSeconds <= 0.001f)
            {
                _wind.SetStrength(_targetWindStrength);
                _wind.SetDirectionDeg(_targetWindDirection);
                return;
            }
            float k = 1f - MathF.Exp(-dt / _config.WindEaseSeconds);

            _wind.SetStrength(_wind.Strength + (_targetWindStrength - _wind.Strength) * k);

            float diff = _targetWindDirection - _wind.DirectionDeg;
            while (diff > 180f) diff -= 360f;
            while (diff < -180f) diff += 360f;
            _wind.SetDirectionDeg(_wind.DirectionDeg + diff * k);
        }

        private void EaseLightningFlash(float dt)
        {
            float flash = _host.LightningFlashIntensity;
            if (flash <= 0f) return;
            float k = 1f - MathF.Exp(-dt / _config.LightningFlashHalfLifeSeconds);
            flash *= (1f - k);
            if (flash < _config.LightningFlashEpsilon) flash = 0f;
            _host.LightningFlashIntensity = flash;
        }

        // ===== Internals: tree sway =====

        private void ApplyTreeSway(float dt)
        {
            if (!_driveSwayFromWeather) return;

            SeasonWeatherKind wt = _host.GetWeatherType();
            bool sway = _host.GetTreeSwayOverride(wt) ?? DefaultSwayForWeather(wt);
            float wInt = _host.GetWeatherIntensity();
            float targetAmp = sway ? _config.SwayAmpStormDeg * MathF.Max(0.5f, wInt) : 0f;

            float k = _config.SwayAmpEaseSeconds <= 0.001f
                ? 1f
                : 1f - MathF.Exp(-dt / _config.SwayAmpEaseSeconds);

            float amp = _host.LeafPlaneWindAmpDeg;
            amp += (targetAmp - amp) * k;
            _host.LeafPlaneWindAmpDeg = amp;
            _host.LeafPlaneWindEnabled = amp > 0.5f;
        }

        /// <summary>True iff this weather should move trees by default.</summary>
        public static bool DefaultSwayForWeather(SeasonWeatherKind t) =>
            t == SeasonWeatherKind.Storm
            || t == SeasonWeatherKind.Blizzard
            || t == SeasonWeatherKind.Sandstorm
            || t == SeasonWeatherKind.Tornado
            || t == SeasonWeatherKind.BloodMoon;

        // ===== Internals: ground + weather (natural year cycle) =====

        private void ApplyGroundAndWeather(float y)
        {
            float wet = ComputeWetCurve(y);
            float snow = ComputeSnowCurve(y);
            _targetWindStrength = ComputeNaturalWindTarget(y);

            ApplyGroundMode(wet, snow);
            if (_driveWeatherParticles) ApplyWeatherParticles(y, wet);

            _lastPhase = ClassifyPhaseLabel(y);
        }

        public static float ComputeWetCurve(float y)
        {
            if (y >= 0.05f && y < 0.10f) return Smoothstep(0.05f, 0.10f, y);
            if (y >= 0.10f && y < 0.15f) return 1f;
            if (y >= 0.15f && y < 0.20f) return 1f - Smoothstep(0.15f, 0.20f, y);
            return 0f;
        }

        public static float ComputeSnowCurve(float y)
        {
            // Continuous wraparound curve. edge1=1.10 / edge0=-0.05 cause the curves to
            // overlap across the year wrap so winter→spring doesn't snap to dry.
            if (y >= 0.78f && y < 0.92f) return Smoothstep(0.78f, 0.92f, y);
            if (y >= 0.95f) return 1f - Smoothstep(0.95f, 1.10f, y);
            if (y < 0.10f) return 1f - Smoothstep(-0.05f, 0.10f, y);
            if (y >= 0.92f && y < 0.95f) return 1f;
            return 0f;
        }

        public static float ComputeNaturalWindTarget(float y)
        {
            if (y < 0.20f) return 0.25f;
            if (y < 0.50f) return 0.10f;
            if (y < 0.78f) return 0.20f;
            if (y < 0.95f) return 0.35f;
            return 0.20f;
        }

        private void ApplyGroundMode(float wet, float snow)
        {
            if (wet > 0f)
                _host.SetGroundEffect(SeasonGroundMode.Wet, wet * 0.85f);
            else if (snow > 0f)
                _host.SetGroundEffect(SeasonGroundMode.Snow, snow * 0.9f);
            else
                _host.SetGroundEffect(SeasonGroundMode.None, 0f);
        }

        private void ApplyWeatherParticles(float y, float wet)
        {
            if (wet > 0.01f)
            {
                EnsureWeather(SeasonWeatherKind.Rain);
                _host.SetWeatherIntensity(wet * 0.7f);
                return;
            }
            if (y >= 0.78f && y < 0.95f)
            {
                EnsureWeather(SeasonWeatherKind.Snow);
                _host.SetWeatherIntensity(Smoothstep(0.78f, 0.92f, y) * 0.65f);
                return;
            }
            if (y >= 0.95f || y < 0.05f)
            {
                EnsureWeather(SeasonWeatherKind.Snow);
                float t = y >= 0.95f ? Smoothstep(0.95f, 1.10f, y) : Smoothstep(-0.05f, 0.05f, y);
                float i = (1f - t) * 0.65f;
                _host.SetWeatherIntensity(i);
                if (i < 0.01f) _host.SetWeatherType(SeasonWeatherKind.None);
                return;
            }
            if (_host.GetWeatherType() != SeasonWeatherKind.None)
                _host.SetWeatherType(SeasonWeatherKind.None);
            _host.SetWeatherIntensity(0f);
        }

        private void EnsureWeather(SeasonWeatherKind type)
        {
            if (_host.GetWeatherType() != type)
                _host.SetWeatherType(type);
        }

        public static string ClassifyPhaseLabel(float y)
        {
            if (y < 0.05f) return "winter wind-down (snow fading on trees + ground)";
            if (y < 0.10f) return "early spring (rain rising while snow finishes fading)";
            if (y < 0.15f) return "spring rain peak";
            if (y < 0.20f) return "spring rain fading";
            if (y < 0.50f) return "summer (dry)";
            if (y < 0.78f) return "autumn peak (fall colors)";
            if (y < 0.92f) return "late autumn → winter (snow rising)";
            if (y < 0.95f) return "winter peak";
            return "winter (particles fading, snow lingering)";
        }

        // ===== Internals: foliage tint =====

        private void ApplyFoliageTint(float y)
        {
            if (!_driveFoliageShaderTint)
            {
                _host.SetFoliageSeason(SeasonFoliageMode.None, 0f);
                return;
            }
            if (y >= 0.50f && y < 0.84f)
            {
                _host.SetFoliageSeason(SeasonFoliageMode.Fall, ComputeFallTintIntensity(y));
                return;
            }
            _host.SetFoliageSeason(SeasonFoliageMode.None, 0f);
        }

        public static float ComputeFallTintIntensity(float y)
        {
            if (y < 0.65f) return Smoothstep(0.50f, 0.65f, y);
            if (y < 0.78f) return 1f;
            return 1f - Smoothstep(0.78f, 0.84f, y);
        }

        // ===== Internals: defoliation =====

        private void ApplyDefoliation(float y)
        {
            _host.DropLeavesWorldwide = false;

            if (_host.DefoliationStaggerEnabled)
            {
                _host.ConfigureDefoliationStagger(ComputeStaggeredWinterT(y));
                _host.LeafPresence = 1f;
                return;
            }
            _host.LeafPresence = ComputeContinuousLeafPresence(y);
        }

        public float ComputeStaggeredWinterT(float y)
        {
            if (y >= 0.78f && y < 0.95f) return Smoothstep(0.78f, 0.95f, y);
            if (y >= 0.95f || y < 0.05f) return 1f;
            if (y >= 0.05f && y < 0.18f)
                return _regrowInSpring ? (1f - Smoothstep(0.05f, 0.18f, y)) : 1f;
            return 0f;
        }

        public float ComputeContinuousLeafPresence(float y)
        {
            if (y < 0.05f) return 0f;
            if (y < 0.18f) return _regrowInSpring ? Smoothstep(0.05f, 0.18f, y) : 0f;
            if (y < 0.78f) return 1f;
            if (y < 0.95f) return 1f - Smoothstep(0.78f, 0.95f, y);
            return 0f;
        }

        // ===== Internals: showcase year-2 weather =====

        private void ApplyShowcaseWeather(float yearPhase, double totalSeconds)
        {
            float rain      = Window(0.02f, 0.07f, 0.015f, yearPhase);
            float storm     = Window(0.10f, 0.16f, 0.02f, yearPhase);
            float sandst    = Window(0.20f, 0.27f, 0.02f, yearPhase);
            float fogBank   = Window(0.31f, 0.37f, 0.02f, yearPhase);
            float embers    = Window(0.41f, 0.47f, 0.02f, yearPhase);
            float bloodM    = Window(0.51f, 0.58f, 0.02f, yearPhase);
            float snowEarly = Window(0.62f, 0.72f, 0.025f, yearPhase);
            float blizzard  = Window(0.72f, 0.88f, 0.025f, yearPhase);
            float snowLate  = Window(0.88f, 0.92f, 0.02f, yearPhase);
            float tornado   = Window(0.93f, 0.97f, 0.012f, yearPhase);
            float nuke      = Window(0.97f, 1.00f, 0.005f, yearPhase);

            if (rain > 0.01f)      { ShowcaseRain(rain); return; }
            if (storm > 0.01f)     { ShowcaseStorm(storm, totalSeconds); return; }
            if (sandst > 0.01f)    { ShowcaseEvent(SeasonWeatherKind.Sandstorm, sandst, 0.95f * sandst, 280f, "sandstorm"); return; }
            if (fogBank > 0.01f)   { ShowcaseEvent(SeasonWeatherKind.Fog, fogBank, 0.10f, 90f, "fog bank"); return; }
            if (embers > 0.01f)    { ShowcaseEvent(SeasonWeatherKind.Embers, embers, 0.20f, 60f, "embers"); return; }
            if (bloodM > 0.01f)    { ShowcaseEvent(SeasonWeatherKind.BloodMoon, bloodM * 0.7f, 0.30f * bloodM, 30f, "blood moon"); return; }
            if (snowEarly > 0.01f) { ShowcaseEvent(SeasonWeatherKind.Snow, snowEarly, 0.30f, 180f, "winter — snow rolling in"); return; }
            if (blizzard > 0.01f)  { ShowcaseEvent(SeasonWeatherKind.Blizzard, blizzard, 0.95f * blizzard, 180f, "BLIZZARD"); return; }
            if (snowLate > 0.01f)  { ShowcaseEvent(SeasonWeatherKind.Snow, snowLate * 0.7f, 0.20f, 180f, "winter — snow easing off"); return; }
            if (tornado > 0.01f)   { ShowcaseEvent(SeasonWeatherKind.Tornado, tornado, 0.95f * tornado, 200f, "finale — TORNADO"); return; }
            if (nuke > 0.01f)      { ShowcaseNuke(); return; }

            ShowcaseSwitch(SeasonWeatherKind.None);
            _targetWindStrength = 0.15f;
            _lastPhase = "SHOWCASE Y2 — clear day (gap between events)";
        }

        private void ShowcaseRain(float intensity)
        {
            ShowcaseSwitch(SeasonWeatherKind.Rain);
            _host.SetWeatherIntensity(intensity);
            _targetWindStrength = 0.30f * intensity;
            _targetWindDirection = 200f;
            _lastPhase = "SHOWCASE Y2 spring rain";
        }

        private void ShowcaseStorm(float intensity, double totalSeconds)
        {
            ShowcaseSwitch(SeasonWeatherKind.Storm);
            _host.SetWeatherIntensity(intensity);
            _targetWindStrength = 0.85f * intensity;
            _targetWindDirection = 215f;
            if (intensity > 0.7f && (_nextLightningT == 0 || totalSeconds >= _nextLightningT))
            {
                _host.TriggerLightning();
                _host.LightningFlashIntensity = 0.85f;
                _nextLightningT = totalSeconds + 2.0 + _showcaseRng.NextDouble() * 4.0;
            }
            _lastPhase = $"SHOWCASE Y2 storm — wind→{_targetWindStrength:F2}";
        }

        private void ShowcaseEvent(SeasonWeatherKind type, float intensity, float windStr, float windDir, string label)
        {
            ShowcaseSwitch(type);
            _host.SetWeatherIntensity(intensity);
            _targetWindStrength = windStr;
            _targetWindDirection = windDir;
            _lastPhase = $"SHOWCASE Y2 {label} — wind→{_targetWindStrength:F2}";
        }

        private void ShowcaseNuke()
        {
            if (!_nukeFiredThisCycle)
            {
                _host.EnableNukeShow();
                _host.TriggerNukeBarrage();
                _nukeFiredThisCycle = true;
            }
            ShowcaseSwitch(SeasonWeatherKind.Tornado);
            _host.SetWeatherIntensity(1f);
            _targetWindStrength = 1.0f;
            _targetWindDirection = 200f;
            _lastPhase = "SHOWCASE Y2 finale — NUKE BARRAGE";
        }

        private void ShowcaseSwitch(SeasonWeatherKind to)
        {
            if (_lastShowcaseType == to) return;
            _host.SetWeatherType(to);
            _lastShowcaseType = to;
        }

        // ===== Pure-math helpers (public so tests can exercise them directly) =====

        public static float Window(float a, float b, float w, float yearPhase)
        {
            if (yearPhase < a - w || yearPhase > b + w) return 0f;
            if (yearPhase < a) return Smoothstep(a - w, a, yearPhase);
            if (yearPhase > b) return 1f - Smoothstep(b, b + w, yearPhase);
            return 1f;
        }

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = (x - edge0) / (edge1 - edge0);
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
