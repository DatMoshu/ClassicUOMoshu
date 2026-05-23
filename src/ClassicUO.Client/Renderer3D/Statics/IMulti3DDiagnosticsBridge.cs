// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing legacy <c>Multi3DRenderer</c> per-frame diagnostic counters.
    /// Read-only from the gump side; the renderer itself writes these from its hot path.
    /// </summary>
    public interface IMulti3DDiagnosticsBridge
    {
        int LastMultiSeen { get; }
        int LastStaticSeen { get; }
        int LastQuadCount { get; }
        int LastTextureCount { get; }
        int LastUnknownCount { get; }
        int LastSkippedCount { get; }
    }
}
