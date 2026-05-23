// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Generic blast-event queue. Producers (NukeShow, future bomb/spell systems) call
    /// <see cref="Add"/> once per detonation. Consumers (tree static draw, future debris/
    /// lamp-flicker systems) call <see cref="Query"/> each frame to get aggregated bend +
    /// leaves-hidden state at a world-space point.
    /// </summary>
    /// <remarks>
    /// World coordinates are tile-units * <c>LandMesh3D.TILE</c> ≈ pixels; producers and
    /// consumers must agree on the convention. The legacy facade exposes a tile-coord
    /// convenience overload that does the multiplication.
    /// </remarks>
    public interface IExplosionService
    {
        bool Enabled { get; }

        /// <summary>Number of currently-active blast events (≤ pool capacity).</summary>
        int LiveEvents { get; }

        void SetEnabled(bool enabled);

        /// <summary>
        /// Register an explosion at a world-space point. <paramref name="radius"/> and
        /// <paramref name="strength"/> are clamped to non-zero floors. When the pool is
        /// full, the oldest event is recycled.
        /// </summary>
        void Add(float centerX, float centerZ, float radius, float strength);

        /// <summary>
        /// Aggregate the influence of all active explosions at a world-space point.
        /// Returns true when at least one event affects this point.
        /// <paramref name="bendX"/>/<paramref name="bendZ"/> are world-unit offsets to apply
        /// to the TOP of a tree billboard (bottom stays planted). <paramref name="leavesHidden"/>
        /// is true when the canopy overlay should be culled.
        /// </summary>
        bool Query(float wx, float wz, out float bendX, out float bendZ, out bool leavesHidden);

        /// <summary>Extinguish all blast events. Used by debug gumps.</summary>
        void Clear();
    }
}
