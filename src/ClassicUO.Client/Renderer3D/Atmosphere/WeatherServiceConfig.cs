// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Tunable parameters for <see cref="WeatherService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>Weather3DSystem</c> public state.
    /// </summary>
    /// <remarks>
    /// This config covers the externally-visible state surface only. Simulation tunables
    /// (lightning strike distances, tornado physics, particle spawn rates) remain in the
    /// legacy class until a future migration pulls the simulation into the service.
    /// </remarks>
    public sealed class WeatherServiceConfig
    {
        public WeatherKind InitialType { get; init; } = WeatherKind.None;
        public float InitialIntensity { get; init; } = 0.5f;

        /// <summary>Cylinder radius around the player (world units) in which particles spawn.</summary>
        public float Radius { get; init; } = 480f;

        /// <summary>Spawn height above the player (world units).</summary>
        public float Height { get; init; } = 520f;

        public bool InitialAutoLightning { get; init; } = false;
        public bool InitialOcclusionCheckEnabled { get; init; } = true;
        public bool InitialApplyProfileOnSetType { get; init; } = true;

        /// <summary>Minimum seconds between auto-lightning strikes (storm mode). Mirrors legacy default.</summary>
        public float LightningEveryMin { get; init; } = 4f;

        /// <summary>Maximum seconds between auto-lightning strikes (storm mode). Mirrors legacy default.</summary>
        public float LightningEveryMax { get; init; } = 9f;

        public static WeatherServiceConfig Default => new();
    }
}
