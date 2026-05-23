// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Statics
{
    public readonly struct WallNeighborClassifierConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly WallNeighborClassifierConfig Config;

        public WallNeighborClassifierConfigLoadResult(bool success, string errorMessage, string configPath, WallNeighborClassifierConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IWallNeighborClassifierConfigStorage
    {
        WallNeighborClassifierConfigLoadResult Load();
    }
}
