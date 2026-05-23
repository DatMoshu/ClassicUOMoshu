// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="BuffParticleService"/>. Per-archetype emit
    /// intervals are baked in (mirroring the legacy <c>EmitInterval</c> switch).
    /// </summary>
    public sealed class BuffParticleServiceConfig
    {
        public bool InitialEnabled { get; init; } = true;
        public bool InitialRequire3DMode { get; init; } = true;

        // ===== Body-anchor offsets (world units relative to player foot) =====

        public float BodyLow { get; init; } = 6f;
        public float BodyMid { get; init; } = 22f;
        public float BodyTop { get; init; } = 44f;
        public float AuraRadius { get; init; } = 14f;

        // ===== Emit intervals per archetype (seconds) =====

        public float IntervalFire { get; init; } = 0.06f;
        public float IntervalIce { get; init; } = 0.12f;
        public float IntervalHoly { get; init; } = 0.10f;
        public float IntervalCurse { get; init; } = 0.12f;
        public float IntervalPoison { get; init; } = 0.10f;
        public float IntervalLightning { get; init; } = 0.18f;
        public float IntervalStat { get; init; } = 0.18f;
        public float IntervalDefense { get; init; } = 0.20f;
        public float IntervalStealth { get; init; } = 0.25f;
        public float IntervalFormShift { get; init; } = 0.30f;
        public float IntervalWind { get; init; } = 0.08f;
        public float IntervalDebuff { get; init; } = 0.20f;
        public float IntervalDefault { get; init; } = 0.40f;

        public float MaxDeltaSeconds { get; init; } = 0.1f;
        public int RandomSeed { get; init; } = 0xB00F;

        public static BuffParticleServiceConfig Default => new();
    }
}
