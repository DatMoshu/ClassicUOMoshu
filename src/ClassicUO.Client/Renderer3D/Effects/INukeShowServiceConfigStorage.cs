// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Effects
{
    public readonly struct NukeShowServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly NukeShowServiceConfig Config;

        public NukeShowServiceConfigLoadResult(bool success, string errorMessage, string configPath, NukeShowServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    public interface INukeShowServiceConfigStorage
    {
        NukeShowServiceConfigLoadResult Load();
    }
}
