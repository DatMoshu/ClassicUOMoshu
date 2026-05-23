// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring ISeasonHostBridge to the legacy static singletons.
// Lives outside the World domain folder because it depends on concrete types in the legacy
// ClassicUO.Renderer.Renderer3D namespace; the service-side interface stays decoupled.

using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="ISeasonHostBridge"/> that mutates the legacy static subsystems
    /// the season cycle was previously coupled to. Each method maps 1:1 to the equivalent
    /// static field/method assignment from the original SeasonCycleDriver. As each
    /// downstream subsystem migrates to its own service, the corresponding methods here are
    /// rewritten to delegate to the new service instead of the static.
    /// </summary>
    internal sealed class LegacySeasonHostBridge : ISeasonHostBridge
    {
        public void SetTreeSeasonState(bool enabled, bool autoFromYear, float yearProgress)
        {
            TreeSeasonManager.Enabled = enabled;
            TreeSeasonManager.AutoFromYear = autoFromYear;
            TreeSeasonManager.YearProgress = yearProgress;
        }

        public void SetGroundEffect(SeasonGroundMode mode, float targetIntensity)
        {
            World3DRenderer.GroundEffectMode = MapGround(mode);
            World3DRenderer.TargetGroundEffectIntensity = targetIntensity;
        }

        public void SetFoliageSeason(SeasonFoliageMode mode, float intensity)
        {
            World3DRenderer.FoliageSeason = MapFoliage(mode);
            World3DRenderer.FoliageSeasonIntensity = intensity;
        }

        public void SetWeatherLinkSuspended(bool suspended)
            => World3DRenderer.LinkToWeather = !suspended;

        public float LightningFlashIntensity
        {
            get => World3DRenderer.LightningFlashIntensity;
            set => World3DRenderer.LightningFlashIntensity = value;
        }

        public SeasonWeatherKind GetWeatherType() => MapWeatherTo(Weather3DSystem.Type);
        public float GetWeatherIntensity() => Weather3DSystem.Intensity;

        public void SetWeatherType(SeasonWeatherKind type)
            => Weather3DSystem.SetType(MapWeatherFrom(type));

        public void SetWeatherIntensity(float intensity)
            => Weather3DSystem.Intensity = intensity;

        public void TriggerLightning() => Weather3DSystem.TriggerLightning();

        public float LeafPlaneWindAmpDeg
        {
            get => Static3DRenderer.LeafPlaneWindAmpDeg;
            set => Static3DRenderer.LeafPlaneWindAmpDeg = value;
        }

        public bool LeafPlaneWindEnabled
        {
            get => Static3DRenderer.LeafPlaneWindEnabled;
            set => Static3DRenderer.LeafPlaneWindEnabled = value;
        }

        public bool DropLeavesWorldwide
        {
            get => Static3DRenderer.DropLeavesWorldwide;
            set => Static3DRenderer.DropLeavesWorldwide = value;
        }

        public float LeafPresence
        {
            get => Static3DRenderer.LeafPresence;
            set => Static3DRenderer.LeafPresence = value;
        }

        public bool DefoliationStaggerEnabled => TreeDefoliationStagger.Enabled;

        public void ConfigureDefoliationStagger(float winterT)
            => TreeDefoliationStagger.Configure(winterT);

        public void EnableNukeShow() => NukeShow.Enabled = true;
        public void TriggerNukeBarrage() => NukeShow.TriggerBarrage();

        public bool? GetTreeSwayOverride(SeasonWeatherKind type)
            => WeatherDefaultsStore.GetTreeSwayEnabled(MapWeatherFrom(type));

        // ===== Enum maps — kept tight, single switch per direction =====

        private static World3DRenderer.GroundEffect MapGround(SeasonGroundMode m) => m switch
        {
            SeasonGroundMode.Wet  => World3DRenderer.GroundEffect.Wet,
            SeasonGroundMode.Snow => World3DRenderer.GroundEffect.Snow,
            _                     => World3DRenderer.GroundEffect.None,
        };

        private static World3DRenderer.FoliageSeasonMode MapFoliage(SeasonFoliageMode m) => m switch
        {
            SeasonFoliageMode.Fall => World3DRenderer.FoliageSeasonMode.Fall,
            _                      => World3DRenderer.FoliageSeasonMode.None,
        };

        private static Weather3DType MapWeatherFrom(SeasonWeatherKind k) => k switch
        {
            SeasonWeatherKind.Rain      => Weather3DType.Rain,
            SeasonWeatherKind.Snow      => Weather3DType.Snow,
            SeasonWeatherKind.Storm     => Weather3DType.Storm,
            SeasonWeatherKind.Fog       => Weather3DType.Fog,
            SeasonWeatherKind.Sandstorm => Weather3DType.Sandstorm,
            SeasonWeatherKind.Tornado   => Weather3DType.Tornado,
            SeasonWeatherKind.Embers    => Weather3DType.Embers,
            SeasonWeatherKind.BloodMoon => Weather3DType.BloodMoon,
            SeasonWeatherKind.Blizzard  => Weather3DType.Blizzard,
            _                           => Weather3DType.None,
        };

        private static SeasonWeatherKind MapWeatherTo(Weather3DType t) => t switch
        {
            Weather3DType.Rain      => SeasonWeatherKind.Rain,
            Weather3DType.Snow      => SeasonWeatherKind.Snow,
            Weather3DType.Storm     => SeasonWeatherKind.Storm,
            Weather3DType.Fog       => SeasonWeatherKind.Fog,
            Weather3DType.Sandstorm => SeasonWeatherKind.Sandstorm,
            Weather3DType.Tornado   => SeasonWeatherKind.Tornado,
            Weather3DType.Embers    => SeasonWeatherKind.Embers,
            Weather3DType.BloodMoon => SeasonWeatherKind.BloodMoon,
            Weather3DType.Blizzard  => SeasonWeatherKind.Blizzard,
            _                       => SeasonWeatherKind.None,
        };
    }
}
