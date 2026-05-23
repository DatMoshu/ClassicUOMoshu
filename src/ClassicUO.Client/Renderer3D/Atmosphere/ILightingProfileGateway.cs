// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Abstraction over the four player-profile fields the lighting service persists:
    /// <c>RealtimeLightingEnabled</c>, <c>SunCycleAuto</c>, <c>SunTimeOfDay</c>,
    /// <c>SunCyclePeriodSeconds</c>. Lets <see cref="LightingService"/> stay decoupled
    /// from the concrete <c>ProfileManager</c> for testability and for future cases where
    /// profiles are loaded from non-standard sources (replays, demos, server push).
    /// </summary>
    public interface ILightingProfileGateway
    {
        /// <summary>True when a profile is loaded and ready to read.</summary>
        bool HasActiveProfile { get; }

        bool ReadEnabled();
        bool ReadAutoCycle();
        float ReadTimeOfDay();
        float ReadCyclePeriodSeconds();

        void WriteEnabled(bool enabled);
        void WriteAutoCycle(bool autoCycle);
        void WriteTimeOfDay(float timeOfDay);
        void WriteCyclePeriodSeconds(float seconds);
    }
}
