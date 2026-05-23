// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct ParticleServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly ParticleServiceConfig Config;

        public ParticleServiceConfigLoadResult(bool success, string errorMessage, string configPath, ParticleServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IParticleServiceConfigStorage
    {
        ParticleServiceConfigLoadResult Load();
    }
}
