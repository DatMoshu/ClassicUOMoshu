// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Implemented by any subsystem that requires a per-frame tick. The composition root
    /// (<c>Renderer3DServices</c>) collects all <see cref="IFrameService"/>s and ticks them
    /// in registration order from a single entrypoint, passing the <see cref="FrameTickContext"/>
    /// produced by <see cref="IFrameClock.Advance"/>.
    /// </summary>
    /// <remarks>
    /// Implementations must be allocation-free in the steady state. Order-of-execution
    /// dependencies between services should be made explicit through the event bus or
    /// constructor-injected dependencies, not through tick ordering.
    /// </remarks>
    public interface IFrameService
    {
        /// <summary>
        /// Advance this service's internal state by one frame.
        /// </summary>
        /// <param name="ctx">Authoritative timing for this frame. Do not read clocks directly.</param>
        void Tick(in FrameTickContext ctx);
    }
}
