// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>Pure-delegation implementation of <see cref="IMulti3DDiagnosticsService"/>.</summary>
    public sealed class Multi3DDiagnosticsService : IMulti3DDiagnosticsService
    {
        private readonly IMulti3DDiagnosticsBridge _bridge;

        public Multi3DDiagnosticsService(IMulti3DDiagnosticsBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public int LastMultiSeen     => _bridge.LastMultiSeen;
        public int LastStaticSeen    => _bridge.LastStaticSeen;
        public int LastQuadCount     => _bridge.LastQuadCount;
        public int LastTextureCount  => _bridge.LastTextureCount;
        public int LastUnknownCount  => _bridge.LastUnknownCount;
        public int LastSkippedCount  => _bridge.LastSkippedCount;
    }
}
