// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>Pure-delegation implementation of <see cref="ICameraInputService"/>.</summary>
    public sealed class CameraInputService : ICameraInputService
    {
        private readonly ICameraInputBridge _bridge;

        public CameraInputService(ICameraInputBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool MiddleMouseRotate => _bridge.MiddleMouseRotate;
        public bool RightMouseLook => _bridge.RightMouseLook;
        public bool InvertX => _bridge.InvertX;
        public bool InvertY => _bridge.InvertY;
        public float SensitivityX => _bridge.SensitivityX;
        public float SensitivityY => _bridge.SensitivityY;
        public float SensMul_ThirdPerson => _bridge.SensMul_ThirdPerson;
        public float SensMul_FirstPerson => _bridge.SensMul_FirstPerson;
        public float SensMul_FreeFly => _bridge.SensMul_FreeFly;

        public void SetMiddleMouseRotate(bool value) => _bridge.MiddleMouseRotate = value;
        public void SetRightMouseLook(bool value) => _bridge.RightMouseLook = value;
        public void SetInvertX(bool value) => _bridge.InvertX = value;
        public void SetInvertY(bool value) => _bridge.InvertY = value;
        public void SetSensitivityX(float value) => _bridge.SensitivityX = value;
        public void SetSensitivityY(float value) => _bridge.SensitivityY = value;
        public void SetSensMul_ThirdPerson(float value) => _bridge.SensMul_ThirdPerson = value;
        public void SetSensMul_FirstPerson(float value) => _bridge.SensMul_FirstPerson = value;
        public void SetSensMul_FreeFly(float value) => _bridge.SensMul_FreeFly = value;
    }
}
