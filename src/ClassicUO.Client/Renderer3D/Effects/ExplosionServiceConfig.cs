// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="ExplosionService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>ExplosionForceSystem</c>.
    /// </summary>
    public sealed class ExplosionServiceConfig
    {
        public bool InitialEnabled { get; init; } = true;

        /// <summary>Hard cap on simultaneously-active blast events.</summary>
        public int MaxEvents { get; init; } = 16;

        // ===== Bend envelope =====
        // Bend ramps in fast (slam) and decays slowly. Total bend window:
        //   0..BendAttackSeconds                      ramp up to peak
        //   BendAttackSeconds..BendDurationSeconds    exponential decay back to 0

        public float BendAttackSeconds { get; init; } = 0.18f;
        public float BendDurationSeconds { get; init; } = 1.6f;

        /// <summary>Exponential decay rate (1/seconds) applied after the attack peak.</summary>
        public float BendDecayRate { get; init; } = 2.4f;

        /// <summary>Trees inside the radius lose their LeafOverlay for this many seconds after the blast.</summary>
        public float LeavesHiddenSeconds { get; init; } = 10f;

        /// <summary>World-units bend at canopy top per unit-strength at zero range.</summary>
        public float BendStrengthPx { get; init; } = 28f;

        /// <summary>
        /// Below this fraction of the radius, treat the point as full-strength so trees right
        /// at the epicenter don't see numerical glitches.
        /// </summary>
        public float CoreFalloffFraction { get; init; } = 0.15f;

        /// <summary>Floor on event radius (world units).</summary>
        public float MinEventRadius { get; init; } = 1f;

        /// <summary>Floor on event strength multiplier.</summary>
        public float MinEventStrength { get; init; } = 0.01f;

        /// <summary>Distance below which the unit-vector from epicenter is treated as zero.</summary>
        public float ZeroDirectionDistance { get; init; } = 0.5f;

        public static ExplosionServiceConfig Default => new();
    }
}
