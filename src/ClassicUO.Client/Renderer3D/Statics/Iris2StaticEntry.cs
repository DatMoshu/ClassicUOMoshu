// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Iris 2 static-graphic registry entry: the GLB path to load, the source mesh ID,
    /// the human-readable static name, and the UO tiledata height. Mirrors the legacy
    /// <c>Iris2StaticEntry</c> bit-for-bit.
    /// </summary>
    /// <remarks>
    /// Iris-2-derived GLBs are GPLv3-tainted reference assets — internal-use only.
    /// See <c>design/Core/Reference/iris2/03-licensing-and-reuse.md</c>.
    /// </remarks>
    public struct Iris2StaticEntry
    {
        /// <summary>GLB path relative to <see cref="IIris2StaticService.RepoRoot"/>, e.g. "Data/iris2-glb/mdl_000002.glb".</summary>
        public string GlbRelative;

        /// <summary>Source mesh ID, e.g. "mdl_000002".</summary>
        public string MeshId;

        /// <summary>Human-readable static name from UO tiledata.</summary>
        public string StaticName;

        /// <summary>UO tiledata height in Z units.</summary>
        public int TileHeight;
    }
}
