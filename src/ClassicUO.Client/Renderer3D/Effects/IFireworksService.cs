// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Scripted fireworks show. Drives an <see cref="IParticleSpawner"/> for the bursts
    /// and an <see cref="IParticleStringEmitter"/> for the climax text constellation.
    /// </summary>
    public interface IFireworksService
    {
        bool Enabled { get; }
        bool Loop { get; }
        bool IsRunning { get; }
        float CurrentTime { get; }
        string ClimaxText { get; }

        void SetEnabled(bool enabled);
        void SetLoop(bool loop);

        /// <summary>Set the climax text. Calls <see cref="IParticleStringEmitter.InvalidateLayout"/>.</summary>
        void SetClimaxText(string text);

        /// <summary>Update the player anchor used for burst positions and climax origin.</summary>
        void Configure(Vector3 anchorWorld);

        /// <summary>Start the show from t=0.</summary>
        void Trigger();

        /// <summary>Stop and reset to t=0.</summary>
        void Stop();
    }
}
