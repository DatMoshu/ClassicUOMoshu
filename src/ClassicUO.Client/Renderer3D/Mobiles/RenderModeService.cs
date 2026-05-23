// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>Pure-delegation implementation of <see cref="IRenderModeService"/>.</summary>
    public sealed class RenderModeService : IRenderModeService
    {
        private readonly IRenderModeBridge _bridge;

        public RenderModeService(IRenderModeBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public RenderMode Mode => _bridge.Mode;
        public bool Use3DPlayerInClassic2D => _bridge.Use3DPlayerInClassic2D;
        public bool MobilesIn3D => _bridge.MobilesIn3D;
        public bool WorldIs3D => _bridge.WorldIs3D;
        public string CurrentLabel => _bridge.CurrentLabel;

        public void SetMode(RenderMode mode) => _bridge.SetMode(mode);
        public void SetMobilesIn3D(bool value) => _bridge.SetMobilesIn3D(value);
        public void SetUse3DPlayerInClassic2D(bool value) => _bridge.SetUse3DPlayerInClassic2D(value);
    }
}
