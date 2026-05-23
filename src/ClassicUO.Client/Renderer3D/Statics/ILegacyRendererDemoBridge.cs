// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing the legacy demo renderers (HeadMeshRenderer .umesh smoke test +
    /// Vendor3DRenderer single-shared-mesh path superseded by the NPC pipeline). Read+write.
    /// State-of-record stays on the legacy classes since their per-frame draw paths read
    /// these in the hot path.
    /// </summary>
    public interface ILegacyRendererDemoBridge
    {
        bool HeadMeshEnabled { get; set; }
        float HeadMeshSpinDegPerSec { get; set; }

        bool VendorLinkScaleToPlayer { get; set; }
        float VendorModelScale { get; set; }
        float VendorModelYawDegrees { get; set; }
    }
}
