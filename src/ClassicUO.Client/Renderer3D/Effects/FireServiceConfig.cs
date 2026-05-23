// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="FireService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>FireSpreadSystem</c>.
    /// </summary>
    public sealed class FireServiceConfig
    {
        public bool InitialEnabled { get; init; } = true;

        /// <summary>Default ignition radius in world units.</summary>
        public float DefaultRadius { get; init; } = 35f;

        /// <summary>Default fire lifetime in seconds.</summary>
        public float DefaultLifetime { get; init; } = 9f;

        /// <summary>Peak ember spawn rate per fire patch (per second).</summary>
        public float EmberRatePerSec { get; init; } = 90f;

        /// <summary>Peak smoke spawn rate per fire patch (per second).</summary>
        public float SmokeRatePerSec { get; init; } = 18f;

        /// <summary>Hard cap on simultaneously-burning fires. Pool size fixed at construction.</summary>
        public int MaxFires { get; init; } = 16;

        /// <summary>Wind-vector multiplier applied to ember/smoke horizontal drift.</summary>
        public float WindAdvectionScale { get; init; } = 35f;

        /// <summary>Floor on ignite radius (world units).</summary>
        public float MinIgniteRadius { get; init; } = 2f;

        /// <summary>Floor on ignite lifetime (seconds).</summary>
        public float MinIgniteLifetime { get; init; } = 0.5f;

        /// <summary>Maximum allowed dt per tick (seconds) — clamps stalls.</summary>
        public float MaxDeltaSeconds { get; init; } = 0.1f;

        /// <summary>Deterministic seed for reproducibility in tests/replay.</summary>
        public int RandomSeed { get; init; } = 0xF1AE;

        public static FireServiceConfig Default => new();
    }
}
