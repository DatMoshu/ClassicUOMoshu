// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.World
{
    public readonly struct MousePickServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly MousePickServiceConfig Config;

        public MousePickServiceConfigLoadResult(bool success, string errorMessage, string configPath, MousePickServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IMousePickServiceConfigStorage
    {
        MousePickServiceConfigLoadResult Load();
    }
}
