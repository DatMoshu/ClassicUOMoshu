// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Source of available GLB paths for a given attachment slot. Production-side wraps
    /// <c>AttachmentRenderer.FindSlotPublic(kind).Available</c>; tests provide a fake.
    /// </summary>
    /// <remarks>
    /// The path format is <c>"{pack}/{filename}.glb"</c> — the <see cref="MobileOutfitService"/>
    /// filters by leading directory segment to pick from the requested Synty pack.
    /// </remarks>
    public interface IOutfitSlotProvider
    {
        /// <summary>
        /// Available GLB paths for <paramref name="slot"/>. Returns null when the slot is
        /// unknown or empty (saves an allocation; service treats null as "no candidates").
        /// </summary>
        IReadOnlyList<string> GetAvailable(OutfitSlot slot);
    }
}
