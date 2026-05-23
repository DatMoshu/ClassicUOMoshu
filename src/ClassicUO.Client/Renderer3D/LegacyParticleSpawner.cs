// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wrapping the legacy Particle3DSystem behind
// IParticleSpawner. When Particle3DSystem migrates to IParticleService, this adapter
// rewrites in place — service-side consumers see no change.

using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>Production <see cref="IParticleSpawner"/> backed by <see cref="Particle3DSystem"/>.</summary>
    internal sealed class LegacyParticleSpawner : IParticleSpawner
    {
        public int AliveParticles => Particle3DSystem.AliveParticles;

        public void Spawn(
            Vector3 position,
            Vector3 velocity,
            Vector3 acceleration,
            float lifetimeSeconds,
            float sizeStart,
            float sizeEnd,
            Color colorStart,
            Color colorEnd,
            ParticleFlags flags)
        {
            Particle3DSystem.Spawn(
                position, velocity, acceleration,
                lifetimeSeconds,
                sizeStart, sizeEnd,
                colorStart, colorEnd,
                (byte)flags);
        }

        public void EnsureFlashTexture(string registryName)
        {
            if (Particle3DSystem.ParticleFlashTexture != null) return;
            if (string.IsNullOrEmpty(registryName)) return;
            Particle3DSystem.ParticleFlashTexture = ParticleTextureRegistry.Get(registryName);
        }
    }
}
