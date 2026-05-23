// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin contract for the legacy demo renderers (HeadMesh smoke + Vendor3D).
    /// Replaces direct reads/writes of <c>HeadMeshRenderer.{Enabled, SpinDegPerSec}</c> and
    /// <c>Vendor3DRenderer.{LinkScaleToPlayer, ModelScale, ModelYawDegrees}</c>.
    /// </summary>
    public interface ILegacyRendererDemoService
    {
        bool HeadMeshEnabled { get; }
        float HeadMeshSpinDegPerSec { get; }
        bool VendorLinkScaleToPlayer { get; }
        float VendorModelScale { get; }
        float VendorModelYawDegrees { get; }

        void SetHeadMeshEnabled(bool value);
        void SetHeadMeshSpinDegPerSec(float value);
        void SetVendorLinkScaleToPlayer(bool value);
        void SetVendorModelScale(float value);
        void SetVendorModelYawDegrees(float value);
    }
}
