// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Gump/admin read-only view over the renderer's last-frame counters. Replaces
    /// direct reads of <c>World3DRenderer.{LastVisibleChunks, LastDrawCalls, ...}</c>.
    /// </summary>
    public interface IRenderDiagnosticsService
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
