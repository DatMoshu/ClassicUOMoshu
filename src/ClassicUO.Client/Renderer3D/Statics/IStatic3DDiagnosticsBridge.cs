// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing legacy <c>Static3DRenderer</c> per-frame diagnostic counters.
    /// Read-only from the gump side; the renderer itself writes these from its hot path.
    /// </summary>
    public interface IStatic3DDiagnosticsBridge
    {
        int LastStaticSeen { get; }
        int LastBillboards { get; }
        int LastGroundDecals { get; }
        int LastTextures { get; }
        int LastSkipped { get; }
        int LastBillboardedItems { get; }
        int LastWholeTrees { get; }
        int LastLeafOverlays { get; }
        int LastLeafFadeQuads { get; }
        int LastIris2Meshes { get; }
    }
}
