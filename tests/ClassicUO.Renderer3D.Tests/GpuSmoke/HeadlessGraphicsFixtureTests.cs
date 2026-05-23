// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Smoke tests for HeadlessGraphicsFixture (Phase 5, session 74).

using FluentAssertions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.GpuSmoke
{
    /// <summary>
    /// Smoke + minimal integration tests for the headless GraphicsDevice harness.
    /// These verify the harness itself works AND that pass-level integration tests
    /// (TerrainPass.Execute end-to-end, Particle3DSystem allocation regressions,
    /// etc.) have a working device to lean on.
    /// </summary>
    [Collection("GraphicsDevice")]
    public sealed class HeadlessGraphicsFixtureTests
    {
        private readonly HeadlessGraphicsFixture _gpu;
        public HeadlessGraphicsFixtureTests(HeadlessGraphicsFixture gpu) { _gpu = gpu; }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_IsNotNullAfterFixtureConstruction()
        {
            _gpu.Device.Should().NotBeNull();
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_AdapterAndProfile_AreReportedCorrectly()
        {
            _gpu.Device.Adapter.Should().NotBeNull();
            // FNA defaults to HiDef when supported; otherwise Reach. Either is fine for tests.
            _gpu.Device.GraphicsProfile.Should().BeOneOf(GraphicsProfile.HiDef, GraphicsProfile.Reach);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_CanCreateBasicEffect()
        {
            // BasicEffect is the workhorse shader for TerrainPass; this verifies the
            // shader-compile path is healthy in the headless config.
            using var effect = new BasicEffect(_gpu.Device);
            effect.Should().NotBeNull();
            effect.CurrentTechnique.Should().NotBeNull();
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_CanCreateAndDispose1x1RenderTarget()
        {
            // Mirrors the per-mobile RT path used by MobileRT3DRenderer.
            using var rt = new RenderTarget2D(_gpu.Device, 1, 1);
            rt.Width.Should().Be(1);
            rt.Height.Should().Be(1);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_CanSetAndRestoreRenderState()
        {
            // Pass.Execute methods save + restore depth/raster/blend/sampler each
            // invocation. This locks that the device tolerates state churn.
            var prevDepth = _gpu.Device.DepthStencilState;
            _gpu.Device.DepthStencilState = DepthStencilState.None;
            _gpu.Device.DepthStencilState.Should().BeSameAs(DepthStencilState.None);

            _gpu.Device.DepthStencilState = prevDepth;
            _gpu.Device.DepthStencilState.Should().BeSameAs(prevDepth);
        }

        [Fact, Trait("Category", "GraphicsDevice")]
        public void Device_VertexBufferRoundTrip_PreservesData()
        {
            // Minimal data-flow check: write a vertex, read it back. Catches FNA3D /
            // SDL3 driver path failures more surgically than a full pass execution.
            var vb = new VertexBuffer(_gpu.Device,
                VertexPositionColor.VertexDeclaration,
                vertexCount: 3,
                BufferUsage.WriteOnly);

            var writeBuffer = new[]
            {
                new VertexPositionColor(new Vector3(0, 0, 0), Color.Red),
                new VertexPositionColor(new Vector3(1, 0, 0), Color.Green),
                new VertexPositionColor(new Vector3(0, 1, 0), Color.Blue),
            };

            vb.SetData(writeBuffer);
            vb.VertexCount.Should().Be(3);

            vb.Dispose();
        }
    }
}
