// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Authoritative source of realtime lighting state — sun direction, time of day,
    /// and the day/night auto-cycle. Replaces the legacy <c>LightingState</c> static class.
    /// </summary>
    /// <remarks>
    /// Closes review finding #2 for this subsystem (legacy <c>LightingState.Tick</c>
    /// read <c>Environment.TickCount64</c> directly; the service now ticks from
    /// <see cref="ClassicUO.Renderer.Core.IFrameClock"/>).
    /// </remarks>
    public interface ILightingService
    {
        // ===== Read state =====

        /// <summary>Whether realtime lighting is active. When false, <see cref="CurrentLightDir"/> returns the legacy fallback.</summary>
        bool Enabled { get; }

        /// <summary>Whether the sun auto-cycles through the day. Only meaningful when <see cref="Enabled"/>.</summary>
        bool AutoCycle { get; }

        /// <summary>Current sun time-of-day in hours, [0,24).</summary>
        float TimeOfDay { get; }

        /// <summary>Seconds per full 24-hour cycle when <see cref="AutoCycle"/> is on.</summary>
        float CyclePeriodSeconds { get; }

        /// <summary>
        /// Toward-sun unit vector consumed by the lit shader's <c>dot(N, LightDir)</c>.
        /// Returns the legacy hardcoded fallback when <see cref="Enabled"/> is false.
        /// </summary>
        Vector3 CurrentLightDir { get; }

        /// <summary>
        /// Compute the sun direction at an arbitrary time of day without touching state.
        /// Useful for UI previews and for tests.
        /// </summary>
        Vector3 ComputeSunDir(float timeOfDay);

        // ===== Mutate state =====

        void SetEnabled(bool enabled);
        void SetAutoCycle(bool autoCycle);
        /// <summary>Set time of day. Wraps to [0,24).</summary>
        void SetTimeOfDay(float timeOfDay);
        /// <summary>Set the cycle period. Floor-clamped to a minimum to prevent divide-by-zero.</summary>
        void SetCyclePeriodSeconds(float seconds);

        // ===== Persistence =====

        /// <summary>
        /// Save the current state to the active player profile. Called by UI (e.g., the
        /// lighting gump's apply button) and at scene unload.
        /// </summary>
        void SaveToProfile();

        /// <summary>
        /// Reset the profile-load latch so the next tick re-reads from the active profile.
        /// Called when a profile is loaded after the service was already running (login mid-session).
        /// </summary>
        void OnProfileLoaded();
    }
}
