// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy Multi3DRenderer core-config statics to IMulti3DConfigBridge.

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyMulti3DConfigBridge : IMulti3DConfigBridge
    {
        public bool Enabled
        {
            get => Multi3DRenderer.Enabled;
            set => Multi3DRenderer.Enabled = value;
        }

        public bool VerboseLog
        {
            get => Multi3DRenderer.VerboseLog;
            set => Multi3DRenderer.VerboseLog = value;
        }

        public bool ShowFallbackForUnknownWalls
        {
            get => Multi3DRenderer.ShowFallbackForUnknownWalls;
            set => Multi3DRenderer.ShowFallbackForUnknownWalls = value;
        }

        public bool Use3DWallMeshes
        {
            get => Multi3DRenderer.Use3DWallMeshes;
            set => Multi3DRenderer.Use3DWallMeshes = value;
        }

        public bool DeskewMultiTexture
        {
            get => Multi3DRenderer.DeskewMultiTexture;
            set => Multi3DRenderer.DeskewMultiTexture = value;
        }

        public bool ForceTestQuadAtPlayer
        {
            get => Multi3DRenderer.ForceTestQuadAtPlayer;
            set => Multi3DRenderer.ForceTestQuadAtPlayer = value;
        }

        public bool HideAbovePlayerZ
        {
            get => Multi3DRenderer.HideAbovePlayerZ;
            set => Multi3DRenderer.HideAbovePlayerZ = value;
        }

        public bool RoofSnowOverlay
        {
            get => Multi3DRenderer.RoofSnowOverlay;
            set => Multi3DRenderer.RoofSnowOverlay = value;
        }
    }
}
