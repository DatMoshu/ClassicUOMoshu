// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>Pure-delegation implementation of <see cref="IMovementInputService"/>.</summary>
    public sealed class MovementInputService : IMovementInputService
    {
        private readonly IMovementInputBridge _bridge;

        public MovementInputService(IMovementInputBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool Enabled => _bridge.Enabled;
        public bool BindWasd => _bridge.BindWasd;
        public bool BindArrows => _bridge.BindArrows;
        public bool RunWithShift => _bridge.RunWithShift;
        public bool OnlyWhenCameraModeActive => _bridge.OnlyWhenCameraModeActive;
        public bool VerboseLog => _bridge.VerboseLog;

        public void SetEnabled(bool value) => _bridge.Enabled = value;
        public void SetBindWasd(bool value) => _bridge.BindWasd = value;
        public void SetBindArrows(bool value) => _bridge.BindArrows = value;
        public void SetRunWithShift(bool value) => _bridge.RunWithShift = value;
        public void SetOnlyWhenCameraModeActive(bool value) => _bridge.OnlyWhenCameraModeActive = value;
        public void SetVerboseLog(bool value) => _bridge.VerboseLog = value;
    }
}
