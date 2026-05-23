// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring IWeatherDefaultsHost to the legacy
// Static3DRenderer + World3DRenderer state.

using System;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IWeatherDefaultsHost"/>. Reads/writes the legacy
    /// <c>Static3DRenderer</c> and reads <c>World3DRenderer</c>'s atmosphere fields.
    /// When those systems migrate to services, this adapter rewrites in place — the
    /// service-side contract is unchanged.
    /// </summary>
    internal sealed class LegacyWeatherDefaultsHost : IWeatherDefaultsHost
    {
        public WeatherDefaultsLegacyState ReadLegacyState()
        {
            return new WeatherDefaultsLegacyState(
                leafPlaneWindAmpDeg: Static3DRenderer.LeafPlaneWindAmpDeg,
                leafPlaneWindEnabled: Static3DRenderer.LeafPlaneWindEnabled,
                leafPlaneCount: Static3DRenderer.LeafPlaneCount,
                leafPlaneYawDeg: Static3DRenderer.LeafPlaneYawDeg,
                leafSwayMode: Static3DRenderer.LeafSwayMode.ToString(),
                leafSwayBobAmount: Static3DRenderer.LeafSwayBobAmount,
                leafSwayPerTreePhase: Static3DRenderer.LeafSwayPerTreePhase,
                leafSwayPerTreeAmount: Static3DRenderer.LeafSwayPerTreeAmount,
                backgroundColor: World3DRenderer.BackgroundColor,
                fogColor: World3DRenderer.FogColor,
                atmospherePulseAmount: World3DRenderer.AtmospherePulseAmount);
        }

        public void ApplyLeafCanopyOverrides(
            bool? leafSwayEnabled,
            int? leafPlaneCount,
            float? leafPlaneYawDeg,
            float? leafSwayBobAmount,
            bool? leafSwayPerTreePhase,
            float? leafSwayPerTreeAmount,
            string leafSwayMode)
        {
            if (leafSwayEnabled.HasValue) Static3DRenderer.LeafPlaneWindEnabled = leafSwayEnabled.Value;
            if (leafPlaneCount.HasValue) Static3DRenderer.LeafPlaneCount = leafPlaneCount.Value;
            if (leafPlaneYawDeg.HasValue) Static3DRenderer.LeafPlaneYawDeg = leafPlaneYawDeg.Value;
            if (leafSwayBobAmount.HasValue) Static3DRenderer.LeafSwayBobAmount = leafSwayBobAmount.Value;
            if (leafSwayPerTreePhase.HasValue) Static3DRenderer.LeafSwayPerTreePhase = leafSwayPerTreePhase.Value;
            if (leafSwayPerTreeAmount.HasValue) Static3DRenderer.LeafSwayPerTreeAmount = leafSwayPerTreeAmount.Value;

            if (!string.IsNullOrEmpty(leafSwayMode) &&
                Enum.TryParse<Static3DRenderer.LeafSwayModeT>(leafSwayMode, ignoreCase: true, out Static3DRenderer.LeafSwayModeT mode))
            {
                Static3DRenderer.LeafSwayMode = mode;
            }
        }
    }
}
