// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Tunable parameters for <see cref="FootstepAudioService"/>. Covers material resolution,
    /// pack folder mapping, variant counts, and pitch jitter.
    /// </summary>
    public sealed class FootstepAudioServiceConfig
    {
        public bool InitialEnabled { get; init; } = false;
        public float InitialVolume { get; init; } = 0.7f;
        public bool VerboseLog { get; init; } = false;
        public bool InitialAutoUseSnowInWinter { get; init; } = true;
        public FootstepMaterial InitialOverrideMaterial { get; init; } = FootstepMaterial.Auto;
        public FootstepMaterial InitialDefaultMaterial { get; init; } = FootstepMaterial.Grass;
        public FootwearKind InitialFootwear { get; init; } = FootwearKind.Shoe;
        public int Variants { get; init; } = 5;

        /// <summary>Pitch jitter (XNA range [-1,1]). 0.18 = +/-9%.</summary>
        public float PitchJitter { get; init; } = 0.18f;

        /// <summary>RNG seed for hardness/letter pick + pitch jitter.</summary>
        public int RandomSeed { get; init; } = 0xF007;

        /// <summary>
        /// Subfolder under the audio root containing the footstep pack. Relative-path
        /// children of this directory are passed to <see cref="IFootstepAudioPlayer.Play"/>.
        /// </summary>
        public string PackSubPath { get; init; } =
            @"Audio\AutoFootStepsPlugin_Godot_0.8.2\addons\ovani_auto_footsteps\DefaultSounds";

        /// <summary>
        /// Material → folder name mapping. The pack's filenames also include the material
        /// name; "Water or Mud" is special-cased to read "Water" in filenames.
        /// </summary>
        public IReadOnlyDictionary<FootstepMaterial, string> FolderNames { get; init; }
            = new Dictionary<FootstepMaterial, string>
            {
                { FootstepMaterial.Grass,  "Grass" },
                { FootstepMaterial.Stone,  "Stone" },
                { FootstepMaterial.Sand,   "Sand" },
                { FootstepMaterial.Wood,   "Wood" },
                { FootstepMaterial.Snow,   "Snow" },
                { FootstepMaterial.Gravel, "Gravel" },
                { FootstepMaterial.Water,  "Water or Mud" },
                { FootstepMaterial.Metal,  "Metal" },
                { FootstepMaterial.Rug,    "Rug" },
            };

        public static FootstepAudioServiceConfig Default => new();
    }
}
