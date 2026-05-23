// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gump/admin contract for the wet/snow/fall ground-overlay shader's tunables.
    /// Replaces direct reads/writes of <c>GroundOverlayEffect.{NoiseScale, ...}</c>
    /// consumed by WeatherAdminGump (sliders + DumpFullState).
    /// </summary>
    public interface IGroundOverlayService
    {
        float NoiseScale { get; }
        float PuddleScale { get; }
        float FlakeScale { get; }
        float PuddleThresh { get; }
        float HeightBias { get; }
        float FallTreeUnit { get; }
        float FallSaturation { get; }
        float SnowDetile { get; }
        float SnowCellSize { get; }
        float PuddleAmount { get; }

        void SetNoiseScale(float value);
        void SetPuddleScale(float value);
        void SetFlakeScale(float value);
        void SetPuddleThresh(float value);
        void SetHeightBias(float value);
        void SetFallTreeUnit(float value);
        void SetFallSaturation(float value);
        void SetSnowDetile(float value);
        void SetSnowCellSize(float value);
        void SetPuddleAmount(float value);
    }
}
