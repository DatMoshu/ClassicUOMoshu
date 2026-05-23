// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Per-weather override store + persistence. Lets users pin specific values for a
    /// weather kind (e.g., "Storm should always have BgColor=#080820 and intensity=0.85"),
    /// merges them on top of hardcoded defaults whenever <see cref="IWeatherService.SetType"/>
    /// fires, and saves/loads the pinned set as JSON.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>WeatherDefaultsStore</c> static class. The override map is
    /// exposed via <see cref="Overrides"/> so legacy-style direct dictionary edits in the
    /// gump continue to work; setters route through the service for invariant maintenance
    /// (e.g., recomputing <see cref="Count"/>, marking dirty for the next save).
    /// </remarks>
    public interface IWeatherDefaultsService
    {
        // ===== Read state =====

        IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> Overrides { get; }
        int Count { get; }
        string LastSavedPath { get; }
        string LastError { get; }

        // ===== Override CRUD =====

        bool TryGetOverride(WeatherKind kind, out WeatherOverrideRecord record);

        /// <summary>
        /// Replace (or insert) the override record for <paramref name="kind"/>.
        /// </summary>
        void SetOverride(WeatherKind kind, WeatherOverrideRecord record);

        /// <summary>Get-or-create a thin override row so the caller can mutate fields directly.</summary>
        WeatherOverrideRecord GetOrCreateOverride(WeatherKind kind);

        /// <summary>Drop the override for <paramref name="kind"/>. Idempotent.</summary>
        void Reset(WeatherKind kind);

        /// <summary>Drop all overrides.</summary>
        void ResetAll();

        // ===== Lookup helpers =====

        /// <summary>Per-weather sway-enable override; null when nothing is pinned.</summary>
        bool? GetTreeSwayEnabled(WeatherKind kind);

        // ===== Lifecycle =====

        /// <summary>Capture the currently-active live state into an override for <paramref name="kind"/>.</summary>
        void CaptureCurrent(WeatherKind kind);

        /// <summary>
        /// Apply the runtime-only knobs (Intensity / Radius / Height / leaf canopy state)
        /// for <paramref name="kind"/>. No-op when no override is pinned. Called by
        /// <c>Weather3DSystem.SetType</c> after the heavy-weather pin so user overrides win.
        /// </summary>
        void ApplyRuntimeOverrides(WeatherKind kind);

        /// <summary>Reload overrides from storage. Returns true on success.</summary>
        bool Load();

        /// <summary>Persist overrides to storage. Returns true on success.</summary>
        bool Save();
    }
}
