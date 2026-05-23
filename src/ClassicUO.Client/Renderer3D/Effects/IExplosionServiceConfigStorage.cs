// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct ExplosionServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly ExplosionServiceConfig Config;

        public ExplosionServiceConfigLoadResult(bool success, string errorMessage, string configPath, ExplosionServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IExplosionServiceConfigStorage
    {
        ExplosionServiceConfigLoadResult Load();
    }
}
