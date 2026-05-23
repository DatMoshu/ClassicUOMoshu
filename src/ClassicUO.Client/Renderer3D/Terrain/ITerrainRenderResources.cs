// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Terrain domain (ADR-012 Phase 3, session 63).

using ClassicUO.Renderer.Renderer3D; // GroundOverlayEffect
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Terrain
{
    /// <summary>
    /// GPU resources shared by <see cref="ClassicUO.Renderer.Passes.TerrainPass"/> and
    /// <see cref="ClassicUO.Renderer.Passes.GroundOverlayPass"/>: the heightmap's
    /// <see cref="BasicEffect"/>, the wireframe <see cref="RasterizerState"/>, and the
    /// ground-overlay shader. Lifted from <c>World3DRenderer</c> private statics in
    /// session 63 so passes can have real Execute bodies without reaching back into the
    /// legacy facade.
    /// </summary>
    /// <remarks>
    /// All resources are GPU-side <see cref="System.IDisposable"/> handles; the service
    /// tracks them on the renderer's <see cref="ClassicUO.Renderer.Core.IDisposableRegistry"/>
    /// at first use so they're released at shutdown. Internal because
    /// <see cref="GroundOverlayEffect"/> is internal.
    /// </remarks>
    internal interface ITerrainRenderResources
    {
        /// <summary>
        /// Shared <see cref="BasicEffect"/> for the land heightmap pass. Lazily constructed
        /// on first <see cref="EnsureLoaded"/> call. Vertex-color + texture + optional fog.
        /// </summary>
        BasicEffect Effect { get; }

        /// <summary>Wireframe <see cref="RasterizerState"/> used when <c>Wireframe=true</c>.</summary>
        RasterizerState Wireframe { get; }

        /// <summary>
        /// The wet/snow ground overlay shader. May be <c>null</c> if the shader file failed
        /// to load — callers must null-check (matches legacy <c>_groundOverlayEffect</c> contract).
        /// </summary>
        GroundOverlayEffect GroundOverlay { get; }

        /// <summary>
        /// Lazily construct all three resources on the supplied <see cref="GraphicsDevice"/>.
        /// Safe to call every frame — internal flags ensure single construction. Required
        /// because GPU resources cannot be allocated in the service constructor (GraphicsDevice
        /// isn't available at <c>Renderer3DServices</c> registration time).
        /// </summary>
        void EnsureLoaded(GraphicsDevice gd);
    }
}
