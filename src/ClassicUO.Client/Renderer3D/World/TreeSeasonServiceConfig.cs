// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Tunable parameters for <see cref="TreeSeasonService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>TreeSeasonManager</c>.
    /// </summary>
    /// <remarks>
    /// Boundaries here match the legacy <c>TreeSeasonManager.RecomputeFromYear</c>
    /// (0.25/0.50/0.75). These are <b>different</b> from the <c>SeasonService</c> boundaries
    /// (0.05/0.20/0.50/0.78) — the two systems classify season differently. Reconciling them
    /// is a deliberate behavior change deferred to a future cleanup PR; this migration
    /// preserves legacy behavior.
    /// </remarks>
    public sealed class TreeSeasonServiceConfig
    {
        // ===== Season boundaries on YearProgress (preserved from legacy) =====

        public float SpringSummerBoundary { get; init; } = 0.25f;
        public float SummerAutumnBoundary { get; init; } = 0.50f;
        public float AutumnWinterBoundary { get; init; } = 0.75f;

        // ===== Snow-on-trees curve (wraparound continuous) =====

        public float SnowRiseStart { get; init; } = 0.78f;
        public float SnowRiseEnd { get; init; } = 0.95f;
        public float SnowFallEnd { get; init; } = 1.10f;     // continues past y=1.0; legacy wrap math
        public float SnowFadeStart { get; init; } = -0.05f;  // before y=0.0; legacy wrap math
        public float SnowFadeEnd { get; init; } = 0.10f;

        // ===== Initial values =====

        public bool InitialEnabled { get; init; } = false;
        public float InitialYearProgress { get; init; } = 0.25f;
        public TreeSeasonKind InitialSeason { get; init; } = TreeSeasonKind.Summer;
        public float InitialSnowLineFrac { get; init; } = 0.4f;
        public float FallColorSharpness { get; init; } = 1f;
        public bool InitialAutoFromYear { get; init; } = true;

        public static TreeSeasonServiceConfig Default => new();
    }

    /// <summary>
    /// Coarse tree-season label as used by the legacy TreeSeasonManager. Distinct from the
    /// canonical <see cref="Season"/> enum because the legacy boundaries differ — see
    /// <see cref="TreeSeasonServiceConfig"/> remarks.
    /// </summary>
    public enum TreeSeasonKind : byte
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }
}
