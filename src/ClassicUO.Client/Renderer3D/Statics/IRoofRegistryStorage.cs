// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Composite load result for the roof registry: the graphic -> tag table from
    /// <c>multi-tile-roof-archetypes.auto.json</c> plus the family|canonical -> entry table
    /// from the external manifest. Either half may fail independently; the service
    /// degrades gracefully (tags only / manifest only / neither).
    /// </summary>
    internal readonly struct RoofRegistryLoadResult
    {
        public readonly bool TagsSuccess;
        public readonly bool ManifestSuccess;
        public readonly string TagLoadError;
        public readonly string ManifestLoadError;
        public readonly string TagDataPath;
        public readonly string MeshesDir;
        public readonly string ResolvedVersion;
        public readonly IReadOnlyDictionary<ushort, RoofTileTag> TagsByGraphic;
        public readonly IReadOnlyDictionary<string, RoofMeshEntry> EntriesByFamilyMesh;

        public RoofRegistryLoadResult(
            bool tagsSuccess, bool manifestSuccess,
            string tagLoadError, string manifestLoadError,
            string tagDataPath, string meshesDir, string resolvedVersion,
            IReadOnlyDictionary<ushort, RoofTileTag> tagsByGraphic,
            IReadOnlyDictionary<string, RoofMeshEntry> entriesByFamilyMesh)
        {
            TagsSuccess = tagsSuccess;
            ManifestSuccess = manifestSuccess;
            TagLoadError = tagLoadError;
            ManifestLoadError = manifestLoadError;
            TagDataPath = tagDataPath;
            MeshesDir = meshesDir;
            ResolvedVersion = resolvedVersion;
            TagsByGraphic = tagsByGraphic;
            EntriesByFamilyMesh = entriesByFamilyMesh;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="RoofRegistryService"/>. Production-side
    /// reads the tag JSON + external manifest; tests pre-seed in-memory dictionaries.
    /// </summary>
    internal interface IRoofRegistryStorage
    {
        /// <summary>
        /// Resolve and parse both registry sources. Returns a composite result; partial
        /// failures (tags load, manifest missing) produce a degraded but usable result.
        /// </summary>
        RoofRegistryLoadResult Load();
    }
}
