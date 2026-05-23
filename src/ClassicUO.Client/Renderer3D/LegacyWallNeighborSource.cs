// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Wraps the legacy World/Map/Chunk lookup chain so the pure-logic classifier
// service can stay free of game-runtime types.

using System;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using ClassicUO.Renderer.Statics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyWallNeighborSource : IWallNeighborSource
    {
        public bool HasWallAt(int x, int y, sbyte z, int zTolerance)
        {
            World world = ClassicUO.Client.Game.UO.World;
            if (world == null || world.Map == null) return false;

            int cx = x >> 3, cy = y >> 3;
            int tx = x & 7, ty = y & 7;
            Chunk chunk;
            try { chunk = world.Map.GetChunk2(cx, cy, load: false); }
            catch { return false; }
            if (chunk == null) return false;

            for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
            {
                if (obj.IsDestroyed) continue;
                if (Math.Abs(obj.Z - z) > zTolerance) continue;

                if (obj is Static s)
                {
                    ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[s.Graphic];
                    if (data.IsWall) return true;
                }
                else if (obj is Multi m)
                {
                    if (m.ItemData.IsWall) return true;
                }
            }
            return false;
        }
    }
}
