// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyMulti3DDiagnosticsBridge : IMulti3DDiagnosticsBridge
    {
        public int LastMultiSeen     => Multi3DRenderer.LastMultiSeen;
        public int LastStaticSeen    => Multi3DRenderer.LastStaticSeen;
        public int LastQuadCount     => Multi3DRenderer.LastQuadCount;
        public int LastTextureCount  => Multi3DRenderer.LastTextureCount;
        public int LastUnknownCount  => Multi3DRenderer.LastUnknownCount;
        public int LastSkippedCount  => Multi3DRenderer.LastSkippedCount;
    }
}
