// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin read-only view over <c>Multi3DRenderer</c>'s last-frame counters.
    /// </summary>
    public interface IMulti3DDiagnosticsService
    {
        int LastMultiSeen { get; }
        int LastStaticSeen { get; }
        int LastQuadCount { get; }
        int LastTextureCount { get; }
        int LastUnknownCount { get; }
        int LastSkippedCount { get; }
    }
}
