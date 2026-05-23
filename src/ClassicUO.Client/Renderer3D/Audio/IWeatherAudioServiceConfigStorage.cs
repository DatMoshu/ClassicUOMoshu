// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Audio
{
    public readonly struct WeatherAudioServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly WeatherAudioServiceConfig Config;

        public WeatherAudioServiceConfigLoadResult(bool success, string errorMessage, string configPath, WeatherAudioServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="WeatherAudioServiceConfig"/>. JSON load covers
    /// scalar tunables only (volumes, crossfade, jitter, seed). Audio bank dictionaries
    /// (<see cref="WeatherAudioServiceConfig.AmbientLoops"/>, <see cref="WeatherAudioServiceConfig.ThunderOneShots"/>)
    /// stay at <see cref="WeatherAudioServiceConfig.Default"/> values until a follow-up
    /// session extends the JSON parser.
    /// </summary>
    public interface IWeatherAudioServiceConfigStorage
    {
        WeatherAudioServiceConfigLoadResult Load();
    }
}
