// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — dedicated file logger for renderer diagnostics.
// Writes to dumps/renderer.log next to the existing dump pngs. Console output
// remains for high-signal events (errors, load completions); per-frame and
// per-second diagnostics route here so they don't drown the console.
//
// The file is opened on first write and stays open for the process lifetime.
// We append (no rotation) — rebuild rerolls automatically on each session
// because the renderer-state-dump command writes a separator at start.

using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class RendererLog
    {
        public static bool Enabled = true;

        // Dumps land alongside the existing screenshot/state dumps.
        public static string FilePath { get; } =
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "dumps", "renderer.log");

        private static readonly object _gate = new();
        private static StreamWriter _writer;

        private static StreamWriter Writer
        {
            get
            {
                if (_writer != null) return _writer;
                lock (_gate)
                {
                    if (_writer != null) return _writer;
                    try
                    {
                        var full = Path.GetFullPath(FilePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(full));
                        _writer = new StreamWriter(full, append: true, Encoding.UTF8) { AutoFlush = true };
                        _writer.WriteLine();
                        _writer.WriteLine($"=== renderer.log opened {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[3DCUO] RendererLog open failed: {ex.Message}");
                        _writer = StreamWriter.Null;
                    }
                }
                return _writer;
            }
        }

        public static void Write(string tag, string message)
        {
            if (!Enabled) return;
            try
            {
                Writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{tag}] {message}");
            }
            catch { /* never let logging crash the renderer */ }
        }

        public static void DumpMatrix(string tag, string label, in Matrix m)
        {
            if (!Enabled) return;
            try
            {
                Writer.WriteLine(
                    $"{DateTime.Now:HH:mm:ss.fff} [{tag}] {label}\n" +
                    $"    [{m.M11,8:F3} {m.M12,8:F3} {m.M13,8:F3} {m.M14,8:F3}]\n" +
                    $"    [{m.M21,8:F3} {m.M22,8:F3} {m.M23,8:F3} {m.M24,8:F3}]\n" +
                    $"    [{m.M31,8:F3} {m.M32,8:F3} {m.M33,8:F3} {m.M34,8:F3}]\n" +
                    $"    [{m.M41,8:F3} {m.M42,8:F3} {m.M43,8:F3} {m.M44,8:F3}]");
            }
            catch { }
        }

        public static void Flush()
        {
            try { _writer?.Flush(); } catch { }
        }
    }
}
