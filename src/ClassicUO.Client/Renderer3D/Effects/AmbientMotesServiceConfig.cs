// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="AmbientMotesService"/>. Mirrors the prior hardcoded
    /// constants in the legacy <c>AmbientMotes3D</c>.
    /// </summary>
    public sealed class AmbientMotesServiceConfig
    {
        // ===== Initial state =====

        public bool InitialEnabled { get; init; } = false;
        public string InitialPalette { get; init; } = "kokiri";
        public bool UseSoftGlow { get; init; } = true;

        // ===== Cylinder geometry around the player anchor (world units; 22 = 1 tile) =====

        public float Radius { get; init; } = 264f;       // 12 tiles
        public float MinHeight { get; init; } = 8f;
        public float MaxHeight { get; init; } = 140f;

        // ===== Population =====

        public int TargetAlive { get; init; } = 120;
        public float SpawnRatePerSecond { get; init; } = 28f;

        /// <summary>Hard cap on motes spawned in a single tick (defends against long stalls).</summary>
        public int PerTickSpawnCap { get; init; } = 32;

        /// <summary>Maximum allowed dt per tick (s) — clamps stalls so motes don't burst.</summary>
        public float MaxDeltaSeconds { get; init; } = 0.1f;

        // ===== Motion =====

        public float DriftUp { get; init; } = 12f;
        public float SwayHorizontalMax { get; init; } = 22f;
        public float LifetimeMin { get; init; } = 6f;
        public float LifetimeMax { get; init; } = 11f;

        // ===== Visuals =====

        public float SizeStart { get; init; } = 5f;
        public float SizeEnd { get; init; } = 1f;

        // ===== RNG =====

        /// <summary>Deterministic seed for reproducibility in tests/replay.</summary>
        public int RandomSeed { get; init; } = 0xA17B;

        public static AmbientMotesServiceConfig Default => new();
    }
}
