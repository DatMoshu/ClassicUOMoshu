// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.World
{
    public readonly struct LeafFallServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly LeafFallServiceConfig Config;

        public LeafFallServiceConfigLoadResult(bool success, string errorMessage, string configPath, LeafFallServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="LeafFallServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/leaf-fall-defaults.json</c>.
    /// </summary>
    public interface ILeafFallServiceConfigStorage
    {
        LeafFallServiceConfigLoadResult Load();
    }
}
