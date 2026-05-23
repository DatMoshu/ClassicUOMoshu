// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>Pure-delegation implementation of <see cref="IMobileRT3DService"/>.</summary>
    public sealed class MobileRT3DService : IMobileRT3DService
    {
        private readonly IMobileRT3DBridge _bridge;

        public MobileRT3DService(IMobileRT3DBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool Enabled => _bridge.Enabled;
        public int MaxRender3DDistance => _bridge.MaxRender3DDistance;
        public int RTWidth => _bridge.RTWidth;
        public int RTHeight => _bridge.RTHeight;
        public int SuperSample => _bridge.SuperSample;
        public int RTYAnchorOffset => _bridge.RTYAnchorOffset;
        public bool Show2DPlayerSprite => _bridge.Show2DPlayerSprite;
        public int FootMarginFromBottom => _bridge.FootMarginFromBottom;

        public void SetEnabled(bool value) => _bridge.Enabled = value;
        public void SetMaxRender3DDistance(int value) => _bridge.MaxRender3DDistance = value;
        public void SetRTWidth(int value) => _bridge.RTWidth = value;
        public void SetRTHeight(int value) => _bridge.RTHeight = value;
        public void SetSuperSample(int value) => _bridge.SuperSample = value;
        public void SetRTYAnchorOffset(int value) => _bridge.RTYAnchorOffset = value;
        public void SetShow2DPlayerSprite(bool value) => _bridge.Show2DPlayerSprite = value;
        public void SetFootMarginFromBottom(int value) => _bridge.FootMarginFromBottom = value;
    }
}
