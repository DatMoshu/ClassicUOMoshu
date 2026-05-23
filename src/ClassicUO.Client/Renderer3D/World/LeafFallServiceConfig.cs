// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Tunable parameters for <see cref="LeafFallService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>LeafFallSystem</c>.
    /// </summary>
    public sealed class LeafFallServiceConfig
    {
        /// <summary>Hard upper bound on simultaneously-alive leaves. Pool size is fixed at construction.</summary>
        public int MaxLeaves { get; init; } = 1024;

        /// <summary>Peak spawn rate at the autumn drop window (leaves/second).</summary>
        public float MaxSpawnPerSecond { get; init; } = 30f;

        /// <summary>Fallback spawn-disc radius (world units) when no visible tree anchors exist.</summary>
        public float SpawnRadius { get; init; } = 480f;

        /// <summary>Fallback spawn-disc height above the player anchor (world units).</summary>
        public float SpawnHeight { get; init; } = 220f;

        /// <summary>Wind-vector influence on horizontal drift (multiplied with VectorXZ).</summary>
        public float WindAdvectionScale { get; init; } = 35f;

        /// <summary>Per-particle wind drift fraction inside the simulation step.</summary>
        public float WindDriftFraction { get; init; } = 0.6f;

        /// <summary>Sinusoidal sway amplitude in world units per second.</summary>
        public float SwayAmplitude { get; init; } = 18f;

        /// <summary>Sinusoidal sway frequency in radians per second.</summary>
        public float SwayFrequencyRad { get; init; } = 2.2f;

        /// <summary>Initial enabled state.</summary>
        public bool InitialEnabled { get; init; } = true;

        /// <summary>RNG seed for deterministic spawn / color jitter.</summary>
        public int RandomSeed { get; init; } = unchecked((int)0xCAFE_1EAF);

        /// <summary>Maximum allowed dt per tick (s) — clamps long stalls so leaves don't lurch.</summary>
        public float MaxDeltaSeconds { get; init; } = 0.1f;

        public static LeafFallServiceConfig Default => new();
    }
}
