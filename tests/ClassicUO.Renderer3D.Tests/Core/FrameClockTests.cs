// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for FrameClock (Renderer3D Core, ADR-012).

using ClassicUO.Renderer.Core;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Core
{
    /// <summary>
    /// Locks the authoritative dt contract: services + passes that read
    /// <see cref="FrameTickContext.DeltaSeconds"/> trust that it's clamped and monotonic.
    /// The "single source of truth for per-frame timing" rule (ADR-012 §2) hinges on
    /// these guarantees holding.
    /// </summary>
    public sealed class FrameClockTests
    {
        [Fact]
        public void InitialCurrent_BeforeAdvance_IsZeroState()
        {
            var clock = new FrameClock();

            clock.Current.DeltaSeconds.Should().Be(0f);
            clock.Current.TotalSeconds.Should().Be(0.0);
            clock.Current.FrameNumber.Should().Be(0);
        }

        [Fact]
        public void Advance_NormalDelta_PassesThroughAndAccumulates()
        {
            var clock = new FrameClock();
            var ctx = clock.Advance(0.016f); // ~60 FPS

            ctx.DeltaSeconds.Should().BeApproximately(0.016f, 1e-6f);
            ctx.TotalSeconds.Should().BeApproximately(0.016, 1e-6);
            ctx.FrameNumber.Should().Be(1);
        }

        [Fact]
        public void Advance_BelowMinDelta_ClampsToMinDeltaSeconds()
        {
            var clock = new FrameClock();
            var ctx = clock.Advance(0f); // zero dt would break dt-divisions

            ctx.DeltaSeconds.Should().Be(FrameClock.MinDeltaSeconds);
            // ≈1/600 s ≈ 1.67ms — protects integrators downstream.
            ctx.DeltaSeconds.Should().BeApproximately(1f / 600f, 1e-7f);
        }

        [Fact]
        public void Advance_AboveMaxDelta_ClampsToMaxDeltaSeconds()
        {
            // Long pause / debugger break / minimised window scenario — playbook lesson
            // about not letting wind/fire/weather integrate a 5-second gap.
            var clock = new FrameClock();
            var ctx = clock.Advance(5.0f);

            ctx.DeltaSeconds.Should().Be(FrameClock.MaxDeltaSeconds);
            ctx.DeltaSeconds.Should().Be(0.2f); // 6 frames at 30 FPS
        }

        [Fact]
        public void Advance_NegativeDelta_ClampsUpToMinDelta()
        {
            // Negative dt should never happen but if it does, we want a safe floor not a
            // backwards jump in totals.
            var clock = new FrameClock();
            var ctx = clock.Advance(-1.0f);

            ctx.DeltaSeconds.Should().Be(FrameClock.MinDeltaSeconds);
            ctx.TotalSeconds.Should().BeGreaterThan(0.0);
        }

        [Fact]
        public void Advance_IsMonotonic_OverManyFrames()
        {
            var clock = new FrameClock();
            double last = 0.0;
            long lastFrame = 0;
            for (int i = 0; i < 1000; i++)
            {
                var ctx = clock.Advance(0.016f);
                ctx.TotalSeconds.Should().BeGreaterThan(last);
                ctx.FrameNumber.Should().BeGreaterThan(lastFrame);
                last = ctx.TotalSeconds;
                lastFrame = ctx.FrameNumber;
            }
            // After 1000 frames at 16ms, total should be ~16s.
            clock.Current.TotalSeconds.Should().BeApproximately(16.0, 0.001);
            clock.Current.FrameNumber.Should().Be(1000);
        }

        [Fact]
        public void Current_ReturnsLastAdvanceResult()
        {
            var clock = new FrameClock();
            clock.Advance(0.01f);
            clock.Advance(0.02f);
            var third = clock.Advance(0.03f);

            clock.Current.DeltaSeconds.Should().Be(third.DeltaSeconds);
            clock.Current.TotalSeconds.Should().Be(third.TotalSeconds);
            clock.Current.FrameNumber.Should().Be(3);
        }
    }
}
