// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>Pure-delegation implementation of <see cref="IMulti3DConfigService"/>.</summary>
    public sealed class Multi3DConfigService : IMulti3DConfigService
    {
        private readonly IMulti3DConfigBridge _bridge;

        public Multi3DConfigService(IMulti3DConfigBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool Enabled => _bridge.Enabled;
        public bool VerboseLog => _bridge.VerboseLog;
        public bool ShowFallbackForUnknownWalls => _bridge.ShowFallbackForUnknownWalls;
        public bool Use3DWallMeshes => _bridge.Use3DWallMeshes;
        public bool DeskewMultiTexture => _bridge.DeskewMultiTexture;
        public bool ForceTestQuadAtPlayer => _bridge.ForceTestQuadAtPlayer;
        public bool HideAbovePlayerZ => _bridge.HideAbovePlayerZ;
        public bool RoofSnowOverlay => _bridge.RoofSnowOverlay;

        public void SetEnabled(bool value) => _bridge.Enabled = value;
        public void SetVerboseLog(bool value) => _bridge.VerboseLog = value;
        public void SetShowFallbackForUnknownWalls(bool value) => _bridge.ShowFallbackForUnknownWalls = value;
        public void SetUse3DWallMeshes(bool value) => _bridge.Use3DWallMeshes = value;
        public void SetDeskewMultiTexture(bool value) => _bridge.DeskewMultiTexture = value;
        public void SetForceTestQuadAtPlayer(bool value) => _bridge.ForceTestQuadAtPlayer = value;
        public void SetHideAbovePlayerZ(bool value) => _bridge.HideAbovePlayerZ = value;
        public void SetRoofSnowOverlay(bool value) => _bridge.RoofSnowOverlay = value;
    }
}
