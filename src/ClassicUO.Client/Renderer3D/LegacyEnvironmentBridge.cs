// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer background+fog+sky state to IEnvironmentBridge.
// Replaced when the per-frame sky/fog rendering migrates into a renderer service.

using ClassicUO.Renderer.EnvRender;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyEnvironmentBridge : IEnvironmentBridge
    {
        // ----- Background -----

        public Color BackgroundColor
        {
            get => World3DRenderer.BackgroundColor;
            set => World3DRenderer.BackgroundColor = value;
        }

        public Color GetEffectiveClearColor() => World3DRenderer.GetEffectiveClearColor();

        // ----- Fog -----

        public bool FogEnabled
        {
            get => World3DRenderer.FogEnabled;
            set => World3DRenderer.FogEnabled = value;
        }

        public float FogStart
        {
            get => World3DRenderer.FogStart;
            set => World3DRenderer.FogStart = value;
        }

        public float FogEnd
        {
            get => World3DRenderer.FogEnd;
            set => World3DRenderer.FogEnd = value;
        }

        public Color FogColor
        {
            get => World3DRenderer.FogColor;
            set => World3DRenderer.FogColor = value;
        }

        // ----- Sky -----

        public SkyboxMode SkyMode
        {
            get => (SkyboxMode)(int)World3DRenderer.SkyMode;
            set => World3DRenderer.SkyMode = (World3DRenderer.SkyboxMode)(int)value;
        }

        public Color SkyTopColor
        {
            get => World3DRenderer.SkyTopColor;
            set => World3DRenderer.SkyTopColor = value;
        }

        public Color SkyHorizonColor
        {
            get => World3DRenderer.SkyHorizonColor;
            set => World3DRenderer.SkyHorizonColor = value;
        }

        public Color SkyBottomColor
        {
            get => World3DRenderer.SkyBottomColor;
            set => World3DRenderer.SkyBottomColor = value;
        }

        public float SkyHorizonY
        {
            get => World3DRenderer.SkyHorizonY;
            set => World3DRenderer.SkyHorizonY = value;
        }
    }
}
