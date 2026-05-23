// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Characterization tests for the particle spawn pool.
//
// Session 75 wrote these against the legacy Particle3DSystem static. Session 76
// migrated state ownership into ParticleService; the legacy class is now a thin
// facade that delegates to the service via Renderer3DHost. Tests retargeted to
// drive ParticleService directly — same behavior, no host-binding dependency,
// cleaner isolation between test cases (new ParticleService per test, no shared
// global static state).

using System;
using ClassicUO.Renderer.Effects;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Effects
{
    /// <summary>
    /// Characterization tests for the SoA spawn pool in <see cref="ParticleService"/>.
    /// Locks playbook §E allocation contract — Spawn dirties array elements by index
    /// and Clear walks the high-water flag slice. Both must remain allocation-free
    /// in steady state. <see cref="Particle3DSystem.Spawn"/> / <c>Clear</c> are thin
    /// facades over these methods so any future change to the SoA semantics changes
    /// both surfaces atomically.
    /// </summary>
    public sealed class Particle3DSystemSpawnPoolTests
    {
        private static ParticleService NewService() =>
            new ParticleService(ParticleServiceConfig.Default);

        // ===== Basic spawn / alive counting =====

        [Fact]
        public void Spawn_FromEmptyPool_ReturnsValidIndexAndIncrementsAlive()
        {
            var svc = NewService();
            int idx = SpawnDefault(svc);

            idx.Should().BeInRange(0, ParticleService.MaxParticles - 1);
            svc.AliveParticles.Should().Be(1);
            svc.HighWater.Should().BeGreaterOrEqualTo(idx + 1);
        }

        [Fact]
        public void Spawn_ManyParticles_AliveCountMatchesSpawnCount()
        {
            var svc = NewService();
            for (int i = 0; i < 100; i++)
            {
                SpawnDefault(svc);
            }
            svc.AliveParticles.Should().Be(100);
        }

        [Fact]
        public void Spawn_PoolFull_ReturnsMinusOne()
        {
            var svc = NewService();
            // Fill the pool exactly.
            for (int i = 0; i < ParticleService.MaxParticles; i++)
            {
                SpawnDefault(svc).Should().NotBe(-1, $"alive should fit at slot #{i}");
            }
            svc.AliveParticles.Should().Be(ParticleService.MaxParticles);

            // One more should fail.
            SpawnDefault(svc).Should().Be(-1);
            svc.AliveParticles.Should().Be(ParticleService.MaxParticles);
        }

        [Fact]
        public void Spawn_DistinctIndicesReturned_WhileBelowCap()
        {
            var svc = NewService();
            var seenIndices = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 256; i++)
            {
                int idx = SpawnDefault(svc);
                idx.Should().NotBe(-1);
                seenIndices.Add(idx).Should().BeTrue($"duplicate slot reused while pool not full: {idx}");
            }
        }

        // ===== Clear =====

        [Fact]
        public void Clear_DropsAllParticles_AndResetsCounters()
        {
            var svc = NewService();
            for (int i = 0; i < 50; i++) SpawnDefault(svc);
            svc.AliveParticles.Should().Be(50);

            svc.Clear();

            svc.AliveParticles.Should().Be(0);
            svc.HighWater.Should().Be(0);
        }

        [Fact]
        public void Clear_IsIdempotent()
        {
            var svc = NewService();
            svc.Clear();
            svc.Clear();
            svc.Clear();

            svc.AliveParticles.Should().Be(0);
            svc.HighWater.Should().Be(0);
        }

        [Fact]
        public void Clear_ThenSpawn_AllowsFullPoolAgain()
        {
            var svc = NewService();
            for (int i = 0; i < ParticleService.MaxParticles; i++) SpawnDefault(svc);
            svc.AliveParticles.Should().Be(ParticleService.MaxParticles);

            svc.Clear();

            for (int i = 0; i < ParticleService.MaxParticles; i++)
            {
                SpawnDefault(svc).Should().NotBe(-1);
            }
            svc.AliveParticles.Should().Be(ParticleService.MaxParticles);
        }

        // ===== Allocation contract (playbook §E) =====

        [Fact]
        public void Spawn_SteadyState_IsAllocationFree()
        {
            var svc = NewService();
            // Warm path so JIT compiles + arrays are touched.
            for (int i = 0; i < 32; i++) SpawnDefault(svc);
            svc.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                SpawnDefault(svc);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            (after - before).Should().Be(0,
                "Spawn must remain allocation-free per playbook §E — the SoA pool " +
                "is preallocated and Spawn only dirties array elements by index.");
        }

        [Fact]
        public void Clear_SteadyState_IsAllocationFree()
        {
            var svc = NewService();
            for (int i = 0; i < 1024; i++) SpawnDefault(svc);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            svc.Clear();
            long after = GC.GetAllocatedBytesForCurrentThread();

            (after - before).Should().Be(0, "Clear is a fixed-size loop over the flag array.");
        }

        // ===== Pool layout invariants =====

        [Fact]
        public void HighWater_NeverExceedsMaxParticles()
        {
            var svc = NewService();
            for (int i = 0; i < ParticleService.MaxParticles; i++) SpawnDefault(svc);
            svc.HighWater.Should().BeLessOrEqualTo(ParticleService.MaxParticles);
        }

        [Fact]
        public void MaxParticles_IsTheCanonicalCap()
        {
            // Locks the documented constant — if anyone changes MaxParticles without a
            // playbook entry, this test forces a deliberate update. Both surfaces
            // (service constant + legacy facade constant) must agree.
            ParticleService.MaxParticles.Should().Be(8192);
            ClassicUO.Renderer.Renderer3D.Particle3DSystem.MaxParticles.Should().Be(8192);
            ParticleService.MaxParticles.Should().Be(ClassicUO.Renderer.Renderer3D.Particle3DSystem.MaxParticles);
        }

        // ===== Helpers =====

        private static int SpawnDefault(ParticleService svc)
        {
            return svc.Spawn(
                pos: Vector3.Zero,
                vel: Vector3.UnitY,
                accel: new Vector3(0f, -9.8f, 0f),
                life: 1.0f,
                size: 4f,
                sizeEnd: 0f,
                colorStart: Color.White,
                colorEnd: Color.Transparent,
                flags: 0);
        }
    }
}
