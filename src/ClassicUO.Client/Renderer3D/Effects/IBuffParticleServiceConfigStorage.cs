// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct BuffParticleServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly BuffParticleServiceConfig Config;

        public BuffParticleServiceConfigLoadResult(bool success, string errorMessage, string configPath, BuffParticleServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IBuffParticleServiceConfigStorage
    {
        BuffParticleServiceConfigLoadResult Load();
    }
}
