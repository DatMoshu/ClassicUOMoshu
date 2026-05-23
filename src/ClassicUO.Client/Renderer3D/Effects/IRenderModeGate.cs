// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Gateway over the renderer's "is the world being drawn in pure 2D mode" flag.
    /// Used by services that should suppress emission while in legacy 2D-only mode
    /// (e.g., buff-particle effects). Production-side wraps
    /// <c>RenderModeController.Is2DOnly</c>; tests provide a settable property.
    /// </summary>
    public interface IRenderModeGate
    {
        bool Is2DOnly { get; }
    }
}
