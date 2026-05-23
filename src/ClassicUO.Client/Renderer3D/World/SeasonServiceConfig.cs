// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Tunable parameters for <see cref="SeasonService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>SeasonCycleDriver</c>. Values not exposed to UI sliders
    /// (constants like phase boundaries) live here as <c>init</c> properties so a config
    /// file can override them, but most operators will leave them at <see cref="Default"/>.
    /// </summary>
    public sealed class SeasonServiceConfig
    {
        // ===== Initial runtime state =====

        /// <summary>Initial enabled state. Cycle does not advance until the user enables it.</summary>
        public bool InitialEnabled { get; init; } = false;

        /// <summary>Seconds per full natural-cycle year.</summary>
        public float SecondsPerYear { get; init; } = 60f;

        /// <summary>Seconds per year when in showcase mode (year 2 layered weather).</summary>
        public float ShowcaseSecondsPerYear { get; init; } = 180f;

        // ===== Wind ramp =====

        /// <summary>e-folding time of the wind-strength exponential ease, seconds.</summary>
        public float WindEaseSeconds { get; init; } = 2.5f;

        /// <summary>e-folding time of the canopy-sway-amplitude ease, seconds.</summary>
        public float SwayAmpEaseSeconds { get; init; } = 1.8f;

        /// <summary>Peak canopy sway amplitude in degrees during a heavy storm.</summary>
        public float SwayAmpStormDeg { get; init; } = 22f;

        // ===== Lightning flash =====

        /// <summary>Half-life of the lightning screen-flash decay, seconds.</summary>
        public float LightningFlashHalfLifeSeconds { get; init; } = 0.15f;

        /// <summary>Floor below which the flash is snapped to zero.</summary>
        public float LightningFlashEpsilon { get; init; } = 0.005f;

        // ===== Behaviour toggles (initial values; user can override at runtime) =====

        public bool DriveWeatherParticles { get; init; } = true;
        public bool RegrowInSpring { get; init; } = true;
        public bool DriveSwayFromWeather { get; init; } = true;
        public bool DriveFoliageShaderTint { get; init; } = false;

        public static SeasonServiceConfig Default => new();
    }
}
