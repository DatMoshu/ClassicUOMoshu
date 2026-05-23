// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Production implementation of <see cref="IMobileOutfitService"/>. Per-mobile
    /// deterministic outfit picker driven by a serial-seeded <see cref="Random"/>.
    /// </summary>
    public sealed class MobileOutfitService : IMobileOutfitService
    {
        private readonly MobileOutfitServiceConfig _config;
        private readonly IOutfitSlotProvider _slotProvider;
        private readonly Dictionary<uint, MobileOutfit> _cache = new();
        private readonly List<int> _candidateScratch = new(capacity: 32);

        public MobileOutfitService(MobileOutfitServiceConfig config, IOutfitSlotProvider slotProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _slotProvider = slotProvider ?? throw new ArgumentNullException(nameof(slotProvider));
        }

        public int CachedCount => _cache.Count;

        public MobileOutfit GetOrPick(uint serial, string pack)
        {
            if (string.IsNullOrEmpty(pack)) return null;

            if (_cache.TryGetValue(serial, out MobileOutfit existing) && existing.Pack == pack)
                return existing;

            // Hash serial → seed; XOR with the mixer constant so adjacent serials don't
            // produce visually-similar outfits.
            int seed = unchecked((int)(serial ^ _config.SerialSeedMixer));
            Random rng = new Random(seed);

            MobileOutfit outfit = new MobileOutfit { Pack = pack };
            foreach (OutfitSlot slot in _config.OutfitSlots)
                outfit.SlotIndex[slot] = PickFromPack(slot, pack, rng);

            _cache[serial] = outfit;
            return outfit;
        }

        public void Drop(uint serial) => _cache.Remove(serial);
        public void Clear() => _cache.Clear();

        private int PickFromPack(OutfitSlot slot, string pack, Random rng)
        {
            IReadOnlyList<string> available = _slotProvider.GetAvailable(slot);
            if (available is null || available.Count == 0) return -1;

            // Available paths look like "FantasyVillagers/SK_FANT_VILL_03_10TORS_HU01.glb"
            // — match the leading directory segment, case-insensitive.
            string prefix = pack + "/";
            _candidateScratch.Clear();
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    _candidateScratch.Add(i);
            }
            if (_candidateScratch.Count == 0) return -1;
            return _candidateScratch[rng.Next(_candidateScratch.Count)];
        }
    }
}
