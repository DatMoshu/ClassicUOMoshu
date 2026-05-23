// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Input;
using ClassicUO.Resources;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal sealed class CommandManager
    {
        private readonly Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();
        private readonly World _world;

        public CommandManager(World world)
        {
            _world = world;
        }

        public void Initialize()
        {
            Register
            (
                "info",
                s =>
                {
                    if (_world.TargetManager.IsTargeting)
                    {
                        _world.TargetManager.CancelTarget();
                    }

                    _world.TargetManager.SetTargeting(CursorTarget.SetTargetClientSide, CursorType.Target, TargetType.Neutral);
                }
            );

            Register
            (
                "datetime",
                s =>
                {
                    if (_world.Player != null)
                    {
                        GameActions.Print(_world, string.Format(ResGeneral.CurrentDateTimeNowIs0, DateTime.Now));
                    }
                }
            );

            Register
            (
                "hue",
                s =>
                {
                    if (_world.TargetManager.IsTargeting)
                    {
                        _world.TargetManager.CancelTarget();
                    }

                    _world.TargetManager.SetTargeting(CursorTarget.HueCommandTarget, CursorType.Target, TargetType.Neutral);
                }
            );


            Register
            (
                "debug",
                s =>
                {
                    CUOEnviroment.Debug = !CUOEnviroment.Debug;

                }
            );

            // 3DCUO PROTOTYPE — fake-crowd ambience. `[crowd` overlaps random
            // mp3s from Data/voicelines/JarJar/ for 30 seconds. `[crowd stop`
            // cancels early. Optional args: `[crowd <durationSec> <voiceCount>`.
            Register
            (
                "crowd",
                s =>
                {
                    if (s != null && s.Length >= 2 && string.Equals(s[1], "stop", StringComparison.OrdinalIgnoreCase))
                    {
                        CrowdMimicManager.Stop();
                        GameActions.Print(_world, "[crowd] stopped");
                        return;
                    }

                    int dur = CrowdMimicManager.DefaultDurationSec;
                    int voices = CrowdMimicManager.DefaultVoiceCount;
                    if (s != null && s.Length >= 2 && int.TryParse(s[1], out var d) && d > 0) dur = d;
                    if (s != null && s.Length >= 3 && int.TryParse(s[2], out var v) && v > 0) voices = v;

                    string msg = CrowdMimicManager.Start("Data/voicelines/JarJar", dur, voices);
                    GameActions.Print(_world, "[crowd] " + msg);
                }
            );

            // -replayvoice / -rv — replay the last N NPC voice lines from the
            // client-side history ring. Optional N, default 1.
            Action<string[]> replay = s =>
            {
                int n = 1;
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out var k) && k > 0) n = k;
                ReplayVoice.Run(_world, n);
            };
            Register("replayvoice", replay);
            Register("rv", replay);

            // 3DCUO PROTOTYPE — admin tuning gump for crowd ambience.
            Register
            (
                "crowdgump",
                s =>
                {
                    if (UI.Gumps.CrowdGump.Instance == null)
                    {
                        var g = new UI.Gumps.CrowdGump(_world);
                        UI.Gumps.CrowdGump.Instance = g;
                        UIManager.Add(g);
                    }
                    else
                    {
                        UI.Gumps.CrowdGump.Instance.SetInScreen();
                        UI.Gumps.CrowdGump.Instance.BringOnTop();
                    }
                }
            );

            // 3DCUO PROTOTYPE — open the 3D renderer launcher (hub gump).
            // The launcher exposes one button per topic-split sub-gump
            // (RenderMode / World / Camera / Player Mesh / Multi-Static /
            // Walls / Equipment Slots / Player Mounts) plus a DEBUG section
            // for dev-only gumps in DEBUG builds. Replaces the legacy
            // `[debug3d` command which opened the monolithic Debug3DGump.
            Register
            (
                "3d",
                s =>
                {
                    if (UI.Gumps.Render3DLauncherGump.Instance == null)
                    {
                        var g = new UI.Gumps.Render3DLauncherGump(_world);
                        UI.Gumps.Render3DLauncherGump.Instance = g;
                        UIManager.Add(g);
                    }
                    else
                    {
                        UI.Gumps.Render3DLauncherGump.Instance.SetInScreen();
                        UI.Gumps.Render3DLauncherGump.Instance.BringOnTop();
                    }
                }
            );

            // 3DCUO PROTOTYPE — open the dedicated mobile-3D rendering form.
            // Sibling to `[debug3d`. Hosts the render-mode selector, RT
            // sliders, and satellite-gump launchers (PlayerMounts, ModelInfo,
            // EquipmentTagger). Mode toggles route through RenderModeController
            // so the three modes (Default 2D / 2D + 3D Mobiles / Full 3D) stay
            // consistent across both gumps.
            Register
            (
                "mobile3d",
                s =>
                {
                    // Diagnostic — confirm the lambda fires before any gump
                    // construction. Prints to both the OS console (VS Output)
                    // and the in-game chat so we can tell from either side
                    // whether the dispatch reached us and whether the gump
                    // constructor threw.
                    System.Console.WriteLine("[3DCUO] [mobile3d] command fired");
                    GameActions.Print(_world, "[3DCUO] [mobile3d] command fired");
                    try
                    {
                        if (UI.Gumps.MobileRender3DGump.Instance == null)
                        {
                            var g = new UI.Gumps.MobileRender3DGump(_world);
                            UI.Gumps.MobileRender3DGump.Instance = g;
                            UIManager.Add(g);
                            System.Console.WriteLine("[3DCUO] [mobile3d] gump constructed + added");
                            GameActions.Print(_world, "[3DCUO] [mobile3d] gump opened");
                        }
                        else
                        {
                            UI.Gumps.MobileRender3DGump.Instance.SetInScreen();
                            UI.Gumps.MobileRender3DGump.Instance.BringOnTop();
                            GameActions.Print(_world, "[3DCUO] [mobile3d] gump brought to front");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine("[3DCUO] [mobile3d] EXCEPTION: " + ex);
                        GameActions.Print(_world, "[3DCUO] [mobile3d] EXCEPTION: " + ex.Message);
                    }
                }
            );

            // 3DCUO PROTOTYPE — open the consolidated 3D NPC debug gump.
            // Sole home for NPC-3D toggles, mobile-3D index status, the
            // nearby-NPC inspector table, and the mobiles-wireframe toggle.
            Register
            (
                "npc3d",
                s =>
                {
                    try
                    {
                        if (UI.Gumps.Npc3DDebugGump.Instance == null)
                        {
                            var g = new UI.Gumps.Npc3DDebugGump(_world);
                            UI.Gumps.Npc3DDebugGump.Instance = g;
                            UIManager.Add(g);
                            GameActions.Print(_world, "[3DCUO] [npc3d] gump opened");
                        }
                        else
                        {
                            UI.Gumps.Npc3DDebugGump.Instance.SetInScreen();
                            UI.Gumps.Npc3DDebugGump.Instance.BringOnTop();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine("[3DCUO] [npc3d] EXCEPTION: " + ex);
                        GameActions.Print(_world, "[3DCUO] [npc3d] EXCEPTION: " + ex.Message);
                    }
                }
            );

            // matdbg — open Material Debug gump directly. PROTOTYPE diagnostic
            // for the white-meshes texture bug; provides test-pattern injection,
            // sampler slot/filter cycling, and CPU-side pixel readback.
            Register
            (
                "matdbg",
                s =>
                {
                    try
                    {
                        if (UI.Gumps.MaterialDebugGump.Instance == null)
                        {
                            var g = new UI.Gumps.MaterialDebugGump(_world);
                            UI.Gumps.MaterialDebugGump.Instance = g;
                            UIManager.Add(g);
                            GameActions.Print(_world, "[3DCUO] material debug gump opened");
                        }
                        else
                        {
                            UI.Gumps.MaterialDebugGump.Instance.SetInScreen();
                            UI.Gumps.MaterialDebugGump.Instance.BringOnTop();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine("[3DCUO] [matdbg] EXCEPTION: " + ex);
                        GameActions.Print(_world, "[3DCUO] [matdbg] EXCEPTION: " + ex.Message);
                    }
                }
            );

            // 3DCUO PROTOTYPE — path recorder + replay (PathRecorderManager /
            // PathReplayManager). Records the local player's position+facing to
            // Data/Replays/<name>.json then teleport-replays it on demand.
            // Replay files can also script camera, audio cues, captions, and
            // SeasonCycleDriver/Weather3DSystem hooks for authored cinematics.
            Register("recordpath", s =>
            {
                string name = (s != null && s.Length >= 2 && !string.IsNullOrWhiteSpace(s[1])) ? s[1] : "test1";
                PathRecorderManager.StartRecording(name);
                GameActions.Print(_world, "[recordpath] recording '" + name + "' — type [stoprecord to save");
            });
            Register("stoprecord", s =>
            {
                string file = PathRecorderManager.StopAndSave();
                GameActions.Print(_world, "[recordpath] saved → " + file);
            });
            Register("playpath", s =>
            {
                string name = (s != null && s.Length >= 2 && !string.IsNullOrWhiteSpace(s[1])) ? s[1] : "test1";
                bool loop = s != null && s.Length >= 3 && string.Equals(s[2], "loop", StringComparison.OrdinalIgnoreCase);
                if (!PathReplayManager.Load(name))
                {
                    GameActions.Print(_world, "[playpath] not found: " + name);
                    return;
                }
                PathReplayManager.Start(_world, loop);
                GameActions.Print(_world, "[playpath] " + name + (loop ? " (loop)" : ""));
            });
            Register("stopplay", s =>
            {
                PathReplayManager.Stop();
                GameActions.Print(_world, "[playpath] stopped");
            });
            Register("seasonsdemo", s =>
            {
                if (!PathReplayManager.Load("seasons-demo"))
                {
                    GameActions.Print(_world, "[seasonsdemo] missing Data/Replays/seasons-demo.json");
                    return;
                }
                PathReplayManager.Start(_world, true);
                GameActions.Print(_world, "[seasonsdemo] started — [stopplay to interrupt");
            });
            // 3DCUO PROTOTYPE — staggered per-tree defoliation (winter mode).
            // `[winterstagger on|off`           toggle the per-tree stagger.
            // `[winterstrip <seconds>`          ramp WinterT 0→1 over <seconds> live (no season cycle needed).
            // `[winterstrip pop|lerp|mix`       set the variant pool (mix = 50/50, default).
            // `[winterstripreset`               WinterT back to 0 (canopy fully restored).
            Register("winterstagger", s =>
            {
                bool on = !(s != null && s.Length >= 2
                    && (string.Equals(s[1], "off", StringComparison.OrdinalIgnoreCase)
                        || s[1] == "0" || string.Equals(s[1], "false", StringComparison.OrdinalIgnoreCase)));
                ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.Enabled = on;
                if (!on) ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.Configure(0f);
                GameActions.Print(_world, "[winterstagger] " + (on ? "ON" : "off"));
            });
            Register("winterstrip", s =>
            {
                if (s != null && s.Length >= 2)
                {
                    string a = s[1];
                    if (string.Equals(a, "pop", StringComparison.OrdinalIgnoreCase))
                    {
                        ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.PopProbability = 1f;
                        GameActions.Print(_world, "[winterstrip] all POP");
                        return;
                    }
                    if (string.Equals(a, "lerp", StringComparison.OrdinalIgnoreCase))
                    {
                        ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.PopProbability = 0f;
                        GameActions.Print(_world, "[winterstrip] all LERP");
                        return;
                    }
                    if (string.Equals(a, "mix", StringComparison.OrdinalIgnoreCase))
                    {
                        ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.PopProbability = 0.5f;
                        GameActions.Print(_world, "[winterstrip] mix 50/50");
                        return;
                    }
                    if (float.TryParse(a, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out float secs)
                        && secs > 0f)
                    {
                        ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.Enabled = true;
                        WinterStripRamp.Start(secs);
                        GameActions.Print(_world, $"[winterstrip] ramping over {secs:0.0}s");
                        return;
                    }
                }
                // No arg → default 8s ramp.
                ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.Enabled = true;
                WinterStripRamp.Start(8f);
                GameActions.Print(_world, "[winterstrip] ramping over 8.0s — use 'pop' / 'lerp' / 'mix' to change variant");
            });
            Register("winterstripreset", s =>
            {
                WinterStripRamp.Stop();
                ClassicUO.Renderer.Renderer3D.TreeDefoliationStagger.Configure(0f);
                GameActions.Print(_world, "[winterstrip] reset — WinterT=0 (full canopy)");
            });

            // 3DCUO PROTOTYPE — ambient floating motes (OoT Kokiri-style).
            //   [motes on|off                — toggle the system
            //   [motes <palette>             — kokiri | lostwoods | spirit | embers | bloodmoon | fairy
            //   [motes count <n>             — target alive population (default 120)
            //   [motes radius <tiles>        — cylinder radius in tiles (default 12)
            // Particles use the existing depth-tested additive pass so cliffs,
            // walls, and statics naturally occlude them.
            Register("motes", s =>
            {
                if (s == null || s.Length < 2)
                {
                    AmbientMotes3D.Enabled = !AmbientMotes3D.Enabled;
                    GameActions.Print(_world, "[motes] " + (AmbientMotes3D.Enabled ? "ON" : "off"));
                    return;
                }
                string a = s[1].ToLowerInvariant();
                if (a == "on" || a == "true" || a == "1")
                {
                    AmbientMotes3D.Enabled = true;
                    GameActions.Print(_world, "[motes] ON");
                    return;
                }
                if (a == "off" || a == "false" || a == "0")
                {
                    AmbientMotes3D.Enabled = false;
                    GameActions.Print(_world, "[motes] off");
                    return;
                }
                if (a == "count" && s.Length >= 3 && int.TryParse(s[2], out int n) && n >= 0)
                {
                    AmbientMotes3D.TargetAlive = n;
                    AmbientMotes3D.Enabled = n > 0;
                    GameActions.Print(_world, "[motes] target=" + n);
                    return;
                }
                if (a == "radius" && s.Length >= 3
                    && float.TryParse(s[2], System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out float tiles)
                    && tiles > 0f)
                {
                    AmbientMotes3D.Radius = tiles * 22f;   // 22 world units per tile
                    GameActions.Print(_world, $"[motes] radius={tiles:0.0} tiles");
                    return;
                }
                // Treat anything else as a palette name.
                AmbientMotes3D.SetPalette(a);
                AmbientMotes3D.Enabled = true;
                GameActions.Print(_world, "[motes] palette=" + a);
            });

            Register("recordergump", s =>
            {
                if (UI.Gumps.PathRecorderGump.Instance == null)
                {
                    var g = new UI.Gumps.PathRecorderGump(_world);
                    UI.Gumps.PathRecorderGump.Instance = g;
                    UIManager.Add(g);
                }
                else
                {
                    UI.Gumps.PathRecorderGump.Instance.SetInScreen();
                    UI.Gumps.PathRecorderGump.Instance.BringOnTop();
                }
            });

            // rtdbg — open the dedicated 3D RT debug gump (resolution,
            // supersample, framing). All RT-tuning controls live here.
            Register
            (
                "rtdbg",
                s =>
                {
                    try
                    {
                        if (UI.Gumps.Debug3DRTGump.Instance == null)
                        {
                            var g = new UI.Gumps.Debug3DRTGump(_world);
                            UI.Gumps.Debug3DRTGump.Instance = g;
                            UIManager.Add(g);
                            GameActions.Print(_world, "[3DCUO] RT debug gump opened");
                        }
                        else
                        {
                            UI.Gumps.Debug3DRTGump.Instance.SetInScreen();
                            UI.Gumps.Debug3DRTGump.Instance.BringOnTop();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine("[3DCUO] [rtdbg] EXCEPTION: " + ex);
                        GameActions.Print(_world, "[3DCUO] [rtdbg] EXCEPTION: " + ex.Message);
                    }
                }
            );

            // [exportworld [radius] [block]
            //   Runs WorldGlbExporter.ExportFullWorld and writes the master
            //   manifest.json + per-block GLBs under prototypes/3DCUO/dumps/world_<stamp>/.
            //   No args -> uses the values currently set on WorldGlbExporter
            //   (defaults: radius=2048, block=256). Optional args override
            //   FullWorldRadiusTiles / FullWorldBlockTiles for this run only.
            Register("exportworld", s =>
            {
                try
                {
                    int? overrideRadius = null;
                    int? overrideBlock  = null;
                    if (s != null && s.Length >= 2 && int.TryParse(s[1], out var r)) overrideRadius = r;
                    if (s != null && s.Length >= 3 && int.TryParse(s[2], out var b)) overrideBlock  = b;

                    int prevRadius = WorldGlbExporter.FullWorldRadiusTiles;
                    int prevBlock  = WorldGlbExporter.FullWorldBlockTiles;
                    if (overrideRadius.HasValue) WorldGlbExporter.FullWorldRadiusTiles = overrideRadius.Value;
                    if (overrideBlock.HasValue)  WorldGlbExporter.FullWorldBlockTiles  = overrideBlock.Value;

                    string outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        System.AppContext.BaseDirectory, "..", "..", "..", "dumps",
                        $"world_{System.DateTime.Now:yyyyMMdd_HHmmss}"));

                    GameActions.Print(_world,
                        $"[exportworld] starting (radius={WorldGlbExporter.FullWorldRadiusTiles}, " +
                        $"block={WorldGlbExporter.FullWorldBlockTiles}) -> {outDir}");

                    string manifest = WorldGlbExporter.ExportFullWorld(_world, ClassicUO.Client.Game.GraphicsDevice, outDir);

                    // Restore overrides so the gump reflects the same numbers next time.
                    if (overrideRadius.HasValue) WorldGlbExporter.FullWorldRadiusTiles = prevRadius;
                    if (overrideBlock.HasValue)  WorldGlbExporter.FullWorldBlockTiles  = prevBlock;

                    if (manifest != null)
                        GameActions.Print(_world, $"[exportworld] DONE -> {manifest}");
                    else
                        GameActions.Print(_world, $"[exportworld] FAILED: {WorldGlbExporter.LastError}");
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine("[3DCUO] [exportworld] EXCEPTION: " + ex);
                    GameActions.Print(_world, "[exportworld] EXCEPTION: " + ex.Message);
                }
            });
        }


        public void Register(string name, Action<string[]> callback)
        {
            name = name.ToLower();

            if (!_commands.ContainsKey(name))
            {
                _commands.Add(name, callback);
            }
            else
            {
                Log.Error($"Attempted to register command: '{name}' twice.");
            }
        }

        public void UnRegister(string name)
        {
            name = name.ToLower();

            if (_commands.ContainsKey(name))
            {
                _commands.Remove(name);
            }
        }

        public void UnRegisterAll()
        {
            _commands.Clear();
        }

        public void Execute(string name, params string[] args)
        {
            name = name.ToLower();

            if (_commands.TryGetValue(name, out Action<string[]> action))
            {
                action.Invoke(args);
            }
            else
            {
                Log.Warn($"Command: '{name}' not exists");
            }
        }

        public void OnHueTarget(Entity entity)
        {
            if (entity != null)
            {
                _world.TargetManager.Target(entity);
                Mouse.LastLeftButtonClickTime = 0;
                GameActions.Print(_world, string.Format(ResGeneral.ItemID0Hue1, entity.Graphic, entity.Hue));
            }
            else
            {
                Mouse.LastLeftButtonClickTime = 0;
            }
        }
    }
}