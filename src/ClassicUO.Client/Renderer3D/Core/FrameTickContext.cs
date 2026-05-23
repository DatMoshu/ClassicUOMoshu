// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).
//
// FrameTickContext — the single authoritative per-frame snapshot passed to
// every IFrameService.Tick() call. No subsystem may read Environment.TickCount64,
// DateTime.UtcNow, or any other clock directly; all timing flows through this
// context produced by the IFrameClock service.

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Per-frame timing snapshot. Constructed once per update by <see cref="IFrameClock"/>
    /// and passed to every <see cref="IFrameService"/>. Read-only by contract.
    /// </summary>
    public readonly struct FrameTickContext
    {
        /// <summary>
        /// Seconds elapsed since the previous frame's tick. Always &gt; 0; clamped to a
        /// safe upper bound by <see cref="IFrameClock"/> to prevent integration blowups
        /// after debugger pause or window minimisation.
        /// </summary>
        public readonly float DeltaSeconds;

        /// <summary>
        /// Total accumulated seconds since the renderer started. Monotonic, double-precision
        /// to remain stable over long sessions.
        /// </summary>
        public readonly double TotalSeconds;

        /// <summary>
        /// Frame counter, incremented by <see cref="IFrameClock.Advance"/>. Useful for
        /// periodic logging and for keying once-per-N-frames work.
        /// </summary>
        public readonly long FrameNumber;

        public FrameTickContext(float deltaSeconds, double totalSeconds, long frameNumber)
        {
            DeltaSeconds = deltaSeconds;
            TotalSeconds = totalSeconds;
            FrameNumber = frameNumber;
        }
    }
}
