// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.WorldEnv
{
    public readonly struct TreeSeasonServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly TreeSeasonServiceConfig Config;

        public TreeSeasonServiceConfigLoadResult(bool success, string errorMessage, string configPath, TreeSeasonServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="TreeSeasonServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/tree-season-defaults.json</c>.
    /// </summary>
    public interface ITreeSeasonServiceConfigStorage
    {
        TreeSeasonServiceConfigLoadResult Load();
    }
}
