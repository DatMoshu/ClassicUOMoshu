// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Tunable parameters for <see cref="MobileAnimService"/>.
    /// </summary>
    public sealed class MobileAnimServiceConfig
    {
        /// <summary>Entries unseen for this many seconds are dropped at the next eviction sweep.</summary>
        public float StaleEntrySeconds { get; init; } = 120f;

        /// <summary>Hard upper bound on the dictionary size (hub defense).</summary>
        public int MaxTrackedEntries { get; init; } = 256;

        /// <summary>Eviction sweep cadence in frames. 60 frames at 60 FPS = once per second.</summary>
        public int EvictionFrameInterval { get; init; } = 60;

        /// <summary>
        /// Seconds since last detected motion below which the NPC is considered "walking".
        /// Above this threshold the service uses the Idle animation.
        /// </summary>
        public float WalkingMotionThresholdSeconds { get; init; } = 0.5f;

        /// <summary>Squared-distance threshold for "did the NPC move this frame".</summary>
        public float MotionDeltaSqEpsilon { get; init; } = 0.001f;

        public static MobileAnimServiceConfig Default => new();
    }
}
