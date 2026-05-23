// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Tunable parameters for <see cref="WeatherAudioService"/>. All audio paths and
    /// volumes are data-driven so the gump (or a future JSON config) can rebind clips
    /// without code changes.
    /// </summary>
    public sealed class WeatherAudioServiceConfig
    {
        public bool InitialEnabled { get; init; } = true;
        public float InitialAmbientVolume { get; init; } = 0.55f;
        public float InitialThunderVolume { get; init; } = 0.85f;
        public float CrossfadeSeconds { get; init; } = 1.4f;
        public bool VerboseLog { get; init; } = false;

        /// <summary>RNG seed for thunder clip selection. Deterministic for tests/replay.</summary>
        public int RandomSeed { get; init; } = 0xCAFE;

        /// <summary>
        /// Per-weather ambient loop relative paths. Missing entries (e.g., None / Snow /
        /// BloodMoon) play no loop and the previous loop fades out.
        /// </summary>
        public IReadOnlyDictionary<WeatherKind, string> AmbientLoops { get; init; }
            = new Dictionary<WeatherKind, string>
            {
                { WeatherKind.Rain,
                  @"Audio/OvaniAmbiencePlugin/ExampleScene/Sounds/Quiet_Suburban_Rain_2.wav" },
                { WeatherKind.Storm,
                  @"PhatAudio/ultimateambientmusicbundle_volume5/ultimateambientmusicbundle_volume5/Thunderstorm/1 - Phat Phrog Studios - Thunderstorms Ambience.wav" },
                { WeatherKind.Fog,
                  @"Audio/OvaniAmbiencePlugin/Ambience Loops/Roomtone Night Loop.wav" },
                { WeatherKind.Tornado,
                  @"Audio/OvaniAmbiencePlugin/Ambience Loops/Windy Field Loop.wav" },
            };

        /// <summary>Thunder one-shot clip pool — one is picked at random per LightningStruckEvent.</summary>
        public IReadOnlyList<string> ThunderOneShots { get; init; } = new[]
        {
            @"Audio/Magic II/Air and Thunder/Thunder Strike 001.wav",
            @"Audio/Magic II/Air and Thunder/Thunder Strike 002.wav",
            @"Audio/Magic II/Air and Thunder/Thunder Strike 003.wav",
            @"Audio/Magic II/Air and Thunder/Thunder Strike 004.wav",
        };

        /// <summary>Thunder pitch jitter range (XNA pitch is [-1,1]).</summary>
        public float ThunderPitchJitter { get; init; } = 0.1f;

        public static WeatherAudioServiceConfig Default => new();
    }
}
