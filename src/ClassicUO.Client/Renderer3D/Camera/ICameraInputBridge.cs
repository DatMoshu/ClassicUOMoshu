// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gateway exposing legacy <c>CameraInputController</c> mouse-look tunables. Read+write.
    /// State-of-record stays on the legacy class because per-frame mouse-input math reads
    /// these in the hot path.
    /// </summary>
    public interface ICameraInputBridge
    {
        bool MiddleMouseRotate { get; set; }
        bool RightMouseLook { get; set; }
        bool InvertX { get; set; }
        bool InvertY { get; set; }
        float SensitivityX { get; set; }
        float SensitivityY { get; set; }
        float SensMul_ThirdPerson { get; set; }
        float SensMul_FirstPerson { get; set; }
        float SensMul_FreeFly { get; set; }
    }
}
