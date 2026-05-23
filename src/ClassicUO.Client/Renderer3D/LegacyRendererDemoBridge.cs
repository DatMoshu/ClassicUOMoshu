// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyRendererDemoBridge : ILegacyRendererDemoBridge
    {
        public bool HeadMeshEnabled
        {
            get => HeadMeshRenderer.Enabled;
            set => HeadMeshRenderer.Enabled = value;
        }
        public float HeadMeshSpinDegPerSec
        {
            get => HeadMeshRenderer.SpinDegPerSec;
            set => HeadMeshRenderer.SpinDegPerSec = value;
        }
        public bool VendorLinkScaleToPlayer
        {
            get => Vendor3DRenderer.LinkScaleToPlayer;
            set => Vendor3DRenderer.LinkScaleToPlayer = value;
        }
        public float VendorModelScale
        {
            get => Vendor3DRenderer.ModelScale;
            set => Vendor3DRenderer.ModelScale = value;
        }
        public float VendorModelYawDegrees
        {
            get => Vendor3DRenderer.ModelYawDegrees;
            set => Vendor3DRenderer.ModelYawDegrees = value;
        }
    }
}
