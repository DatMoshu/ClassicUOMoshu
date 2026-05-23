// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Default <see cref="IFrameClock"/> implementation. Produces a single authoritative
    /// <see cref="FrameTickContext"/> per frame from a host-supplied raw delta.
    /// </summary>
    /// <remarks>
    /// Clamping policy: a single frame's delta is bounded to <see cref="MaxDeltaSeconds"/>
    /// so that long pauses (debugger break, window minimised, OS hitch) do not produce
    /// integration blowups in subsystems that scale by <c>dt</c>. The renderer logically
    /// "skips" the lost time rather than letting wind, fire, weather, etc. lurch forward.
    /// </remarks>
    public sealed class FrameClock : IFrameClock
    {
        /// <summary>Upper bound on a single frame's delta, in seconds. ~6 frames at 30 FPS.</summary>
        public const float MaxDeltaSeconds = 0.2f;

        /// <summary>Lower bound on a single frame's delta. Prevents zero-dt division paths in subsystems.</summary>
        public const float MinDeltaSeconds = 1f / 600f;

        private double _totalSeconds;
        private long _frameNumber;
        private FrameTickContext _current;

        public FrameTickContext Current => _current;

        public FrameTickContext Advance(float deltaSecondsRaw)
        {
            float dt = deltaSecondsRaw;
            if (dt < MinDeltaSeconds) dt = MinDeltaSeconds;
            else if (dt > MaxDeltaSeconds) dt = MaxDeltaSeconds;

            _totalSeconds += dt;
            _frameNumber++;
            _current = new FrameTickContext(dt, _totalSeconds, _frameNumber);
            return _current;
        }
    }
}
