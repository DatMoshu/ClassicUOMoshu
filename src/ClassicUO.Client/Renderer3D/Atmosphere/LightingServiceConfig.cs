// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Tunable parameters for <see cref="LightingService"/>. Loaded from
    /// <c>Data/renderer3d/lighting-defaults.json</c> at composition time. Values mirror the
    /// prior hardcoded constants in the legacy <c>LightingState</c> static class.
    /// </summary>
    public sealed class LightingServiceConfig
    {
        /// <summary>
        /// Direction the unlit fallback shader was initialised with before the lighting
        /// system existed. Returned by <see cref="ILightingService.CurrentLightDir"/> when
        /// the service is disabled, so toggling off restores the exact pre-feature look.
        /// </summary>
        public Vector3 LegacyHardcodedDir { get; init; } = new Vector3(0f, 1f, 1f);

        /// <summary>Peak sun elevation in radians. Formula F2 from realtime-lighting-3d.md §F2 (~70°).</summary>
        public float MaxElevation { get; init; } = 1.22f;

        /// <summary>Initial enabled state if no profile override is present.</summary>
        public bool InitialEnabled { get; init; } = false;

        /// <summary>Initial auto-cycle state if no profile override is present.</summary>
        public bool InitialAutoCycle { get; init; } = true;

        /// <summary>Initial time-of-day if no profile override is present (hours, [0,24)).</summary>
        public float InitialTimeOfDay { get; init; } = 12.0f;

        /// <summary>
        /// Initial seconds per full 24h cycle if no profile override is present.
        /// 120s = prototype default for visual demo; production target is 7200s (2 hours).
        /// </summary>
        public float InitialCyclePeriodSeconds { get; init; } = 120.0f;

        /// <summary>Default config used when no JSON file is present.</summary>
        public static LightingServiceConfig Default => new();
    }
}
