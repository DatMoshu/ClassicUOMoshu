// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — first-person / third-person / cinematic camera modes.
//
// Plugs into World3DRenderer.Draw via Apply(target, cam) right after the camera
// target is set from the player position. In each mode it adjusts:
//
//   ThirdPerson : free-cam perspective, EyeDistance from controller, player visible.
//   FirstPerson : eye AT head anchor (target + UpHeadHeight), looks along yaw+pitch
//                 forward. Player skipped in render so we don't see our own torso.
//   Cinematic   : like ThirdPerson but yaw auto-rotates, pitch oscillates gently,
//                 narrow FOV, larger distance. GTA/RDR "let go of the wheel" feel.
//
// Mode entry snapshots the relevant Camera3D fields (UsePerspective, FovDegrees,
// EyeDistance, UseEyeOverride). Mode exit restores them. Iso projection is
// force-disabled in any non-default mode because all three need a perspective
// camera; restored on return to the implicit "off/default" mode.
//
// Sprint-13 additions (Iris2 gap-fill):
//   Feature 1 - Scroll-wheel zoom: handled in GameSceneInputHandler.OnMouseWheel
//               (already had partial impl; FreeFly speed branch added there).
//   Feature 2 - Ground-collision auto-pitch (3rd person): Apply() ThirdPerson branch.
//   Feature 3 - Look-ahead target offset (3rd person): Apply() ThirdPerson branch.
//   Feature 4 - Player-faces-camera yaw: Apply() FirstPerson branch (rate-limited).

using System;
using ClassicUO.Game.Data;
using ClassicUO.Renderer.Renderer3D;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class CameraModeController
    {
        public enum Mode { Off, ThirdPerson, FirstPerson, Cinematic, FreeFly }

        public static Mode CurrentMode { get; private set; } = Mode.Off;

        // ===== Tunables =====
        // 3rd person: how far behind the player the eye sits (world units; 1 tile = 22).
        public static float ThirdPersonDistance = 220f;
        // 1st person: how high above the player anchor the eye sits.
        // Player Z is the foot; head sits ~1.5 UO Z * 4 (Z_SCALE) = ~6 world units, but
        // model heights vary — 60 reads as "head" with the default skinned model scale.
        public static float FirstPersonHeadHeight = 60f;
        public static float FirstPersonForwardOffset = 0f; // small nudge ahead, useful if model clips

        // Cinematic
        public static float CinematicDistance     = 380f;
        public static float CinematicYawSpeedDeg  = 6f;     // deg/sec auto-orbit
        public static float CinematicPitchAmpDeg  = 4f;     // ± degrees
        public static float CinematicPitchPeriodS = 9f;     // seconds per full wobble
        public static float CinematicBasePitchDeg = 18f;    // low cinematic pitch
        public static float CinematicFovDeg       = 35f;    // narrow FOV

        // 3rd-person FOV (separate from cinematic so they don't fight each other)
        public static float ThirdPersonFovDeg     = 60f;
        public static float FirstPersonFovDeg     = 75f;

        // FreeFly — camera detaches from the player, WASD/arrows fly the eye.
        // Speeds in world units per second (1 tile = 22 along X/Z; 1 UO Z step = 4 along Y).
        public static float FreeFlySpeed       = 600f;   // ~27 tiles/sec
        public static float FreeFlySprintMul   = 3f;     // Shift held
        public static float FreeFlyVerticalMul = 1f;     // Q/E rate vs forward
        public static float FreeFlyFovDeg      = 70f;
        // When true, FreeFly substitutes its eye position (in UO tile coords) for
        // the player's tile when computing the chunk-load window in GetViewPort.
        // Lets you fly far from the player and still see terrain. Off = chunks
        // remain glued to the player, you'll fly out of loaded geometry.
        public static bool  FreeFlyDrivesChunks = true;

        // ===== Feature 2: Ground-collision auto-pitch (3rd person) =====
        // When true, if the computed eye would clip below terrain, it is lifted
        // above ground+margin and pitch is rotated toward the player automatically.
        // Inspired by Iris2 lib.3d.cam.lua lines 286-310.
        public static bool  Enable3rdPersonGroundCollide = true;
        // Margin above ground (world units). 16 ≈ 4 UO Z steps.
        public static float GroundCollideMargin = 16f;

        // ===== Feature 3: Look-ahead target offset (3rd person) =====
        // Shifts the camera target along the ground-plane camera forward by
        // ThirdPersonLookAheadDistance before computing the eye, lifting the
        // player off dead-center and pulling geometry-of-interest into view.
        // Inspired by Iris2 lib.3d.cam.lua lines 117-125, 321.
        public static bool  EnableLookAhead = true;
        public static float ThirdPersonLookAheadDistance = 220f; // 1× ThirdPersonDistance default

        // ===== Feature 4: Player faces camera yaw =====
        // 1st person: every frame, snap the player's server direction to the
        // nearest 8-cardinal from the camera yaw. Rate-limited to ≤8 Hz.
        // 3rd person: same logic but off by default (breaks UO muscle memory).
        // Inspired by Iris2 lib.3d.cam.lua line 244.
        public static bool  ThirdPersonRotatePlayerWithCamera = false;
        // Rate-limit: send at most this many direction packets per second.
        private const float FACE_RATE_HZ = 8f;
        private static double _lastFaceSendTime;

        // ===== Saved state for clean exit =====
        private static bool   _saved;
        private static bool   _savedUseIso;
        private static bool   _savedUsePerspective;
        private static float  _savedFov;
        private static float  _savedEyeDistance;
        private static bool   _savedUseEyeOverride;
        private static float  _savedPitch;

        // True between enter and exit of FirstPerson — used by World3DRenderer
        // to skip the player draw so the eye isn't inside the torso.
        public static bool ShouldHidePlayerThisFrame { get; private set; }

        // Cinematic time accumulator (seconds).
        private static double _cineTime;
        private static DateTime _lastTick;

        // FreeFly: independent camera position in world space. Initialized from the
        // player target on mode entry, then advanced by WASD/arrows + Q/E each frame.
        public static Vector3 FreeFlyEye;
        private static bool _flyEyeValid;

        public static void SetMode(Mode m, World3DRenderer_AccessHack hack = null)
        {
            if (m == CurrentMode) return;

            // Capture state on first transition out of Off.
            if (CurrentMode == Mode.Off && !_saved)
            {
                _savedUseIso         = World3DRenderer.UseIsoProjection;
                _savedUsePerspective = World3DRenderer.Camera.UsePerspective;
                _savedFov            = World3DRenderer.Camera.FovDegrees;
                _savedEyeDistance    = World3DRenderer.Camera.EyeDistance;
                _savedUseEyeOverride = World3DRenderer.Camera.UseEyeOverride;
                _savedPitch          = World3DRenderer.Camera.PitchDegrees;
                _saved = true;
            }

            CurrentMode = m;

            // Restore + leave on Off.
            if (m == Mode.Off)
            {
                if (_saved)
                {
                    World3DRenderer.UseIsoProjection      = _savedUseIso;
                    World3DRenderer.Camera.UsePerspective = _savedUsePerspective;
                    World3DRenderer.Camera.FovDegrees     = _savedFov;
                    World3DRenderer.Camera.EyeDistance    = _savedEyeDistance;
                    World3DRenderer.Camera.UseEyeOverride = _savedUseEyeOverride;
                    World3DRenderer.Camera.PitchDegrees   = _savedPitch;
                    _saved = false;
                }
                ShouldHidePlayerThisFrame = false;
                return;
            }

            // Common setup for all active modes: free-cam perspective.
            World3DRenderer.UseIsoProjection      = false;
            World3DRenderer.Camera.UsePerspective = true;
            World3DRenderer.Camera.UseEyeOverride = false;

            switch (m)
            {
                case Mode.ThirdPerson:
                    World3DRenderer.Camera.FovDegrees  = ThirdPersonFovDeg;
                    World3DRenderer.Camera.EyeDistance = ThirdPersonDistance;
                    break;

                case Mode.FirstPerson:
                    World3DRenderer.Camera.FovDegrees  = FirstPersonFovDeg;
                    // eye-override applied per-frame in Apply()
                    World3DRenderer.Camera.UseEyeOverride = true;
                    break;

                case Mode.Cinematic:
                    World3DRenderer.Camera.FovDegrees   = CinematicFovDeg;
                    World3DRenderer.Camera.EyeDistance  = CinematicDistance;
                    World3DRenderer.Camera.PitchDegrees = CinematicBasePitchDeg;
                    _cineTime = 0;
                    _lastTick = DateTime.UtcNow;
                    break;

                case Mode.FreeFly:
                    World3DRenderer.Camera.FovDegrees     = FreeFlyFovDeg;
                    World3DRenderer.Camera.UseEyeOverride = true;
                    // Eye seeded from current target on first Apply() of this mode.
                    _flyEyeValid = false;
                    _lastTick = DateTime.UtcNow;
                    break;
            }
        }

        // Reset the camera angles + zoom + offset to the defaults appropriate
        // for the active mode. Exposed for the Camera Gizmo gump's "Reset" button.
        public static void ResetToDefaults()
        {
            World3DRenderer.Camera.Zoom = 1f;
            World3DRenderer.CameraOffset = Vector3.Zero;
            switch (CurrentMode)
            {
                case Mode.Off:
                    World3DRenderer.Camera.PitchDegrees = 30f;
                    World3DRenderer.Camera.YawDegrees   = 45f;
                    break;
                case Mode.ThirdPerson:
                    World3DRenderer.Camera.PitchDegrees = 30f;
                    World3DRenderer.Camera.YawDegrees   = 45f;
                    World3DRenderer.Camera.EyeDistance  = ThirdPersonDistance;
                    World3DRenderer.Camera.FovDegrees   = ThirdPersonFovDeg;
                    break;
                case Mode.FirstPerson:
                    World3DRenderer.Camera.PitchDegrees = 5f;
                    World3DRenderer.Camera.YawDegrees   = 45f;
                    World3DRenderer.Camera.FovDegrees   = FirstPersonFovDeg;
                    break;
                case Mode.Cinematic:
                    World3DRenderer.Camera.PitchDegrees = CinematicBasePitchDeg;
                    World3DRenderer.Camera.EyeDistance  = CinematicDistance;
                    World3DRenderer.Camera.FovDegrees   = CinematicFovDeg;
                    break;
                case Mode.FreeFly:
                    World3DRenderer.Camera.PitchDegrees = 30f;
                    World3DRenderer.Camera.YawDegrees   = 45f;
                    World3DRenderer.Camera.FovDegrees   = FreeFlyFovDeg;
                    _flyEyeValid = false;
                    break;
            }
        }

        // Snap the FreeFly eye back over the player target, looking down. If
        // not currently in FreeFly, switches to FreeFly first.
        public static void GoToPlayer(Vector3 playerTarget)
        {
            if (CurrentMode != Mode.FreeFly)
            {
                SetMode(Mode.FreeFly);
            }
            // 8 tiles = 8 * 22 world units up; pitch down hard so we look at the player.
            FreeFlyEye = playerTarget + new Vector3(0f, 8f * 22f, 0f);
            _flyEyeValid = true;
            World3DRenderer.Camera.PitchDegrees = 75f;
        }

        public static void Cycle()
        {
            switch (CurrentMode)
            {
                case Mode.Off:         SetMode(Mode.ThirdPerson); break;
                case Mode.ThirdPerson: SetMode(Mode.FirstPerson); break;
                case Mode.FirstPerson: SetMode(Mode.Cinematic);   break;
                case Mode.Cinematic:   SetMode(Mode.FreeFly);     break;
                case Mode.FreeFly:     SetMode(Mode.Off);         break;
            }
        }

        public static string Label() => CurrentMode switch
        {
            Mode.Off         => "Off (free)",
            Mode.ThirdPerson => "3rd person",
            Mode.FirstPerson => "1st person",
            Mode.Cinematic   => "Cinematic",
            Mode.FreeFly     => "Free fly",
            _ => CurrentMode.ToString(),
        };

        /// <summary>
        /// Per-frame update. Mutates the camera target / pitch / yaw / eye-override
        /// based on the active mode. Call after World3DRenderer.Draw sets Camera.Target.
        /// </summary>
        public static void Apply(ref Vector3 cameraTarget)
        {
            ShouldHidePlayerThisFrame = false;
            if (CurrentMode == Mode.Off) return;

            var cam = World3DRenderer.Camera;

            switch (CurrentMode)
            {
                case Mode.ThirdPerson:
                {
                    // --- Feature 3: Look-ahead target offset ---
                    // Shift the camera look-at target along ground-plane forward (yaw only)
                    // so the player is offset from dead center toward the screen bottom.
                    if (EnableLookAhead && ThirdPersonLookAheadDistance != 0f)
                    {
                        float laYawRad = MathHelper.ToRadians(cam.YawDegrees);
                        // Ground-plane forward: ignore pitch, normalize to unit.
                        var laFwd = new Vector3((float)Math.Sin(laYawRad), 0f, (float)Math.Cos(laYawRad));
                        cameraTarget += laFwd * ThirdPersonLookAheadDistance;
                        cam.Target    = cameraTarget;
                    }

                    // --- Feature 2: Ground-collision auto-pitch ---
                    // Compute where the eye would land with the shifted target,
                    // then lift it above the terrain if it would clip.
                    if (Enable3rdPersonGroundCollide)
                    {
                        float pitchRad = MathHelper.ToRadians(cam.PitchDegrees);
                        float yawRad   = MathHelper.ToRadians(cam.YawDegrees);
                        // Same forward formula Camera3D.View uses.
                        var fwd = new Vector3(
                            (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                            -(float)Math.Sin(pitchRad),
                            (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad));
                        var eye = cameraTarget - fwd * cam.EyeDistance;

                        // Convert eye world position → UO tile coords for Z lookup.
                        // World.X = uoX * TILE (22), World.Z = uoY * TILE (22), World.Y = uoZ * Z_SCALE (4).
                        const float TILE = 22f;
                        const float Z_SCALE = 4f;
                        int uoX = (int)(eye.X / TILE);
                        int uoY = (int)(eye.Z / TILE);
                        var map = ClassicUO.Client.Game.UO.World?.Map;
                        if (map != null && uoX >= 0 && uoY >= 0)
                        {
                            sbyte tileZ = map.GetTileZ(uoX, uoY);
                            float groundWorldY = tileZ * Z_SCALE;
                            float minEyeY = groundWorldY + GroundCollideMargin;
                            if (eye.Y < minEyeY)
                            {
                                eye.Y = minEyeY;
                                // Auto-pitch: tilt camera up so it still looks toward the player.
                                float dy = eye.Y - cameraTarget.Y;
                                float newPitchRad = (float)Math.Atan2(cam.EyeDistance, dy);
                                float newPitchDeg = MathHelper.ToDegrees(newPitchRad);
                                cam.PitchDegrees = MathHelper.Clamp(newPitchDeg, 1f, 89f);
                                // Override eye so Camera3D.View uses our corrected position.
                                cam.UseEyeOverride = true;
                                cam.EyeOverride    = eye;
                            }
                        }
                    }

                    // --- Feature 4: 3rd-person player-faces-camera (optional) ---
                    if (ThirdPersonRotatePlayerWithCamera)
                    {
                        TrySendFacingFromYaw(cam.YawDegrees);
                    }
                    break;
                }

                case Mode.FirstPerson:
                {
                    // Place eye at the player's head: target + (0, head, 0).
                    // Look along forward (yaw/pitch).
                    cam.UseEyeOverride = true;
                    cam.EyeOverride    = cameraTarget + new Vector3(0f, FirstPersonHeadHeight, 0f);

                    if (FirstPersonForwardOffset != 0f)
                    {
                        var pitchRad = MathHelper.ToRadians(cam.PitchDegrees);
                        var yawRad   = MathHelper.ToRadians(cam.YawDegrees);
                        var forward  = new Vector3(
                            (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                            -(float)Math.Sin(pitchRad),
                            (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad));
                        cam.EyeOverride += forward * FirstPersonForwardOffset;
                    }

                    // --- Feature 4: 1st-person player-faces-camera (mandatory) ---
                    // Only fire when WASD isn't steering (Walk calls take priority).
                    if (WasdMovementController.HeldCount == 0)
                    {
                        TrySendFacingFromYaw(cam.YawDegrees);
                    }

                    ShouldHidePlayerThisFrame = true;
                    break;
                }

                case Mode.Cinematic:
                {
                    var now = DateTime.UtcNow;
                    float dt = (float)(now - _lastTick).TotalSeconds;
                    _lastTick = now;
                    if (dt > 0.5f) dt = 0f; // first frame / paused
                    _cineTime += dt;

                    cam.YawDegrees += CinematicYawSpeedDeg * dt;
                    while (cam.YawDegrees >= 360f) cam.YawDegrees -= 360f;
                    while (cam.YawDegrees < 0f)    cam.YawDegrees += 360f;

                    float wob = (float)Math.Sin(_cineTime * (Math.PI * 2.0 / CinematicPitchPeriodS));
                    cam.PitchDegrees = CinematicBasePitchDeg + wob * CinematicPitchAmpDeg;
                    if (cam.PitchDegrees < 1f)  cam.PitchDegrees = 1f;
                    if (cam.PitchDegrees > 89f) cam.PitchDegrees = 89f;
                    break;
                }

                case Mode.FreeFly:
                {
                    var now = DateTime.UtcNow;
                    float dt = (float)(now - _lastTick).TotalSeconds;
                    _lastTick = now;
                    if (dt > 0.5f) dt = 0f;

                    // Seed eye at the player target on entry, then ignore further player motion.
                    if (!_flyEyeValid)
                    {
                        // Pull eye back along forward so we don't start inside the player.
                        var p0 = MathHelper.ToRadians(cam.PitchDegrees);
                        var y0 = MathHelper.ToRadians(cam.YawDegrees);
                        var f0 = new Vector3(
                            (float)Math.Sin(y0) * (float)Math.Cos(p0),
                            -(float)Math.Sin(p0),
                            (float)Math.Cos(y0) * (float)Math.Cos(p0));
                        FreeFlyEye = cameraTarget - f0 * ThirdPersonDistance;
                        _flyEyeValid = true;
                    }

                    // Camera basis (full 3D forward — pitch DOES count in fly mode).
                    var pitchRad = MathHelper.ToRadians(cam.PitchDegrees);
                    var yawRad   = MathHelper.ToRadians(cam.YawDegrees);
                    var forward  = new Vector3(
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        -(float)Math.Sin(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad));
                    var right    = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));

                    float fwd = 0f, strafe = 0f, vert = 0f;
                    if (WasdMovementController.Enabled)
                    {
                        if (WasdMovementController.ForwardHeld) fwd    += 1f;
                        if (WasdMovementController.BackHeld)    fwd    -= 1f;
                        if (WasdMovementController.RightHeld)   strafe += 1f;
                        if (WasdMovementController.LeftHeld)    strafe -= 1f;
                        if (WasdMovementController.UpHeld)      vert   += 1f;
                        if (WasdMovementController.DownHeld)    vert   -= 1f;
                    }

                    if (fwd != 0f || strafe != 0f || vert != 0f)
                    {
                        float speed = FreeFlySpeed * (WasdMovementController.RunHeld ? FreeFlySprintMul : 1f);
                        var delta = (forward * fwd + right * strafe) * speed
                                  + Vector3.Up * (vert * speed * FreeFlyVerticalMul);
                        FreeFlyEye += delta * dt;
                    }

                    cam.UseEyeOverride = true;
                    cam.EyeOverride    = FreeFlyEye;
                    break;
                }
            }
        }

        // --- Feature 4 helper: snap yaw → nearest UO 8-direction and walk/turn ---
        // Rate-limited to FACE_RATE_HZ. Uses Walk(dir, false) — if the player is
        // standing still the UO walker treats it as a turn-only (blocked-tile
        // convention). If WASD is firing Walk() simultaneously, that wins because
        // we guard with HeldCount == 0 at the call site.
        private static void TrySendFacingFromYaw(float yawDegrees)
        {
            var world = ClassicUO.Client.Game.UO.World;
            if (world == null || !world.InGame || world.Player == null) return;

            double nowSec = (double)ClassicUO.Time.Ticks / 1000.0;
            if (nowSec - _lastFaceSendTime < 1.0 / FACE_RATE_HZ) return;

            // Map camera yaw to UO Direction.
            // yaw=0 → camera looks south (+Z). UO dir 4 = South.
            // Each 45° clockwise step advances to next direction.
            // Camera yaw increases CCW (looking East at yaw=90). Map carefully:
            //   yaw 0   = South (4), 45 = SW (5), 90 = West (6), 135 = NW (7),
            //   180 = North (0), 225 = NE (1), 270 = East (2), 315 = SE (3).
            float wrapped = ((yawDegrees % 360f) + 360f) % 360f;
            int   snap    = (int)Math.Round(wrapped / 45f) % 8;
            // snap idx: 0=S,1=SW,2=W,3=NW,4=N,5=NE,6=E,7=SE
            byte[] dirMap = { 4, 5, 6, 7, 0, 1, 2, 3 };
            var targetDir = (Direction)dirMap[snap];

            if (world.Player.Direction == targetDir) return;

            _lastFaceSendTime = nowSec;
            world.Player.Walk(targetDir, false);
        }

        // Reserved hook for callers that want to pin lifetime to the renderer
        // (avoid accidental cycles in Off→Off SetMode). Empty marker class.
        internal sealed class World3DRenderer_AccessHack { }
    }
}
