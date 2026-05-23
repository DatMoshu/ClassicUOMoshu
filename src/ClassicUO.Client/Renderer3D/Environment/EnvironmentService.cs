// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IEnvironmentService"/>. Every read+write
    /// flows through the supplied <see cref="IEnvironmentBridge"/>.
    /// </summary>
    public sealed class EnvironmentService : IEnvironmentService
    {
        private readonly IEnvironmentBridge _bridge;

        public EnvironmentService(IEnvironmentBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        // ----- Background -----

        public Color BackgroundColor => _bridge.BackgroundColor;
        public Color GetEffectiveClearColor() => _bridge.GetEffectiveClearColor();
        public void SetBackgroundColor(Color color) => _bridge.BackgroundColor = color;

        // ----- Fog -----

        public bool FogEnabled => _bridge.FogEnabled;
        public float FogStart => _bridge.FogStart;
        public float FogEnd => _bridge.FogEnd;
        public Color FogColor => _bridge.FogColor;

        public void SetFogEnabled(bool enabled) => _bridge.FogEnabled = enabled;
        public void SetFogStart(float start) => _bridge.FogStart = start;
        public void SetFogEnd(float end) => _bridge.FogEnd = end;
        public void SetFogColor(Color color) => _bridge.FogColor = color;

        // ----- Sky -----

        public SkyboxMode SkyMode => _bridge.SkyMode;
        public Color SkyTopColor => _bridge.SkyTopColor;
        public Color SkyHorizonColor => _bridge.SkyHorizonColor;
        public Color SkyBottomColor => _bridge.SkyBottomColor;
        public float SkyHorizonY => _bridge.SkyHorizonY;

        public void SetSkyMode(SkyboxMode mode) => _bridge.SkyMode = mode;
        public void SetSkyTopColor(Color color) => _bridge.SkyTopColor = color;
        public void SetSkyHorizonColor(Color color) => _bridge.SkyHorizonColor = color;
        public void SetSkyBottomColor(Color color) => _bridge.SkyBottomColor = color;
        public void SetSkyHorizonY(float y) => _bridge.SkyHorizonY = y;
    }
}
