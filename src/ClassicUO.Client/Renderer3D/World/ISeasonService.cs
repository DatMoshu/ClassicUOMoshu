// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Authoritative source of seasonal cycle state. Drives the continuous
    /// Spring→Summer→Autumn→Winter→Spring loop and publishes
    /// <see cref="SeasonChangedEvent"/> at coarse-season transitions.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>SeasonCycleDriver</c> static class. Closes review finding #2
    /// for this subsystem (the legacy code read <c>Environment.TickCount64</c> from two
    /// independent timers; the service ticks from <see cref="ClassicUO.Renderer.Core.IFrameClock"/>).
    /// </remarks>
    public interface ISeasonService
    {
        // ===== Read state =====

        /// <summary>Whether the cycle is advancing each tick.</summary>
        bool Enabled { get; }

        /// <summary>Continuous progress in [0,1) for natural mode, [0,2) for showcase mode.</summary>
        float Progress { get; }

        /// <summary>Year phase in [0,1). Identical to <see cref="Progress"/> outside showcase mode.</summary>
        float YearPhase { get; }

        /// <summary>Coarse current season label.</summary>
        Season CurrentSeason { get; }

        /// <summary>Showcase mode — 2-year cycle with year-2 layered weather events.</summary>
        bool ShowcaseMode { get; }

        /// <summary>Period of one full season cycle, in seconds. Initial value comes from <see cref="SeasonServiceConfig.SecondsPerYear"/>.</summary>
        float SecondsPerYear { get; }

        /// <summary>When true, the cycle drives rain/snow particles in lockstep with phase.</summary>
        bool DriveWeatherParticles { get; }

        /// <summary>When true, leaves regrow on the spring transition.</summary>
        bool RegrowInSpring { get; }

        /// <summary>When true, weather extremes (Storm/Blizzard/Sandstorm) auto-enable tree sway.</summary>
        bool DriveSwayFromWeather { get; }

        /// <summary>When true, the FoliageSeason=Fall shader pass also fires alongside per-species recolor.</summary>
        bool DriveFoliageShaderTint { get; }

        /// <summary>Diagnostic phase string for the gump readout. Set by the most recent tick.</summary>
        string LastPhase { get; }

        /// <summary>True iff the most recent tick wrote subsystem state (false when disabled).</summary>
        bool LastTickWrote { get; }

        // ===== Mutate state =====

        void SetEnabled(bool enabled);
        void SetSecondsPerYear(float seconds);
        void SetShowcaseMode(bool showcase);
        void SetDriveWeatherParticles(bool drive);
        void SetRegrowInSpring(bool regrow);
        void SetDriveSwayFromWeather(bool drive);
        void SetDriveFoliageShaderTint(bool drive);

        /// <summary>
        /// Re-seed Progress to a season boundary and immediately apply state so the user
        /// sees the snap without waiting for the next frame. Fires
        /// <see cref="SeasonChangedEvent"/> if the snap crosses a season boundary.
        /// </summary>
        void SnapTo(float progress);

        /// <summary>
        /// Stop the cycle and clear effects driven by it (so the user gets a clean baseline
        /// back). Idempotent.
        /// </summary>
        void Stop();
    }
}
