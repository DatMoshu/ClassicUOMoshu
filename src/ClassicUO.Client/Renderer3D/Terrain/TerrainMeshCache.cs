// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Terrain domain (ADR-012 Phase 3).

using System.Collections.Generic;
using ClassicUO.Game.Map;
using ClassicUO.Renderer.Renderer3D; // LandMesh3D
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Terrain
{
    /// <summary>
    /// Hash-table implementation of <see cref="ITerrainMeshCache"/>. Lifted verbatim
    /// from the inline build loop in <c>World3DRenderer.Draw</c> (lines 281-307 pre-Phase-3
    /// migration). State-of-record for terrain meshes now lives here.
    /// </summary>
    internal sealed class TerrainMeshCache : ITerrainMeshCache
    {
        private readonly Dictionary<Chunk, LandMesh3D> _cache = new();

        public int Count => _cache.Count;

        public bool TryGet(Chunk chunk, out LandMesh3D mesh)
        {
            if (chunk is null)
            {
                mesh = null;
                return false;
            }
            return _cache.TryGetValue(chunk, out mesh);
        }

        public bool BuildIfDirty(Chunk chunk, GraphicsDevice gd, out LandMesh3D mesh)
        {
            mesh = null;
            if (chunk is null || gd is null) return false;

            bool needBuild;
            if (!_cache.TryGetValue(chunk, out mesh))
            {
                mesh = new LandMesh3D();
                _cache[chunk] = mesh;
                needBuild = true;
            }
            else
            {
                needBuild = chunk.Mesh.IsDirty3D || !mesh.HasGeometry;
            }

            if (needBuild)
            {
                mesh.Build(chunk, gd);
                chunk.Mesh.IsDirty3D = false;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            foreach (var m in _cache.Values) m.Dispose();
            _cache.Clear();
        }
    }
}
