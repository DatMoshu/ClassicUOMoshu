// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — central launcher for the broken-up 3D debug gumps.
// Replaces the monolithic Debug3DGump (667 lines, ~30 ad-hoc button IDs).
// Each button opens (or focuses) a focused sub-gump owning one concern.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Render3DLauncherGump : Gump
    {
        public static Render3DLauncherGump Instance;

        // Captured at construction so child gumps that take constructor-injected
        // services (per ADR-012 §6 / playbook §Q) receive them from a single resolution
        // point instead of each child gump re-resolving via Renderer3DHost.
        private readonly Renderer3DServices _services;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 260;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        // Per-gump button-id namespace — avoids the case-collision risk that
        // plagued Debug3DGump (which had 30+ ad-hoc IDs sharing one switch).
        private static class BtnId
        {
            // TUNING (always available)
            public const int RenderMode      = 100;
            public const int World           = 101;
            public const int Camera          = 102;
            public const int Environment     = 115;
            public const int PlayerMesh      = 103;
            public const int MultiStatic     = 104;
            public const int Walls           = 105;
            public const int WorldExport     = 118;
            public const int EquipmentSlots  = 106;
            public const int PlayerMounts    = 107;
            public const int Fireworks       = 108;
            public const int Nuke            = 117;
            public const int Weather         = 109;
            public const int Wind            = 116;
            public const int BuffEffects     = 110;
            public const int Mobile3D        = 111;
            public const int Npc3D           = 119;
            public const int Crowd           = 112;
            public const int Trees           = 113;
            public const int TreeSeasons     = 114;

            // DEBUG (gated; visible only in DEBUG builds)
            public const int RtDiag          = 200;
            public const int ModelInfo       = 201;
            public const int PlayerMaterial  = 202;
            public const int MultiUv         = 203;
            public const int Tagger          = 204;
            public const int MaterialDebug   = 205;

            // Footer
            public const int DumpState       = 300;
        }

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves the renderer service container from the
        /// active <see cref="Renderer3DHost"/>. Existing call sites
        /// (Debug3DRTGump, ModelInfoGump, MobileRender3DGump) continue to use
        /// <c>new Render3DLauncherGump(World)</c> until they migrate.
        /// </summary>
        public Render3DLauncherGump(World world)
            : this(world, Renderer3DHost.Services) { }

        public Render3DLauncherGump(World world, Renderer3DServices services) : base(world, 0, 0)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3D RENDERER",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- TUNING ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TUNING", y);
            y = AddButton("Render Mode...",      BtnId.RenderMode,     contentX, innerW, y);
            y = AddButton("World / Heightmap...", BtnId.World,          contentX, innerW, y);
            y = AddButton("Camera...",           BtnId.Camera,         contentX, innerW, y);
            y = AddButton("Environment & Lighting...", BtnId.Environment, contentX, innerW, y);
            y = AddButton("Player Mesh...",      BtnId.PlayerMesh,     contentX, innerW, y);
            y = AddButton("Multi / Static...",   BtnId.MultiStatic,    contentX, innerW, y);
            y = AddButton("Walls (3D meshes)...", BtnId.Walls,          contentX, innerW, y);
            y = AddButton("World Export (GLB)...", BtnId.WorldExport,    contentX, innerW, y);
            y = AddButton("Equipment Slots...",  BtnId.EquipmentSlots, contentX, innerW, y);
            y = AddButton("Player Mounts...",    BtnId.PlayerMounts,   contentX, innerW, y);
            y = AddButton("Fireworks Show...",   BtnId.Fireworks,      contentX, innerW, y);
            y = AddButton("Nuke / D-Day...",     BtnId.Nuke,           contentX, innerW, y);
            y = AddButton("Weather Admin...",    BtnId.Weather,        contentX, innerW, y);
            y = AddButton("Wind...",             BtnId.Wind,           contentX, innerW, y);
            y = AddButton("Buff Particle FX...", BtnId.BuffEffects,    contentX, innerW, y);
            y = AddButton("3D Mobile (Anim/RT)...", BtnId.Mobile3D,    contentX, innerW, y);
            y = AddButton("3D NPCs (toggles + inspector)...", BtnId.Npc3D, contentX, innerW, y);
            y = AddButton("Crowd Density...",    BtnId.Crowd,          contentX, innerW, y);
            y = AddButton("3D Trees...",         BtnId.Trees,          contentX, innerW, y);
            y = AddButton("Tree Seasons / Snow...", BtnId.TreeSeasons, contentX, innerW, y);
            y += Debug3DStyle.SECTION_GAP;

#if DEBUG
            // ---------- DEBUG (dev tools — stripped from release) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "DEBUG", y);
            y = AddButton("RT Diagnostics...",   BtnId.RtDiag,         contentX, innerW, y);
            y = AddButton("Model Info...",       BtnId.ModelInfo,      contentX, innerW, y);
            y = AddButton("Player Material...",  BtnId.PlayerMaterial, contentX, innerW, y);
            y = AddButton("Multi UV Editor...",  BtnId.MultiUv,        contentX, innerW, y);
            y = AddButton("Equipment Tagger...", BtnId.Tagger,         contentX, innerW, y);
            y = AddButton("Material Debug...",   BtnId.MaterialDebug,  contentX, innerW, y);
            y += Debug3DStyle.SECTION_GAP;
#endif

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            y = AddButton("Dump State to Console", BtnId.DumpState, contentX, innerW, y);
            y += INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 8;
            Y = 60;
            WantUpdateSize = false;
        }

        private int AddButton(string label, int id, int contentX, int innerW, int y)
        {
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, label) { ButtonParameter = id });
            return y + ROW_H + 2;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:        Dispose(); break;

                case BtnId.RenderMode:      Open<RenderModeGump>(w     => new RenderModeGump(w, _services.CameraMode, _services.RenderMode)); break;
                case BtnId.World:           Open<WorldRenderGump>(w    => new WorldRenderGump(w, _services.RenderQuality, _services.Multi3DConfig, _services.MovementInput, _services.LegacyRendererDemo, _services.CameraMode));    break;
                case BtnId.Camera:          OpenCamera();                                              break;
                case BtnId.Environment:     OpenEnvironment();                                         break;
                case BtnId.PlayerMesh:      Open<PlayerMeshGump>(w     => new PlayerMeshGump(w, _services.Player3D)); break;
                case BtnId.MultiStatic:     OpenMultiStatic();                                         break;
                case BtnId.Walls:           Open<WallMeshGump>(w       => new WallMeshGump(w, _services.Static3DConfig, _services.Multi3DConfig)); break;
                case BtnId.WorldExport:     Open<WorldExportGump>(w    => new WorldExportGump(w));    break;
                case BtnId.EquipmentSlots:  Open<EquipmentSlotsGump>(w => new EquipmentSlotsGump(w)); break;
                case BtnId.PlayerMounts:    OpenPlayerMounts();                                        break;
                case BtnId.Fireworks:       OpenFireworks();                                           break;
                case BtnId.Nuke:            OpenNuke();                                                break;
                case BtnId.Weather:         OpenWeather();                                             break;
                case BtnId.Wind:            OpenWind();                                                break;
                case BtnId.BuffEffects:     OpenBuffEffects();                                         break;
                case BtnId.Mobile3D:        Open<MobileRender3DGump>(w => new MobileRender3DGump(w, _services.Player3D, _services.RenderMode));  break;
                case BtnId.Npc3D:           Open<Npc3DDebugGump>(w      => new Npc3DDebugGump(w, _services.MobileOutfit, _services.MobileAnim, _services.RenderDiagnostics, _services.Player3D, _services.MobileRT3D, _services.RenderMode)); break;
                case BtnId.Crowd:           Open<CrowdGump>(w           => new CrowdGump(w));          break;
                case BtnId.Trees:           Open<Trees3DSettingsGump>(w => new Trees3DSettingsGump(w, _services.Season, _services.TreeStaticRegistry, _services.Static3DConfig, _services.Static3DDiagnostics, _services.Foliage3DConfig)); break;
                case BtnId.TreeSeasons:     Open<TreeSeasonsGump>(w     => new TreeSeasonsGump(w, _services.TreeSeason)); break;

#if DEBUG
                case BtnId.RtDiag:          OpenRtDiag();                                              break;
                case BtnId.ModelInfo:       OpenModelInfo();                                           break;
                case BtnId.PlayerMaterial:  OpenPlayerMaterial();                                      break;
                case BtnId.MultiUv:         OpenMultiUv();                                             break;
                case BtnId.Tagger:          OpenTagger();                                              break;
                case BtnId.MaterialDebug:   Open<MaterialDebugGump>(w   => new MaterialDebugGump(w, _services.Player3D));  break;
#endif

                case BtnId.DumpState:       Render3DDumper.DumpAll();                                  break;
            }
        }

        private void Open<T>(Func<World, T> ctor) where T : Gump
        {
            // Walk UIManager for an existing instance — avoids each sub-gump
            // needing its own static Instance field that the launcher reads.
            // Existing sub-gumps still keep their .Instance for back-compat
            // with sibling references; we just don't depend on them here.
            foreach (var g in Game.Managers.UIManager.Gumps)
            {
                if (g is T existing && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
            }
            var n = ctor(World);
            Game.Managers.UIManager.Add(n);
        }

        // Existing gumps still expose static Instance fields. Keep their open
        // pattern intact so we don't churn unrelated code in this pass.
        private void OpenCamera()
        {
            if (CameraGump.Instance == null)
            {
                var g = new CameraGump(World, _services.Lighting, _services.CameraState, _services.Environment,
                    _services.RenderQuality, _services.RenderDiagnostics,
                    _services.CameraInput, _services.MovementInput);
                CameraGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { CameraGump.Instance.SetInScreen(); CameraGump.Instance.BringOnTop(); }
        }

        private void OpenEnvironment()
        {
            if (EnvironmentLightingGump.Instance == null)
            {
                var g = new EnvironmentLightingGump(World, _services.Environment);
                EnvironmentLightingGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { EnvironmentLightingGump.Instance.SetInScreen(); EnvironmentLightingGump.Instance.BringOnTop(); }
        }

        private void OpenMultiStatic()
        {
            if (Multi3DSettingsGump.Instance == null)
            {
                var g = new Multi3DSettingsGump(World, _services.MultiOrientation, _services.CameraState,
                    _services.RenderQuality, _services.RenderDiagnostics,
                    _services.Static3DConfig, _services.Static3DDiagnostics,
                    _services.Multi3DConfig, _services.Multi3DDiagnostics);
                Multi3DSettingsGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { Multi3DSettingsGump.Instance.SetInScreen(); Multi3DSettingsGump.Instance.BringOnTop(); }
        }

        private void OpenPlayerMounts()
        {
            if (PlayerMountsGump.Instance == null)
            {
                var g = new PlayerMountsGump(World);
                PlayerMountsGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { PlayerMountsGump.Instance.SetInScreen(); PlayerMountsGump.Instance.BringOnTop(); }
        }

        private void OpenFireworks()
        {
            if (FireworksGump.Instance == null)
            {
                var g = new FireworksGump(World, _services.Fireworks, _services.Particle);
                FireworksGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { FireworksGump.Instance.SetInScreen(); FireworksGump.Instance.BringOnTop(); }
        }

        private void OpenNuke()
        {
            if (NukeGump.Instance == null)
            {
                var g = new NukeGump(World, _services.NukeShow, _services.Explosion, _services.Fire, _services.Particle);
                NukeGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { NukeGump.Instance.SetInScreen(); NukeGump.Instance.BringOnTop(); }
        }

        private void OpenWeather()
        {
            if (WeatherAdminGump.Instance == null)
            {
                var g = new WeatherAdminGump(World, _services.Weather, _services.Wind,
                    _services.WeatherAudio, _services.FootstepAudio, _services.Particle,
                    _services.GroundEffect, _services.FoliageSeason, _services.Multi3DConfig,
                    _services.Foliage3DConfig, _services.GroundOverlay);
                WeatherAdminGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { WeatherAdminGump.Instance.SetInScreen(); WeatherAdminGump.Instance.BringOnTop(); }
        }

        private void OpenWind()
        {
            // Explicit constructor injection — bypasses the WindGump host-locator
            // convenience overload now that the launcher itself has a resolved services
            // container. As more child gumps gain a `(World, IXxxService)` constructor,
            // their Open* methods migrate to this same shape.
            if (WindGump.Instance == null)
            {
                var g = new WindGump(World, _services.Wind, _services.Foliage3DConfig, _services.Weather);
                WindGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { WindGump.Instance.SetInScreen(); WindGump.Instance.BringOnTop(); }
        }

        private void OpenBuffEffects()
        {
            if (BuffEffectsGump.Instance == null)
            {
                var g = new BuffEffectsGump(World, _services.BuffParticles, _services.Particle);
                BuffEffectsGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { BuffEffectsGump.Instance.SetInScreen(); BuffEffectsGump.Instance.BringOnTop(); }
        }

#if DEBUG
        private void OpenRtDiag()
        {
            if (Debug3DRTGump.Instance == null)
            {
                var g = new Debug3DRTGump(World);
                Debug3DRTGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { Debug3DRTGump.Instance.SetInScreen(); Debug3DRTGump.Instance.BringOnTop(); }
        }

        private void OpenModelInfo()
        {
            if (ModelInfoGump.Instance == null)
            {
                var g = new ModelInfoGump(World, _services.Player3D, _services.MobileRT3D);
                ModelInfoGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { ModelInfoGump.Instance.SetInScreen(); ModelInfoGump.Instance.BringOnTop(); }
        }

        private void OpenPlayerMaterial()
        {
            if (PlayerMaterialGump.Instance == null)
            {
                var g = new PlayerMaterialGump(World, _services.Player3D, _services.MobileRT3D);
                PlayerMaterialGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { PlayerMaterialGump.Instance.SetInScreen(); PlayerMaterialGump.Instance.BringOnTop(); }
        }

        private void OpenMultiUv()
        {
            if (MultiUvGump.Instance == null)
            {
                var g = new MultiUvGump(World);
                MultiUvGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { MultiUvGump.Instance.SetInScreen(); MultiUvGump.Instance.BringOnTop(); }
        }

        private void OpenTagger()
        {
            if (EquipmentTaggerGump.Instance == null)
            {
                var g = new EquipmentTaggerGump(World);
                EquipmentTaggerGump.Instance = g;
                Game.Managers.UIManager.Add(g);
            }
            else { EquipmentTaggerGump.Instance.SetInScreen(); EquipmentTaggerGump.Instance.BringOnTop(); }
        }
#endif
    }
}
