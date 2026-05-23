// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Production implementation of <see cref="ITreeSeasonService"/>. Pure-state service —
    /// no per-frame tick required. Setters invalidate the texture cache via the injected
    /// gateway so the renderer's next frame rebuilds with the new parameters.
    /// </summary>
    /// <remarks>
    /// <para>Allocation-free. No clock reads (state-only).</para>
    /// <para>Subscribes to <see cref="SeasonChangedEvent"/> as a diagnostic hook — the
    /// payload is tracked in <see cref="LastObservedSeasonChange"/> for tests/tools but
    /// does not drive state. Season classification uses different boundaries than the
    /// canonical <see cref="ISeasonService"/>; see <see cref="TreeSeasonServiceConfig"/>
    /// remarks.</para>
    /// </remarks>
    public sealed class TreeSeasonService : ITreeSeasonService, IDisposable
    {
        private readonly TreeSeasonServiceConfig _config;
        private readonly ITreeSeasonCacheGateway _cache;
        private readonly IDisposable _seasonChangedSubscription;

        // Mutable runtime state
        private bool _enabled;
        private TreeSeasonKind _season;
        private float _yearProgress;
        private float _snowAmount;
        private float _snowLineFrac;
        private float _hueShiftDeg;
        private float _saturationBoost = 1f;
        private bool _autoFromYear;
        private float _fallColorSharpness;

        // Diagnostic — last observed season-changed event from the bus.
        public SeasonChangedEvent? LastObservedSeasonChange { get; private set; }

        public TreeSeasonService(
            TreeSeasonServiceConfig config,
            IRendererEventBus bus,
            ITreeSeasonCacheGateway cache)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (bus is null) throw new ArgumentNullException(nameof(bus));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            _enabled = _config.InitialEnabled;
            _season = _config.InitialSeason;
            _yearProgress = Math.Clamp(_config.InitialYearProgress, 0f, 1f);
            _snowLineFrac = Math.Clamp(_config.InitialSnowLineFrac, 0.05f, 1f);
            _autoFromYear = _config.InitialAutoFromYear;
            _fallColorSharpness = _config.FallColorSharpness;

            _seasonChangedSubscription = bus.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
        }

        public void Dispose() => _seasonChangedSubscription?.Dispose();

        private void OnSeasonChanged(SeasonChangedEvent evt) => LastObservedSeasonChange = evt;

        // ===== ITreeSeasonService — read =====

        public bool Enabled => _enabled;
        public TreeSeasonKind Season => _season;
        public float YearProgress => _yearProgress;
        public float SnowAmount => _snowAmount;
        public float SnowLineFrac => _snowLineFrac;
        public float HueShiftDeg => _hueShiftDeg;
        public float SaturationBoost => _saturationBoost;
        public bool AutoFromYear => _autoFromYear;
        public float FallColorSharpness => _fallColorSharpness;

        // ===== ITreeSeasonService — mutate =====

        public void SetEnabled(bool enabled) => _enabled = enabled;

        public void SetSeason(TreeSeasonKind season)
        {
            if (_season == season) return;
            _season = season;
            _cache.InvalidateAll();
        }

        public void SetYearProgress(float yearProgress)
        {
            float clamped = Math.Clamp(yearProgress, 0f, 1f);
            if (_yearProgress == clamped) return;
            _yearProgress = clamped;
            if (_autoFromYear) RecomputeFromYear();
            _cache.InvalidateAll();
        }

        public void SetSnowAmount(float amount)
        {
            float clamped = Math.Clamp(amount, 0f, 1f);
            if (_snowAmount == clamped) return;
            _snowAmount = clamped;
            _cache.InvalidateAll();
        }

        public void SetSnowLineFrac(float frac)
        {
            float clamped = Math.Clamp(frac, 0.05f, 1f);
            if (_snowLineFrac == clamped) return;
            _snowLineFrac = clamped;
            _cache.InvalidateAll();
        }

        public void SetHueShiftDeg(float degrees)
        {
            float clamped = Math.Clamp(degrees, -180f, 180f);
            if (_hueShiftDeg == clamped) return;
            _hueShiftDeg = clamped;
            _cache.InvalidateAll();
        }

        public void SetSaturationBoost(float boost)
        {
            float clamped = Math.Clamp(boost, 0.1f, 2f);
            if (_saturationBoost == clamped) return;
            _saturationBoost = clamped;
            _cache.InvalidateAll();
        }

        public void SetAutoFromYear(bool autoFromYear)
        {
            if (_autoFromYear == autoFromYear) return;
            _autoFromYear = autoFromYear;
            if (_autoFromYear) RecomputeFromYear();
        }

        public void SetFallColorSharpness(float sharpness) => _fallColorSharpness = sharpness;

        public void SnapToSeason(TreeSeasonKind season)
        {
            _season = season;
            _yearProgress = SeasonCentre(season);
            _autoFromYear = true;
            RecomputeFromYear();
            _cache.InvalidateAll();
        }

        public int QuantisedCacheToken()
        {
            // Same packing as legacy TreeSeasonManager.QuantisedCacheToken.
            int snow     = (int)Math.Round(_snowAmount * 100f);
            int snowLine = (int)Math.Round(_snowLineFrac * 100f);
            int hue      = (int)Math.Round(_hueShiftDeg / 5f) + 64;
            int sat      = (int)Math.Round(_saturationBoost * 10f);
            int sharp = (int)Math.Round(
                (Math.Clamp(_fallColorSharpness, 0.25f, 3.0f) - 0.25f) * 15f / 2.75f);
            return (snow & 0x7F)
                 | ((snowLine & 0x7F) << 7)
                 | ((hue & 0x7F) << 14)
                 | ((sat & 0x1F) << 21)
                 | ((_enabled ? 1 : 0) << 26)
                 | ((sharp & 0xF) << 27);
        }

        // ===== Internals =====

        private static float SeasonCentre(TreeSeasonKind s) => s switch
        {
            TreeSeasonKind.Spring => 0.0f,
            TreeSeasonKind.Summer => 0.25f,
            TreeSeasonKind.Autumn => 0.5f,
            TreeSeasonKind.Winter => 0.75f,
            _ => 0.25f,
        };

        private void RecomputeFromYear()
        {
            float y = _yearProgress;

            _season = ClassifyTreeSeason(y, _config);
            _snowAmount = ComputeSnowOnTrees(y, _config);
            (_hueShiftDeg, _saturationBoost) = ComputeHueAndSat(y, _season);
        }

        /// <summary>Classify a yearProgress value using the legacy 0.25/0.50/0.75 boundaries.</summary>
        public static TreeSeasonKind ClassifyTreeSeason(float y, TreeSeasonServiceConfig cfg)
        {
            if (y < cfg.SpringSummerBoundary) return TreeSeasonKind.Spring;
            if (y < cfg.SummerAutumnBoundary) return TreeSeasonKind.Summer;
            if (y < cfg.AutumnWinterBoundary) return TreeSeasonKind.Autumn;
            return TreeSeasonKind.Winter;
        }

        /// <summary>
        /// Wraparound-continuous snow-on-trees curve. Same shape as the legacy
        /// <c>TreeSeasonManager.RecomputeFromYear</c> snow path.
        /// </summary>
        public static float ComputeSnowOnTrees(float y, TreeSeasonServiceConfig cfg)
        {
            if (y >= cfg.SnowRiseStart && y < cfg.SnowRiseEnd)
                return Smoothstep(cfg.SnowRiseStart, cfg.SnowRiseEnd, y);
            if (y >= cfg.SnowRiseEnd)
                return 1f - Smoothstep(cfg.SnowRiseEnd, cfg.SnowFallEnd, y);
            if (y < cfg.SnowFadeEnd)
                return 1f - Smoothstep(cfg.SnowFadeStart, cfg.SnowFadeEnd, y);
            return 0f;
        }

        /// <summary>
        /// Map year-progress + classified season to (hueShiftDeg, saturationBoost).
        /// Curves match legacy TreeSeasonManager exactly.
        /// </summary>
        public static (float hue, float sat) ComputeHueAndSat(float y, TreeSeasonKind season)
        {
            switch (season)
            {
                case TreeSeasonKind.Spring:
                {
                    float t = (y - 0.0f) / 0.25f;
                    return (-10f * (1f - t), 1.15f);
                }
                case TreeSeasonKind.Summer: return (0f, 1f);
                case TreeSeasonKind.Autumn:
                {
                    float t = (y - 0.5f) / 0.25f;
                    return (Lerp(0f, -45f, t), Lerp(1.0f, 1.15f, t));
                }
                case TreeSeasonKind.Winter:
                {
                    float t = (y - 0.75f) / 0.25f;
                    return (Lerp(-45f, -60f, t), Lerp(0.6f, 0.4f, t));
                }
                default: return (0f, 1f);
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = (x - edge0) / (edge1 - edge0);
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
