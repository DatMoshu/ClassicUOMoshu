// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using ClassicUO.Renderer.Renderer3D; // legacy RoofArchetype enum

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Roof-tile tag from <c>Data/3D/multi-tile-roof-archetypes.auto.json</c>:
    /// graphic ID -> {family, archetype}. The family + canonical mesh name then keys
    /// into the manifest table (<see cref="RoofMeshEntry"/>) for the actual GLB/atlas
    /// file pair.
    /// </summary>
    /// <remarks>
    /// <see cref="ClassicUO.Renderer.Renderer3D.RoofArchetype"/> stays in the legacy
    /// namespace because <see cref="ClassicUO.Renderer.Renderer3D.RoofArchetypeMath"/>
    /// provides shape-specific math (yaw, canonical mesh name) consumed by the GPU
    /// draw path. Moving it would require duplicating that math.
    /// </remarks>
    internal readonly struct RoofTileTag
    {
        public readonly string Family;
        public readonly string FamilyName;
        public readonly RoofArchetype Archetype;

        public RoofTileTag(string family, string familyName, RoofArchetype archetype)
        {
            Family = family;
            FamilyName = familyName;
            Archetype = archetype;
        }
    }

    /// <summary>
    /// Roof-mesh manifest entry: the GLB file + atlas texture for a
    /// (family, canonical mesh) pair. Atlases are family-wide; GLBs are canonical-per-shape.
    /// </summary>
    internal readonly struct RoofMeshEntry
    {
        public readonly string MeshFile;
        public readonly string AtlasFile;

        public RoofMeshEntry(string meshFile, string atlasFile)
        {
            MeshFile = meshFile;
            AtlasFile = atlasFile;
        }
    }
}
