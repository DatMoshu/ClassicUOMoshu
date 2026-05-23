// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// One active buff bucketed into a visual archetype, with an opaque
    /// <see cref="Key"/> the service uses for per-buff timer keying. The key is
    /// implementation-defined (production uses the underlying buff-icon enum value cast
    /// to ulong); consumers only need it to be stable for the lifetime of the buff.
    /// </summary>
    public readonly struct ActiveBuffEntry
    {
        public readonly BuffArchetype Archetype;
        public readonly ulong Key;

        public ActiveBuffEntry(BuffArchetype archetype, ulong key)
        {
            Archetype = archetype;
            Key = key;
        }
    }

    /// <summary>
    /// Gateway over the player's active buff list. Production-side wraps
    /// <c>player.BuffIcons</c> and runs the legacy 12-archetype classification;
    /// tests provide a settable list.
    /// </summary>
    /// <remarks>
    /// Returns null when the player is null or has no buffs (saves a per-frame allocation
    /// of an empty list). Service must handle the null case.
    /// </remarks>
    public interface IActiveBuffSource
    {
        IReadOnlyCollection<ActiveBuffEntry> GetActiveBuffs();
    }
}
