// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Text;
using ClassicUO.Configuration;

namespace ClassicUO.SpeechRecognition.Diagnostics
{
    /// <summary>Log verbosity levels, ordered from most to least verbose.</summary>
    public enum SpeechLogLevel
    {
        /// <summary>All output including per-frame data (RMS, partial JSON).</summary>
        Trace = 0,
        /// <summary>Development diagnostics (routing decisions, config dumps).</summary>
        Debug = 1,
        /// <summary>Noteworthy runtime events (model loaded, LLM call complete).</summary>
        Info = 2,
        /// <summary>Recoverable problems (timeout, fallback triggered).</summary>
        Warn = 3,
        /// <summary>Failures that break functionality.</summary>
        Error = 4,
        /// <summary>No output.</summary>
        Off = 5
    }

    /// <summary>
    /// Subsystems that can be independently tuned.
    /// Each maps to a settings key like <c>speech_log_level_llm</c>.
    /// </summary>
    public enum SpeechLogChannel
    {
        Voice,
        Audio,
        Stt,
        Llm,
        Inference,
        Route,
        Avatar,
        Tts
    }

    /// <summary>
    /// Per-subsystem, level-gated logger for the speech recognition stack.
    ///
    /// Usage:
    ///   <c>SpeechLog.Info(SpeechLogChannel.Llm, $"POST {uri} model={model}");</c>
    ///   <c>SpeechLog.Trace(SpeechLogChannel.Audio, $"RMS={rms:F0}");</c>
    ///
    /// Each channel has an independent log level read from <see cref="Settings"/>.
    /// Output goes to console and (optionally) a flat file when <c>speech_log_file</c> is set.
    /// </summary>
    public static class SpeechLog
    {
        private static readonly string[] ChannelTags =
        {
            "[Voice]",
            "[Audio]",
            "[STT]",
            "[LLM]",
            "[Inference]",
            "[Route]",
            "[Avatar]",
            "[TTS]"
        };

        private static readonly string[] LevelTags =
        {
            "TRC",
            "DBG",
            "INF",
            "WRN",
            "ERR",
            ""
        };

        private static readonly ConsoleColor[] LevelColors =
        {
            ConsoleColor.DarkGray,   // Trace
            ConsoleColor.Gray,       // Debug
            ConsoleColor.Cyan,       // Info
            ConsoleColor.Yellow,     // Warn
            ConsoleColor.Red,        // Error
            ConsoleColor.White       // Off (unused)
        };

        private static StreamWriter _fileWriter;
        private static string _activeLogPath;
        private static readonly object _fileLock = new object();

        // ── Convenience methods ─────────────────────────────────────────────

        public static void Trace(SpeechLogChannel ch, string msg) => Write(ch, SpeechLogLevel.Trace, msg);
        public static void Debug(SpeechLogChannel ch, string msg) => Write(ch, SpeechLogLevel.Debug, msg);
        public static void Info(SpeechLogChannel ch, string msg)  => Write(ch, SpeechLogLevel.Info, msg);
        public static void Warn(SpeechLogChannel ch, string msg)  => Write(ch, SpeechLogLevel.Warn, msg);
        public static void Error(SpeechLogChannel ch, string msg) => Write(ch, SpeechLogLevel.Error, msg);

        // ── Core write ──────────────────────────────────────────────────────

        public static void Write(SpeechLogChannel channel, SpeechLogLevel level, string message)
        {
            if (level == SpeechLogLevel.Off) return;

            SpeechLogLevel threshold = GetChannelLevel(channel);
            if (level < threshold) return;

            string channelTag = ChannelTags[(int)channel];
            string levelTag = LevelTags[(int)level];
            string line = $"{channelTag} {levelTag} {message}";

            // Console output
            WriteConsole(level, line);

            // File output
            WriteFile(line);
        }

        // ── File lifecycle ──────────────────────────────────────────────────

        /// <summary>
        /// Called once during initialization (e.g. from VoiceInteractionManager.Initialize).
        /// Opens the log file if <c>speech_log_file</c> is set in settings.
        /// </summary>
        public static void Initialize()
        {
            string path = Settings.GlobalSettings.SpeechLogFile;
            Console.WriteLine($"[SpeechLog] Initialize: SpeechLogFile='{path}', SpeechLogLevelDefault={Settings.GlobalSettings.SpeechLogLevelDefault}");

            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("[SpeechLog] No log file path configured — file logging disabled.");
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                lock (_fileLock)
                {
                    _fileWriter?.Dispose();
                    _fileWriter = new StreamWriter(path, append: true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                    _activeLogPath = path;
                }

                Console.WriteLine($"[SpeechLog] File opened OK: {path}");
                Info(SpeechLogChannel.Voice, $"Log file opened: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpeechLog] ERR Failed to open log file '{path}': {ex.Message}");
            }
        }

        /// <summary>Flush and close the log file. Safe to call multiple times.</summary>
        public static void Shutdown()
        {
            lock (_fileLock)
            {
                _fileWriter?.Dispose();
                _fileWriter = null;
                _activeLogPath = null;
            }
        }

        // ── Private helpers ─────────────────────────────────────────────────

        private static SpeechLogLevel GetChannelLevel(SpeechLogChannel channel)
        {
            var s = Settings.GlobalSettings;

            // Per-channel override, falling back to the global default
            return channel switch
            {
                SpeechLogChannel.Voice     => s.SpeechLogLevelVoice     ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Audio     => s.SpeechLogLevelAudio     ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Stt       => s.SpeechLogLevelStt       ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Llm       => s.SpeechLogLevelLlm       ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Inference => s.SpeechLogLevelInference ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Route     => s.SpeechLogLevelRoute     ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Avatar    => s.SpeechLogLevelAvatar    ?? s.SpeechLogLevelDefault,
                SpeechLogChannel.Tts       => s.SpeechLogLevelTts       ?? s.SpeechLogLevelDefault,
                _ => s.SpeechLogLevelDefault
            };
        }

        private static void WriteConsole(SpeechLogLevel level, string line)
        {
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = LevelColors[(int)level];
            Console.WriteLine(line);
            Console.ForegroundColor = prev;
        }

        private static void WriteFile(string line)
        {
            lock (_fileLock)
            {
                if (_fileWriter == null) return;

                try
                {
                    _fileWriter.Write(DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                    _fileWriter.Write(' ');
                    _fileWriter.WriteLine(line);
                }
                catch
                {
                    // Don't let file I/O errors crash the game
                }
            }
        }
    }
}
