// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Per-particle render flags. Bit-for-bit identical to the legacy
    /// <c>Particle3DSystem.F_*</c> constants so casts round-trip during the transitional
    /// period (locked by parity test).
    /// </summary>
    /// <remarks>
    /// <see cref="Alive"/> and <see cref="Pinned"/> are management flags owned by the
    /// particle system; spawners do not normally pass them.
    /// </remarks>
    [Flags]
    public enum ParticleFlags : byte
    {
        None = 0,
        Alive = 0x01,
        Pinned = 0x02,
        Trail = 0x04,
        Textured = 0x08,
        Spin = 0x10,
        TexturedAdd = 0x20,
        Streak = 0x40,
        Flash = 0x80,
    }
}
