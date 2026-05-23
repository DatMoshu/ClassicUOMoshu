// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Single authoritative source of per-frame timing. Replaces direct calls to
    /// <c>Environment.TickCount64</c> and <c>DateTime.UtcNow.Ticks</c> across the
    /// renderer. All <see cref="IFrameService"/> implementations receive a
    /// <see cref="FrameTickContext"/> derived from this clock.
    /// </summary>
    /// <remarks>
    /// Closes review finding #2 (three divergent frame-delta sources).
    /// AOT-safe: no reflection, no dynamic dispatch beyond the interface.
    /// </remarks>
    public interface IFrameClock
    {
        /// <summary>
        /// The most recent <see cref="FrameTickContext"/> produced by <see cref="Advance"/>.
        /// Valid after the first call to <see cref="Advance"/>; default-initialised before.
        /// </summary>
        FrameTickContext Current { get; }

        /// <summary>
        /// Advance the clock by the supplied delta. Called exactly once per frame from
        /// the renderer's update entrypoint with the host engine's authoritative delta
        /// (typically <c>GameTime.ElapsedGameTime.TotalSeconds</c>).
        /// </summary>
        /// <param name="deltaSecondsRaw">Raw delta from the host engine. May be unbounded
        /// after debugger pause; the implementation clamps to a safe ceiling.</param>
        /// <returns>The new <see cref="FrameTickContext"/> (also reflected in <see cref="Current"/>).</returns>
        FrameTickContext Advance(float deltaSecondsRaw);
    }
}
