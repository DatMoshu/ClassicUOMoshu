// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gateway exposing the legacy <c>CameraModeController</c>'s state-mutation actions
    /// (which touch Camera3D snapshot/restore + FOV/EyeDistance + the FreeFlyEye position).
    /// Lets <see cref="ICameraModeService"/> stay a thin contract while the legacy class
    /// owns the per-frame Apply() pipeline and Camera3D coupling.
    /// </summary>
    /// <remarks>
    /// When the per-frame Apply() and Camera3D snapshots migrate into the service, this
    /// gateway is deleted and the service owns the state directly.
    /// </remarks>
    public interface ICameraModeBridge
    {
        /// <summary>Current mode read-through to the underlying state machine.</summary>
        CameraMode CurrentMode { get; }

        /// <summary>True between FirstPerson enter/exit; renderer skips player draw.</summary>
        bool ShouldHidePlayerThisFrame { get; }

        /// <summary>FreeFly eye world position. Diagnostic + GoToPlayer use.</summary>
        Vector3 FreeFlyEye { get; }

        /// <summary>
        /// When true, FreeFly substitutes its eye position for the player's tile when
        /// computing the chunk-load window. Read by the chunk-sort hot path.
        /// </summary>
        bool FreeFlyDrivesChunks { get; set; }

        /// <summary>FreeFly base movement speed in world units/sec. Mouse-wheel adjusts this.</summary>
        float FreeFlySpeed { get; set; }

        /// <summary>
        /// Switch the active mode. Triggers the legacy's Camera3D snapshot/restore + FOV +
        /// EyeDistance setup.
        /// </summary>
        void SetMode(CameraMode mode);

        /// <summary>Reset the camera angles + zoom + offset to the active mode's defaults.</summary>
        void ResetToDefaults();

        /// <summary>Snap the FreeFly eye over the player target. Switches into FreeFly first if needed.</summary>
        void GoToPlayer(Vector3 playerTarget);

        /// <summary>Cycle through Off → 3rd → 1st → Cine → FreeFly → Off.</summary>
        void Cycle();

        /// <summary>Human-readable label for the current mode (gump diagnostics).</summary>
        string Label();
    }
}
