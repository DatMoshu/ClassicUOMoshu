// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IMultiOrientationService (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Per-graphic→<see cref="WallTileOrientation"/> registry. Backed by a hand-authored
    /// table plus an auto-derived sweep, merged at load. Includes a TileData-flag
    /// fallback path so unknown graphics still classify as Roof/Floor when the flags
    /// allow it.
    /// </summary>
    public interface IMultiOrientationService
    {
        // ===== Diagnostics =====
        string LastError { get; }
        int LoadedEntryCount { get; }
        int LoadedEntryCountHand { get; }
        int LoadedEntryCountAuto { get; }

        // ===== Lifecycle =====
        /// <summary>Load the table on first call. Subsequent calls are no-ops.</summary>
        void EnsureLoaded();
        /// <summary>Force a reload from storage (debug gump).</summary>
        void Reload();

        // ===== Lookup =====
        /// <summary>JSON-only lookup. Returns false if the graphic has no JSON entry.</summary>
        bool TryGet(ushort graphic, out WallTileOrientation kind);

        /// <summary>
        /// Combined JSON + TileData-flag fallback lookup. Returns
        /// <see cref="WallTileOrientation.Unknown"/> only when no source can place the tile.
        /// Flags are passed primitively so the service stays free of game-runtime types.
        /// </summary>
        WallTileOrientation Resolve(ushort graphic, bool isRoof, bool isSurface, bool isWall);
    }
}
