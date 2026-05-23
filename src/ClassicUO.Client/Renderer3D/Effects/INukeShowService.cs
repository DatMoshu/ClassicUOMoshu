// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// D-Day style nuke barrage orchestrator. Drives <see cref="IParticleSpawner"/> for the
    /// visual effects, calls <see cref="IExplosionService"/> for tree-bend / leaf-blast,
    /// calls <see cref="IFireService"/> for the burning crater, and uses
    /// <see cref="IAudioClipLibrary"/> + <see cref="IUOWorldSoundPlayer"/> for audio.
    /// </summary>
    public interface INukeShowService
    {
        bool Enabled { get; }
        bool VerboseLog { get; }
        bool IsRunning { get; }

        // Tunable barrage shape (UI-bound).
        int BarrageCount { get; }
        float BarrageRadius { get; }
        float Stagger { get; }
        float SingleDistance { get; }
        float NukeScale { get; }
        float BlastRadius { get; }
        bool PlayDDayAudio { get; }

        /// <summary>Number of detonations remaining in the current barrage.</summary>
        int RemainingDetonations { get; }

        /// <summary>Elapsed time within the current show, in seconds.</summary>
        float CurrentTime { get; }

        /// <summary>Last anchor passed to <see cref="Configure"/>; used by gump diagnostics.</summary>
        Vector3 Anchor { get; }

        // ===== Setters =====
        void SetEnabled(bool enabled);
        void SetVerboseLog(bool verbose);
        void SetBarrageCount(int count);
        void SetBarrageRadius(float radius);
        void SetStagger(float seconds);
        void SetNukeScale(float scale);
        void SetBlastRadius(float radius);
        void SetPlayDDayAudio(bool play);

        // ===== Lifecycle =====

        /// <summary>
        /// Update the player anchor used as the origin for new triggers. Called each frame
        /// before world rendering.
        /// </summary>
        void Configure(Vector3 anchorWorld);

        /// <summary>Drop a single nuke at a random direction <c>SingleDistance</c> from the anchor.</summary>
        void TriggerSingle();

        /// <summary>D-Day barrage: <see cref="BarrageCount"/> bombs around the anchor at <see cref="BarrageRadius"/>.</summary>
        void TriggerBarrage();

        /// <summary>Cancel any in-flight show and clear pending detonations.</summary>
        void Stop();
    }
}
