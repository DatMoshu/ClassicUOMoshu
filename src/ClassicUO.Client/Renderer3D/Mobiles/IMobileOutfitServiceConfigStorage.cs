// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Mobiles
{
    public readonly struct MobileOutfitServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly MobileOutfitServiceConfig Config;

        public MobileOutfitServiceConfigLoadResult(bool success, string errorMessage, string configPath, MobileOutfitServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="MobileOutfitServiceConfig"/>. JSON load
    /// covers <see cref="MobileOutfitServiceConfig.SerialSeedMixer"/> only;
    /// <see cref="MobileOutfitServiceConfig.OutfitSlots"/> array stays at default
    /// pending an array-aware extension to the parser.
    /// </summary>
    public interface IMobileOutfitServiceConfigStorage
    {
        MobileOutfitServiceConfigLoadResult Load();
    }
}
