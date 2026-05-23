// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Active camera-mode contract. Replaces the public-static surface of the legacy
    /// <c>CameraModeController</c> for admin and diagnostic callers (gumps, dev tools).
    /// </summary>
    /// <remarks>
    /// <para>This is a delegating facade in this transitional window: every read+write
    /// flows through the supplied <see cref="ICameraModeBridge"/> over the legacy class.
    /// State-of-record stays on the legacy class because its <c>SetMode</c> does heavy
    /// Camera3D snapshot/restore work that the service can't yet duplicate.</para>
    ///
    /// <para>When the Camera3D snapshots + per-frame <c>Apply()</c> migrate, this service
    /// owns the state directly and the bridge is deleted.</para>
    /// </remarks>
    public interface ICameraModeService
    {
        // ===== Read state =====

        CameraMode CurrentMode { get; }
        bool ShouldHidePlayerThisFrame { get; }
        Vector3 FreeFlyEye { get; }

        /// <summary>
        /// When true, FreeFly substitutes its eye position for the player's tile when
        /// computing the chunk-load window (lets you fly far from the player and still see
        /// terrain). UI-mutable.
        /// </summary>
        bool FreeFlyDrivesChunks { get; }

        /// <summary>FreeFly base movement speed in world units/sec. UI-mutable.</summary>
        float FreeFlySpeed { get; }

        // ===== Mutate state =====

        void SetMode(CameraMode mode);
        void Cycle();
        void GoToPlayer(Vector3 playerTarget);
        void ResetToDefaults();

        /// <summary>Toggle whether FreeFly drives the chunk-load window.</summary>
        void SetFreeFlyDrivesChunks(bool drive);
        /// <summary>Set the FreeFly base movement speed. Clamped to non-negative.</summary>
        void SetFreeFlySpeed(float speed);

        // ===== Diagnostics =====

        /// <summary>Human-readable label for the current mode.</summary>
        string Label();
    }
}
