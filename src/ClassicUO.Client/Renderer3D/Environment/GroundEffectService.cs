// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IGroundEffectService"/>. Forwards every
    /// read+write through the supplied <see cref="IGroundEffectBridge"/>.
    /// </summary>
    public sealed class GroundEffectService : IGroundEffectService
    {
        private readonly IGroundEffectBridge _bridge;

        public GroundEffectService(IGroundEffectBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public GroundEffectMode Mode => _bridge.Mode;
        public float CurrentIntensity => _bridge.CurrentIntensity;
        public float TargetIntensity => _bridge.TargetIntensity;
        public float EaseSpeed => _bridge.EaseSpeed;
        public bool LinkToWeather => _bridge.LinkToWeather;
        public float LinkStrength => _bridge.LinkStrength;

        public void SetMode(GroundEffectMode mode) => _bridge.Mode = mode;
        public void SetTargetIntensity(float intensity) => _bridge.TargetIntensity = intensity;
        public void SetEaseSpeed(float speed) => _bridge.EaseSpeed = speed;
        public void SetLinkToWeather(bool link) => _bridge.LinkToWeather = link;
        public void SetLinkStrength(float strength) => _bridge.LinkStrength = strength;
    }
}
