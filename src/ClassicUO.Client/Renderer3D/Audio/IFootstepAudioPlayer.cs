// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Plays footstep audio clips with internal ring-buffer voice management to prevent
    /// XAudio2 voice leaks. Distinct from <see cref="IAudioClipLibrary"/> because footsteps
    /// fire 2-3x per second and the fire-and-forget one-shot path in <c>IAudioClipLibrary</c>
    /// would leak instances.
    /// </summary>
    public interface IFootstepAudioPlayer
    {
        /// <summary>
        /// Play <paramref name="relativePath"/> at <paramref name="volume"/> with
        /// <paramref name="pitch"/>. Returns true on successful dispatch (audio actually
        /// queued); false when the file is missing or the audio device rejected the call.
        /// </summary>
        bool Play(string relativePath, float volume, float pitch);

        /// <summary>True if the file at <paramref name="relativePath"/> exists in the audio root.</summary>
        bool Exists(string relativePath);
    }
}
