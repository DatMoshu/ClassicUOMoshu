// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gateway exposing legacy <c>GroundOverlayEffect</c> shader-tunable statics
    /// (NoiseScale / PuddleScale / FlakeScale / PuddleThresh / HeightBias /
    /// FallTreeUnit / FallSaturation / SnowDetile / SnowCellSize / PuddleAmount).
    /// The renderer reads these every frame via <c>ApplyTunables()</c>; the
    /// service contract simply round-trips them.
    /// </summary>
    public interface IGroundOverlayBridge
    {
        float NoiseScale { get; set; }
        float PuddleScale { get; set; }
        float FlakeScale { get; set; }
        float PuddleThresh { get; set; }
        float HeightBias { get; set; }
        float FallTreeUnit { get; set; }
        float FallSaturation { get; set; }
        float SnowDetile { get; set; }
        float SnowCellSize { get; set; }
        float PuddleAmount { get; set; }
    }
}
