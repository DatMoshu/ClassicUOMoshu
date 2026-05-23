// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct AmbientMotesServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly AmbientMotesServiceConfig Config;

        public AmbientMotesServiceConfigLoadResult(bool success, string errorMessage, string configPath, AmbientMotesServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IAmbientMotesServiceConfigStorage
    {
        AmbientMotesServiceConfigLoadResult Load();
    }
}
