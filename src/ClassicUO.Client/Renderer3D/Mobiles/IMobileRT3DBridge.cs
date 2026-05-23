// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gateway exposing legacy <c>MobileRT3DRenderer</c> per-mobile RT (RenderTarget) tunables.
    /// Read+write. State-of-record stays on the legacy class because the per-mobile RT
    /// allocation + draw loop reads these in the hot path. <c>GetCached(Mobile)</c> is NOT
    /// exposed — diagnostic gumps that need the cached RT read off the legacy class directly.
    /// </summary>
    public interface IMobileRT3DBridge
    {
        bool Enabled { get; set; }
        int MaxRender3DDistance { get; set; }
        int RTWidth { get; set; }
        int RTHeight { get; set; }
        int SuperSample { get; set; }
        int RTYAnchorOffset { get; set; }
        bool Show2DPlayerSprite { get; set; }
        int FootMarginFromBottom { get; set; }
    }
}
