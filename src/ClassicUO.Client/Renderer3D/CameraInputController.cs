// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — mouse-drag rotation for the perspective 3D camera.
//
// Active only when:
//   - World3DRenderer.UseIsoProjection == false (perspective mode)
//   - At least one of MiddleMouseRotate / RightMouseLook is enabled
// On drag, accumulates pixel deltas into Camera3D.YawDegrees (horizontal) and
// Camera3D.PitchDegrees (vertical). Pitch is clamped to [1°, 89°] so the camera
// never flips through the world.
//
// New in this revision:
//   - Separate horizontal / vertical sensitivity
//   - InvertX as well as InvertY
//   - RMB-hold mouse-look option (much closer to modern game conventions)
//   - Per-mode sensitivity multipliers (1st-person needs slower than FreeFly)

using ClassicUO.Renderer.Renderer3D;
using SDL3;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class CameraInputController
    {
        // ===== Toggles =====
        public static bool MiddleMouseRotate = true;
        // RMB-drag look. Note: when active, RMB-as-walk in GameSceneInputHandler will
        // still run; we only intercept for camera rotation. Suppression of the walk
        // cursor while looking is left to the input handler (future Phase 1.5 work).
        public static bool RightMouseLook = true;

        // ===== Sensitivity (degrees per pixel) =====
        public static float SensitivityX = 0.20f;
        public static float SensitivityY = 0.18f;

        // Per-mode multiplier so 1st-person doesn't whip around at FreeFly speeds.
        public static float SensMul_ThirdPerson = 1.0f;
        public static float SensMul_FirstPerson = 0.85f;
        public static float SensMul_FreeFly     = 1.10f;

        // ===== Inverts =====
        public static bool InvertX = false;
        public static bool InvertY = false;

        // ===== State =====
        private static bool _wasMDown;
        private static bool _wasRDown;
        private static int _lastX, _lastY;
        // Tracks which button is currently driving the rotation (so we don't mix deltas
        // when the user transitions from MMB to RMB without releasing).
        private enum Active { None, Middle, Right }
        private static Active _active = Active.None;

        public static void Update()
        {
            if (World3DRenderer.UseIsoProjection || !(MiddleMouseRotate || RightMouseLook))
            {
                _wasMDown = _wasRDown = false;
                _active = Active.None;
                return;
            }

            bool mDown = MiddleMouseRotate && ClassicUO.Input.Mouse.MButtonPressed;
            bool rDown = RightMouseLook    && ClassicUO.Input.Mouse.RButtonPressed;
            int mx = ClassicUO.Input.Mouse.Position.X;
            int my = ClassicUO.Input.Mouse.Position.Y;

            // Pick / hold an active button. Middle wins if both are down at start
            // (less likely to conflict with right-mouse-walk).
            if (_active == Active.None)
            {
                if (mDown) { _active = Active.Middle; _lastX = mx; _lastY = my; }
                else if (rDown) { _active = Active.Right; _lastX = mx; _lastY = my; }
            }
            else if (_active == Active.Middle && !mDown) _active = Active.None;
            else if (_active == Active.Right  && !rDown) _active = Active.None;

            if (_active != Active.None)
            {
                int dx = mx - _lastX;
                int dy = my - _lastY;
                _lastX = mx;
                _lastY = my;

                if (dx != 0 || dy != 0)
                {
                    float mul = ModeMultiplier();
                    float yawDelta   = (InvertX ? -1f : +1f) * dx * SensitivityX * mul;
                    float pitchDelta = (InvertY ? +1f : -1f) * dy * SensitivityY * mul;

                    float yaw = World3DRenderer.Camera.YawDegrees + yawDelta;
                    while (yaw < 0f) yaw += 360f;
                    while (yaw >= 360f) yaw -= 360f;
                    World3DRenderer.Camera.YawDegrees = yaw;

                    float pitch = World3DRenderer.Camera.PitchDegrees + pitchDelta;
                    if (pitch < 1f) pitch = 1f;
                    if (pitch > 89f) pitch = 89f;
                    World3DRenderer.Camera.PitchDegrees = pitch;
                }
            }

            _wasMDown = mDown;
            _wasRDown = rDown;
        }

        private static float ModeMultiplier()
        {
            switch (CameraModeController.CurrentMode)
            {
                case CameraModeController.Mode.FirstPerson: return SensMul_FirstPerson;
                case CameraModeController.Mode.FreeFly:     return SensMul_FreeFly;
                case CameraModeController.Mode.ThirdPerson:
                case CameraModeController.Mode.Cinematic:
                default:                                    return SensMul_ThirdPerson;
            }
        }

        // Back-compat shim for any caller that still reads the old single field.
        public static float Sensitivity
        {
            get => SensitivityX;
            set { SensitivityX = value; SensitivityY = value; }
        }
    }
}
