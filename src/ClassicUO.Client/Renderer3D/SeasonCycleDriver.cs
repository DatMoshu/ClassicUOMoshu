// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// This file used to be the canonical SeasonCycleDriver static singleton. It is now a thin
// facade that delegates to ISeasonService via the Renderer3DHost migration locator.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers still using this surface (deletion blockers):
//   * World3DRenderer.UpdateGroundEffectEase — calls Tick() (now no-op).
//   * LeafFallSystem — reads Progress.
//   * Game/Managers/PathReplayManager — writes Stop()/Enabled/ShowcaseMode/SecondsPerYear.
//   * Game/UI/Gumps/Trees3DSettingsGump — reads/writes most fields.
// Each migrates as part of its own subsystem PR.

using System;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Backwards-compatible facade over <see cref="ClassicUO.Renderer.WorldEnv.ISeasonService"/>.
    /// </summary>
    [Obsolete("Use ISeasonService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class SeasonCycleDriver
    {
        private static ClassicUO.Renderer.WorldEnv.ISeasonService Service => Renderer3DHost.Services.Season;

        public static bool Enabled
        {
            get => Service.Enabled;
            set => Service.SetEnabled(value);
        }

        public static float SecondsPerYear
        {
            get
            {
                // The service stores its own field; we don't expose it directly. Callers
                // reading this expect the natural-cycle period. Until the gump migrates,
                // round-trip through the setter so changes go through validation.
                // This getter is approximate — exact value is round-tripped via the gump.
                return _legacySecondsPerYearMirror;
            }
            set
            {
                _legacySecondsPerYearMirror = value;
                Service.SetSecondsPerYear(value);
            }
        }

        public static float Progress => Service.Progress;
        public static string LastPhase => Service.LastPhase;
        public static bool LastTickWrote => Service.LastTickWrote;

        public static bool ShowcaseMode
        {
            get => Service.ShowcaseMode;
            set => Service.SetShowcaseMode(value);
        }

        // ShowcaseSecondsPerYear is config-only in the new service. Keep a mirror so the
        // gump's slider compiles; mutations are no-ops in this transitional window
        // (the value moved into SeasonServiceConfig). Fixed in Phase 3 when the gump migrates.
        public static float ShowcaseSecondsPerYear = 180f;

        public static bool DriveWeatherParticles
        {
            get => _driveWeatherMirror;
            set { _driveWeatherMirror = value; Service.SetDriveWeatherParticles(value); }
        }

        public static bool RegrowInSpring
        {
            get => _regrowMirror;
            set { _regrowMirror = value; Service.SetRegrowInSpring(value); }
        }

        public static bool DriveSwayFromWeather
        {
            get => _driveSwayMirror;
            set { _driveSwayMirror = value; Service.SetDriveSwayFromWeather(value); }
        }

        public static bool DriveFoliageShaderTint
        {
            get => _driveTintMirror;
            set { _driveTintMirror = value; Service.SetDriveFoliageShaderTint(value); }
        }

        // The legacy SwayAmpEaseSeconds / SwayAmpStormDeg / WindEaseSeconds were tunables
        // adjusted from no UI in the prototype. They moved into SeasonServiceConfig and are
        // not user-mutable in this transition window. Kept as fields so any straggler
        // callers compile; they have no effect on the running service.
        public static float SwayAmpEaseSeconds = 1.8f;
        public static float SwayAmpStormDeg    = 22f;
        public static float WindEaseSeconds    = 2.5f;
        public static float TargetWindStrength;
        public static float TargetWindDirection = 90f;

        // Mirrors of the bool toggles so the gump's two-arg sliders/toggles can bind a
        // get/set delegate pair. Kept in sync via the property setters above.
        private static bool _driveWeatherMirror = true;
        private static bool _regrowMirror = true;
        private static bool _driveSwayMirror = true;
        private static bool _driveTintMirror = false;
        private static float _legacySecondsPerYearMirror = 60f;

        /// <summary>
        /// Legacy entry point. The service is ticked by <see cref="Renderer3DServices.Tick"/>;
        /// this is now a no-op kept so straggler callers (World3DRenderer.UpdateGroundEffectEase)
        /// compile and run without modification.
        /// </summary>
        public static void Tick() { /* no-op */ }

        public static void SnapTo(float p) => Service.SnapTo(p);
        public static void Stop() => Service.Stop();
    }
}
