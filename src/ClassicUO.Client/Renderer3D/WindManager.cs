// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// This file used to be the canonical WindManager static singleton. It is now a thin
// facade that delegates every member to the IWindService registered in the renderer's
// service container, accessed through the Renderer3DHost migration locator.
//
// Scheduled for deletion in ADR-012 Phase 3, once every caller has migrated to read
// IWindService directly through constructor injection or via Renderer3DServices.
//
// Do NOT add new members here. New consumers should inject IWindService.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy gust-mode enum aliased to <see cref="ClassicUO.Renderer.Atmosphere.WindGustMode"/>.
    /// New code should use the Atmosphere-namespaced enum directly.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Atmosphere.WindGustMode. Will be removed in ADR-012 Phase 3.")]
    internal enum WindGustMode
    {
        None     = 0,
        Steady   = 1,
        Variable = 2,
        Storm    = 3,
    }

    /// <summary>
    /// Backwards-compatible facade over <see cref="IWindService"/>. All reads/writes are
    /// forwarded to the active service instance via <see cref="Renderer3DHost"/>.
    /// </summary>
    /// <remarks>
    /// <b>Scheduled for deletion in ADR-012 Phase 3.</b> Migrate callers to constructor-injected
    /// <see cref="IWindService"/>. The <c>[Obsolete]</c> attribute on the type drives the migration.
    /// </remarks>
    [Obsolete("Use IWindService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class WindManager
    {
        private static IWindService Service => Renderer3DHost.Services.Wind;

        // ===== Read state (delegated) =====
        public static float Strength
        {
            get => Service.Strength;
            set => Service.SetStrength(value);
        }

        public static float DirectionDeg
        {
            get => Service.DirectionDeg;
            set => Service.SetDirectionDeg(value);
        }

        public static float Frequency
        {
            get => Service.Frequency;
            set => Service.SetFrequency(value);
        }

        public static float Sample => Service.Sample;
        public static float Phase => Service.Phase;
        public static Vector2 VectorXZ => Service.VectorXZ;

        public static bool LinkToWeather
        {
            get => Service.LinkToWeather;
            set => Service.SetLinkToWeather(value);
        }

        public static float WeatherWindStrength
        {
            get => Service.WeatherWindStrength;
            set => Service.SetWeatherWindStrength(value);
        }

        // ===== Gust mode (legacy enum delegated) =====
        public static WindGustMode GustMode
        {
            get => (WindGustMode)(int)Service.GustMode;
            set => Service.SetGustMode((ClassicUO.Renderer.Atmosphere.WindGustMode)(int)value);
        }

        public static float GustChangeMin
        {
            get => Service.GustChangeMin;
            set => Service.SetGustChangeMin(value);
        }
        public static float GustChangeMax
        {
            get => Service.GustChangeMax;
            set => Service.SetGustChangeMax(value);
        }
        public static float GustStrengthMin
        {
            get => Service.GustStrengthMin;
            set => Service.SetGustStrengthMin(value);
        }
        public static float GustStrengthMax
        {
            get => Service.GustStrengthMax;
            set => Service.SetGustStrengthMax(value);
        }
        public static float GustDirectionRangeDeg
        {
            get => Service.GustDirectionRangeDeg;
            set => Service.SetGustDirectionRangeDeg(value);
        }
        public static float GustLerpSpeed
        {
            get => Service.GustLerpSpeed;
            set => Service.SetGustLerpSpeed(value);
        }

        public static float SampleAt(float phaseOffsetRad) => Service.SampleAt(phaseOffsetRad);

        /// <summary>
        /// Legacy entry point. The legacy code path called <c>WindManager.Tick(dt)</c> from
        /// the renderer. The new architecture ticks <see cref="WindService"/> via
        /// <see cref="Renderer3DServices.Tick"/>; this method is now a no-op kept only so
        /// any straggler caller compiles. Delete in Phase 3.
        /// </summary>
        public static void Tick(float dt)
        {
            // No-op. WindService is ticked by Renderer3DServices.Tick.
        }
    }
}
