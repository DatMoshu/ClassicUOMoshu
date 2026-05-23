// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Terrain domain (ADR-012 Phase 3).

using ClassicUO.Game.Map;
using ClassicUO.Renderer.Renderer3D; // LandMesh3D
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Terrain
{
    /// <summary>
    /// Per-chunk land-mesh cache shared by the terrain draw pass + the ground-overlay
    /// effect pass. Each <see cref="Chunk"/> gets one <see cref="LandMesh3D"/> on first
    /// visibility; rebuilds happen when the chunk's <c>IsDirty3D</c> flag is set or the
    /// cached mesh has no geometry yet.
    /// </summary>
    /// <remarks>
    /// Owns the GPU resource (vertex buffers via <see cref="LandMesh3D"/>); registers
    /// itself with the renderer's <see cref="ClassicUO.Renderer.Core.IDisposableRegistry"/>
    /// at construction so all cached meshes are released at shutdown. Internal because
    /// <see cref="LandMesh3D"/> + <see cref="Chunk"/> are internal types.
    /// </remarks>
    internal interface ITerrainMeshCache
    {
        /// <summary>Number of cached entries. Diagnostic.</summary>
        int Count { get; }

        /// <summary>
        /// Look up a cached mesh without building. Returns false when the chunk has
        /// never been seen or the cache has been cleared.
        /// </summary>
        bool TryGet(Chunk chunk, out LandMesh3D mesh);

        /// <summary>
        /// Get-or-build for a chunk. If the cache has no entry, allocates a new
        /// <see cref="LandMesh3D"/> and builds it from the chunk. If the existing entry
        /// has <c>IsDirty3D</c> set or no geometry, rebuilds it. Returns true when a
        /// build was performed this call (lets the caller count "built this frame").
        /// </summary>
        bool BuildIfDirty(Chunk chunk, GraphicsDevice gd, out LandMesh3D mesh);

        /// <summary>Dispose every cached mesh + clear. Re-population happens on next visibility.</summary>
        void Clear();
    }
}
