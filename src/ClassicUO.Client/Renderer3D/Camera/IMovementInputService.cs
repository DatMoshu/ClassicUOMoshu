// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gump/admin contract for the WASD/arrow-keys movement input controller.
    /// Replaces direct reads/writes of <c>WasdMovementController.{Enabled, BindWasd, ...}</c>.
    /// </summary>
    public interface IMovementInputService
    {
        bool Enabled { get; }
        bool BindWasd { get; }
        bool BindArrows { get; }
        bool RunWithShift { get; }
        bool OnlyWhenCameraModeActive { get; }
        bool VerboseLog { get; }

        void SetEnabled(bool value);
        void SetBindWasd(bool value);
        void SetBindArrows(bool value);
        void SetRunWithShift(bool value);
        void SetOnlyWhenCameraModeActive(bool value);
        void SetVerboseLog(bool value);
    }
}
