// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Slow population of soft glowing motes that drift, sway, and fade in a cylinder
    /// around the player. Driven by an <see cref="IParticleSpawner"/> gateway so the
    /// service stays decoupled from the legacy <c>Particle3DSystem</c>.
    /// </summary>
    public interface IAmbientMotesService
    {
        bool Enabled { get; }
        int TargetAlive { get; }
        float Radius { get; }
        AmbientMotesPalette CurrentPalette { get; }

        void SetEnabled(bool enabled);
        void SetTargetAlive(int target);
        void SetRadius(float radius);

        /// <summary>Apply a named palette from <see cref="AmbientMotesPaletteLibrary"/>. No-op on unknown name.</summary>
        void SetPalette(string name);

        /// <summary>Apply a custom palette explicitly.</summary>
        void SetPalette(AmbientMotesPalette palette);

        /// <summary>
        /// Update the player anchor used for spawn positions. Called once per frame by
        /// GameScene before world rendering.
        /// </summary>
        void Configure(Vector3 anchorWorld);
    }
}
