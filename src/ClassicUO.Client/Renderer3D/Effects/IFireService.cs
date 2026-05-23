// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Manages a fixed-size pool of fire patches that linger after a lightning strike,
    /// nuke detonation, or future fire-magic ignition. Each patch emits embers + smoke
    /// through <see cref="IParticleSpawner"/>; horizontal drift is sourced from
    /// <see cref="WindUpdatedEvent"/> via the event bus.
    /// </summary>
    public interface IFireService
    {
        bool Enabled { get; }

        /// <summary>Number of fires currently burning (≤ pool capacity).</summary>
        int LiveFires { get; }

        void SetEnabled(bool enabled);

        /// <summary>Light a fire at <paramref name="worldGround"/> using default radius/lifetime.</summary>
        void Ignite(Vector3 worldGround);

        /// <summary>Light a fire at <paramref name="worldGround"/> with explicit radius/lifetime.</summary>
        void Ignite(Vector3 worldGround, float radius, float lifetimeSeconds);

        /// <summary>Extinguish all fires immediately. Used by debug gumps.</summary>
        void Clear();
    }
}
