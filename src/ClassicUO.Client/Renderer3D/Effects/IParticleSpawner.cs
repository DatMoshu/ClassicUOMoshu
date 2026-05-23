// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Narrow gateway over the legacy <c>Particle3DSystem</c> spawn surface. Lets new
    /// services (<see cref="AmbientMotesService"/>, future fire/explosion/buff services)
    /// stay decoupled from the legacy class until <c>Particle3DSystem</c> migrates.
    /// </summary>
    /// <remarks>
    /// When <c>Particle3DSystem</c> migrates to <c>IParticleService</c>, the production
    /// adapter rewrites to forward to that service; consumers see no change.
    /// </remarks>
    public interface IParticleSpawner
    {
        /// <summary>Current alive-particle count (across all sources).</summary>
        int AliveParticles { get; }

        /// <summary>
        /// Spawn one particle. Linear interpolation between <paramref name="colorStart"/>/
        /// <paramref name="sizeStart"/> and the corresponding <c>End</c> values is applied
        /// over the particle's lifetime.
        /// </summary>
        void Spawn(
            Vector3 position,
            Vector3 velocity,
            Vector3 acceleration,
            float lifetimeSeconds,
            float sizeStart,
            float sizeEnd,
            Color colorStart,
            Color colorEnd,
            ParticleFlags flags);

        /// <summary>
        /// Lazy-bind the <see cref="ParticleFlags.Flash"/> texture slot to the named texture
        /// from the particle texture registry. Called once before the first flash spawn
        /// (e.g., from <see cref="INukeShowService.TriggerSingle"/>). No-op when the slot
        /// is already bound or the name is unknown.
        /// </summary>
        void EnsureFlashTexture(string registryName);
    }
}
