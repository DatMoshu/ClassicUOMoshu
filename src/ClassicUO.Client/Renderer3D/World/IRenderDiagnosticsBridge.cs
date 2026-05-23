// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Gateway exposing legacy <c>World3DRenderer</c> per-frame diagnostic counters.
    /// Read-only from the gump side; the renderer itself writes these from its hot path.
    /// </summary>
    public interface IRenderDiagnosticsBridge
    {
        int LastVisibleChunks { get; }
        int LastBuiltChunks { get; }
        int LastDrawnChunks { get; }
        int LastDrawCalls { get; }
        int LastPrimitives { get; }
        int LastViewportWidth { get; }
        int LastViewportHeight { get; }
        int LastInlineNpcsDrawn { get; }
        Vector3 LastCameraTarget { get; }
    }
}
