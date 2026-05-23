// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Active camera mode. Mirrors legacy <c>CameraModeController.Mode</c> bit-for-bit;
    /// numeric values are locked by an xUnit parity test so accidental reordering
    /// surfaces immediately at the boundary cast.
    /// </summary>
    public enum CameraMode
    {
        Off = 0,
        ThirdPerson = 1,
        FirstPerson = 2,
        Cinematic = 3,
        FreeFly = 4,
    }
}
