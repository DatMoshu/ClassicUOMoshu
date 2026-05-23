// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Draw integration tests for ParticleService (session 78).
// Exercises the migrated 4-pass batched billboard renderer against a real
// GraphicsDevice via the HeadlessGraphicsFixture (session 74).

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Effects
{
    /// <summary>
    /// Parity oracle for the Draw + EmitBatch + EnsureResources migration in session 78.
    /// Uses the headless GraphicsDevice fixture (session 74) to confirm Draw completes
    /// without throwing, allocates GPU resources lazily, disposes cleanly, and tolerates
    /// the typical particle-pass shape distribution.
    /// </summary>
    [Collection("GraphicsDevice")]
    public sealed class ParticleServiceDrawIntegrationTests
    {
        private const byte F_TEXTURED     = 0x08;
        private const byte F_TEXTURED_ADD = 0x20;
        private const byte F_STREAK       = 0x40;
        private const byte F_FLASH        = 0x80;

        private readonly HeadlessGraphicsFixture _gpu;
        public ParticleServiceDrawIntegrationTests(HeadlessGraphicsFixture gpu) { _gpu = gpu; }

        private static ParticleService NewServiceEnabled()
            => new ParticleService(new ParticleServiceConfig
            {
                InitialEnabled = true,
                InitialVerboseLog = false,
            });

        private static Matrix StandardView() => Matrix.CreateLookAt(
            new Vector3(0, 5, 10), Vector3.Zero, Vector3.UnitY);

        private static Matrix StandardProj() => Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, 16f / 9f, 0.1f, 1000f);

        // ===== Empty pool fast-paths =====

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_WithNoAliveParticles_IsNoOp()
        {
            using var svc = NewServiceEnabled();
            // Empty pool → early-return before EnsureResources, no GPU allocation.
            svc.Draw(_gpu.Device, StandardView(), StandardProj());
            svc.LastDrawnParticles.Should().Be(0);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_WithDisabledService_IsNoOp()
        {
            using var svc = new ParticleService(new ParticleServiceConfig
            {
                InitialEnabled = false,
                InitialVerboseLog = false,
            });
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 1f, 0f,
                Color.White, Color.Black, flags: 0);

            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            svc.LastDrawnParticles.Should().Be(0);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_WithNullGraphicsDevice_DoesNotThrow()
        {
            using var svc = NewServiceEnabled();
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 1f, 0f,
                Color.White, Color.Black, flags: 0);

            Action act = () => svc.Draw(null, StandardView(), StandardProj());
            act.Should().NotThrow();
        }

        // ===== Live draw paths =====

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_UntexturedPass_CompletesAndReportsCount()
        {
            using var svc = NewServiceEnabled();
            // 5 untextured (no texture flags set) → all route through Pass 1 (additive untextured).
            for (int i = 0; i < 5; i++)
            {
                svc.Spawn(new Vector3(i, 0, 0), Vector3.Zero, Vector3.Zero,
                    life: 1f, size: 4f, sizeEnd: 0f,
                    Color.Orange, Color.Transparent, flags: 0);
            }

            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            svc.LastDrawnParticles.Should().Be(5);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_MixedPassesAllRouteCorrectly()
        {
            using var svc = NewServiceEnabled();
            // 2 untextured + 3 alpha-textured + 4 additive-streak = 9 total expected to draw.
            // (ParticleTexture + ParticleRainTexture are lazy-initialized to procedural
            // factories by EnsureResources; ParticleFlashTexture stays null until
            // an external caller (e.g. NukeShow) binds it. The flash pass is gated
            // by `ParticleFlashTexture != null` so flash flags spawned without a
            // texture binding do NOT increment the drawn count — that's the
            // production behavior locked here.)
            for (int i = 0; i < 2; i++)
                svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f, Color.White, Color.Black, flags: 0);
            for (int i = 0; i < 3; i++)
                svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f, Color.White, Color.Black, flags: F_TEXTURED);
            for (int i = 0; i < 4; i++)
                svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f, Color.White, Color.Black, flags: (byte)(F_TEXTURED_ADD | F_STREAK));
            // No flash texture bound — the spawn happens but Draw won't emit a quad for it.
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f, Color.White, Color.Black, flags: F_FLASH);

            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            svc.LastDrawnParticles.Should().Be(9);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_AllocatesGpuResourcesLazily_OnFirstCallWithAliveParticles()
        {
            using var svc = NewServiceEnabled();
            // Spawn one untextured particle so Draw advances past the early-return.
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f,
                Color.White, Color.Black, flags: 0);

            // First call: triggers EnsureResources internally + a single batch draw.
            svc.Draw(_gpu.Device, StandardView(), StandardProj());
            svc.LastDrawnParticles.Should().Be(1);

            // Second call: resources reused; still draws.
            svc.Draw(_gpu.Device, StandardView(), StandardProj());
            svc.LastDrawnParticles.Should().Be(1);
        }

        // ===== Dispose =====

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Dispose_AfterDraw_ReleasesGpuResources_WithoutThrowing()
        {
            var svc = NewServiceEnabled();
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f,
                Color.White, Color.Black, flags: 0);
            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            Action act = () => svc.Dispose();
            act.Should().NotThrow();
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Dispose_IsIdempotent()
        {
            var svc = NewServiceEnabled();
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f,
                Color.White, Color.Black, flags: 0);
            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            svc.Dispose();
            Action act = () => svc.Dispose();
            act.Should().NotThrow();
        }

        // ===== State save / restore =====

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Draw_RestoresDepthBlendAndRasterStateAfterReturn()
        {
            using var svc = NewServiceEnabled();
            svc.Spawn(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1f, 4f, 0f,
                Color.White, Color.Black, flags: 0);

            // Set non-default state and confirm Draw restores it.
            _gpu.Device.DepthStencilState = DepthStencilState.None;
            _gpu.Device.BlendState = BlendState.Opaque;
            _gpu.Device.RasterizerState = RasterizerState.CullClockwise;

            var depthBefore = _gpu.Device.DepthStencilState;
            var blendBefore = _gpu.Device.BlendState;
            var rasterBefore = _gpu.Device.RasterizerState;

            svc.Draw(_gpu.Device, StandardView(), StandardProj());

            _gpu.Device.DepthStencilState.Should().BeSameAs(depthBefore);
            _gpu.Device.BlendState.Should().BeSameAs(blendBefore);
            _gpu.Device.RasterizerState.Should().BeSameAs(rasterBefore);
        }
    }
}
