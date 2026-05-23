// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy CameraModeController static class to ICameraModeBridge.
// Replaced when the per-frame Apply pipeline + Camera3D snapshots migrate into
// CameraModeService directly.

using ClassicUO.Renderer.Camera;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyCameraModeBridge : ICameraModeBridge
    {
        public CameraMode CurrentMode => (CameraMode)(int)CameraModeController.CurrentMode;

        public bool ShouldHidePlayerThisFrame => CameraModeController.ShouldHidePlayerThisFrame;

        public Vector3 FreeFlyEye => CameraModeController.FreeFlyEye;

        public bool FreeFlyDrivesChunks
        {
            get => CameraModeController.FreeFlyDrivesChunks;
            set => CameraModeController.FreeFlyDrivesChunks = value;
        }

        public float FreeFlySpeed
        {
            get => CameraModeController.FreeFlySpeed;
            set => CameraModeController.FreeFlySpeed = value;
        }

        public void SetMode(CameraMode mode)
            => CameraModeController.SetMode((CameraModeController.Mode)(int)mode);

        public void ResetToDefaults() => CameraModeController.ResetToDefaults();

        public void GoToPlayer(Vector3 playerTarget) => CameraModeController.GoToPlayer(playerTarget);

        public void Cycle() => CameraModeController.Cycle();

        public string Label() => CameraModeController.Label();
    }
}
