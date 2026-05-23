// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gateway exposing legacy <c>WasdMovementController</c> WASD/arrow input tunables.
    /// Read+write. State-of-record stays on the legacy class because per-frame keyboard
    /// scan reads these in the hot path.
    /// </summary>
    public interface IMovementInputBridge
    {
        bool Enabled { get; set; }
        bool BindWasd { get; set; }
        bool BindArrows { get; set; }
        bool RunWithShift { get; set; }
        bool OnlyWhenCameraModeActive { get; set; }
        bool VerboseLog { get; set; }
    }
}
