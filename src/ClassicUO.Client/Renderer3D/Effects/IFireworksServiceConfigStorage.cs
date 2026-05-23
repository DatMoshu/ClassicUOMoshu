// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct FireworksServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly FireworksServiceConfig Config;

        public FireworksServiceConfigLoadResult(bool success, string errorMessage, string configPath, FireworksServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IFireworksServiceConfigStorage
    {
        FireworksServiceConfigLoadResult Load();
    }
}
