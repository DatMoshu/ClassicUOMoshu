// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Published by <see cref="IWindService"/> on every frame the wind state changes.
    /// Replaces hardcoded cross-system reads (e.g., <c>FireSpreadSystem</c> directly accessing
    /// <c>WindManager.VectorXZ</c>).
    /// </summary>
    public readonly struct WindUpdatedEvent
    {
        /// <summary>Horizontal wind vector, scaled by current strength. World units.</summary>
        public readonly Vector2 VectorXZ;

        /// <summary>Wind intensity in [0,1].</summary>
        public readonly float Strength;

        /// <summary>Direction wind blows TOWARD, in degrees (0..360).</summary>
        public readonly float DirectionDeg;

        /// <summary>Current sine phase sample in [-1,1] for breathing/sway oscillation.</summary>
        public readonly float Sample;

        public WindUpdatedEvent(Vector2 vectorXZ, float strength, float directionDeg, float sample)
        {
            VectorXZ = vectorXZ;
            Strength = strength;
            DirectionDeg = directionDeg;
            Sample = sample;
        }
    }
}
