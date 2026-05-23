// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IStatic3DDiagnosticsService"/>.
    /// </summary>
    public sealed class Static3DDiagnosticsService : IStatic3DDiagnosticsService
    {
        private readonly IStatic3DDiagnosticsBridge _bridge;

        public Static3DDiagnosticsService(IStatic3DDiagnosticsBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public int LastStaticSeen        => _bridge.LastStaticSeen;
        public int LastBillboards        => _bridge.LastBillboards;
        public int LastGroundDecals      => _bridge.LastGroundDecals;
        public int LastTextures          => _bridge.LastTextures;
        public int LastSkipped           => _bridge.LastSkipped;
        public int LastBillboardedItems  => _bridge.LastBillboardedItems;
        public int LastWholeTrees        => _bridge.LastWholeTrees;
        public int LastLeafOverlays      => _bridge.LastLeafOverlays;
        public int LastLeafFadeQuads     => _bridge.LastLeafFadeQuads;
        public int LastIris2Meshes       => _bridge.LastIris2Meshes;
    }
}
