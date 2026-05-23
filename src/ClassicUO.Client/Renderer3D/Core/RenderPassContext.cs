// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System.Collections.Generic;
using ClassicUO.Game.Map;
using ClassicUO.Renderer.Renderer3D; // existing-namespace Camera3D until Phase 2 migration
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Per-pass rendering context. Carries timing, GPU device, camera, world inputs,
    /// and viewport into every <see cref="IRenderPass.Execute"/> call. Read-only by
    /// contract: passes must not mutate the context, only the GPU state they own.
    /// </summary>
    /// <remarks>
    /// World inputs (<see cref="World"/>, <see cref="VisibleChunks"/>, <see cref="PlayerUoX"/>,
    /// <see cref="PlayerUoY"/>, <see cref="PlayerUoZ"/>, <see cref="PlayerFacing"/>,
    /// <see cref="ViewportWidth"/>, <see cref="ViewportHeight"/>) replace the long parameter
    /// list of the legacy <c>World3DRenderer.Draw(...)</c> entrypoint. They are filled in by
    /// the composition root (<see cref="ClassicUO.Game.Scenes.GameScene"/>) once per frame
    /// and shared by every pass.
    /// </remarks>
    internal readonly struct RenderPassContext
    {
        /// <summary>Authoritative timing for this frame. Same value used by <see cref="IFrameService.Tick"/>.</summary>
        public readonly FrameTickContext Frame;

        /// <summary>The MonoGame GPU device. Passes own their render-state save/restore.</summary>
        public readonly GraphicsDevice Graphics;

        /// <summary>The active 3D camera (view + projection matrices).</summary>
        public readonly Camera3D Camera;

        /// <summary>The active game world. May be null in non-gameplay scenes (passes must guard).</summary>
        public readonly ClassicUO.Game.World World;

        /// <summary>Currently visible chunks supplied by GameScene's culling pass. May be empty.</summary>
        public readonly IList<Chunk> VisibleChunks;

        /// <summary>Player position in UO tile units (X axis).</summary>
        public readonly float PlayerUoX;

        /// <summary>Player position in UO tile units (Y axis).</summary>
        public readonly float PlayerUoY;

        /// <summary>Player position in UO tile units (Z axis).</summary>
        public readonly float PlayerUoZ;

        /// <summary>Player facing direction (UO 0..7 Direction enum cast to int, mask already applied).</summary>
        public readonly int PlayerFacing;

        /// <summary>Viewport width in pixels.</summary>
        public readonly int ViewportWidth;

        /// <summary>Viewport height in pixels.</summary>
        public readonly int ViewportHeight;

        public RenderPassContext(
            in FrameTickContext frame,
            GraphicsDevice graphics,
            Camera3D camera,
            ClassicUO.Game.World world,
            IList<Chunk> visibleChunks,
            float playerUoX,
            float playerUoY,
            float playerUoZ,
            int playerFacing,
            int viewportWidth,
            int viewportHeight)
        {
            Frame = frame;
            Graphics = graphics;
            Camera = camera;
            World = world;
            VisibleChunks = visibleChunks;
            PlayerUoX = playerUoX;
            PlayerUoY = playerUoY;
            PlayerUoZ = playerUoZ;
            PlayerFacing = playerFacing;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        /// <summary>
        /// Convenience overload for tests + passes that don't yet need the world inputs.
        /// World defaults to null, visible chunks to empty array, coords/viewport to zero.
        /// </summary>
        public RenderPassContext(in FrameTickContext frame, GraphicsDevice graphics, Camera3D camera)
            : this(frame, graphics, camera, world: null, visibleChunks: System.Array.Empty<Chunk>(),
                   0f, 0f, 0f, 0, 0, 0) { }
    }
}
