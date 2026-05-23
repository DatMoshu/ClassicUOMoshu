// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IRenderModeGate backed by RenderModeController.

using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyRenderModeGate : IRenderModeGate
    {
        public bool Is2DOnly => RenderModeController.Is2DOnly;
    }
}
