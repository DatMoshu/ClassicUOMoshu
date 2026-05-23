// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tick characterization tests for ParticleService (session 77).
// Locks the lifecycle sweep + physics behavior migrated from Particle3DSystem.Tick.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Effects
{
    /// <summary>
    /// Locks the per-frame lifecycle + physics semantics migrated from
    /// <c>Particle3DSystem.Tick</c> into <see cref="ParticleService.Tick"/>. The body
    /// reads <c>ctx.DeltaSeconds</c> (instead of the legacy Stopwatch) and accesses
    /// the SoA arrays directly. These tests confirm life advancement, death-on-expiry,
    /// physics integration (vel += accel*dt; pos += vel*dt), F_PINNED skipping the
    /// position update, and HighWater compaction.
    /// </summary>
    public sealed class ParticleServiceTickTests
    {
        private const byte F_PINNED = 0x02;

        private static ParticleService NewServiceEnabled()
        {
            var svc = new ParticleService(new ParticleServiceConfig
            {
                InitialEnabled = true,
                InitialVerboseLog = false,
            });
            return svc;
        }

        private static FrameTickContext Frame(float dt, double total = 0.0)
            => new FrameTickContext(dt, total, frameNumber: 1);

        // ===== Disabled path =====

        [Fact]
        public void Tick_DisabledService_DoesNotMutateParticles()
        {
            var svc = new ParticleService(new ParticleServiceConfig
            {
                InitialEnabled = false,
                InitialVerboseLog = false,
            });
            int idx = svc.Spawn(Vector3.One, Vector3.Zero, Vector3.Zero,
                life: 5f, size: 4f, sizeEnd: 0f,
                Color.White, Color.Transparent, flags: 0);

            FrameTickContext ctx = Frame(0.1f);
            svc.Tick(in ctx);

            // Disabled: life should not advance.
            svc.LifeArr[idx].Should().Be(0f);
            svc.AliveParticles.Should().Be(1);
        }

        // ===== Life advancement + death =====

        [Fact]
        public void Tick_AliveParticle_AdvancesLifeBySingleDt()
        {
            var svc = NewServiceEnabled();
            int idx = svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                life: 10f, size: 1f, sizeEnd: 0f,
                Color.White, Color.Black, flags: 0);

            FrameTickContext ctx = Frame(0.016f);
            svc.Tick(in ctx);

            svc.LifeArr[idx].Should().BeApproximately(0.016f, 1e-6f);
            svc.AliveParticles.Should().Be(1);
        }

        [Fact]
        public void Tick_ParticleExpires_KillsSlotAndDecrementsAlive()
        {
            var svc = NewServiceEnabled();
            int idx = svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                life: 0.05f, size: 1f, sizeEnd: 0f,
                Color.White, Color.Black, flags: 0);

            // First tick advances life past the cap → particle dies this frame.
            FrameTickContext ctx = Frame(0.1f);
            svc.Tick(in ctx);

            svc.FlagsArr[idx].Should().Be(0);
            svc.AliveParticles.Should().Be(0);
        }

        // ===== Physics integration =====

        [Fact]
        public void Tick_FreeParticle_IntegratesVelocityAndPosition()
        {
            var svc = NewServiceEnabled();
            int idx = svc.Spawn(
                pos: new Vector3(10f, 0f, 0f),
                vel: new Vector3(0f, 5f, 0f),
                accel: new Vector3(0f, 0f, 0f), // no accel → constant velocity
                life: 10f, size: 1f, sizeEnd: 0f,
                Color.White, Color.Black, flags: 0);

            FrameTickContext ctx = Frame(0.2f);
            svc.Tick(in ctx);

            // pos += vel * dt = 10 + (0,5*0.2,0) — but wait, vel update happens FIRST
            // (vel += accel*dt = unchanged since accel=0), then pos += vel*dt.
            svc.PosArr[idx].X.Should().Be(10f);
            svc.PosArr[idx].Y.Should().BeApproximately(1.0f, 1e-6f); // 5 * 0.2
            svc.PosArr[idx].Z.Should().Be(0f);
        }

        [Fact]
        public void Tick_AccelerationAppliedToVelocityBeforePosition()
        {
            var svc = NewServiceEnabled();
            int idx = svc.Spawn(
                pos: Vector3.Zero,
                vel: new Vector3(0f, 0f, 0f),
                accel: new Vector3(0f, 10f, 0f),
                life: 10f, size: 1f, sizeEnd: 0f,
                Color.White, Color.Black, flags: 0);

            FrameTickContext ctx = Frame(0.5f);
            svc.Tick(in ctx);

            // vel = 0 + 10*0.5 = 5; pos = 0 + 5*0.5 = 2.5
            svc.VelArr[idx].Y.Should().BeApproximately(5f, 1e-6f);
            svc.PosArr[idx].Y.Should().BeApproximately(2.5f, 1e-6f);
        }

        [Fact]
        public void Tick_PinnedParticle_SkipsPositionAndVelocityUpdate()
        {
            var svc = NewServiceEnabled();
            Vector3 startPos = new Vector3(7f, 3f, 1f);
            Vector3 startVel = new Vector3(5f, 5f, 5f);
            int idx = svc.Spawn(
                pos: startPos,
                vel: startVel,
                accel: new Vector3(100f, 100f, 100f),
                life: 10f, size: 1f, sizeEnd: 0f,
                Color.White, Color.Black, flags: F_PINNED);

            FrameTickContext ctx = Frame(0.5f);
            svc.Tick(in ctx);

            // F_PINNED: velocity AND position both untouched by the integrator.
            svc.PosArr[idx].Should().Be(startPos);
            svc.VelArr[idx].Should().Be(startVel);
        }

        // ===== HighWater compaction =====

        [Fact]
        public void Tick_TrailingDeadSlots_ShrinkHighWater()
        {
            var svc = NewServiceEnabled();
            // Spawn 3 short-lived particles; first tick should kill them all and
            // collapse HighWater to 0.
            for (int i = 0; i < 3; i++)
            {
                svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                    life: 0.05f, size: 1f, sizeEnd: 0f,
                    Color.White, Color.Black, flags: 0);
            }
            svc.HighWater.Should().Be(3);

            FrameTickContext ctx = Frame(0.1f);
            svc.Tick(in ctx);

            svc.AliveParticles.Should().Be(0);
            svc.HighWater.Should().Be(0);
        }

        [Fact]
        public void Tick_MixedAliveDead_KeepsHighWaterAtLastAliveIndex()
        {
            var svc = NewServiceEnabled();

            // Slot 0: dies this tick (life 0.05, dt 0.1)
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                life: 0.05f, size: 1f, sizeEnd: 0f, Color.White, Color.Black, flags: 0);
            // Slot 1: survives (life 10s)
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                life: 10f, size: 1f, sizeEnd: 0f, Color.White, Color.Black, flags: 0);
            // Slot 2: dies this tick
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                life: 0.05f, size: 1f, sizeEnd: 0f, Color.White, Color.Black, flags: 0);

            svc.HighWater.Should().Be(3);

            FrameTickContext ctx = Frame(0.1f);
            svc.Tick(in ctx);

            // Slot 1 still alive; HighWater is 1 + max alive index = 2.
            svc.AliveParticles.Should().Be(1);
            svc.HighWater.Should().Be(2);
        }

        // ===== Allocation regression (playbook §E) =====

        [Fact]
        public void Tick_SteadyState_IsAllocationFree()
        {
            var svc = NewServiceEnabled();
            // Fill 64 long-lived particles (lifetime > stress run).
            for (int i = 0; i < 64; i++)
            {
                svc.Spawn(Vector3.Zero, Vector3.One, Vector3.Zero,
                    life: 1000f, size: 1f, sizeEnd: 0f,
                    Color.White, Color.Black, flags: 0);
            }
            // Warm path: one tick to JIT the loop body.
            FrameTickContext warm = Frame(0.016f);
            svc.Tick(in warm);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            // 100 ticks * 64 alive = 6400 lifecycle iterations + 6400 physics integrations.
            for (int t = 0; t < 100; t++)
            {
                FrameTickContext ctx = Frame(0.016f, t * 0.016);
                svc.Tick(in ctx);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            (after - before).Should().Be(0,
                "Tick must be allocation-free per playbook §E. Lifecycle sweep + physics " +
                "integration operate on preallocated SoA arrays; no per-particle temp objects.");
        }
    }
}
