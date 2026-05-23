// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012 Phase 4).

namespace ClassicUO.Renderer.Audio
{
    public readonly struct FootstepAudioServiceConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly FootstepAudioServiceConfig Config;

        public FootstepAudioServiceConfigLoadResult(bool success, string errorMessage, string configPath, FootstepAudioServiceConfig config)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Config = config;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="FootstepAudioServiceConfig"/>. JSON load
    /// covers scalar + enum fields; <see cref="FootstepAudioServiceConfig.FolderNames"/>
    /// stays at default until a follow-up extends the parser.
    /// </summary>
    public interface IFootstepAudioServiceConfigStorage
    {
        FootstepAudioServiceConfigLoadResult Load();
    }
}
