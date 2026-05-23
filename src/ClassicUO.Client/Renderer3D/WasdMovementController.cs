// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — camera-relative WASD / arrow-key walking.
//
// Tracks held W/A/S/D and arrow keys, builds a 2D vector in (uoX, uoY) space
// rotated by the 3D camera yaw, snaps to the nearest of UO's 8 cardinal
// directions, and pings PlayerMobile.Walk() each tick. Walk() self-rate-limits
// (Walker.LastStepRequestTime gate) so per-frame calls are safe.
//
// Hook points:
//   - GameSceneInputHandler.OnKeyDown / OnKeyUp call OnKeyDown(code) / OnKeyUp(code)
//   - GameScene.Update calls Update(world)
//
// Disabled while chat input has focus, while a text box is focused, or when
// the world is paused.

using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using SDL3;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WasdMovementController
    {
        // ===== Toggles (exposed via Debug3DGump) =====
        // Default ON: every active camera mode (1st/3rd/Cinematic/FreeFly) expects WASD to drive
        // either the player or the camera. Off mode keeps native UO behavior because of
        // OnlyWhenCameraModeActive below.
        public static bool Enabled = true;
        public static bool BindWasd = true;
        public static bool BindArrows = true;
        // Hold Shift to run; otherwise walk. AlwaysRun profile flag still applies inside Walk().
        public static bool RunWithShift = true;
        // Only fire when one of the new camera modes is active. Off mode = native macros only.
        public static bool OnlyWhenCameraModeActive = true;
        // Print one log line per second describing what's eating WASD input (focus / mode / walker).
        public static bool VerboseLog = true;

        // ===== State =====
        private static readonly HashSet<SDL.SDL_Keycode> _held = new();
        private static uint _nextLogMs;

        public static void OnKeyDown(SDL.SDL_Keycode code)
        {
            if (IsTracked(code)) _held.Add(code);
        }

        public static void OnKeyUp(SDL.SDL_Keycode code)
        {
            _held.Remove(code);
        }

        public static void ClearHeld() => _held.Clear();

        private static bool IsTracked(SDL.SDL_Keycode c)
        {
            if (BindWasd && (c == SDL.SDL_Keycode.SDLK_W || c == SDL.SDL_Keycode.SDLK_A
                          || c == SDL.SDL_Keycode.SDLK_S || c == SDL.SDL_Keycode.SDLK_D))
                return true;
            if (BindArrows && (c == SDL.SDL_Keycode.SDLK_UP || c == SDL.SDL_Keycode.SDLK_DOWN
                            || c == SDL.SDL_Keycode.SDLK_LEFT || c == SDL.SDL_Keycode.SDLK_RIGHT))
                return true;
            // FreeFly vertical axis — Q (up) / E (down). Always tracked so toggling
            // BindWasd off doesn't disable fly-mode altitude.
            if (c == SDL.SDL_Keycode.SDLK_Q || c == SDL.SDL_Keycode.SDLK_E)
                return true;
            return false;
        }

        // ===== Public input accessors (consumed by CameraModeController in FreeFly) =====
        public static bool ForwardHeld => (BindWasd  && _held.Contains(SDL.SDL_Keycode.SDLK_W))
                                       || (BindArrows && _held.Contains(SDL.SDL_Keycode.SDLK_UP));
        public static bool BackHeld    => (BindWasd  && _held.Contains(SDL.SDL_Keycode.SDLK_S))
                                       || (BindArrows && _held.Contains(SDL.SDL_Keycode.SDLK_DOWN));
        public static bool LeftHeld    => (BindWasd  && _held.Contains(SDL.SDL_Keycode.SDLK_A))
                                       || (BindArrows && _held.Contains(SDL.SDL_Keycode.SDLK_LEFT));
        public static bool RightHeld   => (BindWasd  && _held.Contains(SDL.SDL_Keycode.SDLK_D))
                                       || (BindArrows && _held.Contains(SDL.SDL_Keycode.SDLK_RIGHT));
        public static bool UpHeld      => _held.Contains(SDL.SDL_Keycode.SDLK_Q);
        public static bool DownHeld    => _held.Contains(SDL.SDL_Keycode.SDLK_E);
        public static bool RunHeld     => RunWithShift && Input.Keyboard.Shift;
        public static int  HeldCount   => _held.Count;

        public static void Update(World world)
        {
            if (!Enabled || world == null || !world.InGame || world.Player == null) return;
            // FreeFly mode owns WASD entirely — moves the camera, not the player.
            if (CameraModeController.CurrentMode == CameraModeController.Mode.FreeFly) return;

            bool chatFocused = UIManager.KeyboardFocusControl != null
                && UIManager.SystemChat != null
                && UIManager.KeyboardFocusControl == UIManager.SystemChat.TextBoxControl;
            bool gatedByMode = OnlyWhenCameraModeActive
                && CameraModeController.CurrentMode == CameraModeController.Mode.Off;

            // Compose forward / strafe demand from held keys.
            float fwd = 0f, strafe = 0f;
            if (ForwardHeld) fwd    += 1f;
            if (BackHeld)    fwd    -= 1f;
            if (RightHeld)   strafe += 1f;
            if (LeftHeld)    strafe -= 1f;

            bool anyHeld = fwd != 0f || strafe != 0f;
            if (anyHeld && (chatFocused || gatedByMode))
            {
                LogPeriodic(chatFocused
                    ? "WASD suppressed: chat textbox has focus (click into the world to release)"
                    : "WASD suppressed: 'Only in cam mode' is on and camera mode is Off (pick 1st/3rd/Cinematic/FreeFly)");
                return;
            }
            if (!anyHeld) return;

            // Camera-relative basis. Pitch is ignored — movement is on the ground plane.
            // forward (uoX, uoY) = (sin(yaw), cos(yaw))   [yaw=0 → south]
            // right   (uoX, uoY) = (-cos(yaw), sin(yaw))  [yaw=0 → west; "right" of facing south]
            float yawRad = (float)(World3DRenderer.Camera.YawDegrees * Math.PI / 180.0);
            float fx =  (float)Math.Sin(yawRad);
            float fy =  (float)Math.Cos(yawRad);
            float rx = -(float)Math.Cos(yawRad);
            float ry =  (float)Math.Sin(yawRad);

            float vx = fwd * fx + strafe * rx;
            float vy = fwd * fy + strafe * ry;

            // Snap to nearest of 8 directions. atan2(vy, vx) — angle from +uoX (East),
            // CCW positive toward +uoY (South).
            double angle = Math.Atan2(vy, vx);
            int idx = (int)Math.Round(angle / (Math.PI / 4));
            idx = ((idx % 8) + 8) % 8;
            // idx 0..7 = East, SE(Down), South, SW(Left), West, NW(Up), North, NE(Right)
            // Map to Direction enum byte values.
            byte[] dirMap = { 2, 3, 4, 5, 6, 7, 0, 1 };
            Direction dir = (Direction)dirMap[idx];

            if (!world.Player.Pathfinder.AutoWalking)
            {
                bool ok = world.Player.Walk(dir, RunHeld);
                if (!ok) LogPeriodic($"WASD held but Walk() refused (Walker queue full / paralyzed / failed) dir={dir}");
            }
        }

        private static void LogPeriodic(string msg)
        {
            if (!VerboseLog) return;
            uint now = ClassicUO.Time.Ticks;
            if (now < _nextLogMs) return;
            _nextLogMs = now + 1000;
            System.Console.WriteLine("[3DCUO][WASD] " + msg);
        }
    }
}
