// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer ground-overlay state to IGroundEffectBridge.
// Replaced when the per-frame ground pass migrates into a renderer service.

using ClassicUO.Renderer.Environment;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyGroundEffectBridge : IGroundEffectBridge
    {
        public GroundEffectMode Mode
        {
            get => (GroundEffectMode)(int)World3DRenderer.GroundEffectMode;
            set => World3DRenderer.GroundEffectMode = (World3DRenderer.GroundEffect)(int)value;
        }

        public float CurrentIntensity => World3DRenderer.GroundEffectIntensity;

        public float TargetIntensity
        {
            get => World3DRenderer.TargetGroundEffectIntensity;
            set => World3DRenderer.TargetGroundEffectIntensity = value;
        }

        public float EaseSpeed
        {
            get => World3DRenderer.GroundEffectEaseSpeed;
            set => World3DRenderer.GroundEffectEaseSpeed = value;
        }

        public bool LinkToWeather
        {
            get => World3DRenderer.LinkToWeather;
            set => World3DRenderer.LinkToWeather = value;
        }

        public float LinkStrength
        {
            get => World3DRenderer.LinkStrength;
            set => World3DRenderer.LinkStrength = value;
        }
    }
}
