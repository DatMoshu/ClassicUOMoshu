// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Canonical weather classification owned by the Atmosphere domain. Values match the
    /// legacy <c>Weather3DType</c> ordering bit-for-bit so casts between the two enums
    /// round-trip during the transitional period.
    /// </summary>
    /// <remarks>
    /// Note: this ordering differs from <see cref="ClassicUO.Renderer.World.SeasonWeatherKind"/>
    /// (which was authored before <c>WeatherKind</c> existed). The
    /// <c>LegacySeasonHostBridge</c> maps between the two with an explicit switch.
    /// </remarks>
    public enum WeatherKind
    {
        None = 0,
        Rain = 1,
        Snow = 2,
        Storm = 3,
        Sandstorm = 4,
        Embers = 5,
        Fog = 6,
        BloodMoon = 7,
        Blizzard = 8,
        Tornado = 9,
    }
}
