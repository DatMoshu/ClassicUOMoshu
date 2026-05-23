// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — record local player position/facing samples to JSON.
// Companion to PathReplayManager. Static + global by design (prototype).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class PathRecorderManager
    {
        public const int SampleIntervalMs = 100;

        public static bool IsRecording { get; private set; }
        public static string CurrentName { get; private set; } = "untitled";
        public static int SampleCount => _samples.Count;
        public static long ElapsedMs { get; private set; }

        private static readonly List<ReplaySample> _samples = new();
        private static long _startTicks;
        private static long _lastSampleTicks;

        public static string ReplaysDir
        {
            get
            {
                string dir = Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Replays");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string PathForName(string name)
        {
            // Sanitize a little — no path separators.
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            if (string.IsNullOrWhiteSpace(name)) name = "untitled";
            return Path.Combine(ReplaysDir, name + ".json");
        }

        public static void StartRecording(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "untitled";
            CurrentName = name;
            _samples.Clear();
            _startTicks = Time.Ticks;
            _lastSampleTicks = -1;
            ElapsedMs = 0;
            IsRecording = true;
            Console.WriteLine($"[PathRecorder] start name='{name}'");
        }

        public static string StopAndSave()
        {
            if (!IsRecording)
            {
                return "(not recording)";
            }
            IsRecording = false;

            var doc = new ReplayDoc
            {
                name = CurrentName,
                version = 1,
                loop = false,
                samples = _samples.ToArray(),
            };
            string file = PathForName(CurrentName);
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(file, JsonSerializer.Serialize(doc, opts));
                Console.WriteLine($"[PathRecorder] saved {_samples.Count} samples → {file}");
                return file;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PathRecorder] SAVE FAILED: {ex}");
                return "(save failed: " + ex.Message + ")";
            }
        }

        public static void Cancel()
        {
            IsRecording = false;
            _samples.Clear();
        }

        public static void Tick(World world)
        {
            if (!IsRecording || world == null || world.Player == null) return;

            long now = Time.Ticks;
            ElapsedMs = (long)(now - _startTicks);

            if (_lastSampleTicks >= 0 && (now - _lastSampleTicks) < SampleIntervalMs)
            {
                return;
            }
            _lastSampleTicks = now;

            var p = world.Player;
            _samples.Add(new ReplaySample
            {
                t = (int)ElapsedMs,
                x = p.X,
                y = p.Y,
                z = p.Z,
                dir = (byte)((byte)p.Direction & 0x07),
                running = ((byte)p.Direction & 0x80) != 0,
            });
        }
    }

    // Shared serialization shapes — used by both recorder and replay.
    internal sealed class ReplayDoc
    {
        public string name { get; set; } = "untitled";
        public int version { get; set; } = 1;
        public bool loop { get; set; }
        public ReplaySample[] samples { get; set; } = Array.Empty<ReplaySample>();
        public ReplayCameraKey[] cameraKeys { get; set; }
        public ReplayAudioCue[] audio { get; set; }
        public ReplayCaption[] captions { get; set; }
        public ReplayWorldHook[] world { get; set; }
    }

    internal sealed class ReplaySample
    {
        public int t { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }
        public byte dir { get; set; }
        public bool running { get; set; }
    }

    internal sealed class ReplayCameraKey
    {
        public int t { get; set; }
        public string mode { get; set; }     // "Off" | "ThirdPerson" | "FirstPerson" | "Cinematic" | "FreeFly"
        public float? yaw { get; set; }
        public float? pitch { get; set; }
        public float? dist { get; set; }     // EyeDistance
        public float? fov { get; set; }
        public int lerp { get; set; }        // ms to ease from previous key (0 = snap)
    }

    internal sealed class ReplayAudioCue
    {
        public int t { get; set; }
        public string kind { get; set; }     // "sound" | "music"
        public int id { get; set; }
    }

    internal sealed class ReplayCaption
    {
        public int t { get; set; }
        public int dur { get; set; }
        public string text { get; set; }
    }

    internal sealed class ReplayWorldHook
    {
        public int t { get; set; }
        public string kind { get; set; }     // "season" | "weather" | "lightning" | "secondsPerYear"
        public string value { get; set; }    // string form for enum-ish values; numbers parsed lazily
    }
}
