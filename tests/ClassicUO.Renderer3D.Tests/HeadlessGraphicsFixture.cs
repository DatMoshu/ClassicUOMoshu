// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — FNA headless GraphicsDevice fixture (ADR-012 Phase 5, session 74).
//
// Approach A from the README, refined: uses FNA's Game.RunOneFrame() instead of
// the blocking Run() entry point. RunOneFrame calls DoInitialize + one Tick and
// returns synchronously — no background thread, no event-loop juggling, no
// platform-main-loop blocker on macOS. The GraphicsDevice is available the
// moment RunOneFrame returns.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer3D.Tests
{
    /// <summary>
    /// xUnit fixture that boots FNA's GraphicsDevice via <c>Game.RunOneFrame()</c>.
    /// <see cref="Device"/> is available the moment the constructor returns.
    /// Construction throws if FNA fails to initialise — most often a SDL3.dll /
    /// FNA3D.dll resolution failure in the test process bin.
    /// </summary>
    /// <remarks>
    /// Tests that use this fixture must be marked
    /// <c>[Trait("Category", "GraphicsDevice")]</c> so CI runners without a display
    /// server (or without the native libs available) can filter them out via
    /// <c>dotnet test --filter "Category!=GraphicsDevice"</c>.
    /// </remarks>
    public sealed class HeadlessGraphicsFixture : IDisposable
    {
        private readonly HeadlessGame _game;

        /// <summary>The active GraphicsDevice. Non-null after the ctor returns.</summary>
        public GraphicsDevice Device { get; }

        public HeadlessGraphicsFixture()
        {
            _game = new HeadlessGame();
            try
            {
                // DoInitialize() runs synchronously inside RunOneFrame, builds the
                // GraphicsDevice, ticks once, and returns. No event loop blocking.
                _game.RunOneFrame();
            }
            catch (Exception ex)
            {
                _game.Dispose();
                throw new InvalidOperationException(
                    "FNA Game.RunOneFrame() threw during construction. " +
                    "Check that FNA3D / SDL3 / FAudio native libs are in the test bin output. " +
                    $"Inner: {ex.GetType().Name}: {ex.Message}",
                    ex);
            }

            Device = _game.GraphicsDevice
                ?? throw new InvalidOperationException(
                    "FNA Game.RunOneFrame() returned but GraphicsDevice was null.");
        }

        public void Dispose()
        {
            try { _game.Dispose(); } catch { /* best effort */ }
        }

        private sealed class HeadlessGame : Game
        {
            public HeadlessGame()
            {
                _ = new GraphicsDeviceManager(this)
                {
                    PreferredBackBufferWidth = 1,
                    PreferredBackBufferHeight = 1,
                    SynchronizeWithVerticalRetrace = false,
                };
                IsFixedTimeStep = false;
                IsMouseVisible = false;
                if (Window is not null)
                {
                    Window.AllowUserResizing = false;
                    Window.Title = "ClassicUO Test Harness";
                }
            }

            // Skip presenting — keeps RunOneFrame cheap. Tests that need a Present
            // can call GraphicsDevice.Present() manually after their setup.
            protected override void Draw(GameTime gameTime) { }
        }
    }
}
