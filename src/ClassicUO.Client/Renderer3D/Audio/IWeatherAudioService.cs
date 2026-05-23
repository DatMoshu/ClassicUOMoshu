// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Drives weather ambience (looped beds with crossfade on type change) and thunder
    /// one-shots. Subscribes to <see cref="WeatherChangedEvent"/> and
    /// <see cref="LightningStruckEvent"/>; ticks each frame to advance crossfade volumes.
    /// </summary>
    public interface IWeatherAudioService
    {
        // ===== Read state =====

        bool Enabled { get; }
        float AmbientVolume { get; }
        float ThunderVolume { get; }
        float CrossfadeSeconds { get; }

        /// <summary>
        /// Diagnostic flag — when true, future log statements inside the service emit.
        /// The current implementation contains no log calls (no spam in steady state);
        /// the flag exists so the WeatherAdminGump's "Verbose log" toggle has a service
        /// home and so future tracing can gate on it without touching the contract.
        /// Mutable at runtime; initial value comes from <see cref="WeatherAudioServiceConfig.VerboseLog"/>.
        /// </summary>
        bool VerboseLog { get; }

        /// <summary>Currently playing ambient loop's weather kind. <see cref="WeatherKind.None"/> when silent.</summary>
        WeatherKind ActiveType { get; }

        // ===== Mutate state =====

        void SetEnabled(bool enabled);
        void SetAmbientVolume(float volume);
        void SetThunderVolume(float volume);
        void SetVerboseLog(bool verbose);

        /// <summary>Stop all currently-playing audio (ambient + any thunder mid-flight is unaffected).</summary>
        void StopAll();

        /// <summary>
        /// Force a fresh crossfade — useful after the operator changed <see cref="AmbientVolume"/>
        /// and wants the active loop to re-skin to the new level.
        /// </summary>
        void Refresh();
    }
}
