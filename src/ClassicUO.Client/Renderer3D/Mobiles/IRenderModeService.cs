// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gump/admin contract for the cross-mode render-mode controller.
    /// Replaces direct reads/writes of <c>RenderModeController.{SetMode, MobilesIn3D, ...}</c>.
    /// </summary>
    public interface IRenderModeService
    {
        RenderMode Mode { get; }
        bool Use3DPlayerInClassic2D { get; }
        bool MobilesIn3D { get; }
        bool WorldIs3D { get; }
        string CurrentLabel { get; }

        void SetMode(RenderMode mode);
        void SetMobilesIn3D(bool value);
        void SetUse3DPlayerInClassic2D(bool value);
    }
}
