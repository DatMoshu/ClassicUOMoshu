// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IWallNeighborClassifier service (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Per-instance corner-orientation refiner. <see cref="WallTileOrientation.Corner"/>
    /// is graphic-ambiguous — the same corner piece is the NW/NE/SW/SE corner of
    /// different buildings depending on placement. This service inspects the four
    /// cardinal neighbours of a corner tile and narrows the orientation.
    /// </summary>
    public interface IWallNeighborClassifier
    {
        /// <summary>Master enable switch. Mutable at runtime via the debug gump.</summary>
        bool Enabled { get; set; }

        /// <summary>Z band, in UO units, around the corner tile. Mutable at runtime.</summary>
        int ZTolerance { get; set; }

        /// <summary>
        /// If <paramref name="baseKind"/> is <see cref="WallTileOrientation.Corner"/>, refine to
        /// CornerNW/NE/SW/SE based on cardinal neighbour walls. Otherwise returns
        /// <paramref name="baseKind"/> unchanged.
        /// </summary>
        WallTileOrientation Refine(int x, int y, sbyte z, WallTileOrientation baseKind);
    }
}
