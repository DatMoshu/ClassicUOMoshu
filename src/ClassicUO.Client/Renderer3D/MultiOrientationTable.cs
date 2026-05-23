// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
// Delegates to IMultiOrientationService via Renderer3DHost. The legacy enum
// TileOrientation is bridged to the domain WallTileOrientation by integer cast;
// numeric parity is locked by the WallTileOrientation parity test in
// WallNeighborClassifierServiceTests.

using System;
using ClassicUO.Assets;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy parallel enum. Numeric layout matches <see cref="WallTileOrientation"/>
    /// exactly (parity-locked by xUnit theory). Existing call sites continue to use
    /// this name; the facade casts at the boundary.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Statics.WallTileOrientation. Will be removed in ADR-012 Phase 3.")]
    internal enum TileOrientation
    {
        Unknown = 0,
        Floor = 1,
        WallSouth = 2,
        WallEast = 3,
        WallNorth = 4,
        WallWest = 5,
        Corner = 6,
        CornerNW = 7,
        CornerNE = 8,
        CornerSW = 9,
        CornerSE = 10,
        Roof = 11,
    }

    [Obsolete("Use IMultiOrientationService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class MultiOrientationTable
    {
        private static IMultiOrientationService Svc => Renderer3DHost.Services.MultiOrientation;

        // ===== Legacy diagnostic surface (delegated) =====

        public static string LastError => Svc.LastError;
        public static int LoadedEntryCount => Svc.LoadedEntryCount;
        public static int LoadedEntryCountHand => Svc.LoadedEntryCountHand;
        public static int LoadedEntryCountAuto => Svc.LoadedEntryCountAuto;

        // Path knobs retained for source-compat with the debug gump. Reading them
        // returns the production defaults; writing has no effect (storage paths are
        // baked into FileMultiOrientationStorage at registration time).
        public static string DataPath
            => FileMultiOrientationStorage.DefaultHandPath;
        public static string AutoDataPath
            => FileMultiOrientationStorage.DefaultAutoPath;

        // ===== Lookup (delegated) =====

        public static bool TryGet(ushort graphic, out TileOrientation kind)
        {
            if (Svc.TryGet(graphic, out WallTileOrientation domain))
            {
                kind = (TileOrientation)(int)domain;
                return true;
            }
            kind = TileOrientation.Unknown;
            return false;
        }

        public static TileOrientation Resolve(ushort graphic, ref StaticTiles itemData)
        {
            WallTileOrientation domain = Svc.Resolve(
                graphic,
                isRoof: itemData.IsRoof,
                isSurface: itemData.IsSurface,
                isWall: itemData.IsWall);
            return (TileOrientation)(int)domain;
        }

        public static void Reload() => Svc.Reload();
    }
}
