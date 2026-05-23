// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Loaded <see cref="LightingServiceConfig"/> + diagnostics. On failure the production
    /// adapter falls back to <see cref="LightingServiceConfig.Default"/>; <see cref="Success"/>
    /// is false and <see cref="ErrorMessage"/> carries the explanation.
    /// </summary>
    public readonly struct LightingServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly LightingServiceConfig Config;

        public LightingServiceConfigLoadResult(bool success, string errorMessage, string configPath, LightingServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="LightingServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/lighting-defaults.json</c>; tests can pre-build an in-memory config.
    /// Mirrors the session-67 wind-defaults storage-gateway pattern.
    /// </summary>
    public interface ILightingServiceConfigStorage
    {
        LightingServiceConfigLoadResult Load();
    }
}
