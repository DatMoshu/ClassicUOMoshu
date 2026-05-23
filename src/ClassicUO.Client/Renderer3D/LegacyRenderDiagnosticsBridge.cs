// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer per-frame counters to IRenderDiagnosticsBridge.

using ClassicUO.Renderer.World;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyRenderDiagnosticsBridge : IRenderDiagnosticsBridge
    {
        public int LastVisibleChunks    => World3DRenderer.LastVisibleChunks;
        public int LastBuiltChunks      => World3DRenderer.LastBuiltChunks;
        public int LastDrawnChunks      => World3DRenderer.LastDrawnChunks;
        public int LastDrawCalls        => World3DRenderer.LastDrawCalls;
        public int LastPrimitives       => World3DRenderer.LastPrimitives;
        public int LastViewportWidth    => World3DRenderer.LastViewportWidth;
        public int LastViewportHeight   => World3DRenderer.LastViewportHeight;
        public int LastInlineNpcsDrawn  => World3DRenderer.LastInlineNpcsDrawn;
        public Vector3 LastCameraTarget => World3DRenderer.LastCameraTarget;
    }
}
