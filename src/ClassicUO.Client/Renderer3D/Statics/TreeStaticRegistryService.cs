// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Production implementation of <see cref="ITreeStaticRegistry"/>. State-only service —
    /// no events, no per-frame tick. Allocation: only on Load (fills the dictionary).
    /// </summary>
    public sealed class TreeStaticRegistryService : ITreeStaticRegistry
    {
        private readonly ITreeStaticRegistryStorage _storage;
        private readonly Dictionary<ushort, TreeStaticEntry> _entries = new();

        public TreeStaticRegistryService(ITreeStaticRegistryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public int Count => _entries.Count;
        public string LastSource => _storage.LastSource;
        public string LastError => _storage.LastError;

        public bool Load()
        {
            _entries.Clear();
            return _storage.TryLoad(_entries);
        }

        public bool TryGet(ushort graphic, out TreeStaticEntry entry)
            => _entries.TryGetValue(graphic, out entry);

        public bool IsDeciduous(ushort graphic)
            => _entries.TryGetValue(graphic, out TreeStaticEntry e) && e.Deciduous;
    }
}
