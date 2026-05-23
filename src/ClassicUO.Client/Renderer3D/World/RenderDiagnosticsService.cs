// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IRenderDiagnosticsService"/>.
    /// </summary>
    public sealed class RenderDiagnosticsService : IRenderDiagnosticsService
    {
        private readonly IRenderDiagnosticsBridge _bridge;

        public RenderDiagnosticsService(IRenderDiagnosticsBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public int LastVisibleChunks => _bridge.LastVisibleChunks;
        public int LastBuiltChunks => _bridge.LastBuiltChunks;
        public int LastDrawnChunks => _bridge.LastDrawnChunks;
        public int LastDrawCalls => _bridge.LastDrawCalls;
        public int LastPrimitives => _bridge.LastPrimitives;
        public int LastViewportWidth => _bridge.LastViewportWidth;
        public int LastViewportHeight => _bridge.LastViewportHeight;
        public int LastInlineNpcsDrawn => _bridge.LastInlineNpcsDrawn;
        public Vector3 LastCameraTarget => _bridge.LastCameraTarget;
    }
}
