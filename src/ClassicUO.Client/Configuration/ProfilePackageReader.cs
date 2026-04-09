// SPDX-License-Identifier: BSD-2-Clause

// PP.A2 — Profile package reader
// --------------------------------
// Validates and extracts a .uowprofile ZIP to a temporary directory.
// Caller owns cleanup:  ProfilePackageReader.CleanupTempDirectory(result.TempDirectory)
//
// Validation rules (design/gdd/player-profile.md — Client-Side Import Validation):
//   1. File size      ≤ MAX_FILE_BYTES (1 MB)
//   2. ZIP integrity  valid ZIP header, no nested ZIPs
//   3. Path traversal no entry name containing ".." — reject entire package
//   4. manifest.json  present in root
//   5. Manifest schema all required fields present
//   6. Client version client_version_min ≤ current client version (warn, don't block)

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal sealed class ProfilePackageReadResult
    {
        public bool IsValid   { get; init; }
        public string Error   { get; init; } = string.Empty;

        /// <summary>Populated when <see cref="IsValid"/> is true.</summary>
        public ProfileManifest Manifest { get; init; }

        /// <summary>
        /// Temp directory containing extracted files.
        /// Empty string when <see cref="IsValid"/> is false.
        /// Always call <see cref="ProfilePackageReader.CleanupTempDirectory"/> when done.
        /// </summary>
        public string TempDirectory { get; init; } = string.Empty;

        /// <summary>True when the package resolution differs from the current screen.</summary>
        public bool ResolutionMismatch { get; init; }

        public static ProfilePackageReadResult Fail(string error)
            => new ProfilePackageReadResult { IsValid = false, Error = error };
    }

    internal static class ProfilePackageReader
    {
        public const long MAX_FILE_BYTES = 1 * 1024 * 1024; // 1 MB

        private const string CurrentClientVersion = "0.1.0";

        /// <summary>
        /// Validates and extracts a .uowprofile file.
        /// On success, <see cref="ProfilePackageReadResult.TempDirectory"/> contains the extracted files.
        /// </summary>
        public static ProfilePackageReadResult Read(string packagePath, Profile activeProfile)
        {
            // ── 1. File size ──────────────────────────────────────────────────────
            long fileSize;
            try { fileSize = new FileInfo(packagePath).Length; }
            catch (Exception ex) { return ProfilePackageReadResult.Fail($"Cannot read package: {ex.Message}"); }

            if (fileSize > MAX_FILE_BYTES)
                return ProfilePackageReadResult.Fail($"Profile too large ({fileSize / 1024} KB). Maximum is {MAX_FILE_BYTES / 1024} KB.");

            // ── 2. ZIP open ───────────────────────────────────────────────────────
            ZipArchive zip;
            try { zip = ZipFile.OpenRead(packagePath); }
            catch (Exception ex) { return ProfilePackageReadResult.Fail($"Corrupt profile file: {ex.Message}"); }

            using (zip)
            {
                var entries = zip.Entries; // cache — avoid re-enumerating the ZIP directory

                // ── 3. Path traversal + nested ZIP check ──────────────────────────
                foreach (ZipArchiveEntry entry in entries)
                {
                    if (entry.FullName.Contains(".."))
                    {
                        Log.Warn($"[ProfilePackageReader] Path traversal attempt: {entry.FullName}");
                        return ProfilePackageReadResult.Fail("Invalid profile file: security violation detected.");
                    }

                    string ext = Path.GetExtension(entry.FullName);
                    if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".uowprofile", StringComparison.OrdinalIgnoreCase))
                        return ProfilePackageReadResult.Fail("Invalid profile file: nested archives not permitted.");
                }

                // ── 4. Manifest present ───────────────────────────────────────────
                ZipArchiveEntry manifestEntry = zip.GetEntry(ProfilePackageFiles.Manifest);
                if (manifestEntry == null)
                    return ProfilePackageReadResult.Fail("Invalid profile format: missing manifest.json.");

                // ── 5. Manifest schema ────────────────────────────────────────────
                ProfileManifest manifest;
                try
                {
                    using Stream ms = manifestEntry.Open();
                    manifest = JsonSerializer.Deserialize(ms, ProfileJsonContext.DefaultToUse.ProfileManifest);
                }
                catch (Exception ex)
                {
                    return ProfilePackageReadResult.Fail($"Invalid profile format: manifest parse error — {ex.Message}");
                }

                if (manifest == null)
                    return ProfilePackageReadResult.Fail("Invalid profile format: empty manifest.");
                if (string.IsNullOrWhiteSpace(manifest.ProfileName))
                    return ProfilePackageReadResult.Fail("Invalid profile format: manifest missing profile_name.");
                if (manifest.Includes == null)
                    return ProfilePackageReadResult.Fail("Invalid profile format: manifest missing includes.");

                // ── 6. Client version ─────────────────────────────────────────────
                bool versionWarning = !string.IsNullOrEmpty(manifest.ClientVersionMin)
                    && !IsVersionCompatible(CurrentClientVersion, manifest.ClientVersionMin);

                if (versionWarning)
                    Log.Warn($"[ProfilePackageReader] Profile requires client {manifest.ClientVersionMin}, current is {CurrentClientVersion}.");

                // ── Extract ───────────────────────────────────────────────────────
                string tempDir = Path.Combine(Path.GetTempPath(), $"uowprofile_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    foreach (ZipArchiveEntry entry in entries)
                    {
                        string entryName = Path.GetFileName(entry.FullName);
                        if (string.IsNullOrEmpty(entryName)) continue;

                        entry.ExtractToFile(Path.Combine(tempDir, entryName), overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    CleanupTempDirectory(tempDir);
                    return ProfilePackageReadResult.Fail($"Failed to extract profile: {ex.Message}");
                }

                // ── Resolution mismatch ───────────────────────────────────────────
                bool resMismatch = activeProfile != null
                    && !string.IsNullOrEmpty(manifest.ResolutionCreated)
                    && !string.Equals(
                        manifest.ResolutionCreated,
                        $"{activeProfile.GameWindowSize.X}x{activeProfile.GameWindowSize.Y}",
                        StringComparison.OrdinalIgnoreCase);

                Log.Trace($"[ProfilePackageReader] Extracted '{manifest.ProfileName}' to {tempDir}");

                return new ProfilePackageReadResult
                {
                    IsValid           = true,
                    Manifest          = manifest,
                    TempDirectory     = tempDir,
                    ResolutionMismatch = resMismatch,
                    Error             = versionWarning
                        ? $"Profile requires client {manifest.ClientVersionMin} — some settings may not apply."
                        : string.Empty
                };
            }
        }

        public static void CleanupTempDirectory(string tempDir)
        {
            if (string.IsNullOrEmpty(tempDir) || !Directory.Exists(tempDir)) return;
            try { Directory.Delete(tempDir, recursive: true); }
            catch (Exception ex) { Log.Warn($"[ProfilePackageReader] Temp cleanup failed for {tempDir}: {ex.Message}"); }
        }

        private static bool IsVersionCompatible(string current, string minimum)
        {
            if (!Version.TryParse(current, out Version cur)) return true;
            if (!Version.TryParse(minimum, out Version min)) return true;
            return cur >= min;
        }
    }
}
