// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — 3D weather effects driven by Particle3DSystem.
//
// Each weather type is a continuous emitter that spawns particles in a
// cylinder around the player every tick. Particles are configured (gravity,
// life, color, size, F_TRAIL) so the fall pattern reads as the weather:
//
//   Rain      — fast vertical streaks (F_TRAIL leaves a fading trail).
//   Snow      — slow drifting flakes with mild horizontal sway.
//   Storm     — heavier rain + auto lightning flashes.
//   Sandstorm — horizontal warm-tinted streaks blown by wind.
//   Embers    — slow upward-drifting orange sparks (post-battle ambience).
//   Fog       — large slow translucent puffs near ground level.
//   BloodMoon — dim red sky, red rain, red fog, ominous calm.
//   Blizzard  — heavy snow + violent shifting wind.
//
// SetType() applies a WeatherProfile that drives WindManager (gust mode +
// cadence), Static3DRenderer leaf sway amplitude, and World3DRenderer
// background/fog color targets so the entire atmosphere shifts together.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy weather-type enum aliased to <see cref="WeatherKind"/>. Bit-for-bit identical
    /// values so casts round-trip. Will be removed in ADR-012 Phase 3 once all callers
    /// migrate to <c>ClassicUO.Renderer.Atmosphere.WeatherKind</c>.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Atmosphere.WeatherKind. Will be removed in ADR-012 Phase 3.")]
    internal enum Weather3DType
    {
        None      = 0,
        Rain      = 1,
        Snow      = 2,
        Storm     = 3,
        Sandstorm = 4,
        Embers    = 5,
        Fog       = 6,
        BloodMoon = 7,
        Blizzard  = 8,
        Tornado   = 9,
    }

    /// <summary>
    /// Legacy weather subsystem, partially migrated. Public state surface (Type, Intensity,
    /// WindX/Z, Radius, Height, AutoLightning, OcclusionCheckEnabled, ApplyProfileOnSetType,
    /// Configure, SetType, TriggerLightning) delegates to <see cref="IWeatherService"/> via
    /// <see cref="Renderer3DHost"/>. The simulation (Update, SpawnOne, lightning bolt
    /// emission, tornado physics, profile application) still lives here and reads state
    /// through the same delegating accessors. Future migration moves the simulation into
    /// the service when <c>Particle3DSystem</c> migrates.
    /// </summary>
    internal static class Weather3DSystem
    {
        private static IWeatherService Svc => Renderer3DHost.Services.Weather;

        // ===== Public state — delegates to IWeatherService =====

        public static Weather3DType Type
        {
            get => (Weather3DType)(int)Svc.Type;
            set => Svc.SetType((WeatherKind)(int)value);
        }
        public static float Intensity
        {
            get => Svc.Intensity;
            set => Svc.SetIntensity(value);
        }
        public static float WindX
        {
            get => Svc.WindX;
            set => Svc.SetWindX(value);
        }
        public static float WindZ
        {
            get => Svc.WindZ;
            set => Svc.SetWindZ(value);
        }
        public static float Radius
        {
            get => Svc.Radius;
            set => Svc.SetRadius(value);
        }
        public static float Height
        {
            get => Svc.Height;
            set => Svc.SetHeight(value);
        }
        public const float HEAVY_WEATHER_RADIUS = 900f;
        public static bool AutoLightning
        {
            get => Svc.AutoLightning;
            set => Svc.SetAutoLightning(value);
        }
        public static float LightningEveryMin
        {
            get => Svc.LightningEveryMin;
            set => Svc.SetLightningEveryMin(value);
        }
        public static float LightningEveryMax
        {
            get => Svc.LightningEveryMax;
            set => Svc.SetLightningEveryMax(value);
        }
        public static float LightningStrikeMinRadius = 110f;
        public static float LightningStrikeMaxRadius = 308f;
        public static bool ApplyProfileOnSetType
        {
            get => Svc.ApplyProfileOnSetType;
            set => Svc.SetApplyProfileOnSetType(value);
        }
        public static bool OcclusionCheckEnabled
        {
            get => Svc.OcclusionCheckEnabled;
            set => Svc.SetOcclusionCheckEnabled(value);
        }

        // ===== Internal simulation state (stays here until simulation migrates) =====
        private static Vector3 _anchor => Svc.Anchor; // delegated; Configure writes through service
        private static float _spawnAccumulator;
        private static float _lightningTimer;
        private static readonly Random _rng = new Random(0xCAFE);

        // Tornado state — funnel position drifts independently of the player
        // anchor. Re-seeded from the current anchor each time SetType(Tornado)
        // is called.
        private static Vector3 _tornadoPos;
        private static Vector3 _tornadoVel;
        private static float _tornadoSwirlPhase;
        private static bool _tornadoNeedsRespawn;
        // Drift speed in world units/sec. 22 u = 1 tile, so ~30 u/s ≈ 1.4 tiles/s.
        public static float TornadoDriftSpeed = 30f;

        // Tornado proximity damage. The client prototype can't actually damage
        // the player (combat is server-authoritative); instead this fires a
        // visual warning + console log when the player anchor enters the kill
        // radius. Real damage requires a server-side region in custom/Scripts/
        // (TODO: TornadoRegion that periodically pings hits on non-GM mobiles).
        public static float TornadoKillRadius      = 80f;     // ~3.6 tiles
        public static bool  TornadoGMAdminBypass   = true;    // GMs/admins ignore the proximity check
        public static float TornadoLastPlayerDist;            // diagnostic
        public static int   TornadoCloseCallCount;            // bumped each warning tick
        // Funnel geometry (world units). Base is at ground; top flares wider.
        public static float TornadoBaseRadius = 22f;   // 1 tile
        public static float TornadoMidRadius  = 70f;
        public static float TornadoTopRadius  = 180f;  // mushroom cap
        public static float TornadoHeight     = 600f;

        public static void Configure(Vector3 anchorWorld)
        {
            Svc.Configure(anchorWorld);
        }

        public static void SetType(Weather3DType type)
        {
            // Clear in-flight particles when the type changes so old flakes
            // don't get redrawn with the new weather's texture (the cause of
            // the "snowflakes turn into black squares with circle inside"
            // artifact when transitioning Snow → Rain). The texture slots are
            // global and would otherwise re-skin still-alive particles next
            // frame.
            Weather3DType previous = Type;
            if (type != previous)
                Particle3DSystem.Clear();

            // Service publishes WeatherChangedEvent; legacy Type setter would also do this,
            // but going direct avoids the recursive setter→Svc.SetType→back call.
            Svc.SetType((WeatherKind)(int)type);
            _spawnAccumulator = 0f;

            // Heavy weather pins intensity AND coverage to max regardless of
            // the gump sliders — a "Storm" / "BloodMoon" should always feel
            // catastrophic, not a half-version. The user can still scrub down
            // via the slider after SetType if they explicitly want a weak run.
            if (type == Weather3DType.Storm    || type == Weather3DType.Tornado ||
                type == Weather3DType.Blizzard || type == Weather3DType.Sandstorm ||
                type == Weather3DType.BloodMoon)
            {
                Intensity = 1.0f;
                Radius    = HEAVY_WEATHER_RADIUS;
            }

            // Tornadoes carry storm conditions plus more frequent strikes —
            // both Storm and Tornado use AutoLightning, but the timer cadence
            // differs (see ResetLightningTimer).
            AutoLightning = (type == Weather3DType.Storm) || (type == Weather3DType.Tornado);
            if (AutoLightning) ResetLightningTimer();
            if (type == Weather3DType.Tornado) _tornadoNeedsRespawn = true;

            // Bind the two Particle3DSystem texture slots (alpha + additive)
            // to the registry PNGs that best fit this weather. The slots are
            // global, so swapping here means in-flight particles inherit the
            // new texture next frame — fine for our purposes since weather
            // changes are intended to look like a fresh storm rolling in.
            BindTexturesForType(type);

            if (ApplyProfileOnSetType)
            {
                // Apply hardcoded profile + any persisted user override.
                ApplyProfile(GetMergedProfile(type));
            }

            // Runtime-only overrides (Intensity / Radius / Height / sway-on
            // bit) come last so a saved override beats the heavy-weather pin
            // — e.g. a user-tuned BloodMoon at 0.85 intensity sticks instead
            // of getting forced to 1.0 by the heavy-weather block above.
            WeatherDefaultsStore.ApplyRuntimeOverrides(type);
        }

        // Pick the best PNG pair from ParticleTextureRegistry for the given
        // weather. ParticleTexture drives F_TEXTURED (alpha-blend, e.g. snow,
        // fog, smoke, sand). ParticleRainTexture drives F_TEXTURED_ADD
        // (additive streaks, e.g. rain, blood rain, lightning ribbons).
        // Missing textures fall back to whatever was previously bound, so a
        // partial registry doesn't blank weather mid-scene.
        private static void BindTexturesForType(Weather3DType t)
        {
            string alphaName = null;     // F_TEXTURED slot (snow/fog/sand)
            string addName   = null;     // F_TEXTURED_ADD slot (rain/blood)
            switch (t)
            {
                case Weather3DType.Rain:
                case Weather3DType.Storm:
                    addName   = "rain";
                    alphaName = "fog";          // ambient mist puff support
                    break;
                case Weather3DType.Snow:
                    alphaName = "snow";
                    break;
                case Weather3DType.Blizzard:
                    alphaName = "snowflake";    // bigger stylized flake reads better in blizzard
                    addName   = "rain";         // blizzard streaks (texture available even if unused)
                    break;
                case Weather3DType.Sandstorm:
                    // Dust streak texture is bright-on-dark — alpha-blend
                    // would render the black background as dark squares.
                    // Route through additive so the dark drops out and only
                    // the warm sand streaks composite over the world.
                    addName = "dust";
                    break;
                case Weather3DType.Fog:
                    alphaName = "fog";
                    break;
                case Weather3DType.BloodMoon:
                    addName   = "rain";         // red blood streaks (vertex color tints it)
                    alphaName = "fog_alt";      // red mist puffs
                    break;
                case Weather3DType.Embers:
                    // Same reason as dust — flash disc is bright-on-dark.
                    addName = "flash";
                    break;
                case Weather3DType.Tornado:
                    // cloud_thick reads better at distance and now has properly
                    // baked alpha (see particle texture audit). smoke_dense kept
                    // its hard-edged 255-alpha until the audit pass — left as a
                    // fallback if cloud_thick is missing.
                    alphaName = "cloud_thick";
                    break;
            }

            var rainTex  = addName   != null ? ParticleTextureRegistry.Get(addName)   : null;
            var alphaTex = alphaName != null ? ParticleTextureRegistry.Get(alphaName) : null;
            if (rainTex  != null) Particle3DSystem.ParticleRainTexture = rainTex;
            if (alphaTex != null) Particle3DSystem.ParticleTexture     = alphaTex;
        }

        // ===== Weather profile =====
        // A bundle of "what each weather feels like" knobs. SetType applies it.
        // Edit the GetProfile switch below to retune any weather's atmosphere.
        internal struct WeatherProfile
        {
            public WindGustMode Gust;
            public float WindStrength;      // 0..1 baseline
            public float WindFrequency;     // Hz (gust breath)
            public float GustChangeMin;     // s
            public float GustChangeMax;     // s
            public float GustStrengthMin;   // 0..1
            public float GustStrengthMax;   // 0..1
            public float GustDirRangeDeg;
            public float LeafSwayAmpDeg;    // tree yaw amplitude
            public Color BackgroundColor;   // sky behind world (target — eased)
            public Color FogColor;          // distance fog (target — eased)
            public float AtmospherePulse;   // 0..1 BloodMoon-only modulation
            public bool  WindLinkToWeather; // push WindManager → Weather wind X/Z
        }

        internal static WeatherProfile GetProfile(Weather3DType t)
        {
            switch (t)
            {
                case Weather3DType.None:
                    // Clear day — daytime blue sky with soft warm horizon haze.
                    return new WeatherProfile {
                        Gust = WindGustMode.None,
                        WindStrength = 0.15f, WindFrequency = 0.30f,
                        GustChangeMin = 8f, GustChangeMax = 14f,
                        GustStrengthMin = 0.10f, GustStrengthMax = 0.25f,
                        GustDirRangeDeg = 30f,
                        LeafSwayAmpDeg = 2.5f,
                        BackgroundColor = new Color(110, 170, 230), // bright daytime blue
                        FogColor        = new Color(190, 210, 235), // pale haze
                        AtmospherePulse = 0f, WindLinkToWeather = false,
                    };
                case Weather3DType.Rain:
                    return new WeatherProfile {
                        Gust = WindGustMode.Steady,
                        WindStrength = 0.45f, WindFrequency = 0.40f,
                        GustChangeMin = 6f, GustChangeMax = 10f,
                        GustStrengthMin = 0.30f, GustStrengthMax = 0.55f,
                        GustDirRangeDeg = 20f,
                        LeafSwayAmpDeg = 5f,
                        BackgroundColor = new Color( 40,  46,  56),
                        FogColor        = new Color( 70,  90, 110),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                case Weather3DType.Snow:
                    return new WeatherProfile {
                        Gust = WindGustMode.Steady,
                        WindStrength = 0.25f, WindFrequency = 0.25f,
                        GustChangeMin = 8f, GustChangeMax = 14f,
                        GustStrengthMin = 0.15f, GustStrengthMax = 0.45f,
                        GustDirRangeDeg = 30f,
                        LeafSwayAmpDeg = 4f,
                        BackgroundColor = new Color(120, 130, 145),
                        FogColor        = new Color(200, 210, 220),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                case Weather3DType.Storm:
                    return new WeatherProfile {
                        Gust = WindGustMode.Storm,
                        WindStrength = 1.00f, WindFrequency = 0.65f,
                        GustChangeMin = 1.5f, GustChangeMax = 3.5f,
                        GustStrengthMin = 0.75f, GustStrengthMax = 1.00f,
                        GustDirRangeDeg = 140f,
                        LeafSwayAmpDeg = 28f, // violent
                        BackgroundColor = new Color( 18,  20,  26),
                        FogColor        = new Color( 30,  35,  45),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                case Weather3DType.Sandstorm:
                    return new WeatherProfile {
                        Gust = WindGustMode.Variable,
                        WindStrength = 1.00f, WindFrequency = 0.55f,
                        GustChangeMin = 2f, GustChangeMax = 4.5f,
                        GustStrengthMin = 0.80f, GustStrengthMax = 1.00f,
                        GustDirRangeDeg = 60f,
                        LeafSwayAmpDeg = 24f,
                        BackgroundColor = new Color(160, 110,  60),
                        FogColor        = new Color(200, 150,  90),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                case Weather3DType.Embers:
                    return new WeatherProfile {
                        Gust = WindGustMode.None,
                        WindStrength = 0.10f, WindFrequency = 0.25f,
                        GustChangeMin = 12f, GustChangeMax = 18f,
                        GustStrengthMin = 0.05f, GustStrengthMax = 0.20f,
                        GustDirRangeDeg = 20f,
                        LeafSwayAmpDeg = 3f,
                        BackgroundColor = new Color( 30,  18,  14),
                        FogColor        = new Color( 80,  40,  20),
                        AtmospherePulse = 0f, WindLinkToWeather = false,
                    };
                case Weather3DType.Fog:
                    return new WeatherProfile {
                        Gust = WindGustMode.None,
                        WindStrength = 0.10f, WindFrequency = 0.20f,
                        GustChangeMin = 12f, GustChangeMax = 20f,
                        GustStrengthMin = 0.05f, GustStrengthMax = 0.20f,
                        GustDirRangeDeg = 15f,
                        LeafSwayAmpDeg = 2f,
                        BackgroundColor = new Color(110, 110, 115),
                        FogColor        = new Color(180, 180, 185),
                        AtmospherePulse = 0f, WindLinkToWeather = false,
                    };
                case Weather3DType.BloodMoon:
                    return new WeatherProfile {
                        // Chaotic violent gusts that yank blood rain around the
                        // sky. Storm-mode wind with the widest direction range
                        // so droplets visibly tumble in random directions.
                        Gust = WindGustMode.Storm,
                        WindStrength = 1.00f, WindFrequency = 0.55f,
                        GustChangeMin = 1.0f, GustChangeMax = 2.5f,
                        GustStrengthMin = 0.70f, GustStrengthMax = 1.00f,
                        GustDirRangeDeg = 180f,
                        LeafSwayAmpDeg = 22f,
                        // Darker so the world reads as ominous night, not red noon.
                        BackgroundColor = new Color( 35,   4,   4),
                        FogColor        = new Color( 50,   8,   8),
                        AtmospherePulse = 0.65f,
                        WindLinkToWeather = true, // blow blood rain around
                    };
                case Weather3DType.Blizzard:
                    return new WeatherProfile {
                        Gust = WindGustMode.Storm,
                        WindStrength = 1.00f, WindFrequency = 0.65f,
                        GustChangeMin = 1.0f, GustChangeMax = 3f,
                        GustStrengthMin = 0.80f, GustStrengthMax = 1.00f,
                        GustDirRangeDeg = 180f,
                        LeafSwayAmpDeg = 26f,
                        BackgroundColor = new Color(170, 180, 195),
                        FogColor        = new Color(220, 225, 235),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                case Weather3DType.Tornado:
                    return new WeatherProfile {
                        Gust = WindGustMode.Storm,
                        WindStrength = 1.00f, WindFrequency = 0.80f,
                        GustChangeMin = 0.8f, GustChangeMax = 2f,
                        GustStrengthMin = 0.85f, GustStrengthMax = 1.00f,
                        GustDirRangeDeg = 220f,
                        LeafSwayAmpDeg = 32f, // most violent
                        BackgroundColor = new Color( 38,  34,  30),
                        FogColor        = new Color( 90,  78,  60),
                        AtmospherePulse = 0f, WindLinkToWeather = true,
                    };
                default:
                    return GetProfile(Weather3DType.None);
            }
        }

        // Public wrapper so the WeatherConfigGump can fetch the merged profile
        // for a given type (hardcoded base + persisted user override applied
        // on top via WeatherDefaultsStore).
        internal static WeatherProfile GetMergedProfile(Weather3DType t)
        {
            var p = GetProfile(t);
            WeatherDefaultsStore.Merge(t, ref p);
            return p;
        }

        internal static void ApplyProfile(WeatherProfile p)
        {
            WindManager.GustMode = p.Gust;
            WindManager.Strength = p.WindStrength;
            WindManager.Frequency = p.WindFrequency;
            WindManager.GustChangeMin = p.GustChangeMin;
            WindManager.GustChangeMax = p.GustChangeMax;
            WindManager.GustStrengthMin = p.GustStrengthMin;
            WindManager.GustStrengthMax = p.GustStrengthMax;
            WindManager.GustDirectionRangeDeg = p.GustDirRangeDeg;
            WindManager.LinkToWeather = p.WindLinkToWeather;

            Static3DRenderer.LeafPlaneWindAmpDeg = p.LeafSwayAmpDeg;
            // Auto-enable leaf sway whenever the profile asks for any amplitude
            // — otherwise heavy weather can land with the toggle still off and
            // the canopy reads as eerily still while everything else thrashes.
            if (p.LeafSwayAmpDeg > 0.5f) Static3DRenderer.LeafPlaneWindEnabled = true;

            World3DRenderer.BackgroundColorTarget = p.BackgroundColor;
            World3DRenderer.FogColorTarget = p.FogColor;
            World3DRenderer.AtmospherePulseAmount = p.AtmospherePulse;
        }

        public static void Update(float dt)
        {
            if (Type == Weather3DType.None || Intensity <= 0f)
            {
                // Atmosphere lerp ticks via AtmospherePass (session 64 switchover);
                // we just stop emitting particles.
                return;
            }

            if (Type == Weather3DType.Tornado)
            {
                if (_tornadoNeedsRespawn) RespawnTornado();
                _tornadoPos += _tornadoVel * dt;
                _tornadoSwirlPhase += dt * 6f; // ~1 rev/sec for spawn-angle sweep
                CheckTornadoProximity();
            }

            float rate = SpawnRatePerSecond() * Intensity;
            _spawnAccumulator += rate * dt;
            int toSpawn = (int)_spawnAccumulator;
            _spawnAccumulator -= toSpawn;

            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne();
            }

            if (AutoLightning &&
                (Type == Weather3DType.Storm || Type == Weather3DType.Tornado))
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0f)
                {
                    TriggerLightning();
                    ResetLightningTimer();
                }
            }
        }

        // Proximity check ticked while a tornado is active. Player anchor is
        // the same _anchor that drives weather spawn — Particle3DSystem pushes
        // it in each frame from the player's world position. If the player is
        // inside TornadoKillRadius and the GM/Admin bypass is off, fire a
        // bright red flash + log; real damage must be applied server-side.
        private static double _nextProximityLog;
        private static void CheckTornadoProximity()
        {
            Vector3 d = _anchor - _tornadoPos;
            d.Y = 0f;
            float dist = d.Length();
            TornadoLastPlayerDist = dist;
            if (dist >= TornadoKillRadius) return;
            if (TornadoGMAdminBypass) return;

            // Inside the kill radius without bypass — visual hit.
            TornadoCloseCallCount++;
            World3DRenderer.LightningFlashIntensity =
                MathHelper.Max(World3DRenderer.LightningFlashIntensity, 0.75f);

            double now = Environment.TickCount64 / 1000.0;
            if (now >= _nextProximityLog)
            {
                Console.WriteLine($"[Tornado] Player inside kill radius ({dist:F0}u < {TornadoKillRadius:F0}u). " +
                                  $"GMAdminBypass=off — server-side region would damage now (TODO).");
                _nextProximityLog = now + 1.0;
            }
        }

        // ============================================================
        //   LIGHTNING — proper bolt + ground burst + screen flash
        // ============================================================
        // Old impl: 60-particle radial burst at cloud height = a square
        // overexposed flash sitting in the air. New impl: zigzag bolt
        // from sky to a strike point near the player, with a bright
        // ground burst and a wide white flash quad at impact.
        public static void TriggerLightning()
        {
            // Publish LightningStruckEvent for any subscribers (audio, gameplay hooks).
            // The legacy logic below still runs in this transitional window — the audio
            // call into WeatherAudioSystem and the screen-flash write to World3DRenderer
            // are unmoved until those systems migrate to subscribers.
            Svc.TriggerLightning();

            // Wash the screen with a bright flash so the world reads as
            // illuminated by the strike. Decays each frame in
            // SeasonCycleDriver.EaseLightningFlash.
            World3DRenderer.LightningFlashIntensity = 0.85f;

            // Audible thunder one-shot — handled by IWeatherAudioService subscribing to
            // LightningStruckEvent (published above by Svc.TriggerLightning). The legacy
            // direct call is removed to prevent double-playback.

            // Choose strike center: tornado bolts cluster around the funnel,
            // regular storm bolts cluster around the player.
            Vector3 center;
            float minR, maxR;
            if (Type == Weather3DType.Tornado)
            {
                center = _tornadoPos;
                minR = TornadoBaseRadius * 1.2f;
                maxR = TornadoTopRadius * 1.5f;     // ring around the funnel
            }
            else
            {
                center = _anchor;
                minR = LightningStrikeMinRadius;
                maxR = LightningStrikeMaxRadius;
            }
            float r = minR + (float)_rng.NextDouble() * MathF.Max(1f, maxR - minR);
            float a = (float)_rng.NextDouble() * MathHelper.TwoPi;
            Vector3 ground = center + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
            Vector3 cloud  = ground + new Vector3(0f, Height, 0f);

            // Build the main bolt as a chain of bright streak particles.
            // 12 segments, each ~Height/12 long, with horizontal jitter that
            // grows mid-bolt and tapers to 0 at the strike point.
            const int SEGMENTS = 12;
            Vector3 prev = cloud;
            for (int i = 1; i <= SEGMENTS; i++)
            {
                float t = i / (float)SEGMENTS;
                // Taper: bell curve peaking near t=0.5.
                float jitterScale = MathF.Sin(t * MathHelper.Pi) * 30f;
                float jx = ((float)_rng.NextDouble() * 2f - 1f) * jitterScale;
                float jz = ((float)_rng.NextDouble() * 2f - 1f) * jitterScale;
                Vector3 next = Vector3.Lerp(cloud, ground, t) + new Vector3(jx, 0f, jz);
                EmitBoltSegment(prev, next);
                prev = next;
            }

            // Branching: occasional split partway down (gives the eye a "fork").
            int branchCount = 1 + _rng.Next(2);
            for (int b = 0; b < branchCount; b++)
            {
                float startT = 0.30f + (float)_rng.NextDouble() * 0.40f;
                Vector3 branchStart = Vector3.Lerp(cloud, ground, startT);
                float branchLen = Height * 0.20f;
                float bAng = (float)_rng.NextDouble() * MathHelper.TwoPi;
                float pitch = 0.4f + (float)_rng.NextDouble() * 0.5f; // angle below horizontal
                Vector3 dir = new Vector3(MathF.Cos(bAng) * MathF.Cos(pitch),
                                          -MathF.Sin(pitch),
                                          MathF.Sin(bAng) * MathF.Cos(pitch));
                Vector3 branchEnd = branchStart + dir * branchLen;
                EmitBoltSegment(branchStart, branchEnd);
            }

            // Ground impact: bright white burst + outward sparks.
            for (int i = 0; i < 24; i++)
            {
                float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
                float speed = 80f + (float)_rng.NextDouble() * 240f;
                Vector3 v = new Vector3(MathF.Cos(ang) * speed,
                                         60f + (float)_rng.NextDouble() * 60f,
                                         MathF.Sin(ang) * speed);
                Particle3DSystem.Spawn(ground, v,
                    new Vector3(0f, -180f, 0f),
                    0.35f, 6f, 1f,
                    new Color(255, 255, 240),
                    new Color(180, 200, 255, (byte)0),
                    Particle3DSystem.F_TRAIL);
            }
            // Lazy-bind the flash texture (Hovl Halo1) the first time we
            // need it. Without F_FLASH these spawns rendered as untextured
            // additive squares — the literal "white square" the user saw.
            if (Particle3DSystem.ParticleFlashTexture == null)
                Particle3DSystem.ParticleFlashTexture = ParticleTextureRegistry.Get("halo");

            // Big core flash at the strike point — soft disc.
            Particle3DSystem.Spawn(ground + new Vector3(0f, 8f, 0f),
                Vector3.Zero, Vector3.Zero,
                0.18f, 220f, 60f,
                new Color(255, 255, 255),
                new Color(160, 200, 255, (byte)0),
                Particle3DSystem.F_FLASH);
            // High-altitude sky flash (fills cloud cover above) — same disc.
            Particle3DSystem.Spawn(cloud, Vector3.Zero, Vector3.Zero,
                0.25f, 360f, 220f,
                new Color(220, 230, 255, (byte)180),
                new Color(120, 160, 255, (byte)0),
                Particle3DSystem.F_FLASH);

            // Lightning ignites a small fire patch at the strike point.
            // Tornado bolts ignite too — visible debris fires along the funnel
            // ring. Fire system is rate-limited globally (16 patch slots) so
            // dense storms don't carpet the world in flame.
            FireSpreadSystem.Ignite(ground, 18f, 6f);
        }

        private static void EmitBoltSegment(Vector3 a, Vector3 b)
        {
            // A run of bright additive particles between two points. Density
            // scales with segment length so long jumps still read solid.
            Vector3 d = b - a;
            float len = d.Length();
            int n = (int)MathHelper.Clamp(len / 8f, 4f, 24f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = a + d * t;
                // Tiny perpendicular jitter so the line reads electric, not laser.
                float jx = ((float)_rng.NextDouble() * 2f - 1f) * 3f;
                float jz = ((float)_rng.NextDouble() * 2f - 1f) * 3f;
                p += new Vector3(jx, 0f, jz);
                Particle3DSystem.Spawn(p, Vector3.Zero, Vector3.Zero,
                    0.28f, 7f, 4f,
                    new Color(255, 255, 240),
                    new Color(200, 220, 255, (byte)0));
            }
        }

        // ===== Per-type spawn behaviour =====

        private static float SpawnRatePerSecond()
        {
            return Type switch
            {
                Weather3DType.Rain      => 600f,
                Weather3DType.Snow      => 220f,
                Weather3DType.Storm     => 900f,
                Weather3DType.Sandstorm => 700f,
                Weather3DType.Embers    => 90f,
                Weather3DType.Fog       => 30f,
                Weather3DType.BloodMoon => 280f,  // light red rain
                Weather3DType.Blizzard  => 520f,  // heavy snow
                Weather3DType.Tornado   => 900f,  // dense funnel + debris cloud
                _                       => 0f,
            };
        }

        private static void SpawnOne()
        {
            switch (Type)
            {
                case Weather3DType.Rain:      SpawnRain(streaky: false); break;
                case Weather3DType.Storm:     SpawnRain(streaky: true);  break;
                case Weather3DType.Snow:      SpawnSnow(blizzard: false); break;
                case Weather3DType.Sandstorm: SpawnSandstorm();          break;
                case Weather3DType.Embers:    SpawnEmbers();             break;
                case Weather3DType.Fog:       SpawnFog();                break;
                case Weather3DType.BloodMoon:
                    // Mix: 1-in-6 spawns is red fog, the rest is blood rain.
                    if (_rng.Next(6) == 0) SpawnFog(red: true);
                    else                   SpawnBloodRain();
                    break;
                case Weather3DType.Blizzard:  SpawnSnow(blizzard: true); break;
                case Weather3DType.Tornado:
                    // Tornadoes carry the storm — most spawns are funnel debris,
                    // but a fraction is streaky rain for full storm atmosphere.
                    if (_rng.Next(5) == 0) SpawnRain(streaky: true);
                    else                   SpawnTornado();
                    break;
            }
        }

        // Re-seed funnel position near current player anchor and pick a random
        // slow drift direction. Called once after SetType(Tornado) so the
        // funnel always appears in view of whoever toggled it.
        private static void RespawnTornado()
        {
            // ~6 tiles offset so the funnel reads on screen but isn't on top
            // of the player.
            float a = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float r = 130f + (float)_rng.NextDouble() * 60f;
            _tornadoPos = _anchor + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);

            float driftAng = (float)_rng.NextDouble() * MathHelper.TwoPi;
            _tornadoVel = new Vector3(
                MathF.Cos(driftAng) * TornadoDriftSpeed, 0f,
                MathF.Sin(driftAng) * TornadoDriftSpeed);
            _tornadoNeedsRespawn = false;
        }

        // One funnel particle: pick a height, compute funnel radius at that
        // height, place the particle on the swept ring, give it a tangential
        // velocity (cyclonic spin) plus an upward draft so debris streams up
        // and out of the cap. Particles inherit _tornadoVel so they drift
        // with the funnel rather than getting "left behind".
        private static void SpawnTornado()
        {
            // Mostly funnel particles; ~1 in 5 is a low ground-debris puff.
            bool groundDebris = _rng.Next(5) == 0;

            // Height parameter 0..1 along funnel.
            float h = groundDebris
                ? (float)_rng.NextDouble() * 0.08f
                : (float)_rng.NextDouble();

            // Cone with mid pinch (narrowest near base, flares to top mushroom).
            // Lerp base→mid for h<0.5, mid→top for h≥0.5.
            float radius = h < 0.5f
                ? MathHelper.Lerp(TornadoBaseRadius, TornadoMidRadius, h * 2f)
                : MathHelper.Lerp(TornadoMidRadius, TornadoTopRadius, (h - 0.5f) * 2f);
            // Slight per-particle radial jitter so the funnel doesn't read as
            // a perfect ring.
            radius *= 0.85f + (float)_rng.NextDouble() * 0.30f;

            // Spawn angle sweeps (so emission isn't biased) plus per-call jitter.
            float angle = _tornadoSwirlPhase + (float)_rng.NextDouble() * MathHelper.TwoPi;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            Vector3 pos = _tornadoPos + new Vector3(cos * radius, h * TornadoHeight, sin * radius);

            // Tangential spin (perpendicular to radial), counter-clockwise.
            // Speed scales with radius so outer particles look like they're
            // being flung. ~3 rad/sec at the mid radius.
            const float angularSpeed = 3.2f;
            float tangentialSpeed = radius * angularSpeed;
            Vector3 tangential = new Vector3(-sin, 0f, cos) * tangentialSpeed;

            // Upward draft: stronger at low heights (sucking up debris) and
            // tapering near the top.
            float updraft = MathHelper.Lerp(180f, 30f, h);

            Vector3 vel = tangential + new Vector3(0f, updraft, 0f) + _tornadoVel;

            // Slight inward pull at base, outward at top (mushroom flare).
            float radialAccelMag = MathHelper.Lerp(-40f, 60f, h);
            Vector3 accel = new Vector3(cos * radialAccelMag, -10f, sin * radialAccelMag);

            // Color: bright off-white funnel that reads against any sky/ground.
            // The properly-alpha'd cloud_thick texture made the previous dim
            // grey-brown puffs disappear into the background — push values up
            // and reduce the warm/cool tint so the funnel pops. Ground debris
            // stays slightly warmer to differentiate it from the airborne body.
            byte shade = (byte)MathHelper.Lerp(195f, 245f, h);
            Color cStart = groundDebris
                ? new Color((byte)205, (byte)190, (byte)170, (byte)200)
                : new Color(shade, (byte)(shade - 5), (byte)(shade - 12), (byte)210);
            Color cEnd = new Color(shade, (byte)(shade - 5), (byte)(shade - 12), (byte)0);

            float life = groundDebris ? 1.4f : (1.6f + (float)_rng.NextDouble() * 0.8f);
            float sizeStart = groundDebris ? 28f : (14f + radius * 0.05f);
            float sizeEnd   = groundDebris ? 60f : (sizeStart * 1.6f);

            // Tornado already uses F_TEXTURED — pairs with the "smoke_dense"
            // PNG bound by BindTexturesForType so the funnel reads as a
            // dense churning column rather than untextured smudges.
            Particle3DSystem.Spawn(pos, vel, accel, life, sizeStart, sizeEnd,
                cStart, cEnd, Particle3DSystem.F_TEXTURED);
        }

        // True when the tile under (worldX, worldZ) is open to the sky. Cheap
        // (single chunk lookup + bit test). Returns true on lookup miss so a
        // missing chunk doesn't blank the storm. _anchor.Y is irrelevant — the
        // exposure mask is XZ-based.
        private static bool IsExposedAt(Vector3 origin)
        {
            try
            {
                var map = ClassicUO.Client.Game.UO.World?.Map;
                if (map == null) return true;
                int tx = (int)(origin.X / LandMesh3D.TILE);
                int ty = (int)(origin.Z / LandMesh3D.TILE);
                if (tx < 0 || ty < 0) return true;
                var chunk = map.GetChunk(tx, ty, false);
                if (chunk == null) return true;
                return chunk.IsTileExposedToSky(tx & 7, ty & 7);
            }
            catch { return true; }
        }

        private static void SpawnRain(bool streaky)
        {
            Vector3 origin = _anchor + RandHorizontal(Radius) + new Vector3(0f, Height, 0f);
            if (OcclusionCheckEnabled && !IsExposedAt(origin)) return;
            float fall = streaky ? -1100f : -800f;
            byte flags = (byte)(Particle3DSystem.F_TEXTURED_ADD | Particle3DSystem.F_STREAK);
            float life = streaky ? 0.55f : 0.7f;
            // Larger size + F_STREAK aspect = visible vertical streak. Bright
            // cool-white start fades to transparent so additive blending
            // composites cleanly against any sky/world colour.
            Particle3DSystem.Spawn(
                origin,
                new Vector3(WindX * 0.4f, fall, WindZ * 0.4f),
                new Vector3(WindX, -200f, WindZ),
                life,
                streaky ? 14f : 10f, streaky ? 10f : 7f,
                new Color(255, 255, 255),
                new Color(180, 210, 240, (byte)0),
                flags);
        }

        private static void SpawnBloodRain()
        {
            Vector3 origin = _anchor + RandHorizontal(Radius) + new Vector3(0f, Height, 0f);
            if (OcclusionCheckEnabled && !IsExposedAt(origin)) return;
            byte flags = (byte)(Particle3DSystem.F_TEXTURED_ADD | Particle3DSystem.F_STREAK);
            // Saturated red — the rain texture has a slight cool tint, so push
            // the vertex colour bright enough that the textured-additive
            // composite still reads clearly red against the dark sky.
            Particle3DSystem.Spawn(
                origin,
                new Vector3(WindX * 0.3f, -700f, WindZ * 0.3f),
                new Vector3(WindX * 0.6f, -200f, WindZ * 0.6f),
                0.75f,
                12f, 8f,
                new Color(230,  30,  30),
                new Color(120,   0,   0, (byte)0),
                flags);
        }

        private static void SpawnSnow(bool blizzard)
        {
            Vector3 origin = _anchor + RandHorizontal(Radius) + new Vector3(0f, Height, 0f);
            if (OcclusionCheckEnabled && !IsExposedAt(origin)) return;
            // In blizzard, snowflakes are bigger, fall a bit faster, and are blown
            // around more aggressively by the wind (which is itself gusting).
            float swayMag = blizzard ? 80f : 30f;
            float swayX = ((float)_rng.NextDouble() * 2f - 1f) * swayMag;
            float swayZ = ((float)_rng.NextDouble() * 2f - 1f) * swayMag;
            float fall   = blizzard ? -180f : -90f;
            float windScale = blizzard ? 1.2f : 0.5f;
            float sizeStart = blizzard ? 22f : 14f;
            float sizeEnd   = blizzard ? 16f : 10f;
            float life     = blizzard ? 4.5f : 6.5f;
            Particle3DSystem.Spawn(
                origin,
                new Vector3(swayX + WindX * windScale, fall, swayZ + WindZ * windScale),
                new Vector3(WindX * 0.6f, -10f, WindZ * 0.6f),
                life,
                sizeStart, sizeEnd,
                new Color(255, 255, 255),
                new Color(235, 245, 255, (byte)0),
                (byte)(Particle3DSystem.F_TEXTURED | Particle3DSystem.F_SPIN));
        }

        private static void SpawnSandstorm()
        {
            // Spawn in a cylinder AROUND the player (same shape rain/snow use)
            // with a small upwind bias so the dust reads as blowing through the
            // viewer rather than parked on the horizon. Previously the spawn
            // sat ~0.9*Radius (≈20 tiles) upwind and up to ~0.8*Height (≈19
            // tiles) high — it landed offscreen for any normal camera.
            Vector3 wind = new Vector3(WindX, 0f, WindZ);
            float windMag = wind.Length();
            Vector3 windDir = windMag > 0.01f ? -wind / windMag : new Vector3(-1, 0, 0);

            // Sandstorm now spans floor-to-cloud so it reads as a tall wall
            // of blowing sand rather than a knee-high band. (User feedback:
            // previous 0–80u cap was "very short". 0..Height*1.1 covers the
            // whole visible vertical, with extra reach above so high-altitude
            // particles drift down through the camera.)
            float yBand = Height * 1.1f;

            Vector3 origin = _anchor
                             + RandHorizontal(Radius)
                             + windDir * Radius * 0.25f
                             + new Vector3(0f, (float)_rng.NextDouble() * yBand + 10f, 0f);

            // F_TEXTURED_ADD — additive blend kills the texture's dark
            // background automatically (was showing as solid black quads
            // around each dust streak under NonPremultiplied alpha blend).
            Particle3DSystem.Spawn(
                origin,
                new Vector3(WindX * 1.2f, -10f, WindZ * 1.2f),
                new Vector3(WindX * 0.4f, -20f, WindZ * 0.4f),
                2.5f,
                14f, 4f,
                new Color(220, 180, 110),
                new Color(180, 140,  80, (byte)0),
                (byte)(Particle3DSystem.F_TEXTURED_ADD | Particle3DSystem.F_TRAIL));
        }

        private static void SpawnEmbers()
        {
            Vector3 origin = _anchor + RandHorizontal(Radius * 0.6f);
            origin.Y += (float)_rng.NextDouble() * 10f;
            // F_TEXTURED_ADD — bright disc on dark background needs additive
            // blend (alpha pass would draw black squares). Trail still emits
            // its fading sub-particle for the streak underneath.
            Particle3DSystem.Spawn(
                origin,
                new Vector3(WindX * 0.2f, 60f + (float)_rng.NextDouble() * 40f, WindZ * 0.2f),
                new Vector3(WindX * 0.3f, 10f, WindZ * 0.3f),
                3.5f,
                7f, 1f,
                new Color(255, 180,  60),
                new Color(120,  40,  10, (byte)0),
                (byte)(Particle3DSystem.F_TEXTURED_ADD | Particle3DSystem.F_TRAIL));
        }

        private static void SpawnFog(bool red = false)
        {
            // Spawn at an absolute world position (cylinder around the player
            // anchor). _pos is then advanced only by _vel * dt — the particle
            // stays anchored in world space and does NOT track the camera.
            // F_TEXTURED so the bound "fog"/"fog_alt" PNG paints each puff —
            // without it the puff is an untextured additive smear.
            Vector3 origin = _anchor + RandHorizontal(Radius);
            origin.Y += (float)_rng.NextDouble() * 60f;
            Color cStart = red ? new Color(160,  20,  20, (byte)80)
                               : new Color(200, 200, 200, (byte)70);
            Color cEnd   = red ? new Color( 80,  10,  10, (byte)0)
                               : new Color(200, 200, 200, (byte)0);
            Particle3DSystem.Spawn(
                origin,
                new Vector3(WindX * 0.5f, 0f, WindZ * 0.5f),
                Vector3.Zero,
                10f,
                120f, 200f,
                cStart, cEnd,
                Particle3DSystem.F_TEXTURED);
        }

        // ===== Helpers =====

        private static Vector3 RandHorizontal(float radius)
        {
            float r = radius * (float)Math.Sqrt(_rng.NextDouble());
            float a = (float)_rng.NextDouble() * MathHelper.TwoPi;
            return new Vector3((float)Math.Cos(a) * r, 0f, (float)Math.Sin(a) * r);
        }

        private static void ResetLightningTimer()
        {
            float min, max;
            if (Type == Weather3DType.Tornado)
            {
                // Tornado: much more frequent. Default 0.6..2.0 seconds —
                // a near-constant flicker around the funnel.
                min = 0.6f;
                max = 2.0f;
            }
            else
            {
                min = MathHelper.Min(LightningEveryMin, LightningEveryMax);
                max = MathHelper.Max(LightningEveryMin, LightningEveryMax);
            }
            _lightningTimer = min + (float)_rng.NextDouble() * (max - min);
        }
    }
}
