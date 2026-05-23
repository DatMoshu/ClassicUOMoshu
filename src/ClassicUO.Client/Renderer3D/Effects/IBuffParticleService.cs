// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Per-archetype particle effects emitted around the player while a buff is active.
    /// Drives <see cref="IParticleSpawner"/>; pulls active buff list from
    /// <see cref="IActiveBuffSource"/>; gates on <see cref="IRenderModeGate"/>.
    /// </summary>
    public interface IBuffParticleService
    {
        bool Enabled { get; }
        bool Require3DMode { get; }

        /// <summary>Number of emit calls dispatched on the most recent tick (diagnostic).</summary>
        int LastTickEmissions { get; }

        /// <summary>Number of active buffs observed on the most recent tick (diagnostic).</summary>
        int LastActiveCount { get; }

        void SetEnabled(bool enabled);
        void SetRequire3DMode(bool require);

        /// <summary>Toggle a specific archetype's emission. Default: all on.</summary>
        void SetArchetypeEnabled(BuffArchetype archetype, bool enabled);
        bool IsArchetypeEnabled(BuffArchetype archetype);

        /// <summary>Update player-foot anchor used for emission positions. Called each frame.</summary>
        void Configure(Vector3 anchorWorld);
    }
}
