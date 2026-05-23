// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin read-only view over <c>Static3DRenderer</c>'s last-frame counters.
    /// Replaces direct reads of <c>Static3DRenderer.{LastStaticSeen, LastBillboards, ...}</c>.
    /// </summary>
    public interface IStatic3DDiagnosticsService
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
