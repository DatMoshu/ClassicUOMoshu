// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// One stage of the renderer's draw pipeline. Concrete passes (SkyPass, TerrainPass,
    /// GroundOverlayPass, StaticGeometryPass, MobilePass, AtmospherePass, OverlayPass) are
    /// registered with the <see cref="RenderPassPipeline"/> and executed in
    /// <see cref="Order"/> sequence.
    /// </summary>
    /// <remarks>
    /// Each pass owns its own render state. Save and restore depth / raster / blend / sampler
    /// state inside <see cref="Execute"/> — the pipeline does not normalise state between
    /// passes. This keeps state changes minimal and explicit.
    /// </remarks>
    internal interface IRenderPass
    {
        /// <summary>Diagnostic name shown in profilers and the debug overlay.</summary>
        string Name { get; }

        /// <summary>Execution order (ascending). See <see cref="RenderPassOrder"/> for canonical values.</summary>
        int Order { get; }

        /// <summary>Whether this pass should run this frame. Cheap to evaluate.</summary>
        bool IsEnabled { get; }

        /// <summary>Render this pass.</summary>
        void Execute(in RenderPassContext ctx);
    }

    /// <summary>
    /// Canonical ordering values for the seven core renderer passes (ADR-012 §Architecture).
    /// Custom passes may interleave by choosing values between these sentinels.
    /// </summary>
    /// <remarks>
    /// <see cref="GroundOverlay"/> (the wet/snow/fall shader pass over terrain) sits between
    /// <see cref="Terrain"/> and <see cref="StaticGeometry"/> because it tints ground only —
    /// running it later would paint over statics and the player. The 500 slot is reserved
    /// for a future screen-space post-FX pass (bloom, tonemap, etc.).
    /// </remarks>
    public static class RenderPassOrder
    {
        public const int Sky = 100;
        public const int Terrain = 200;
        public const int GroundOverlay = 250;
        public const int StaticGeometry = 300;
        public const int Mobile = 400;
        public const int Atmosphere = 600;
        public const int Overlay = 700;
    }
}
