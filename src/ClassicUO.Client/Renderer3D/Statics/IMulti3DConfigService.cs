// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin contract for the 3D multi-renderer's core toggles. Replaces direct
    /// reads/writes of <c>Multi3DRenderer.{Enabled, ShowFallbackForUnknownWalls, ...}</c>.
    /// </summary>
    public interface IMulti3DConfigService
    {
        bool Enabled { get; }
        bool VerboseLog { get; }
        bool ShowFallbackForUnknownWalls { get; }
        bool Use3DWallMeshes { get; }
        bool DeskewMultiTexture { get; }
        bool ForceTestQuadAtPlayer { get; }
        bool HideAbovePlayerZ { get; }
        bool RoofSnowOverlay { get; }

        void SetEnabled(bool value);
        void SetVerboseLog(bool value);
        void SetShowFallbackForUnknownWalls(bool value);
        void SetUse3DWallMeshes(bool value);
        void SetDeskewMultiTexture(bool value);
        void SetForceTestQuadAtPlayer(bool value);
        void SetHideAbovePlayerZ(bool value);
        void SetRoofSnowOverlay(bool value);
    }
}
