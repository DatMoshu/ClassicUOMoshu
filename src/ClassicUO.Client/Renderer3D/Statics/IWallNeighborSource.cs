// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IWallNeighborClassifier gateway (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway into the world-state map. Returns whether any wall-flagged static or
    /// multi sits at the supplied tile within the Z tolerance band. Implementations
    /// are responsible for chunk lookup, head-list traversal, and the IsWall predicate.
    /// </summary>
    public interface IWallNeighborSource
    {
        /// <summary>
        /// True if any wall-flagged static/multi exists at <c>(x,y)</c> with
        /// |objZ - z| ≤ <paramref name="zTolerance"/>.
        /// </summary>
        bool HasWallAt(int x, int y, sbyte z, int zTolerance);
    }
}
