// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy Static3DRenderer per-frame counters to IStatic3DDiagnosticsBridge.

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyStatic3DDiagnosticsBridge : IStatic3DDiagnosticsBridge
    {
        public int LastStaticSeen        => Static3DRenderer.LastStaticSeen;
        public int LastBillboards        => Static3DRenderer.LastBillboards;
        public int LastGroundDecals      => Static3DRenderer.LastGroundDecals;
        public int LastTextures          => Static3DRenderer.LastTextures;
        public int LastSkipped           => Static3DRenderer.LastSkipped;
        public int LastBillboardedItems  => Static3DRenderer.LastBillboardedItems;
        public int LastWholeTrees        => Static3DRenderer.LastWholeTrees;
        public int LastLeafOverlays      => Static3DRenderer.LastLeafOverlays;
        public int LastLeafFadeQuads     => Static3DRenderer.LastLeafFadeQuads;
        public int LastIris2Meshes       => Static3DRenderer.LastIris2Meshes;
    }
}
