// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Renderer3D; // RoofArchetypeMath (canonical mesh name resolution)

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Production implementation of <see cref="IRoofRegistryService"/>. Pure-state service
    /// (no events, no per-frame tick). First <see cref="EnsureLoaded"/> call delegates to
    /// the injected <see cref="IRoofRegistryStorage"/>.
    /// </summary>
    internal sealed class RoofRegistryService : IRoofRegistryService
    {
        private static readonly Dictionary<ushort, RoofTileTag> EmptyTagTable = new();
        private static readonly Dictionary<string, RoofMeshEntry> EmptyEntryTable = new();

        private readonly IRoofRegistryStorage _storage;
        private bool _loaded;
        private IReadOnlyDictionary<ushort, RoofTileTag> _tags = EmptyTagTable;
        private IReadOnlyDictionary<string, RoofMeshEntry> _entries = EmptyEntryTable;
        private string _tagLoadError;
        private string _manifestLoadError;
        private string _tagDataPath;
        private string _meshesDir;
        private string _resolvedVersion;

        public RoofRegistryService(IRoofRegistryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public int LoadedTagCount => _tags?.Count ?? 0;
        public int LoadedManifestCount => _entries?.Count ?? 0;
        public string TagLoadError => _tagLoadError;
        public string ManifestLoadError => _manifestLoadError;
        public string TagDataPath => _tagDataPath;
        public string MeshesDir => _meshesDir;
        public string ResolvedVersion => _resolvedVersion;

        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            RoofRegistryLoadResult r = _storage.Load();
            _tagDataPath = r.TagDataPath;
            _meshesDir = r.MeshesDir;
            _resolvedVersion = r.ResolvedVersion;
            _tagLoadError = r.TagLoadError;
            _manifestLoadError = r.ManifestLoadError;
            _tags = r.TagsByGraphic ?? EmptyTagTable;
            _entries = r.EntriesByFamilyMesh ?? EmptyEntryTable;
        }

        public void Invalidate()
        {
            _loaded = false;
            _tags = EmptyTagTable;
            _entries = EmptyEntryTable;
            _tagLoadError = null;
            _manifestLoadError = null;
            _tagDataPath = null;
            _meshesDir = null;
            _resolvedVersion = null;
        }

        public bool TryResolve(ushort graphic, out RoofTileTag tag, out RoofMeshEntry entry)
        {
            EnsureLoaded();
            entry = default;
            if (!_tags.TryGetValue(graphic, out tag))
            {
                tag = default;
                return false;
            }
            string canonical = RoofArchetypeMath.CanonicalMeshName(tag.Archetype);
            if (string.IsNullOrEmpty(canonical)) return false;
            return _entries.TryGetValue($"{tag.Family}|{canonical}", out entry);
        }
    }
}
