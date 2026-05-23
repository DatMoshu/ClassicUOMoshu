// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy BuffParticleEffects delegated to IBuffParticleService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * GameScene.DrawCustom — calls Configure(anchor) and Tick(player, dt). The Tick
//     overload is preserved for compile compatibility but is now a no-op (the service
//     ticks itself via Renderer3DServices.Tick using its injected IActiveBuffSource).
//   * BuffEffectsGump — toggles Enabled / Require3DMode and per-archetype enables.

using System;
using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy archetype enum aliased to <see cref="BuffArchetype"/>. Bit-for-bit identical
    /// values; casts round-trip.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Effects.BuffArchetype. Will be removed in ADR-012 Phase 3.")]
    internal enum BuffArchetype
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Holy = 3,
        Curse = 4,
        Poison = 5,
        Lightning = 6,
        Stat = 7,
        Defense = 8,
        Stealth = 9,
        FormShift = 10,
        Wind = 11,
        Debuff = 12,
        Default = 13,
    }

    [Obsolete("Use IBuffParticleService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class BuffParticleEffects
    {
        private static IBuffParticleService Svc => Renderer3DHost.Services.BuffParticles;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static bool Require3DMode
        {
            get => Svc.Require3DMode;
            set => Svc.SetRequire3DMode(value);
        }

        // Per-archetype toggles. Backed by the service; the dictionary view is materialised
        // lazily for the gump's iteration use-case. Mutations route through the service.
        public static IDictionary<BuffArchetype, bool> ArchetypeEnabled { get; } = new ArchetypeEnabledView();

        public static int LastTickEmissions => Svc.LastTickEmissions;
        public static int LastActiveCount => Svc.LastActiveCount;

        public static void Configure(Vector3 anchorWorld) => Svc.Configure(anchorWorld);

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>GameScene.DrawCustom</c>) compile.
        /// The player-buff snapshot is now sourced from the injected <see cref="IActiveBuffSource"/>.
        /// </summary>
        public static void Tick(PlayerMobile player, float dt) { /* no-op */ }

        /// <summary>Public for the gump's per-buff "what archetype?" display.</summary>
        public static BuffArchetype ClassifyBuff(ClassicUO.Game.Data.BuffIconType t)
            => (BuffArchetype)(int)LegacyActiveBuffSource.ClassifyBuff(t);

        // ===== ArchetypeEnabled view — IDictionary<BuffArchetype, bool> over the service =====

        private sealed class ArchetypeEnabledView : IDictionary<BuffArchetype, bool>
        {
            private static readonly BuffArchetype[] AllArchetypes =
            {
                BuffArchetype.Fire, BuffArchetype.Ice, BuffArchetype.Holy, BuffArchetype.Curse,
                BuffArchetype.Poison, BuffArchetype.Lightning, BuffArchetype.Stat, BuffArchetype.Defense,
                BuffArchetype.Stealth, BuffArchetype.FormShift, BuffArchetype.Wind, BuffArchetype.Debuff,
                BuffArchetype.Default,
            };

            public bool this[BuffArchetype key]
            {
                get => Svc.IsArchetypeEnabled((ClassicUO.Renderer.Effects.BuffArchetype)(int)key);
                set => Svc.SetArchetypeEnabled((ClassicUO.Renderer.Effects.BuffArchetype)(int)key, value);
            }

            public ICollection<BuffArchetype> Keys => AllArchetypes;
            public ICollection<bool> Values
            {
                get
                {
                    var v = new bool[AllArchetypes.Length];
                    for (int i = 0; i < AllArchetypes.Length; i++) v[i] = this[AllArchetypes[i]];
                    return v;
                }
            }

            public int Count => AllArchetypes.Length;
            public bool IsReadOnly => false;
            public void Add(BuffArchetype key, bool value) => this[key] = value;
            public void Add(KeyValuePair<BuffArchetype, bool> item) => this[item.Key] = item.Value;
            public void Clear() { /* not supported — fixed-size domain */ }
            public bool Contains(KeyValuePair<BuffArchetype, bool> item) => this[item.Key] == item.Value;
            public bool ContainsKey(BuffArchetype key) => true;
            public void CopyTo(KeyValuePair<BuffArchetype, bool>[] array, int arrayIndex)
            {
                for (int i = 0; i < AllArchetypes.Length; i++)
                    array[arrayIndex + i] = new KeyValuePair<BuffArchetype, bool>(AllArchetypes[i], this[AllArchetypes[i]]);
            }
            public IEnumerator<KeyValuePair<BuffArchetype, bool>> GetEnumerator()
            {
                foreach (var a in AllArchetypes)
                    yield return new KeyValuePair<BuffArchetype, bool>(a, this[a]);
            }
            public bool Remove(BuffArchetype key) => false;
            public bool Remove(KeyValuePair<BuffArchetype, bool> item) => false;
            public bool TryGetValue(BuffArchetype key, out bool value) { value = this[key]; return true; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
