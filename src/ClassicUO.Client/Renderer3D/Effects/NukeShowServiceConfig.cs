// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="NukeShowService"/>. Covers barrage shape, audio,
    /// blast scaling, and the F_FLASH texture binding name.
    /// </summary>
    public sealed class NukeShowServiceConfig
    {
        public bool InitialEnabled { get; init; } = false;
        public bool InitialVerboseLog { get; init; } = false;

        // ===== Barrage shape =====

        public int InitialBarrageCount { get; init; } = 6;
        public float InitialBarrageRadius { get; init; } = 900f;
        public float InitialStagger { get; init; } = 1.3f;
        public float InitialSingleDistance { get; init; } = 700f;

        /// <summary>Particle/blast scale multiplier. 1.0 = original; 2.0 = ~Hiroshima from ground.</summary>
        public float InitialNukeScale { get; init; } = 2.0f;

        /// <summary>Tree-bend / leaf-blast radius (world units, multiplied by NukeScale).</summary>
        public float InitialBlastRadius { get; init; } = 600f;

        // ===== Audio =====

        public int UoExplosionSoundId { get; init; } = 0x0207;

        /// <summary>Relative path of the D-Day one-shot WAV under the audio root.</summary>
        public string DDayAudioPath { get; init; } = "Sounds/dday.wav";
        public bool InitialPlayDDayAudio { get; init; } = true;

        /// <summary>D-Day one-shot volume in [0,1].</summary>
        public float DDayVolume { get; init; } = 0.9f;

        /// <summary>Particle texture registry name to lazy-bind for the F_FLASH slot.</summary>
        public string FlashTextureName { get; init; } = "halo";

        // ===== Misc =====

        /// <summary>Maximum allowed dt per tick (seconds) — clamps stalls.</summary>
        public float MaxDeltaSeconds { get; init; } = 0.1f;

        /// <summary>Cooldown after the last detonation before the show auto-stops (seconds).</summary>
        public float TailSeconds { get; init; } = 8f;

        /// <summary>Deterministic seed for reproducibility in tests/replay.</summary>
        public int RandomSeed { get; init; } = 0xDDAA;

        public static NukeShowServiceConfig Default => new();
    }
}
