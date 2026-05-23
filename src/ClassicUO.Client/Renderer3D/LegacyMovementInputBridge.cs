// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.Camera;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyMovementInputBridge : IMovementInputBridge
    {
        public bool Enabled
        {
            get => WasdMovementController.Enabled;
            set => WasdMovementController.Enabled = value;
        }
        public bool BindWasd
        {
            get => WasdMovementController.BindWasd;
            set => WasdMovementController.BindWasd = value;
        }
        public bool BindArrows
        {
            get => WasdMovementController.BindArrows;
            set => WasdMovementController.BindArrows = value;
        }
        public bool RunWithShift
        {
            get => WasdMovementController.RunWithShift;
            set => WasdMovementController.RunWithShift = value;
        }
        public bool OnlyWhenCameraModeActive
        {
            get => WasdMovementController.OnlyWhenCameraModeActive;
            set => WasdMovementController.OnlyWhenCameraModeActive = value;
        }
        public bool VerboseLog
        {
            get => WasdMovementController.VerboseLog;
            set => WasdMovementController.VerboseLog = value;
        }
    }
}
