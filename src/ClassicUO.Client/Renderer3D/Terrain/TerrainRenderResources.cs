// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Terrain domain (ADR-012 Phase 3, session 63).

using ClassicUO.Renderer.Renderer3D; // GroundOverlayEffect
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Terrain
{
    /// <summary>
    /// Default <see cref="ITerrainRenderResources"/> implementation. Construction logic
    /// lifted verbatim from <c>World3DRenderer.EnsureResources</c> (pre-Phase-3); the
    /// three resources move from W3DR private statics to this service.
    /// </summary>
    internal sealed class TerrainRenderResources : ITerrainRenderResources
    {
        private readonly IDisposableRegistry _disposables;

        private BasicEffect _effect;
        private RasterizerState _wireframe;
        private GroundOverlayEffect _groundOverlay;
        private bool _groundOverlayLoadAttempted;

        public TerrainRenderResources(IDisposableRegistry disposables)
        {
            _disposables = disposables;
        }

        public BasicEffect Effect => _effect;
        public RasterizerState Wireframe => _wireframe;
        public GroundOverlayEffect GroundOverlay => _groundOverlay;

        public void EnsureLoaded(GraphicsDevice gd)
        {
            if (gd is null) return;

            if (_effect is null)
            {
                _effect = new BasicEffect(gd);
                _disposables?.Track(_effect);
            }

            if (_wireframe is null)
            {
                _wireframe = new RasterizerState
                {
                    FillMode = FillMode.WireFrame,
                    CullMode = CullMode.None,
                };
                _disposables?.Track(_wireframe);
            }

            if (!_groundOverlayLoadAttempted)
            {
                _groundOverlayLoadAttempted = true;
                _groundOverlay = GroundOverlayEffect.TryLoad(gd);
                if (_groundOverlay is not null)
                    _disposables?.Track(_groundOverlay);
            }
        }
    }
}
