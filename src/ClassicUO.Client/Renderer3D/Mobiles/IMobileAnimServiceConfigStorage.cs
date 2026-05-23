// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Mobiles
{
    public readonly struct MobileAnimServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly MobileAnimServiceConfig Config;

        public MobileAnimServiceConfigLoadResult(bool success, string errorMessage, string configPath, MobileAnimServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface IMobileAnimServiceConfigStorage
    {
        MobileAnimServiceConfigLoadResult Load();
    }
}
