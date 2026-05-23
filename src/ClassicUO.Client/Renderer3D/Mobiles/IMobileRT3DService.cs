// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gump/admin contract for the per-mobile RenderTarget (RT) 3D mobile path.
    /// Replaces direct reads/writes of <c>MobileRT3DRenderer.{Enabled, RTWidth, RTHeight, ...}</c>.
    /// </summary>
    public interface IMobileRT3DService
    {
        bool Enabled { get; }
        int MaxRender3DDistance { get; }
        int RTWidth { get; }
        int RTHeight { get; }
        int SuperSample { get; }
        int RTYAnchorOffset { get; }
        bool Show2DPlayerSprite { get; }
        int FootMarginFromBottom { get; }

        void SetEnabled(bool value);
        void SetMaxRender3DDistance(int value);
        void SetRTWidth(int value);
        void SetRTHeight(int value);
        void SetSuperSample(int value);
        void SetRTYAnchorOffset(int value);
        void SetShow2DPlayerSprite(bool value);
        void SetFootMarginFromBottom(int value);
    }
}
