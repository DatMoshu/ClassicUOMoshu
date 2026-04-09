// SPDX-License-Identifier: BSD-2-Clause

// PP.A2 — Profile package writer
// --------------------------------
// Bundles an existing profile folder into a .uowprofile ZIP archive.
// Caller is responsible for flushing profile saves before calling Write()
// (Profile.Save() and MacroManager.Save() must be called first).

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal static class ProfilePackageWriter
    {
        /// <summary>
        /// Writes a .uowprofile ZIP to <paramref name="outputPath"/>.
        /// </summary>
        /// <param name="profileFolderPath">
        ///   Path to the flushed character profile folder
        ///   (Data/Profiles/{user}/{server}/{char}/).
        /// </param>
        /// <param name="profile">Active profile — used for manifest metadata.</param>
        /// <param name="categories">Categories to include.</param>
        /// <param name="outputPath">Destination .uowprofile file path.</param>
        /// <param name="metadata">Author-supplied name, description, tags, etc.</param>
        /// <returns>True on success.</returns>
        public static bool Write(
            string profileFolderPath,
            Profile profile,
            ProfileCategory categories,
            string outputPath,
            ProfileExportMetadata metadata)
        {
            try
            {
                ProfileManifest manifest = ProfileManifest.Build(profile, categories, metadata);

                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                // FileMode.Create truncates or creates — no explicit delete needed.
                using FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);

                WriteManifest(zip, manifest);

                // profile.json — always included; covers all non-file categories.
                AddFileEntry(zip, profileFolderPath, ProfilePackageFiles.Profile, required: true);

                // gumps.xml — only when GumpLayout is selected.
                if (categories.HasFlag(ProfileCategory.GumpLayout))
                    AddFileEntry(zip, profileFolderPath, ProfilePackageFiles.Gumps, required: false);

                // macros.xml — only when MacroBindings is selected.
                if (categories.HasFlag(ProfileCategory.MacroBindings))
                    AddFileEntry(zip, profileFolderPath, ProfilePackageFiles.Macros, required: false);

                // skillsgroups.xml — always included if present.
                AddFileEntry(zip, profileFolderPath, ProfilePackageFiles.SkillGroups, required: false);

                Log.Trace($"[ProfilePackageWriter] Written: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ProfilePackageWriter] Failed to write {outputPath}: {ex.Message}");
                return false;
            }
        }

        private static void WriteManifest(ZipArchive zip, ProfileManifest manifest)
        {
            ZipArchiveEntry entry = zip.CreateEntry(ProfilePackageFiles.Manifest, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            JsonSerializer.Serialize(stream, manifest, ProfileJsonContext.DefaultToUse.ProfileManifest);
        }

        private static void AddFileEntry(ZipArchive zip, string folder, string filename, bool required)
        {
            string sourcePath = Path.Combine(folder, filename);

            if (!File.Exists(sourcePath))
            {
                if (required)
                    throw new FileNotFoundException($"Required profile file not found: {filename}", sourcePath);

                Log.Trace($"[ProfilePackageWriter] Optional file absent, skipping: {filename}");
                return;
            }

            zip.CreateEntryFromFile(sourcePath, filename, CompressionLevel.Optimal);
        }
    }
}
