// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.EnvRender;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyGroundOverlayBridge : IGroundOverlayBridge
    {
        public float NoiseScale
        {
            get => GroundOverlayEffect.NoiseScale;
            set => GroundOverlayEffect.NoiseScale = value;
        }
        public float PuddleScale
        {
            get => GroundOverlayEffect.PuddleScale;
            set => GroundOverlayEffect.PuddleScale = value;
        }
        public float FlakeScale
        {
            get => GroundOverlayEffect.FlakeScale;
            set => GroundOverlayEffect.FlakeScale = value;
        }
        public float PuddleThresh
        {
            get => GroundOverlayEffect.PuddleThresh;
            set => GroundOverlayEffect.PuddleThresh = value;
        }
        public float HeightBias
        {
            get => GroundOverlayEffect.HeightBias;
            set => GroundOverlayEffect.HeightBias = value;
        }
        public float FallTreeUnit
        {
            get => GroundOverlayEffect.FallTreeUnit;
            set => GroundOverlayEffect.FallTreeUnit = value;
        }
        public float FallSaturation
        {
            get => GroundOverlayEffect.FallSaturation;
            set => GroundOverlayEffect.FallSaturation = value;
        }
        public float SnowDetile
        {
            get => GroundOverlayEffect.SnowDetile;
            set => GroundOverlayEffect.SnowDetile = value;
        }
        public float SnowCellSize
        {
            get => GroundOverlayEffect.SnowCellSize;
            set => GroundOverlayEffect.SnowCellSize = value;
        }
        public float PuddleAmount
        {
            get => GroundOverlayEffect.PuddleAmount;
            set => GroundOverlayEffect.PuddleAmount = value;
        }
    }
}
