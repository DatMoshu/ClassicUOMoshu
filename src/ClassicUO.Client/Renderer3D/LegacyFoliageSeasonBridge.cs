// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer foliage-season state to IFoliageSeasonBridge.
// Replaced when the foliage shader pass migrates into a renderer service.

using ClassicUO.Renderer.Environment;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyFoliageSeasonBridge : IFoliageSeasonBridge
    {
        public FoliageSeasonMode Mode
        {
            get => (FoliageSeasonMode)(int)World3DRenderer.FoliageSeason;
            set => World3DRenderer.FoliageSeason = (World3DRenderer.FoliageSeasonMode)(int)value;
        }

        public float Intensity
        {
            get => World3DRenderer.FoliageSeasonIntensity;
            set => World3DRenderer.FoliageSeasonIntensity = value;
        }
    }
}
