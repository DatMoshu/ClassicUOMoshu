// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// This file used to be the canonical LightingState static singleton. It is now a thin
// facade that delegates to ILightingService via the Renderer3DHost migration locator.
// Scheduled for deletion in ADR-012 Phase 3 once every caller has migrated to read
// ILightingService directly.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Backwards-compatible facade over <see cref="ILightingService"/>. All reads/writes
    /// are forwarded to the active service via <see cref="Renderer3DHost"/>.
    /// </summary>
    [Obsolete("Use ILightingService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class LightingState
    {
        private static ILightingService Service => Renderer3DHost.Services.Lighting;

        /// <summary>Legacy constant exposed for tooling that read it directly. Kept until Phase 3.</summary>
        public static readonly Vector3 LegacyHardcodedDir = new Vector3(0f, 1f, 1f);

        /// <summary>Legacy constant — Formula F2 §F2 max sun elevation in radians (~70°).</summary>
        public const float MAX_ELEVATION = 1.22f;

        public static bool Enabled
        {
            get => Service.Enabled;
            set => Service.SetEnabled(value);
        }

        public static bool AutoCycle
        {
            get => Service.AutoCycle;
            set => Service.SetAutoCycle(value);
        }

        public static float SunTimeOfDay
        {
            get => Service.TimeOfDay;
            set => Service.SetTimeOfDay(value);
        }

        public static float CyclePeriodSeconds
        {
            get => Service.CyclePeriodSeconds;
            set => Service.SetCyclePeriodSeconds(value);
        }

        public static void SaveToProfile() => Service.SaveToProfile();

        public static void OnProfileLoaded() => Service.OnProfileLoaded();

        /// <summary>
        /// Legacy entry point. The service is ticked by <see cref="Renderer3DServices.Tick"/>;
        /// this method is now a no-op kept only so any straggler caller compiles.
        /// </summary>
        public static void Tick()
        {
            // No-op — LightingService is ticked by Renderer3DServices.Tick.
        }

        public static Vector3 ComputeSunDir(float t) => Service.ComputeSunDir(t);

        public static Vector3 CurrentLightDir() => Service.CurrentLightDir;
    }
}
