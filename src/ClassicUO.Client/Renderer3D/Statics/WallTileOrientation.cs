// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — domain enum mirror of the legacy TileOrientation (ADR-012).
// Numeric values match the legacy enum bit-for-bit; locked by an xUnit parity
// theory test so accidental reordering surfaces immediately.

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Per-tile orientation classification used by the multi-renderer and the wall
    /// neighbor classifier.
    /// </summary>
    public enum WallTileOrientation
    {
        Unknown = 0,
        Floor = 1,
        WallSouth = 2,
        WallEast = 3,
        WallNorth = 4,
        WallWest = 5,
        /// <summary>Ambiguous corner (T-junction, cross, isolated). Fallback when neighbour analysis can't pin down a subtype.</summary>
        Corner = 6,
        CornerNW = 7,
        CornerNE = 8,
        CornerSW = 9,
        CornerSE = 10,
        Roof = 11,
    }
}
