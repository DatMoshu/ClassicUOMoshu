// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Tunable parameters for <see cref="WindService"/>. Loaded from
    /// <c>Data/renderer3d/wind-defaults.json</c> at composition time. All values data-driven
    /// per ADR-012 §3 — no hardcoded constants in the service implementation.
    /// </summary>
    public sealed class WindServiceConfig
    {
        /// <summary>Initial wind intensity in [0,1].</summary>
        public float InitialStrength { get; init; } = 0.4f;

        /// <summary>Initial direction wind blows TOWARD, in degrees.</summary>
        public float InitialDirectionDeg { get; init; } = 90f;

        /// <summary>Hz of the base sine pulse for breathing oscillation.</summary>
        public float BaseFrequencyHz { get; init; } = 0.35f;

        /// <summary>
        /// World units/sec² peak horizontal advection applied to weather particles when
        /// strength = 1. Consumed by the weather service to drift rain/snow with the wind.
        /// </summary>
        public float WeatherParticleAdvection { get; init; } = 120f;

        /// <summary>If true, the service publishes wind to the weather service via the event bus on each tick.</summary>
        public bool LinkToWeather { get; init; } = true;

        /// <summary>How often the auto-target picker fires (seconds, lower bound).</summary>
        public float GustChangeMin { get; init; } = 5f;

        /// <summary>How often the auto-target picker fires (seconds, upper bound).</summary>
        public float GustChangeMax { get; init; } = 9f;

        /// <summary>Lower bound of auto-picked target strength.</summary>
        public float GustStrengthMin { get; init; } = 0.3f;

        /// <summary>Upper bound of auto-picked target strength.</summary>
        public float GustStrengthMax { get; init; } = 0.9f;

        /// <summary>Maximum direction wander per gust pick, in degrees.</summary>
        public float GustDirectionRangeDeg { get; init; } = 60f;

        /// <summary>How fast strength and direction ease toward their auto-targets. Larger = snappier.</summary>
        public float GustLerpSpeed { get; init; } = 1.5f;

        /// <summary>Storm-mode strength floor (overrides <see cref="GustStrengthMin"/> upward).</summary>
        public float StormStrengthFloor { get; init; } = 0.55f;

        /// <summary>Storm-mode strength ceiling (overrides <see cref="GustStrengthMax"/> upward).</summary>
        public float StormStrengthCeiling { get; init; } = 1.0f;

        /// <summary>Storm-mode cadence multiplier on <see cref="GustChangeMin"/>/<see cref="GustChangeMax"/>.</summary>
        public float StormCadenceMultiplier { get; init; } = 0.5f;

        /// <summary>Storm-mode minimum cadence floor (seconds) to prevent excessively rapid gusts.</summary>
        public float StormCadenceFloorSeconds { get; init; } = 0.6f;

        /// <summary>RNG seed for deterministic gust patterns. Useful for replay/test reproducibility.</summary>
        public int RandomSeed { get; init; } = 0xB17E5;

        /// <summary>Default config used when no JSON file is present. Mirrors prior hardcoded values.</summary>
        public static WindServiceConfig Default => new();
    }
}
