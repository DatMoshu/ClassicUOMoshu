// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Falling-leaf particle system tinted by season. Subscribes to
    /// <see cref="WindUpdatedEvent"/> for drift and to <see cref="SeasonChangedEvent"/> for
    /// transition diagnostics; reads continuous year progress directly from
    /// <see cref="ISeasonService"/> for spawn-rate selection.
    /// </summary>
    public interface ILeafFallService
    {
        // ===== Read state =====

        bool Enabled { get; }
        int AliveLeaves { get; }
        int LastDrawnLeaves { get; }
        float LastSpawnRate { get; }
        bool UseManualSeason { get; }
        float ManualSeasonProgress { get; }

        // ===== Mutate state =====

        void SetEnabled(bool enabled);
        void SetUseManualSeason(bool manual);
        void SetManualSeasonProgress(float progress);

        /// <summary>Clear the live-leaf pool and reset spawn accumulators.</summary>
        void Clear();

        /// <summary>
        /// Set the player-anchored fallback spawn point used when no visible tree anchors
        /// exist. Called once per frame by GameScene before world rendering.
        /// </summary>
        void Configure(Vector3 playerAnchorWorld);

        /// <summary>
        /// Render all live leaves. Called from GameScene's world draw path.
        /// No-op when disabled or when the texture provider returns null.
        /// </summary>
        void Draw(GraphicsDevice device, Matrix view, Matrix projection);
    }
}
