// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gump/admin contract for the camera mouse-look input controller.
    /// Replaces direct reads/writes of <c>CameraInputController.{MiddleMouseRotate,
    /// SensitivityX, ...}</c>.
    /// </summary>
    public interface ICameraInputService
    {
        bool MiddleMouseRotate { get; }
        bool RightMouseLook { get; }
        bool InvertX { get; }
        bool InvertY { get; }
        float SensitivityX { get; }
        float SensitivityY { get; }
        float SensMul_ThirdPerson { get; }
        float SensMul_FirstPerson { get; }
        float SensMul_FreeFly { get; }

        void SetMiddleMouseRotate(bool value);
        void SetRightMouseLook(bool value);
        void SetInvertX(bool value);
        void SetInvertY(bool value);
        void SetSensitivityX(float value);
        void SetSensitivityY(float value);
        void SetSensMul_ThirdPerson(float value);
        void SetSensMul_FirstPerson(float value);
        void SetSensMul_FreeFly(float value);
    }
}
