// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy TreeSeasonManager delegated to ITreeSeasonService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.

using System;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Backwards-compatible facade over <see cref="ClassicUO.Renderer.World.ITreeSeasonService"/>.
    /// </summary>
    [Obsolete("Use ITreeSeasonService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class TreeSeasonManager
    {
        // Legacy nested enum — aliased to the new TreeSeasonKind so callers comparing
        // TreeSeasonManager.TreeSeason.Spring etc. still compile.
        public enum TreeSeason : byte
        {
            Spring = 0,
            Summer = 1,
            Autumn = 2,
            Winter = 3,
        }

        private static ClassicUO.Renderer.World.ITreeSeasonService Service
            => Renderer3DHost.Services.TreeSeason;

        public static bool Enabled
        {
            get => Service.Enabled;
            set => Service.SetEnabled(value);
        }

        public static TreeSeason Season
        {
            get => (TreeSeason)(int)Service.Season;
            set => Service.SetSeason((ClassicUO.Renderer.World.TreeSeasonKind)(int)value);
        }

        public static float YearProgress
        {
            get => Service.YearProgress;
            set => Service.SetYearProgress(value);
        }

        public static float SnowAmount
        {
            get => Service.SnowAmount;
            set => Service.SetSnowAmount(value);
        }

        public static float SnowLineFrac
        {
            get => Service.SnowLineFrac;
            set => Service.SetSnowLineFrac(value);
        }

        public static float HueShiftDeg
        {
            get => Service.HueShiftDeg;
            set => Service.SetHueShiftDeg(value);
        }

        public static float SaturationBoost
        {
            get => Service.SaturationBoost;
            set => Service.SetSaturationBoost(value);
        }

        public static bool AutoFromYear
        {
            get => Service.AutoFromYear;
            set => Service.SetAutoFromYear(value);
        }

        public static float FallColorSharpness
        {
            get => Service.FallColorSharpness;
            set => Service.SetFallColorSharpness(value);
        }

        public static void SnapToSeason(TreeSeason s)
            => Service.SnapToSeason((ClassicUO.Renderer.World.TreeSeasonKind)(int)s);

        public static int QuantisedCacheToken() => Service.QuantisedCacheToken();
    }
}
