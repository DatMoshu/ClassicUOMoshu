// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Snapshot of the legacy renderer state read by
    /// <see cref="IWeatherDefaultsService.CaptureCurrent"/>. Bundled into a single struct so
    /// the bridge interface stays narrow and the service code is allocation-free.
    /// </summary>
    public readonly struct WeatherDefaultsLegacyState
    {
        // Static3DRenderer leaf canopy
        public readonly float LeafPlaneWindAmpDeg;
        public readonly bool LeafPlaneWindEnabled;
        public readonly int LeafPlaneCount;
        public readonly float LeafPlaneYawDeg;
        public readonly string LeafSwayMode;
        public readonly float LeafSwayBobAmount;
        public readonly bool LeafSwayPerTreePhase;
        public readonly float LeafSwayPerTreeAmount;

        // World3DRenderer atmosphere
        public readonly Color BackgroundColor;
        public readonly Color FogColor;
        public readonly float AtmospherePulseAmount;

        public WeatherDefaultsLegacyState(
            float leafPlaneWindAmpDeg,
            bool leafPlaneWindEnabled,
            int leafPlaneCount,
            float leafPlaneYawDeg,
            string leafSwayMode,
            float leafSwayBobAmount,
            bool leafSwayPerTreePhase,
            float leafSwayPerTreeAmount,
            Color backgroundColor,
            Color fogColor,
            float atmospherePulseAmount)
        {
            LeafPlaneWindAmpDeg = leafPlaneWindAmpDeg;
            LeafPlaneWindEnabled = leafPlaneWindEnabled;
            LeafPlaneCount = leafPlaneCount;
            LeafPlaneYawDeg = leafPlaneYawDeg;
            LeafSwayMode = leafSwayMode;
            LeafSwayBobAmount = leafSwayBobAmount;
            LeafSwayPerTreePhase = leafSwayPerTreePhase;
            LeafSwayPerTreeAmount = leafSwayPerTreeAmount;
            BackgroundColor = backgroundColor;
            FogColor = fogColor;
            AtmospherePulseAmount = atmospherePulseAmount;
        }
    }

    /// <summary>
    /// Bridge over the legacy <c>Static3DRenderer</c> + <c>World3DRenderer</c> state that
    /// <see cref="WeatherDefaultsService"/> reads (during capture) and writes (during
    /// runtime-override application). Service-side bridge: <see cref="IWindService"/> and
    /// <see cref="IWeatherService"/> are constructor-injected directly because they are
    /// already migrated.
    /// </summary>
    public interface IWeatherDefaultsHost
    {
        /// <summary>Snapshot the live state for <see cref="IWeatherDefaultsService.CaptureCurrent"/>.</summary>
        WeatherDefaultsLegacyState ReadLegacyState();

        /// <summary>
        /// Apply the override's leaf-canopy fields to the live <c>Static3DRenderer</c> state.
        /// Each parameter is null when not pinned.
        /// </summary>
        void ApplyLeafCanopyOverrides(
            bool? leafSwayEnabled,
            int? leafPlaneCount,
            float? leafPlaneYawDeg,
            float? leafSwayBobAmount,
            bool? leafSwayPerTreePhase,
            float? leafSwayPerTreeAmount,
            string leafSwayMode);
    }
}
