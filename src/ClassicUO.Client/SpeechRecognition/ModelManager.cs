// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Configuration;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Resolves model file paths and provides async download for optional models.
    /// All models live under &lt;exe&gt;/Models/Voice/.
    /// </summary>
    internal static class ModelManager
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        private static string ModelsRoot =>
            Path.Combine(CUOEnviroment.ExecutablePath, "Models", "Voice");

        // ── Path resolvers ────────────────────────────────────────────────────

        public static string VoskModelPath =>
            Settings.GlobalSettings.VoskModelDirectory;

        public static string WhisperModelPath =>
            ResolvePath(Settings.GlobalSettings.WhisperModelPath, "whisper-base.bin");

        public static string SileroVadPath =>
            ResolvePath(Settings.GlobalSettings.VadModelPath, "silero-vad.onnx");

        public static string KokoroModelPath =>
            ResolvePath(Settings.GlobalSettings.TtsModelPath, "kokoro-tts.onnx");

        public static string KokoroVoicePath(string voiceId) =>
            Path.Combine(ModelsRoot, "kokoro-voices", $"{voiceId}.bin");

        // ── Existence checks ──────────────────────────────────────────────────

        public static bool VoskExists => Directory.Exists(VoskModelPath);
        public static bool WhisperExists => File.Exists(WhisperModelPath);
        public static bool SileroVadExists => File.Exists(SileroVadPath);
        public static bool KokoroExists => File.Exists(KokoroModelPath);

        // ── Download helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Download a Whisper GGML model from HuggingFace.
        /// type: "tiny", "base", "small", "medium", "large-v3", "large-v3-turbo"
        /// </summary>
        public static async Task DownloadWhisperModelAsync(
            string type = "base",
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            string url = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{type}.bin";
            string dest = Path.Combine(ModelsRoot, $"whisper-{type}.bin");
            await DownloadFileAsync(url, dest, progress, ct);
        }

        /// <summary>
        /// Download the Silero VAD ONNX model from the official GitHub release.
        /// </summary>
        public static async Task DownloadSileroVadAsync(
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            const string url = "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx";
            await DownloadFileAsync(url, SileroVadPath, progress, ct);
        }

        private static async Task DownloadFileAsync(
            string url,
            string destination,
            IProgress<float> progress,
            CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;
            long downloaded = 0;

            using var src = await response.Content.ReadAsStreamAsync(ct);
            using var dst = File.Create(destination);

            var buffer = new byte[65536];
            int read;
            while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await dst.WriteAsync(buffer, 0, read, ct);
                downloaded += read;
                if (total > 0)
                    progress?.Report((float)downloaded / total);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string ResolvePath(string configuredPath, string defaultFilename)
        {
            if (!string.IsNullOrEmpty(configuredPath) && Path.IsPathRooted(configuredPath))
                return configuredPath;

            if (!string.IsNullOrEmpty(configuredPath))
                return Path.Combine(CUOEnviroment.ExecutablePath, configuredPath);

            return Path.Combine(ModelsRoot, defaultFilename);
        }
    }
}
