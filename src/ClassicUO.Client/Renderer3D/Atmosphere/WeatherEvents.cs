// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Published by <see cref="IWeatherService.SetType"/> when the active weather kind
    /// changes. Subscribers: weather-audio service, atmospheric profile applier, foliage
    /// sway driver.
    /// </summary>
    public readonly struct WeatherChangedEvent
    {
        /// <summary>Weather the system is now in.</summary>
        public readonly WeatherKind Current;

        /// <summary>Weather the system was in immediately before the transition.</summary>
        public readonly WeatherKind Previous;

        public WeatherChangedEvent(WeatherKind current, WeatherKind previous)
        {
            Current = current;
            Previous = previous;
        }
    }

    /// <summary>
    /// Published by <see cref="IWeatherService.TriggerLightning"/>. Subscribers: weather
    /// audio (thunder clap), screen-flash post-process, gameplay event hooks.
    /// </summary>
    public readonly struct LightningStruckEvent
    {
        /// <summary>World-space strike origin near the player. May be <c>Vector3.Zero</c>
        /// when no anchor has been configured (the legacy code triggers anyway and lets
        /// the audio fire as a generic clap).</summary>
        public readonly Vector3 Origin;

        public LightningStruckEvent(Vector3 origin)
        {
            Origin = origin;
        }
    }
}
