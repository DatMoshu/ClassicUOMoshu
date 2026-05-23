// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Environment;

namespace ClassicUO.Renderer.Passes
{
    /// <summary>
    /// Sixth pass (<see cref="RenderPassOrder.Atmosphere"/> = 600). Advances the active
    /// BG/Fog colors toward the targets owned by <see cref="IAtmosphereService"/>.
    /// </summary>
    /// <remarks>
    /// Session-64 switchover: pipeline-driven. The legacy
    /// <c>Particle3DSystem.Tick → World3DRenderer.TickAtmosphere</c> call site is gone;
    /// <see cref="IRenderPass.Execute"/> is the single authoritative tick (lesson §S).
    /// </remarks>
    internal sealed class AtmospherePass : IRenderPass
    {
        private readonly IAtmosphereService _service;

        public AtmospherePass(IAtmosphereService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "Atmosphere";
        public int Order => RenderPassOrder.Atmosphere;
        public bool IsEnabled => true;

        public void Execute(in RenderPassContext ctx)
            => _service.Tick(ctx.Frame.DeltaSeconds);
    }
}
