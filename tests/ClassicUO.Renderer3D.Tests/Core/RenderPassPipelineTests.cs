// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for RenderPassPipeline (Renderer3D Core, ADR-012 §7).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Core;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Core
{
    /// <summary>
    /// Locks the seven-pass pipeline contract from ADR-012 §7. Tests cover registration,
    /// freeze semantics, ordering by <see cref="IRenderPass.Order"/>, IsEnabled gating,
    /// and the no-allocation iteration path. <see cref="RenderPassContext"/> is the
    /// per-frame ctx; passes only see it through Execute.
    /// </summary>
    /// <remarks>
    /// No <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/> needed — the
    /// pipeline orchestrator is pure logic. GPU-bound integration tests for individual
    /// passes (TerrainPass, etc.) require an FNA harness which lands separately.
    /// </remarks>
    public sealed class RenderPassPipelineTests
    {
        [Fact]
        public void Register_RejectsNullPass()
        {
            var pipeline = new RenderPassPipeline();
            Action act = () => pipeline.Register(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Register_AfterFreeze_Throws()
        {
            var pipeline = new RenderPassPipeline();
            pipeline.Register(new RecordingPass("first", 100));
            pipeline.Freeze();

            Action act = () => pipeline.Register(new RecordingPass("second", 200));
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Execute_AutoFreezesPipeline()
        {
            // The first Execute should freeze even if Freeze wasn't called explicitly.
            var pipeline = new RenderPassPipeline();
            pipeline.Register(new RecordingPass("only", 100));

            RenderPassContext ctx = MakeCtx();
            pipeline.Execute(in ctx);

            Action act = () => pipeline.Register(new RecordingPass("late", 200));
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Freeze_IsIdempotent()
        {
            var pipeline = new RenderPassPipeline();
            pipeline.Register(new RecordingPass("only", 100));

            pipeline.Freeze();
            pipeline.Freeze(); // second freeze must not throw.
            pipeline.Freeze();

            pipeline.Count.Should().Be(1);
        }

        [Fact]
        public void Execute_RunsPassesInOrderAscending()
        {
            var pipeline = new RenderPassPipeline();
            var log = new List<string>();
            // Register OUT of order; pipeline should sort by Order at freeze.
            pipeline.Register(new RecordingPass("mobile",    RenderPassOrder.Mobile,         log));
            pipeline.Register(new RecordingPass("sky",       RenderPassOrder.Sky,            log));
            pipeline.Register(new RecordingPass("overlay",   RenderPassOrder.Overlay,        log));
            pipeline.Register(new RecordingPass("static",    RenderPassOrder.StaticGeometry, log));
            pipeline.Register(new RecordingPass("terrain",   RenderPassOrder.Terrain,        log));
            pipeline.Register(new RecordingPass("ground",    RenderPassOrder.GroundOverlay,  log));
            pipeline.Register(new RecordingPass("atmo",      RenderPassOrder.Atmosphere,     log));

            RenderPassContext ctx = MakeCtx();
            pipeline.Execute(in ctx);

            log.Should().Equal("sky", "terrain", "ground", "static", "mobile", "atmo", "overlay");
        }

        [Fact]
        public void Execute_SkipsDisabledPasses()
        {
            var pipeline = new RenderPassPipeline();
            var log = new List<string>();
            pipeline.Register(new RecordingPass("a", 100, log));
            pipeline.Register(new RecordingPass("b", 200, log) { Enabled = false });
            pipeline.Register(new RecordingPass("c", 300, log));

            RenderPassContext ctx = MakeCtx();
            pipeline.Execute(in ctx);

            log.Should().Equal("a", "c");
        }

        [Fact]
        public void Execute_AllDisabled_IsHarmlessNoOp()
        {
            var pipeline = new RenderPassPipeline();
            pipeline.Register(new RecordingPass("a", 100) { Enabled = false });
            pipeline.Register(new RecordingPass("b", 200) { Enabled = false });

            RenderPassContext ctx = MakeCtx();
            pipeline.Execute(in ctx); // Should not throw.
        }

        [Fact]
        public void Execute_PropagatesContextToEveryPass()
        {
            var pipeline = new RenderPassPipeline();
            var seen = new List<long>();
            var recorder = new ContextRecorderPass(seen);
            pipeline.Register(recorder);

            var frame = new FrameTickContext(0.016f, 1.234, 99);
            RenderPassContext ctx = new RenderPassContext(in frame, graphics: null, camera: null);
            pipeline.Execute(in ctx);

            seen.Should().Equal(99L);
        }

        [Fact]
        public void Count_ReflectsRegistrations()
        {
            var pipeline = new RenderPassPipeline();
            pipeline.Count.Should().Be(0);

            pipeline.Register(new RecordingPass("a", 100));
            pipeline.Count.Should().Be(1);

            pipeline.Register(new RecordingPass("b", 200));
            pipeline.Count.Should().Be(2);
        }

        // ===== Test doubles =====

        private static RenderPassContext MakeCtx()
        {
            var frame = new FrameTickContext(0.016f, 0.016, 1);
            return new RenderPassContext(in frame, graphics: null, camera: null);
        }

        private sealed class RecordingPass : IRenderPass
        {
            private readonly List<string> _log;
            public RecordingPass(string name, int order, List<string> log = null)
            {
                Name = name;
                Order = order;
                _log = log;
                Enabled = true;
            }
            public string Name { get; }
            public int Order { get; }
            public bool Enabled { get; set; }
            public bool IsEnabled => Enabled;
            public void Execute(in RenderPassContext ctx) => _log?.Add(Name);
        }

        private sealed class ContextRecorderPass : IRenderPass
        {
            private readonly List<long> _frames;
            public ContextRecorderPass(List<long> frames) { _frames = frames; }
            public string Name => "ctx-recorder";
            public int Order => 100;
            public bool IsEnabled => true;
            public void Execute(in RenderPassContext ctx) => _frames.Add(ctx.Frame.FrameNumber);
        }
    }
}
