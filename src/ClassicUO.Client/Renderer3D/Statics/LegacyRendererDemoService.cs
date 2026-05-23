// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>Pure-delegation implementation of <see cref="ILegacyRendererDemoService"/>.</summary>
    public sealed class LegacyRendererDemoService : ILegacyRendererDemoService
    {
        private readonly ILegacyRendererDemoBridge _bridge;

        public LegacyRendererDemoService(ILegacyRendererDemoBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool HeadMeshEnabled => _bridge.HeadMeshEnabled;
        public float HeadMeshSpinDegPerSec => _bridge.HeadMeshSpinDegPerSec;
        public bool VendorLinkScaleToPlayer => _bridge.VendorLinkScaleToPlayer;
        public float VendorModelScale => _bridge.VendorModelScale;
        public float VendorModelYawDegrees => _bridge.VendorModelYawDegrees;

        public void SetHeadMeshEnabled(bool value) => _bridge.HeadMeshEnabled = value;
        public void SetHeadMeshSpinDegPerSec(float value) => _bridge.HeadMeshSpinDegPerSec = value;
        public void SetVendorLinkScaleToPlayer(bool value) => _bridge.VendorLinkScaleToPlayer = value;
        public void SetVendorModelScale(float value) => _bridge.VendorModelScale = value;
        public void SetVendorModelYawDegrees(float value) => _bridge.VendorModelYawDegrees = value;
    }
}
