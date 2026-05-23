// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy WeatherAudioSystem delegated to IWeatherAudioService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Particle3DSystem.Tick — calls WeatherAudioSystem.Tick(dt) (now no-op; service ticks itself).
//   * Weather3DSystem.TriggerLightning — calls WeatherAudioSystem.OnLightningStrike() (now no-op;
//     service subscribes to LightningStruckEvent on the bus).
//   * WeatherAdminGump — toggles Enabled / volumes via the property surface preserved here.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Audio;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IWeatherAudioService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class WeatherAudioSystem
    {
        private static IWeatherAudioService Svc => Renderer3DHost.Services.WeatherAudio;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static float AmbientVolume
        {
            get => Svc.AmbientVolume;
            set => Svc.SetAmbientVolume(value);
        }

        public static float ThunderVolume
        {
            get => Svc.ThunderVolume;
            set => Svc.SetThunderVolume(value);
        }

        public static float CrossfadeSeconds => Svc.CrossfadeSeconds;

        public static bool VerboseLog
        {
            get => Svc.VerboseLog;
            set => Svc.SetVerboseLog(value);
        }

        public static Weather3DType ActiveType => (Weather3DType)(int)Svc.ActiveType;

        // Legacy diagnostic — clip count is internal to the audio library now; the
        // facade returns 0 in this transitional window.
        public static int CachedClips => 0;

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this method is now a no-op kept so straggler callers (Particle3DSystem) compile.
        /// </summary>
        public static void Tick(float dt) { /* no-op */ }

        /// <summary>
        /// Legacy entry point. The service subscribes to <see cref="LightningStruckEvent"/>
        /// on the bus; this method is now a no-op kept so straggler callers
        /// (Weather3DSystem.TriggerLightning's transitional double-call) compile.
        /// </summary>
        public static void OnLightningStrike() { /* no-op */ }

        public static void StopAll() => Svc.StopAll();
        public static void Refresh() => Svc.Refresh();
    }
}
