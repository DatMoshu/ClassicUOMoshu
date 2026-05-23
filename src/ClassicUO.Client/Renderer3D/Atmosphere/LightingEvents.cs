// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Published by <see cref="ILightingService"/> when the sun direction changes
    /// (typically once per frame while auto-cycling, or on a manual time-of-day setter).
    /// Subscribers: shader uniform updaters, sky color drivers, fog modulators.
    /// </summary>
    public readonly struct SunDirChangedEvent
    {
        /// <summary>Toward-sun unit vector. Identical to <see cref="ILightingService.CurrentLightDir"/>.</summary>
        public readonly Vector3 LightDir;

        /// <summary>Time of day when the event fired, hours in [0,24).</summary>
        public readonly float TimeOfDay;

        /// <summary>Whether realtime lighting is active. False = legacy hardcoded fallback dir.</summary>
        public readonly bool Enabled;

        public SunDirChangedEvent(Vector3 lightDir, float timeOfDay, bool enabled)
        {
            LightDir = lightDir;
            TimeOfDay = timeOfDay;
            Enabled = enabled;
        }
    }
}
