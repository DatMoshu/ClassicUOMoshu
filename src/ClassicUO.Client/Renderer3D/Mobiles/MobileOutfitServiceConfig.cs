// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Tunable parameters for <see cref="MobileOutfitService"/>. The slot list and seed
    /// mixer are config-driven so future per-region/per-faction outfit variants can ship
    /// alternate configs without code changes.
    /// </summary>
    public sealed class MobileOutfitServiceConfig
    {
        /// <summary>
        /// Slots the service attempts to fill from a pack. Cosmetic slots (Hair, Faces,
        /// Brows) are intentionally absent so the species base mesh shows through.
        /// </summary>
        public OutfitSlot[] OutfitSlots { get; init; } = new[]
        {
            OutfitSlot.Head,
            OutfitSlot.Torso,
            OutfitSlot.HipsMesh,
            OutfitSlot.ArmUpperL,
            OutfitSlot.ArmUpperR,
            OutfitSlot.ArmLowerL,
            OutfitSlot.ArmLowerR,
            OutfitSlot.HandL,
            OutfitSlot.HandR,
            OutfitSlot.LegL,
            OutfitSlot.LegR,
            OutfitSlot.FootL,
            OutfitSlot.FootR,
            OutfitSlot.Back,
            OutfitSlot.ShoulderLeft,
            OutfitSlot.ShoulderRight,
        };

        /// <summary>
        /// XOR mixer applied to the mobile serial when seeding the per-mobile RNG.
        /// Prevents adjacent serials (e.g., back-to-back NPC spawns) from rolling
        /// visually-similar outfits.
        /// </summary>
        public uint SerialSeedMixer { get; init; } = 0x9E3779B1u;

        public static MobileOutfitServiceConfig Default => new();
    }
}
