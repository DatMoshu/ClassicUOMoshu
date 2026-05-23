// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.WorldEnv
{
    public readonly struct SeasonServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly SeasonServiceConfig Config;

        public SeasonServiceConfigLoadResult(bool success, string errorMessage, string configPath, SeasonServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="SeasonServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/season-defaults.json</c>.
    /// </summary>
    public interface ISeasonServiceConfigStorage
    {
        SeasonServiceConfigLoadResult Load();
    }
}
