// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wrapping the legacy AttachmentRenderer.FindSlotPublic
// behind IOutfitSlotProvider. Casts the domain OutfitSlot enum to the legacy AttachSlotKind
// (values are bit-identical — locked by parity test).

using System.Collections.Generic;
using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IOutfitSlotProvider"/>. Looks up the legacy AttachmentRenderer
    /// slot table each call. The slot's <c>Available</c> list is owned by AttachmentRenderer
    /// and may grow during pack-load; we read it live each call rather than caching.
    /// </summary>
    internal sealed class LegacyOutfitSlotProvider : IOutfitSlotProvider
    {
        public IReadOnlyList<string> GetAvailable(OutfitSlot slot)
        {
            var legacySlot = AttachmentRenderer.FindSlotPublic((AttachSlotKind)(int)slot);
            return legacySlot?.Available;
        }
    }
}
