// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Atmosphere
{
    public readonly struct WeatherServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly WeatherServiceConfig Config;

        public WeatherServiceConfigLoadResult(bool success, string errorMessage, string configPath, WeatherServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="WeatherServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/weather-defaults.json</c>.
    /// </summary>
    public interface IWeatherServiceConfigStorage
    {
        WeatherServiceConfigLoadResult Load();
    }
}
