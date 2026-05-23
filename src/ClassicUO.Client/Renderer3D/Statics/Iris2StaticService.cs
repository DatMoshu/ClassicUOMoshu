// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Production implementation of <see cref="IIris2StaticService"/>. Pure-state service
    /// (no events, no per-frame tick). First <see cref="EnsureLoaded"/> call delegates to
    /// the injected <see cref="IIris2StaticRegistryStorage"/>.
    /// </summary>
    public sealed class Iris2StaticService : IIris2StaticService
    {
        private static readonly Dictionary<ushort, Iris2StaticEntry> EmptyTable = new();

        private readonly IIris2StaticRegistryStorage _storage;
        private bool _loaded;
        private IReadOnlyDictionary<ushort, Iris2StaticEntry> _entries = EmptyTable;
        private string _lastError;
        private string _registryPath;
        private string _repoRoot;

        public Iris2StaticService(IIris2StaticRegistryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public int LoadedEntryCount => _entries?.Count ?? 0;
        public string LastError => _lastError;
        public string RegistryPath => _registryPath;
        public string RepoRoot => _repoRoot;

        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            Iris2StaticRegistryLoadResult r = _storage.Load();
            _registryPath = r.RegistryPath;
            _repoRoot = r.RepoRoot;
            if (!r.Success)
            {
                _lastError = r.ErrorMessage;
                _entries = EmptyTable;
                return;
            }
            _lastError = null;
            _entries = r.Entries ?? EmptyTable;
        }

        public void Invalidate()
        {
            _loaded = false;
            _entries = EmptyTable;
            _lastError = null;
            _registryPath = null;
            _repoRoot = null;
        }

        public bool TryGet(ushort graphic, out Iris2StaticEntry entry)
        {
            EnsureLoaded();
            return _entries.TryGetValue(graphic, out entry);
        }
    }
}
