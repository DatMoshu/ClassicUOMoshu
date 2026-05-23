// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — shared audio-directory resolver used by FootstepAudioSystem
// and WeatherAudioSystem.
//
// Resolution order (first candidate that exists wins):
//   1. UOWW_AUDIO_DIR environment variable
//   2. {AppContext.BaseDirectory}/Assets/Audio  (deploy layout)
//   3. D:\_Assets\Audio  (dev-machine fallback)
//
// A single warning is logged the first time none of the candidates exist,
// rather than spamming one warning per clip load.

using System;
using System.IO;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class AudioPathResolver
    {
        // The resolved root; null if no candidate exists on disk.
        private static string _resolved;
        private static bool   _resolved_attempted;
        private static bool   _missingWarned;

        public static string AudioRoot
        {
            get
            {
                if (!_resolved_attempted) Resolve();
                return _resolved;
            }
        }

        // Returns the full path for a relative audio path (using either '/'
        // or '\' separators). Returns null if the audio root doesn't exist.
        public static string GetFullPath(string rel)
        {
            string root = AudioRoot;
            if (root == null) return null;
            return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void Resolve()
        {
            _resolved_attempted = true;

            // Candidate 1: env var override.
            string envDir = Environment.GetEnvironmentVariable("UOWW_AUDIO_DIR");
            if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
            {
                _resolved = envDir;
                return;
            }

            // Candidate 2: deploy layout next to the executable.
            string deployDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
            if (Directory.Exists(deployDir))
            {
                _resolved = deployDir;
                return;
            }

            // Candidate 3: dev-machine absolute fallback.
            const string devDir = @"D:\_Assets\Audio";
            if (Directory.Exists(devDir))
            {
                _resolved = devDir;
                return;
            }

            // None found — warn once and leave _resolved null so callers skip
            // audio gracefully rather than throwing.
            if (!_missingWarned)
            {
                _missingWarned = true;
                Console.WriteLine(
                    "[AudioPathResolver] WARNING: no audio directory found. " +
                    $"Checked: UOWW_AUDIO_DIR env, '{deployDir}', '{devDir}'. " +
                    "Set UOWW_AUDIO_DIR or place audio under Assets/Audio/ next to the exe.");
            }
            _resolved = null;
        }
    }
}
