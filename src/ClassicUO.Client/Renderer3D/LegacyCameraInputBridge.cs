// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).

using ClassicUO.Renderer.Camera;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyCameraInputBridge : ICameraInputBridge
    {
        public bool MiddleMouseRotate
        {
            get => CameraInputController.MiddleMouseRotate;
            set => CameraInputController.MiddleMouseRotate = value;
        }
        public bool RightMouseLook
        {
            get => CameraInputController.RightMouseLook;
            set => CameraInputController.RightMouseLook = value;
        }
        public bool InvertX
        {
            get => CameraInputController.InvertX;
            set => CameraInputController.InvertX = value;
        }
        public bool InvertY
        {
            get => CameraInputController.InvertY;
            set => CameraInputController.InvertY = value;
        }
        public float SensitivityX
        {
            get => CameraInputController.SensitivityX;
            set => CameraInputController.SensitivityX = value;
        }
        public float SensitivityY
        {
            get => CameraInputController.SensitivityY;
            set => CameraInputController.SensitivityY = value;
        }
        public float SensMul_ThirdPerson
        {
            get => CameraInputController.SensMul_ThirdPerson;
            set => CameraInputController.SensMul_ThirdPerson = value;
        }
        public float SensMul_FirstPerson
        {
            get => CameraInputController.SensMul_FirstPerson;
            set => CameraInputController.SensMul_FirstPerson = value;
        }
        public float SensMul_FreeFly
        {
            get => CameraInputController.SensMul_FreeFly;
            set => CameraInputController.SensMul_FreeFly = value;
        }
    }
}
