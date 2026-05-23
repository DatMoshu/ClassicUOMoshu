// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>Pure-delegation implementation of <see cref="IGroundOverlayService"/>.</summary>
    public sealed class GroundOverlayService : IGroundOverlayService
    {
        private readonly IGroundOverlayBridge _bridge;

        public GroundOverlayService(IGroundOverlayBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public float NoiseScale => _bridge.NoiseScale;
        public float PuddleScale => _bridge.PuddleScale;
        public float FlakeScale => _bridge.FlakeScale;
        public float PuddleThresh => _bridge.PuddleThresh;
        public float HeightBias => _bridge.HeightBias;
        public float FallTreeUnit => _bridge.FallTreeUnit;
        public float FallSaturation => _bridge.FallSaturation;
        public float SnowDetile => _bridge.SnowDetile;
        public float SnowCellSize => _bridge.SnowCellSize;
        public float PuddleAmount => _bridge.PuddleAmount;

        public void SetNoiseScale(float value) => _bridge.NoiseScale = value;
        public void SetPuddleScale(float value) => _bridge.PuddleScale = value;
        public void SetFlakeScale(float value) => _bridge.FlakeScale = value;
        public void SetPuddleThresh(float value) => _bridge.PuddleThresh = value;
        public void SetHeightBias(float value) => _bridge.HeightBias = value;
        public void SetFallTreeUnit(float value) => _bridge.FallTreeUnit = value;
        public void SetFallSaturation(float value) => _bridge.FallSaturation = value;
        public void SetSnowDetile(float value) => _bridge.SnowDetile = value;
        public void SetSnowCellSize(float value) => _bridge.SnowCellSize = value;
        public void SetPuddleAmount(float value) => _bridge.PuddleAmount = value;
    }
}
