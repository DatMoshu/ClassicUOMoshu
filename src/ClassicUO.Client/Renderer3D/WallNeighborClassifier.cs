// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
// Delegates to IWallNeighborClassifier via Renderer3DHost. The legacy enum
// TileOrientation is bridged to the domain WallTileOrientation by integer cast;
// numeric parity is locked by an xUnit theory test.

using System;
using ClassicUO.Game;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using World = ClassicUO.Game.World;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IWallNeighborClassifier via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class WallNeighborClassifier
    {
        private static IWallNeighborClassifier Svc => Renderer3DHost.Services.WallNeighbor;

        public static int ZTolerance
        {
            get => Svc.ZTolerance;
            set => Svc.ZTolerance = value;
        }

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.Enabled = value;
        }

        public static TileOrientation Refine(
            World world, ushort graphic, int x, int y, sbyte z,
            TileOrientation baseKind)
        {
            _ = world;   // unused — production source reads UO.World directly.
            _ = graphic; // unused in the legacy code, retained for source-compat.
            var domain = (WallTileOrientation)(int)baseKind;
            var refined = Svc.Refine(x, y, z, domain);
            return (TileOrientation)(int)refined;
        }
    }
}
