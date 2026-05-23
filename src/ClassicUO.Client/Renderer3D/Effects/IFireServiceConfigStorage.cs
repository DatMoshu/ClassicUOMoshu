// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct FireServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly FireServiceConfig Config;

        public FireServiceConfigLoadResult(bool success, string errorMessage, string configPath, FireServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IFireServiceConfigStorage
    {
        FireServiceConfigLoadResult Load();
    }
}
