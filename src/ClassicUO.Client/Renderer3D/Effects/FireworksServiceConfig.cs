// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="FireworksService"/>. Mirrors the legacy
    /// <c>FireworksShow</c> hardcoded constants. Schedule of bursts is built into the
    /// service code (mirrors the prototype hand-authored timeline).
    /// </summary>
    public sealed class FireworksServiceConfig
    {
        public bool InitialEnabled { get; init; } = false;
        public bool InitialLoop { get; init; } = true;
        public string InitialClimaxText { get; init; } = "WE DID IT!";

        public float ShowDurationSeconds { get; init; } = 18f;
        public float ClimaxStartSeconds { get; init; } = 10f;
        public float ClimaxEndSeconds { get; init; } = 17f;
        public float ClimaxEmitIntervalSeconds { get; init; } = 0.18f;

        /// <summary>World units per bitmap pixel for the climax text.</summary>
        public float ClimaxCellSize { get; init; } = 18f;

        /// <summary>Climax text origin offset from the player anchor (world units).</summary>
        public float ClimaxYOffset { get; init; } = 380f;
        public float ClimaxZOffset { get; init; } = -260f;

        /// <summary>Per-glyph particle lifetime (seconds).</summary>
        public float ClimaxLifetimeSeconds { get; init; } = 0.55f;
        public float ClimaxSizeStart { get; init; } = 14f;
        public float ClimaxSizeEnd { get; init; } = 8f;

        /// <summary>Maximum allowed dt per tick (seconds) — clamps stalls.</summary>
        public float MaxDeltaSeconds { get; init; } = 0.1f;

        /// <summary>Deterministic seed for reproducibility.</summary>
        public int RandomSeed { get; init; } = 0xF13E;

        public static FireworksServiceConfig Default => new();
    }
}
