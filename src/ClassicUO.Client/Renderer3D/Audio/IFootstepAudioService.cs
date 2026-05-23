// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Material-aware footstep audio. Replaces the UO 8-bit step PCM with HD WAV samples
    /// from the Ovani AutoFootSteps pack. Material is resolved from
    /// <see cref="OverrideMaterial"/> first, then weather (Snow → snow steps when
    /// <see cref="AutoUseSnowInWinter"/> is on), else <see cref="DefaultMaterial"/>.
    /// </summary>
    public interface IFootstepAudioService
    {
        bool Enabled { get; }
        float Volume { get; }
        bool AutoUseSnowInWinter { get; }
        FootstepMaterial OverrideMaterial { get; }
        FootstepMaterial DefaultMaterial { get; }
        FootwearKind Footwear { get; }

        /// <summary>Diagnostic — material chosen on the most recent <see cref="TryPlayStep"/> call.</summary>
        FootstepMaterial LastMaterial { get; }

        /// <summary>Diagnostic — total step plays since service start.</summary>
        int LastPlayCount { get; }

        void SetEnabled(bool enabled);
        void SetVolume(float volume);
        void SetAutoUseSnowInWinter(bool enabled);
        void SetOverrideMaterial(FootstepMaterial material);
        void SetDefaultMaterial(FootstepMaterial material);
        void SetFootwear(FootwearKind kind);

        /// <summary>
        /// Try to play a step at world tile (<paramref name="x"/>, <paramref name="y"/>).
        /// Returns true when a custom step sample was played and the caller should suppress
        /// the legacy UO step sound. Returns false when disabled, mounted, or the audio
        /// pack lookup fell through every fallback.
        /// </summary>
        bool TryPlayStep(int x, int y, bool running, bool mounted);

        /// <summary>Resolve the active material based on overrides + weather.</summary>
        FootstepMaterial ResolveMaterial();
    }
}
