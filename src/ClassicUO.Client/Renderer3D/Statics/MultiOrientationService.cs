// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IMultiOrientationService implementation (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Pure-state orientation lookup. Replaces internal state atomically when the
    /// storage gateway returns a fresh <see cref="MultiOrientationLoadResult"/>; readers
    /// never observe a half-loaded table.
    /// </summary>
    public sealed class MultiOrientationService : IMultiOrientationService
    {
        private static readonly IReadOnlyDictionary<ushort, WallTileOrientation> EmptyTable
            = new Dictionary<ushort, WallTileOrientation>(0);

        private readonly IMultiOrientationStorage _storage;
        private IReadOnlyDictionary<ushort, WallTileOrientation> _table = EmptyTable;
        private bool _attemptedLoad;

        public string LastError { get; private set; }
        public int LoadedEntryCount { get; private set; }
        public int LoadedEntryCountHand { get; private set; }
        public int LoadedEntryCountAuto { get; private set; }

        public MultiOrientationService(IMultiOrientationStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void EnsureLoaded()
        {
            if (_attemptedLoad) return;
            _attemptedLoad = true;
            ApplyResult(_storage.Load());
        }

        public void Reload()
        {
            _attemptedLoad = true;
            ApplyResult(_storage.Load());
        }

        public bool TryGet(ushort graphic, out WallTileOrientation kind)
        {
            EnsureLoaded();
            if (_table.TryGetValue(graphic, out kind))
                return true;
            kind = WallTileOrientation.Unknown;
            return false;
        }

        public WallTileOrientation Resolve(ushort graphic, bool isRoof, bool isSurface, bool isWall)
        {
            if (TryGet(graphic, out WallTileOrientation kind))
                return kind;

            if (isRoof) return WallTileOrientation.Roof;
            if (isSurface && !isWall) return WallTileOrientation.Floor;
            // IsWall without an entry: caller renders a debug fallback.
            return WallTileOrientation.Unknown;
        }

        private void ApplyResult(MultiOrientationLoadResult r)
        {
            _table = r.Table ?? EmptyTable;
            LoadedEntryCountHand = r.HandCount;
            LoadedEntryCountAuto = r.AutoCount;
            LoadedEntryCount = _table.Count;
            LastError = r.Success ? null : r.ErrorMessage;
        }
    }
}
