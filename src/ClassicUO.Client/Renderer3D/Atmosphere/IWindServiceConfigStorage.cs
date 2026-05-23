// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Loaded <see cref="WindServiceConfig"/> + diagnostics. On failure the production
    /// adapter falls back to <see cref="WindServiceConfig.Default"/>; <see cref="Success"/>
    /// is false and <see cref="ErrorMessage"/> carries the explanation.
    /// </summary>
    public readonly struct WindServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly WindServiceConfig Config;

        public WindServiceConfigLoadResult(bool success, string errorMessage, string configPath, WindServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="WindServiceConfig"/>. Production reads
    /// <c>Data/renderer3d/wind-defaults.json</c>; tests can pre-build an in-memory config.
    /// Mirrors the storage-gateway pattern established by the session-66 Phase 4 pilot.
    /// </summary>
    public interface IWindServiceConfigStorage
    {
        WindServiceConfigLoadResult Load();
    }
}
