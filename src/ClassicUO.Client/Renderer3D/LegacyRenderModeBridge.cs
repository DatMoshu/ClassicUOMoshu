// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
//
// Namespaced outside `ClassicUO.Renderer.Renderer3D` so the bridge sees the
// domain-side `RenderMode` (ClassicUO.Renderer.Mobiles.RenderMode) without
// colliding with the legacy enum of the same name in that namespace.

using ClassicUO.Renderer.Mobiles;
using LegacyRenderModeEnum = ClassicUO.Renderer.Renderer3D.RenderMode;
using LegacyController = ClassicUO.Renderer.Renderer3D.RenderModeController;

namespace ClassicUO.Renderer.Production
{
    internal sealed class LegacyRenderModeBridge : IRenderModeBridge
    {
        public RenderMode Mode => (RenderMode)(int)LegacyController.Mode;
        public bool Use3DPlayerInClassic2D => LegacyController.Use3DPlayerInClassic2D;
        public bool MobilesIn3D => LegacyController.MobilesIn3D;
        public bool WorldIs3D => LegacyController.WorldIs3D;
        public string CurrentLabel => LegacyController.CurrentLabel();

        public void SetMode(RenderMode mode)
            => LegacyController.SetMode((LegacyRenderModeEnum)(int)mode);
        public void SetMobilesIn3D(bool value) => LegacyController.SetMobilesIn3D(value);
        public void SetUse3DPlayerInClassic2D(bool value)
            => LegacyController.SetUse3DPlayerInClassic2D(value);
    }
}
