// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IMultiOrientationStorage result envelope (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Atomic load result returned by <see cref="IMultiOrientationStorage.Load"/>.
    /// Carries the merged orientation table plus the per-source counts and the last
    /// error (if any). The service replaces its state with this envelope as a unit so
    /// readers never observe a partially-populated table.
    /// </summary>
    public readonly struct MultiOrientationLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly int HandCount;
        public readonly int AutoCount;
        public readonly IReadOnlyDictionary<ushort, WallTileOrientation> Table;

        public MultiOrientationLoadResult(
            bool success, string errorMessage,
            int handCount, int autoCount,
            IReadOnlyDictionary<ushort, WallTileOrientation> table)
        {
            Success = success;
            ErrorMessage = errorMessage;
            HandCount = handCount;
            AutoCount = autoCount;
            Table = table;
        }
    }
}
