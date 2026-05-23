// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D World service (ADR-012).

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Pure-math implementation of <see cref="IMousePickService"/>. No GPU or per-frame
    /// state — every input arrives via parameters or the injected
    /// <see cref="IRenderCameraSource"/>. Allocation-free in steady state (Viewport is
    /// a struct; intermediate Vector3s are stack-allocated).
    /// </summary>
    public sealed class MousePickService : IMousePickService
    {
        private readonly MousePickServiceConfig _config;
        private readonly IRenderCameraSource _camera;

        public MousePickService(MousePickServiceConfig config, IRenderCameraSource camera)
        {
            if (camera is null) throw new ArgumentNullException(nameof(camera));
            _config = config;
            _camera = camera;
        }

        public bool TryPickGroundTile(
            int viewportX, int viewportY, int viewportWidth, int viewportHeight,
            int mouseX, int mouseY,
            float playerUoZ,
            out int tileX, out int tileY, out Vector3 worldHit)
        {
            tileX = 0; tileY = 0; worldHit = Vector3.Zero;

            if (!_camera.TryGetLastFrame(out Matrix view, out Matrix projection, out int capturedW, out _))
                return false;
            if (capturedW == 0)
                return false;

            // Build a Viewport matching the frame the matrices were captured for.
            var vp = new Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
            var near = new Vector3(mouseX, mouseY, 0f);
            var far  = new Vector3(mouseX, mouseY, 1f);
            Vector3 w0 = vp.Unproject(near, projection, view, Matrix.Identity);
            Vector3 w1 = vp.Unproject(far,  projection, view, Matrix.Identity);
            Vector3 dir = w1 - w0;
            if (dir.LengthSquared() < 0.0001f) return false;
            dir.Normalize();

            float planeY = playerUoZ * _config.ZScale;
            // Ray-plane intersect: (origin + t*dir).Y = planeY  →  t = (planeY - origin.Y) / dir.Y
            if (Math.Abs(dir.Y) < _config.ParallelEpsilon) return false;
            float t = (planeY - w0.Y) / dir.Y;
            if (t < 0f) return false; // plane is behind the camera
            worldHit = w0 + dir * t;

            tileX = (int)Math.Floor(worldHit.X / _config.TileSize);
            tileY = (int)Math.Floor(worldHit.Z / _config.TileSize);
            return true;
        }
    }
}
