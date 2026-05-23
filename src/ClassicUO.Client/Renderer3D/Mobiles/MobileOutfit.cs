// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Per-mobile outfit assignment: which Synty pack the mobile wears, and which GLB
    /// index within that pack fills each <see cref="OutfitSlot"/>. Replaces the legacy
    /// <c>MobileOutfit</c>.
    /// </summary>
    public sealed class MobileOutfit
    {
        public string Pack;
        public Dictionary<OutfitSlot, int> SlotIndex = new();
    }
}
