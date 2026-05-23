// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Visual archetype the buff-particle service emits for. The legacy
    /// <c>BuffIconType</c> enum (180+ buff IDs) is bucketed into these archetypes by the
    /// production-side <c>IActiveBuffSource</c> adapter; the service itself never sees
    /// game-data buff IDs.
    /// </summary>
    /// <remarks>
    /// Bit-for-bit identical to legacy <c>Renderer3D.BuffArchetype</c> (locked by parity test).
    /// </remarks>
    public enum BuffArchetype
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
}
