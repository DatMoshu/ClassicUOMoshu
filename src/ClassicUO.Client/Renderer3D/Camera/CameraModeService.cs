// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using System;
// MathF is in System on .NET 6+; kept explicit for clarity.
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="ICameraModeService"/>. Every read
    /// and write is forwarded to the supplied <see cref="ICameraModeBridge"/>. This is a
    /// transitional shape; future sessions migrate state-of-record into this class and
    /// delete the bridge.
    /// </summary>
    public sealed class CameraModeService : ICameraModeService
    {
        private readonly ICameraModeBridge _bridge;

        public CameraModeService(ICameraModeBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public CameraMode CurrentMode => _bridge.CurrentMode;
        public bool ShouldHidePlayerThisFrame => _bridge.ShouldHidePlayerThisFrame;
        public Vector3 FreeFlyEye => _bridge.FreeFlyEye;
        public bool FreeFlyDrivesChunks => _bridge.FreeFlyDrivesChunks;
        public float FreeFlySpeed => _bridge.FreeFlySpeed;

        public void SetMode(CameraMode mode) => _bridge.SetMode(mode);
        public void Cycle() => _bridge.Cycle();
        public void GoToPlayer(Vector3 playerTarget) => _bridge.GoToPlayer(playerTarget);
        public void ResetToDefaults() => _bridge.ResetToDefaults();

        public void SetFreeFlyDrivesChunks(bool drive) => _bridge.FreeFlyDrivesChunks = drive;
        public void SetFreeFlySpeed(float speed) => _bridge.FreeFlySpeed = MathF.Max(0f, speed);

        public string Label() => _bridge.Label();
    }
}
