// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Persistence abstraction for the <see cref="WeatherDefaultsService"/>'s override map.
    /// Production-side reads/writes <c>weather-defaults.json</c>; tests use an in-memory fake.
    /// </summary>
    public interface IWeatherDefaultsStorage
    {
        /// <summary>Path of the most recent successful save (for UI display). Null until first save.</summary>
        string LastSavedPath { get; }

        /// <summary>Last error message from a failed Load or Save, or null if all calls succeeded.</summary>
        string LastError { get; }

        /// <summary>
        /// Read overrides from storage into the supplied dictionary. Returns true on success
        /// (even if the file did not exist — in that case the dictionary is left empty).
        /// Returns false on parse/IO error; <see cref="LastError"/> holds details.
        /// </summary>
        bool TryLoad(IDictionary<WeatherKind, WeatherOverrideRecord> destination);

        /// <summary>
        /// Persist <paramref name="overrides"/> to storage. Updates <see cref="LastSavedPath"/>
        /// on success. Returns false on IO error; <see cref="LastError"/> holds details.
        /// </summary>
        bool Save(IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> overrides);
    }
}
