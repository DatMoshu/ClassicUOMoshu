// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IWallNeighborClassifier implementation (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Pure-logic wall-neighbor classifier. Allocation-free in steady state — at most
    /// four <see cref="IWallNeighborSource.HasWallAt"/> calls per refinement.
    /// </summary>
    public sealed class WallNeighborClassifierService : IWallNeighborClassifier
    {
        private readonly IWallNeighborSource _source;

        public bool Enabled { get; set; }
        public int ZTolerance { get; set; }

        public WallNeighborClassifierService(WallNeighborClassifierConfig config, IWallNeighborSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Enabled = config.Enabled;
            ZTolerance = config.ZTolerance;
        }

        public WallTileOrientation Refine(int x, int y, sbyte z, WallTileOrientation baseKind)
        {
            if (!Enabled) return baseKind;
            // Only ambiguous corners need neighbour-derived refinement.
            if (baseKind != WallTileOrientation.Corner) return baseKind;

            int zTol = ZTolerance;
            bool n = _source.HasWallAt(x,     y - 1, z, zTol);
            bool s = _source.HasWallAt(x,     y + 1, z, zTol);
            bool e = _source.HasWallAt(x + 1, y,     z, zTol);
            bool w = _source.HasWallAt(x - 1, y,     z, zTol);

            // Convention (validated empirically against the Sweet Dreams Inn 226-tile sample):
            // the corner sits AT the named compass position of its building, with the wall
            // continuations running back along the OTHER two cardinals.
            //   W + N only → CornerNW   (walls run west and north from here)
            //   E + N only → CornerNE
            //   W + S only → CornerSW
            //   E + S only → CornerSE
            // Any other count (0, 1, 3, 4) → ambiguous → keep plain Corner.
            int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);
            if (count != 2) return WallTileOrientation.Corner;

            if (w && n) return WallTileOrientation.CornerNW;
            if (e && n) return WallTileOrientation.CornerNE;
            if (w && s) return WallTileOrientation.CornerSW;
            if (e && s) return WallTileOrientation.CornerSE;
            // Two opposites (N+S or E+W) = wall pass-through, not a corner.
            return WallTileOrientation.Corner;
        }
    }
}
