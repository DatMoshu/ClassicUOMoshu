// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IMousePickService configuration (ADR-012).

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Configuration for <see cref="IMousePickService"/>. Decouples the picker from
    /// concrete world-scale constants in <c>LandMesh3D</c> so the math can be tested
    /// in isolation and rebalanced from data without touching the picker source.
    /// </summary>
    public readonly struct MousePickServiceConfig
    {
        /// <summary>World-units per UO tile on the X/Z plane. Mirrors <c>LandMesh3D.TILE</c>.</summary>
        public readonly float TileSize;

        /// <summary>World-units per UO Z-step on the Y axis. Mirrors <c>LandMesh3D.Z_SCALE</c>.</summary>
        public readonly float ZScale;

        /// <summary>Minimum |dir.Y| treated as non-parallel-to-plane. Below this the pick fails.</summary>
        public readonly float ParallelEpsilon;

        public MousePickServiceConfig(float tileSize, float zScale, float parallelEpsilon)
        {
            TileSize = tileSize;
            ZScale = zScale;
            ParallelEpsilon = parallelEpsilon;
        }

        /// <summary>Defaults that match the legacy <c>MousePicker3D</c> behavior bit-for-bit.</summary>
        public static MousePickServiceConfig Default => new(
            tileSize: 22f,
            zScale: 4f,
            parallelEpsilon: 1e-5f);
    }
}
