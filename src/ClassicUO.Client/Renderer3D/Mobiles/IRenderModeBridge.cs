// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gateway exposing the legacy <c>RenderModeController</c> mode + override toggles.
    /// State-of-record stays on the legacy class because per-frame Draw uses its derived
    /// flags (Is2DOnly / MobilesAre3D / WorldIs3D / CameraLockedToIso) in the hot path.
    /// </summary>
    public interface IRenderModeBridge
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
