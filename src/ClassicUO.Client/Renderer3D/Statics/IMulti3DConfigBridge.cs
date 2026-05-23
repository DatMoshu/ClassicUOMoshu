// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing legacy <c>Multi3DRenderer</c> core-config tunables. Read+write.
    /// State-of-record stays on the legacy class because per-frame Draw() reads these
    /// in the hot path. TestQuad position fields stay legacy (not gump-mutable).
    /// </summary>
    public interface IMulti3DConfigBridge
    {
        bool Enabled { get; set; }
        bool VerboseLog { get; set; }
        bool ShowFallbackForUnknownWalls { get; set; }
        bool Use3DWallMeshes { get; set; }
        bool DeskewMultiTexture { get; set; }
        bool ForceTestQuadAtPlayer { get; set; }
        bool HideAbovePlayerZ { get; set; }
        bool RoofSnowOverlay { get; set; }
    }
}
