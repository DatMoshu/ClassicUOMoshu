// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Abstraction over the audio-clip cache and playback engine. Production-side wraps
    /// XNA <c>SoundEffect</c> / <c>SoundEffectInstance</c>; tests use a recording fake.
    /// </summary>
    /// <remarks>
    /// The library owns clip caching, file-loading, and the underlying audio device. The
    /// service that uses it (e.g., <see cref="WeatherAudioService"/>) only knows about
    /// relative paths and volumes — never about <c>SoundEffect</c> or file I/O.
    /// </remarks>
    public interface IAudioClipLibrary
    {
        /// <summary>
        /// Play a one-shot clip at the given volume and pitch. Fire-and-forget — the library
        /// disposes the underlying instance when playback ends. No-op if the clip cannot be
        /// loaded (missing file, decode error).
        /// </summary>
        /// <param name="relativePath">Path relative to the audio root, '/' separators.</param>
        /// <param name="volume">[0,1].</param>
        /// <param name="pitch">[-1,1] (XNA convention).</param>
        void PlayOneShot(string relativePath, float volume, float pitch);

        /// <summary>
        /// Start a looping clip. Returns a handle for volume control and explicit stop.
        /// Returns null if the clip cannot be loaded.
        /// </summary>
        IAudioLoopHandle StartLoop(string relativePath, float initialVolume);
    }

    /// <summary>
    /// Live looping playback. Callers update volume during crossfade and call
    /// <see cref="IDisposable.Dispose"/> to stop and release the underlying audio instance.
    /// </summary>
    public interface IAudioLoopHandle : IDisposable
    {
        /// <summary>Update the loop's volume. Used for crossfade animation.</summary>
        void SetVolume(float volume);
    }
}
