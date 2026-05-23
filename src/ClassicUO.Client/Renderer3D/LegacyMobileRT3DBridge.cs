// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyMobileRT3DBridge : IMobileRT3DBridge
    {
        public bool Enabled
        {
            get => MobileRT3DRenderer.Enabled;
            set => MobileRT3DRenderer.Enabled = value;
        }
        public int MaxRender3DDistance
        {
            get => MobileRT3DRenderer.MaxRender3DDistance;
            set => MobileRT3DRenderer.MaxRender3DDistance = value;
        }
        public int RTWidth
        {
            get => MobileRT3DRenderer.RTWidth;
            set => MobileRT3DRenderer.RTWidth = value;
        }
        public int RTHeight
        {
            get => MobileRT3DRenderer.RTHeight;
            set => MobileRT3DRenderer.RTHeight = value;
        }
        public int SuperSample
        {
            get => MobileRT3DRenderer.SuperSample;
            set => MobileRT3DRenderer.SuperSample = value;
        }
        public int RTYAnchorOffset
        {
            get => MobileRT3DRenderer.RTYAnchorOffset;
            set => MobileRT3DRenderer.RTYAnchorOffset = value;
        }
        public bool Show2DPlayerSprite
        {
            get => MobileRT3DRenderer.Show2DPlayerSprite;
            set => MobileRT3DRenderer.Show2DPlayerSprite = value;
        }
        public int FootMarginFromBottom
        {
            get => MobileRT3DRenderer.FootMarginFromBottom;
            set => MobileRT3DRenderer.FootMarginFromBottom = value;
        }
    }
}
