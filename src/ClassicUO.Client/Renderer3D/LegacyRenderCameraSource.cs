// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges legacy World3DRenderer static fields to IRenderCameraSource. Replaced with
// a renderer-owned camera-state publisher in Phase 3 when World3DRenderer migrates.

using ClassicUO.Renderer.WorldEnv;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyRenderCameraSource : IRenderCameraSource
    {
        public bool TryGetLastFrame(out Matrix view, out Matrix projection,
                                    out int viewportWidth, out int viewportHeight)
        {
            view = World3DRenderer.LastView;
            projection = World3DRenderer.LastProjection;
            viewportWidth = World3DRenderer.LastViewportWidth;
            viewportHeight = World3DRenderer.LastViewportHeight;
            return viewportWidth != 0;
        }
    }
}
