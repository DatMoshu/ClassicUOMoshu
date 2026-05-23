// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy WeatherDefaultsStore delegated to IWeatherDefaultsService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Client.cs — calls Load() at startup.
//   * Weather3DSystem.SetType — calls ApplyRuntimeOverrides + Merge.
//   * LegacySeasonHostBridge — calls GetTreeSwayEnabled.
//   * WeatherConfigGump — reads Overrides, calls CaptureCurrent / Reset / ResetAll / Save / Load,
//     and writes via the new SetOverride() helper (replaces direct dict assignment).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Backwards-compatible facade over <see cref="IWeatherDefaultsService"/>.
    /// </summary>
    [Obsolete("Use IWeatherDefaultsService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class WeatherDefaultsStore
    {
        /// <summary>
        /// Legacy DTO type. Now an empty subclass of <see cref="WeatherOverrideRecord"/> so
        /// straggler callers writing <c>new WeatherDefaultsStore.ProfileDto()</c> continue to
        /// compile. JSON shape is identical (same property names inherited from the parent).
        /// </summary>
        [Obsolete("Use ClassicUO.Renderer.Atmosphere.WeatherOverrideRecord. Will be removed in ADR-012 Phase 3.")]
        public sealed class ProfileDto : WeatherOverrideRecord { }

        private static IWeatherDefaultsService Svc => Renderer3DHost.Services.WeatherDefaults;

        // ===== Read-only surface (Overrides exposes the service's read-only view) =====

        /// <summary>
        /// Read-only view of the override map keyed by legacy <see cref="Weather3DType"/>.
        /// Writes go through <see cref="SetOverride"/> (the legacy <c>Overrides[k] = dto</c>
        /// pattern doesn't work against the service's <see cref="IReadOnlyDictionary{TKey, TValue}"/>).
        /// </summary>
        public static IReadOnlyDictionary<Weather3DType, WeatherOverrideRecord> Overrides
            => new EnumKeyMappingView(Svc.Overrides);

        public static int Count => Svc.Count;
        public static string LastSavedPath => Svc.LastSavedPath;
        public static string LastError => Svc.LastError;

        // ===== Write surface =====

        /// <summary>Insert/replace an override row. Replaces the legacy <c>Overrides[k] = dto</c> idiom.</summary>
        public static void SetOverride(Weather3DType type, WeatherOverrideRecord rec)
            => Svc.SetOverride((WeatherKind)(int)type, rec);

        public static void Reset(Weather3DType type) => Svc.Reset((WeatherKind)(int)type);
        public static void ResetAll() => Svc.ResetAll();

        // ===== Lookup helpers =====

        public static bool? GetTreeSwayEnabled(Weather3DType type)
            => Svc.GetTreeSwayEnabled((WeatherKind)(int)type);

        // ===== Lifecycle =====

        public static void CaptureCurrent(Weather3DType type) => Svc.CaptureCurrent((WeatherKind)(int)type);

        public static void ApplyRuntimeOverrides(Weather3DType type)
            => Svc.ApplyRuntimeOverrides((WeatherKind)(int)type);

        public static bool Load() => Svc.Load();
        public static bool Save() => Svc.Save();

        // ===== Merge — pure-data DTO → WeatherProfile struct merge =====
        // Stays in the facade because the WeatherProfile struct is owned by the
        // legacy Weather3DSystem class. When that simulation migrates, this method
        // moves into the new service.
        public static void Merge(Weather3DType type, ref Weather3DSystem.WeatherProfile p)
        {
            if (!Svc.TryGetOverride((WeatherKind)(int)type, out WeatherOverrideRecord dto)) return;

            // p.Gust is the legacy nested WindGustMode (still in this same namespace);
            // parse as that type directly so values round-trip without an enum cast.
            if (Enum.TryParse<ClassicUO.Renderer.Renderer3D.WindGustMode>(dto.Gust, ignoreCase: true, out ClassicUO.Renderer.Renderer3D.WindGustMode gm))
                p.Gust = gm;
            if (dto.WindStrength.HasValue) p.WindStrength = dto.WindStrength.Value;
            if (dto.WindFrequency.HasValue) p.WindFrequency = dto.WindFrequency.Value;
            if (dto.GustChangeMin.HasValue) p.GustChangeMin = dto.GustChangeMin.Value;
            if (dto.GustChangeMax.HasValue) p.GustChangeMax = dto.GustChangeMax.Value;
            if (dto.GustStrengthMin.HasValue) p.GustStrengthMin = dto.GustStrengthMin.Value;
            if (dto.GustStrengthMax.HasValue) p.GustStrengthMax = dto.GustStrengthMax.Value;
            if (dto.GustDirRangeDeg.HasValue) p.GustDirRangeDeg = dto.GustDirRangeDeg.Value;
            if (dto.LeafSwayAmpDeg.HasValue) p.LeafSwayAmpDeg = dto.LeafSwayAmpDeg.Value;
            if (dto.BgR.HasValue && dto.BgG.HasValue && dto.BgB.HasValue)
                p.BackgroundColor = new Color(dto.BgR.Value, dto.BgG.Value, dto.BgB.Value);
            if (dto.FogR.HasValue && dto.FogG.HasValue && dto.FogB.HasValue)
                p.FogColor = new Color(dto.FogR.Value, dto.FogG.Value, dto.FogB.Value);
            if (dto.AtmospherePulse.HasValue) p.AtmospherePulse = dto.AtmospherePulse.Value;
            if (dto.WindLinkToWeather.HasValue) p.WindLinkToWeather = dto.WindLinkToWeather.Value;
        }

        // ===== Enum-key view (WeatherKind → Weather3DType) =====
        // The service stores by WeatherKind; legacy callers iterate by Weather3DType.
        // Values are bit-identical (locked by WeatherServiceTests) so casts round-trip.
        private sealed class EnumKeyMappingView : IReadOnlyDictionary<Weather3DType, WeatherOverrideRecord>
        {
            private readonly IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> _src;
            public EnumKeyMappingView(IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> src) { _src = src; }

            public WeatherOverrideRecord this[Weather3DType key] => _src[(WeatherKind)(int)key];
            public IEnumerable<Weather3DType> Keys
            {
                get
                {
                    foreach (WeatherKind k in _src.Keys) yield return (Weather3DType)(int)k;
                }
            }
            public IEnumerable<WeatherOverrideRecord> Values => _src.Values;
            public int Count => _src.Count;
            public bool ContainsKey(Weather3DType key) => _src.ContainsKey((WeatherKind)(int)key);
            public bool TryGetValue(Weather3DType key, out WeatherOverrideRecord value)
                => _src.TryGetValue((WeatherKind)(int)key, out value);
            public IEnumerator<KeyValuePair<Weather3DType, WeatherOverrideRecord>> GetEnumerator()
            {
                foreach (KeyValuePair<WeatherKind, WeatherOverrideRecord> kvp in _src)
                    yield return new KeyValuePair<Weather3DType, WeatherOverrideRecord>((Weather3DType)(int)kvp.Key, kvp.Value);
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
