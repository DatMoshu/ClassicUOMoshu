// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using System;

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IFoliageSeasonService"/>.
    /// </summary>
    public sealed class FoliageSeasonService : IFoliageSeasonService
    {
        private readonly IFoliageSeasonBridge _bridge;

        public FoliageSeasonService(IFoliageSeasonBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public FoliageSeasonMode Mode => _bridge.Mode;
        public float Intensity => _bridge.Intensity;

        public void SetMode(FoliageSeasonMode mode) => _bridge.Mode = mode;
        public void SetIntensity(float intensity) => _bridge.Intensity = intensity;
    }
}
