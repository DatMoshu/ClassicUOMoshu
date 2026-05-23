// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Authoritative source of global wind state. Replaces the legacy
    /// <c>WindManager</c> static class. Consumers must read wind through this service
    /// (via <c>Renderer3DServices.Wind</c>) or by subscribing to <see cref="WindUpdatedEvent"/>
    /// on the event bus — never by re-reading clocks or maintaining their own wind state.
    /// </summary>
    public interface IWindService
    {
        // ===== Read state =====

        /// <summary>Current wind intensity in [0,1].</summary>
        float Strength { get; }

        /// <summary>Direction wind blows TOWARD, in degrees (0..360).</summary>
        float DirectionDeg { get; }

        /// <summary>Base oscillation frequency in Hz.</summary>
        float Frequency { get; }

        /// <summary>Most recent sine sample in [-1,1].</summary>
        float Sample { get; }

        /// <summary>Current sine phase in radians.</summary>
        float Phase { get; }

        /// <summary>
        /// Strength-scaled horizontal wind vector in world-XZ. Equivalent to
        /// <c>(cos(rad), sin(rad)) * Strength</c>.
        /// </summary>
        Vector2 VectorXZ { get; }

        /// <summary>Active gust mode.</summary>
        WindGustMode GustMode { get; }

        /// <summary>
        /// When true, the wind service drives weather-particle horizontal advection
        /// (rain/snow drift) via the weather service. UI-mutable; initial value comes
        /// from <see cref="WindServiceConfig.LinkToWeather"/>.
        /// </summary>
        bool LinkToWeather { get; }

        /// <summary>
        /// World units/sec² peak horizontal advection at <see cref="Strength"/> = 1.
        /// UI-mutable; initial value comes from <see cref="WindServiceConfig.WeatherParticleAdvection"/>.
        /// </summary>
        float WeatherWindStrength { get; }

        // ===== Gust tunables (runtime-mutable; seeded from WindServiceConfig) =====

        /// <summary>Lower bound of gust auto-target cadence (seconds between target picks).</summary>
        float GustChangeMin { get; }
        /// <summary>Upper bound of gust auto-target cadence.</summary>
        float GustChangeMax { get; }
        /// <summary>Lower bound of auto-picked target strength.</summary>
        float GustStrengthMin { get; }
        /// <summary>Upper bound of auto-picked target strength.</summary>
        float GustStrengthMax { get; }
        /// <summary>Maximum direction wander per gust pick, in degrees.</summary>
        float GustDirectionRangeDeg { get; }
        /// <summary>Easing rate toward gust auto-targets. Larger = snappier.</summary>
        float GustLerpSpeed { get; }

        /// <summary>
        /// Sample the sine wave at the current phase plus an arbitrary radian offset.
        /// Lets consumers (e.g., per-leaf-plane sway) decorrelate sub-elements without
        /// maintaining their own clock.
        /// </summary>
        float SampleAt(float phaseOffsetRad);

        // ===== Mutate state (UI control surface) =====

        /// <summary>Set the user-controlled wind strength target. Clamped to [0,1].</summary>
        void SetStrength(float strength);

        /// <summary>Set the user-controlled direction. Wraps to [0, 360).</summary>
        void SetDirectionDeg(float directionDeg);

        /// <summary>Set the base oscillation frequency.</summary>
        void SetFrequency(float frequencyHz);

        /// <summary>Switch gust mode. Resets gust auto-targets when transitioning.</summary>
        void SetGustMode(WindGustMode mode);

        /// <summary>Toggle whether wind drives weather-particle drift.</summary>
        void SetLinkToWeather(bool linkToWeather);

        /// <summary>Set the weather-particle advection peak. Clamped to non-negative.</summary>
        void SetWeatherWindStrength(float advectionPerSecondSquared);

        /// <summary>Set the lower bound of gust auto-target cadence (seconds). Clamped to non-negative.</summary>
        void SetGustChangeMin(float seconds);
        /// <summary>Set the upper bound of gust auto-target cadence (seconds). Clamped to non-negative.</summary>
        void SetGustChangeMax(float seconds);
        /// <summary>Set the lower bound of auto-picked target strength. Clamped to [0,1].</summary>
        void SetGustStrengthMin(float strength);
        /// <summary>Set the upper bound of auto-picked target strength. Clamped to [0,1].</summary>
        void SetGustStrengthMax(float strength);
        /// <summary>Set the maximum direction wander per gust pick, in degrees. Clamped to non-negative.</summary>
        void SetGustDirectionRangeDeg(float degrees);
        /// <summary>Set the easing rate toward gust auto-targets. Clamped to non-negative.</summary>
        void SetGustLerpSpeed(float speed);
    }
}
