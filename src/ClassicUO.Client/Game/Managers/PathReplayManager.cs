// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — replay a recorded path with optional scripted camera /
// audio cues / captions / world hooks. Looped or one-shot. Companion to
// PathRecorderManager.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer.Renderer3D;

namespace ClassicUO.Game.Managers
{
    internal static class PathReplayManager
    {
        public static bool IsPlaying { get; private set; }
        public static bool Loop;
        public static string CurrentName { get; private set; } = "(none)";
        public static long ElapsedMs { get; private set; }
        public static int LastSampleIdx { get; private set; }

        private static ReplayDoc _doc;
        private static long _startTicks;

        // Cursors for time-keyed lists (advance forward only within a loop).
        private static int _camIdx;
        private static int _audioIdx;
        private static int _captionIdx;
        private static int _worldIdx;

        // Camera ease state — between two cameraKeys we interpolate.
        private static ReplayCameraKey _camFrom;
        private static ReplayCameraKey _camTo;
        private static long _camFromMs;

        public static bool Load(string name)
        {
            string file = PathRecorderManager.PathForName(name);
            if (!File.Exists(file))
            {
                Console.WriteLine($"[PathReplay] file not found: {file}");
                return false;
            }
            try
            {
                string json = File.ReadAllText(file);
                _doc = JsonSerializer.Deserialize<ReplayDoc>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (_doc == null || _doc.samples == null)
                {
                    Console.WriteLine("[PathReplay] empty/invalid replay");
                    return false;
                }
                CurrentName = _doc.name ?? name;
                Console.WriteLine($"[PathReplay] loaded '{CurrentName}' samples={_doc.samples.Length}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PathReplay] LOAD FAILED: {ex}");
                return false;
            }
        }

        public static void Start(World world, bool loop)
        {
            if (_doc == null || world == null || world.Player == null)
            {
                Console.WriteLine("[PathReplay] cannot start — no doc or no player");
                return;
            }
            Loop = loop || (_doc.loop);
            IsPlaying = true;
            _startTicks = Time.Ticks;
            ElapsedMs = 0;
            LastSampleIdx = -1;
            _camIdx = _audioIdx = _captionIdx = _worldIdx = 0;
            _camFrom = _camTo = null;

            if (HasSamples)
            {
                TeleportToFirstSample(world);
            }
            Console.WriteLine($"[PathReplay] start name='{CurrentName}' loop={Loop} samples={(_doc.samples?.Length ?? 0)}");
        }

        private static bool HasSamples => _doc != null && _doc.samples != null && _doc.samples.Length > 0;

        public static void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            Console.WriteLine("[PathReplay] stopped");
            CinematicCaptionGump.HideNow();
        }

        private static void TeleportToFirstSample(World world)
        {
            var s0 = _doc.samples[0];
            world.Player.SetInWorldTile((ushort)s0.x, (ushort)s0.y, (sbyte)s0.z);
            world.Player.Direction = (Direction)(s0.dir & 0x07);
        }

        public static void Tick(World world)
        {
            if (!IsPlaying || _doc == null || world == null || world.Player == null) return;

            ElapsedMs = (long)(Time.Ticks - _startTicks);

            // -------- Player position via samples --------
            if (HasSamples)
            {
                var samples = _doc.samples;
                int last = samples.Length - 1;
                int i = LastSampleIdx;
                // Advance until samples[i+1].t > elapsed.
                while (i < last && samples[i + 1].t <= ElapsedMs)
                {
                    i++;
                }
                if (i != LastSampleIdx && i >= 0)
                {
                    var s = samples[i];
                    world.Player.SetInWorldTile((ushort)s.x, (ushort)s.y, (sbyte)s.z);
                    world.Player.Direction = (Direction)(s.dir & 0x07);
                    LastSampleIdx = i;
                }
            }

            // -------- Camera keys --------
            if (_doc.cameraKeys != null)
            {
                while (_camIdx < _doc.cameraKeys.Length && _doc.cameraKeys[_camIdx].t <= ElapsedMs)
                {
                    var k = _doc.cameraKeys[_camIdx];
                    ApplyCameraModeIfChanged(k.mode);
                    _camFrom = _camTo ?? k;
                    _camFromMs = ElapsedMs;
                    _camTo = k;
                    _camIdx++;
                }
                ApplyCameraEase();
            }

            // -------- Audio cues --------
            if (_doc.audio != null)
            {
                while (_audioIdx < _doc.audio.Length && _doc.audio[_audioIdx].t <= ElapsedMs)
                {
                    var a = _doc.audio[_audioIdx++];
                    HandleAudio(a);
                }
            }

            // -------- Captions --------
            if (_doc.captions != null)
            {
                while (_captionIdx < _doc.captions.Length && _doc.captions[_captionIdx].t <= ElapsedMs)
                {
                    var c = _doc.captions[_captionIdx++];
                    CinematicCaptionGump.ShowCaption(world, c.text ?? "", c.dur > 0 ? c.dur : 3000);
                }
            }

            // -------- World hooks --------
            if (_doc.world != null)
            {
                while (_worldIdx < _doc.world.Length && _doc.world[_worldIdx].t <= ElapsedMs)
                {
                    var h = _doc.world[_worldIdx++];
                    HandleWorldHook(h);
                }
            }

            // -------- End of replay --------
            int totalDur = TotalDurationMs();
            if (ElapsedMs >= totalDur)
            {
                if (Loop)
                {
                    // Reset cursors and re-teleport.
                    _startTicks = Time.Ticks;
                    ElapsedMs = 0;
                    LastSampleIdx = -1;
                    _camIdx = _audioIdx = _captionIdx = _worldIdx = 0;
                    _camFrom = _camTo = null;
                    if (HasSamples) TeleportToFirstSample(world);
                }
                else
                {
                    Stop();
                }
            }
        }

        private static int TotalDurationMs()
        {
            int t = 0;
            if (_doc.samples != null && _doc.samples.Length > 0)
                t = Math.Max(t, _doc.samples[_doc.samples.Length - 1].t);
            if (_doc.cameraKeys != null)
                foreach (var k in _doc.cameraKeys)
                    t = Math.Max(t, k.t + Math.Max(0, k.lerp));
            if (_doc.audio != null)
                foreach (var a in _doc.audio) t = Math.Max(t, a.t);
            if (_doc.captions != null)
                foreach (var c in _doc.captions) t = Math.Max(t, c.t + c.dur);
            if (_doc.world != null)
                foreach (var w in _doc.world) t = Math.Max(t, w.t);
            return t;
        }

        private static void ApplyCameraModeIfChanged(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            if (Enum.TryParse<CameraModeController.Mode>(mode, true, out var m))
            {
                if (CameraModeController.CurrentMode != m)
                {
                    CameraModeController.SetMode(m);
                }
            }
        }

        private static void ApplyCameraEase()
        {
            if (_camTo == null) return;
            var cam = World3DRenderer.Camera;
            // If snap (lerp == 0) or no _camFrom, just write _camTo and stop.
            if (_camFrom == null || _camTo.lerp <= 0)
            {
                if (_camTo.yaw   .HasValue) cam.YawDegrees   = _camTo.yaw.Value;
                if (_camTo.pitch .HasValue) cam.PitchDegrees = _camTo.pitch.Value;
                if (_camTo.dist  .HasValue) cam.EyeDistance  = _camTo.dist.Value;
                if (_camTo.fov   .HasValue) cam.FovDegrees   = _camTo.fov.Value;
                return;
            }
            float t = (ElapsedMs - _camFromMs) / (float)_camTo.lerp;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            // Smoothstep for nicer easing.
            t = t * t * (3f - 2f * t);

            if (_camTo.yaw.HasValue)
            {
                float a = _camFrom.yaw ?? cam.YawDegrees;
                float b = _camTo.yaw.Value;
                // Shortest arc.
                float diff = b - a;
                while (diff > 180f) diff -= 360f;
                while (diff < -180f) diff += 360f;
                cam.YawDegrees = a + diff * t;
            }
            if (_camTo.pitch.HasValue)
            {
                float a = _camFrom.pitch ?? cam.PitchDegrees;
                cam.PitchDegrees = a + (_camTo.pitch.Value - a) * t;
            }
            if (_camTo.dist.HasValue)
            {
                float a = _camFrom.dist ?? cam.EyeDistance;
                cam.EyeDistance = a + (_camTo.dist.Value - a) * t;
            }
            if (_camTo.fov.HasValue)
            {
                float a = _camFrom.fov ?? cam.FovDegrees;
                cam.FovDegrees = a + (_camTo.fov.Value - a) * t;
            }
        }

        private static void HandleAudio(ReplayAudioCue a)
        {
            if (a == null || string.IsNullOrEmpty(a.kind)) return;
            try
            {
                if (string.Equals(a.kind, "sound", StringComparison.OrdinalIgnoreCase))
                {
                    Client.Game.Audio?.PlaySound(a.id);
                }
                else if (string.Equals(a.kind, "music", StringComparison.OrdinalIgnoreCase))
                {
                    Client.Game.Audio?.PlayMusic(a.id);
                }
                // TODO: add "wav"/"voice" kind once we wire SoundEffect.FromStream
                // for arbitrary OGG/WAV files in the prototype audio path.
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PathReplay] audio cue failed: " + ex.Message);
            }
        }

        private static void HandleWorldHook(ReplayWorldHook h)
        {
            if (h == null || string.IsNullOrEmpty(h.kind)) return;
            string v = h.value ?? "";
            try
            {
                switch (h.kind.ToLowerInvariant())
                {
                    case "season":
                        if (string.Equals(v, "off", StringComparison.OrdinalIgnoreCase))
                        {
                            SeasonCycleDriver.Stop();
                        }
                        else if (string.Equals(v, "showcase", StringComparison.OrdinalIgnoreCase))
                        {
                            SeasonCycleDriver.Enabled = true;
                            SeasonCycleDriver.ShowcaseMode = true;
                        }
                        else if (string.Equals(v, "natural", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(v, "on", StringComparison.OrdinalIgnoreCase))
                        {
                            SeasonCycleDriver.Enabled = true;
                            SeasonCycleDriver.ShowcaseMode = false;
                        }
                        break;
                    case "secondsperyear":
                        if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture, out var spy))
                        {
                            SeasonCycleDriver.SecondsPerYear = spy;
                        }
                        break;
                    case "weather":
                        if (Enum.TryParse<Weather3DType>(v, true, out var wt))
                        {
                            Weather3DSystem.SetType(wt);
                        }
                        break;
                    case "lightning":
                        Weather3DSystem.TriggerLightning();
                        World3DRenderer.LightningFlashIntensity = 0.85f;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PathReplay] world hook failed: " + ex.Message);
            }
        }
    }
}
