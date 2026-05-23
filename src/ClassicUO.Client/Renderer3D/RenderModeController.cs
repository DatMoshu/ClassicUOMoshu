// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — single source of truth for the render-pipeline mode.
//
// Three user-meaningful modes:
//
//   Classic2D — Pure 2D world, classic iso camera. Default for fresh
//               profiles. Optional sub-toggle Use3DPlayerInClassic2D drives
//               the player as a 3D RT-blit on top of the otherwise-2D world
//               (the legacy "MobilesIn3D" behaviour).
//
//   Iso3D     — 3D heightmap + Multi3D + Static3D + 3D inline player.
//               Camera locked to classic UO iso angles (no free pitch/yaw,
//               no perspective). Same visual feel as classic UO with proper
//               3D depth so walls occlude the player correctly.
//
//   Full3D    — 3D world + 3D inline player + free perspective camera.
//               Experimental.
//
// Profile persistence: the chosen mode + sub-toggle survive client restart
// (see Profile.RenderMode3D / Profile.Use3DPlayerInClassic2D). HydrateFromProfile
// is called once after profile load; SetMode writes back automatically.
//
// Cycling: F3 calls CycleNext() — Classic2D → Iso3D → Full3D → Classic2D.

using System;

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum RenderMode
    {
        Classic2D = 0,
        Iso3D     = 1,
        Full3D    = 2,
    }

    internal static class RenderModeController
    {
        // Default = Classic2D. Safest, most-compatible baseline; users opt
        // into 3D via F3 or the Render Mode gump and the choice persists per
        // profile.
        public static RenderMode Mode { get; private set; } = RenderMode.Classic2D;

        // Sub-toggle for Classic2D: when true, the player still renders as a
        // 3D mobile (RT-blit pipeline) on top of the 2D world. For purists
        // the toggle stays off and the world is pure UO.
        public static bool Use3DPlayerInClassic2D { get; private set; } = false;

        // Cross-mode toggle: when true, non-player human mobiles render as
        // composed 3D humanoids. The backend that draws them depends on the
        // active mode — see Npc3DDebugGump:
        //   Classic2D + Use3DPlayerInClassic2D : routed through MobileRT3DRenderer
        //                                        (per-mobile RT blits)
        //   Iso3D / Full3D                     : routed through World3DRenderer's
        //                                        inline NPC pass (true 3D space,
        //                                        shared depth buffer)
        //   Classic2D pure                     : no-op (NPCs stay 2D sprites)
        // Default ON so 3D modes immediately show 3D NPCs without an extra
        // toggle hunt. Not currently persisted to Profile — re-toggle after
        // restart if you want it off by default.
        public static bool MobilesIn3D { get; private set; } = true;

        // Convenience predicates so call sites can ask intent rather than
        // pattern-match against the enum each time.
        public static bool Is2DOnly => Mode == RenderMode.Classic2D;
        public static bool MobilesAre3D => Mode != RenderMode.Classic2D || Use3DPlayerInClassic2D;
        public static bool WorldIs3D => Mode == RenderMode.Iso3D || Mode == RenderMode.Full3D;
        public static bool CameraLockedToIso => Mode == RenderMode.Classic2D || Mode == RenderMode.Iso3D;

        // Bumps each time the mode changes. Lets caches (e.g. the RT pool)
        // detect a stale state without subscribing to events.
        public static int Generation { get; private set; }

        public static void Initialize()
        {
            ApplyToLegacyFlags(Mode);
        }

        public static void SetMode(RenderMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            Generation++;
            ApplyToLegacyFlags(mode);
            WriteToProfile();
        }

        public static void SetMobilesIn3D(bool value)
        {
            if (value == MobilesIn3D) return;
            MobilesIn3D = value;
            Generation++;
            // No legacy-flag mirroring — both backends (RT path + inline pass)
            // read MobilesIn3D directly each frame.
        }

        public static void SetUse3DPlayerInClassic2D(bool value)
        {
            if (value == Use3DPlayerInClassic2D) return;
            Use3DPlayerInClassic2D = value;
            Generation++;
            ApplyToLegacyFlags(Mode);
            WriteToProfile();
        }

        // F3 cycles Classic2D → Iso3D → Full3D → Classic2D.
        public static void CycleNext()
        {
            var next = (RenderMode)(((int)Mode + 1) % 3);
            // Force-apply even if the enum value matches (covers fresh-startup
            // case where Mode already equals the default but legacy flags
            // haven't been wired yet).
            Mode = next;
            Generation++;
            ApplyToLegacyFlags(next);
            WriteToProfile();
        }

        // Pulls persisted state from the active profile and applies it.
        // Called once after ProfileManager.CurrentProfile is populated
        // (typically from GameScene.Load). No-op if the profile is null.
        public static void HydrateFromProfile()
        {
            var p = ClassicUO.Configuration.ProfileManager.CurrentProfile;
            if (p == null) return;

            int raw = Math.Clamp(p.RenderMode3D, 0, 2);
            Mode = (RenderMode)raw;
            Use3DPlayerInClassic2D = p.Use3DPlayerInClassic2D;
            Generation++;
            ApplyToLegacyFlags(Mode);
        }

        private static void WriteToProfile()
        {
            var p = ClassicUO.Configuration.ProfileManager.CurrentProfile;
            if (p == null) return;
            p.RenderMode3D = (int)Mode;
            p.Use3DPlayerInClassic2D = Use3DPlayerInClassic2D;
        }

        // Mirror the chosen mode into the legacy flat-bool toggles that the
        // existing renderers still read. One source of truth — every renderer
        // flag that varies by mode is driven from this single switch.
        private static void ApplyToLegacyFlags(RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.Classic2D:
                {
                    bool rt = Use3DPlayerInClassic2D;
                    Player3DRenderer.Enabled = rt;
                    MobileRT3DRenderer.Enabled = rt;
                    AttachmentRenderer.Enabled = rt;
                    World3DRenderer.Enabled = false;
                    World3DRenderer.Disable2DWorld = false;
                    Multi3DRenderer.Enabled = false;
                    Static3DRenderer.Enabled = false;
                    World3DRenderer.UseIsoProjection = true;
                    World3DRenderer.Camera.UsePerspective = false;
                    CameraInputController.MiddleMouseRotate = false;
                    Camera3D.LockToIso = true;
                    break;
                }

                case RenderMode.Iso3D:
                {
                    // 3D world + 3D inline player; camera clamped to iso.
                    Player3DRenderer.Enabled = true;
                    MobileRT3DRenderer.Enabled = false;
                    AttachmentRenderer.Enabled = true;
                    World3DRenderer.Enabled = true;
                    World3DRenderer.Disable2DWorld = true;
                    Multi3DRenderer.Enabled = true;
                    Static3DRenderer.Enabled = true;
                    World3DRenderer.UseIsoProjection = true;
                    World3DRenderer.Camera.UsePerspective = false;
                    World3DRenderer.DepthTestEnabled = true;
                    CameraInputController.MiddleMouseRotate = false;
                    Camera3D.LockToIso = true;
                    break;
                }

                case RenderMode.Full3D:
                {
                    // 3D world + 3D inline player; free perspective camera.
                    Player3DRenderer.Enabled = true;
                    MobileRT3DRenderer.Enabled = false;
                    AttachmentRenderer.Enabled = true;
                    World3DRenderer.Enabled = true;
                    World3DRenderer.Disable2DWorld = true;
                    Multi3DRenderer.Enabled = true;
                    Static3DRenderer.Enabled = true;
                    World3DRenderer.UseIsoProjection = false;
                    World3DRenderer.Camera.UsePerspective = true;
                    World3DRenderer.DepthTestEnabled = true;
                    CameraInputController.MiddleMouseRotate = true;
                    Camera3D.LockToIso = false;
                    break;
                }
            }
        }

        public static string CurrentLabel() => Mode switch
        {
            RenderMode.Classic2D => Use3DPlayerInClassic2D ? "Classic 2D (3D player)" : "Classic 2D",
            RenderMode.Iso3D     => "3D Iso",
            RenderMode.Full3D    => "Full 3D (experimental)",
            _ => Mode.ToString(),
        };
    }
}
