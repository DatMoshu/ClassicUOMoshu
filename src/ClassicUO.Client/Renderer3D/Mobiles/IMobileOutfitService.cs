// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Per-mobile outfit picker — assigns each NPC a deterministic Synty-pack outfit
    /// keyed on serial. Same serial + same pack → same outfit forever (across frames,
    /// across sessions) unless <see cref="Clear"/> or <see cref="Drop"/> is called.
    /// </summary>
    public interface IMobileOutfitService
    {
        /// <summary>Number of mobiles with cached outfits.</summary>
        int CachedCount { get; }

        /// <summary>
        /// Return the cached outfit for <paramref name="serial"/>, or pick + cache a new
        /// one if absent (or if the cached entry's pack mismatches). Returns null when
        /// <paramref name="pack"/> is null/empty.
        /// </summary>
        MobileOutfit GetOrPick(uint serial, string pack);

        /// <summary>Drop the cached outfit for <paramref name="serial"/>. Idempotent.</summary>
        void Drop(uint serial);

        /// <summary>Drop all cached outfits.</summary>
        void Clear();
    }
}
