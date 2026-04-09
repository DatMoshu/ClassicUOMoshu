// SPDX-License-Identifier: BSD-2-Clause

// PP.A7 — Profile import orchestrator
// -------------------------------------
// Central pipeline for importing a .uowprofile package into the active profile.
//
// Pipeline steps:
//   1. Validate + extract the package (ProfilePackageReader)
//   2. Backup the existing profile folder
//   3. If GumpLayout + resolution mismatch → scale gumps.xml positions
//   4. Copy category files from temp dir to profile folder
//   5. Apply non-file category settings (ProfileCategoryApplier)
//   6. Sanitise macros.xml if MacroBindings was imported (ProfileMacroSanitiser)
//   7. Reload macros in the live engine (MacroManager.Load)
//   8. Live-reload gumps from the new gumps.xml (close CanBeSaved → ReadGumps → re-add)
//   9. Cleanup temp dir

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal sealed class ProfileImportResult
    {
        public bool Success          { get; init; }
        public string Error          { get; init; } = string.Empty;
        public string Warning        { get; init; } = string.Empty;
        public ProfileManifest Manifest { get; init; }
        public int MacroActionsModified { get; init; }

        public static ProfileImportResult Fail(string error)
            => new ProfileImportResult { Success = false, Error = error };
    }

    internal static class ProfileImportOrchestrator
    {
        /// <summary>
        /// Imports a .uowprofile package into <paramref name="activeProfile"/>, overwriting
        /// the files in <paramref name="profileFolderPath"/> for selected categories.
        /// </summary>
        /// <param name="packagePath">Path to the .uowprofile file chosen by the user.</param>
        /// <param name="profileFolderPath">Active profile folder (Data/Profiles/user/server/char/).</param>
        /// <param name="activeProfile">The currently loaded Profile instance to update.</param>
        /// <param name="categories">Which categories to apply from the package.</param>
        /// <param name="world">Active World — needed to reload macros and gumps.</param>
        public static ProfileImportResult Import(
            string packagePath,
            string profileFolderPath,
            Profile activeProfile,
            ProfileCategory categories,
            World world)
        {
            // ── 1. Validate + extract ─────────────────────────────────────────────
            ProfilePackageReadResult readResult = ProfilePackageReader.Read(packagePath, activeProfile);

            if (!readResult.IsValid)
                return ProfileImportResult.Fail(readResult.Error);

            string tempDir = readResult.TempDirectory;

            try
            {
                // ── 2. Backup existing profile folder ─────────────────────────────
                string backupDir = profileFolderPath + $"_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                BackupProfileFolder(profileFolderPath, backupDir);

                // ── 3. Scale gumps.xml if resolution differs ──────────────────────
                if (categories.HasFlag(ProfileCategory.GumpLayout) && readResult.ResolutionMismatch)
                {
                    string tempGumps = Path.Combine(tempDir, ProfilePackageFiles.Gumps);
                    if (File.Exists(tempGumps))
                    {
                        ParseResolution(readResult.Manifest.ResolutionCreated, out int origW, out int origH);
                        int curW = activeProfile.GameWindowSize.X;
                        int curH = activeProfile.GameWindowSize.Y;

                        if (origW > 0 && origH > 0 && curW > 0 && curH > 0)
                            ProfileResolutionScaler.Scale(tempGumps, origW, origH, curW, curH);
                    }
                }

                // ── 4. Copy category files to live profile folder ─────────────────
                CopyCategoryFiles(tempDir, profileFolderPath, readResult.Manifest, categories);

                // ── 5. Apply non-file settings from the imported profile.json ─────
                string importedProfilePath = Path.Combine(tempDir, ProfilePackageFiles.Profile);
                if (File.Exists(importedProfilePath))
                {
                    Profile importedProfile = ConfigurationResolver.Load<Profile>(
                        importedProfilePath, ProfileJsonContext.DefaultToUse.Profile);

                    if (importedProfile != null)
                        ProfileCategoryApplier.Apply(importedProfile, activeProfile, categories);
                }

                // ── 6. Sanitise macros if MacroBindings imported ──────────────────
                int macrosModified = 0;
                if (categories.HasFlag(ProfileCategory.MacroBindings))
                {
                    string macrosPath = Path.Combine(profileFolderPath, ProfilePackageFiles.Macros);
                    macrosModified = ProfileMacroSanitiser.Sanitise(macrosPath);
                }

                // ── 7. Reload macros ──────────────────────────────────────────────
                if (categories.HasFlag(ProfileCategory.MacroBindings))
                    world.Macros.Load();

                // ── 8. Live-reload gumps from new gumps.xml ───────────────────────
                if (categories.HasFlag(ProfileCategory.GumpLayout))
                    ReloadGumpsLive(world);

                Log.Trace($"[ProfileImportOrchestrator] Import complete: '{readResult.Manifest.ProfileName}'");

                return new ProfileImportResult
                {
                    Success              = true,
                    Warning              = readResult.Error, // version warning forwarded
                    Manifest             = readResult.Manifest,
                    MacroActionsModified = macrosModified,
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[ProfileImportOrchestrator] Import failed: {ex.Message}");
                return ProfileImportResult.Fail($"Import failed: {ex.Message}");
            }
            finally
            {
                ProfilePackageReader.CleanupTempDirectory(tempDir);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void CopyCategoryFiles(
            string tempDir,
            string destDir,
            ProfileManifest manifest,
            ProfileCategory categories)
        {
            // profile.json — always copy (contains all non-file category settings)
            CopyIfPresent(tempDir, destDir, ProfilePackageFiles.Profile);

            // gumps.xml — only if GumpLayout selected and present in package
            if (categories.HasFlag(ProfileCategory.GumpLayout) && manifest.Includes.GumpLayout)
                CopyIfPresent(tempDir, destDir, ProfilePackageFiles.Gumps);

            // macros.xml — only if MacroBindings selected and present in package
            if (categories.HasFlag(ProfileCategory.MacroBindings) && manifest.Includes.MacroBindings)
                CopyIfPresent(tempDir, destDir, ProfilePackageFiles.Macros);

            // skillsgroups.xml — always copy if present
            CopyIfPresent(tempDir, destDir, ProfilePackageFiles.SkillGroups);
        }

        private static void CopyIfPresent(string srcDir, string destDir, string filename)
        {
            string src = Path.Combine(srcDir, filename);
            if (!File.Exists(src)) return;
            File.Copy(src, Path.Combine(destDir, filename), overwrite: true);
        }

        private static void BackupProfileFolder(string src, string dest)
        {
            if (!Directory.Exists(src)) return;

            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);

            Log.Trace($"[ProfileImportOrchestrator] Backup created: {dest}");
        }

        /// <summary>
        /// Closes all restorable gumps (CanBeSaved == true) then re-reads gumps.xml
        /// from the live profile path, mirroring what login does in PacketHandlers.
        /// OptionsGump (GumpType.None) is NOT closed by this method.
        /// </summary>
        private static void ReloadGumpsLive(World world)
        {
            // Snapshot to avoid modifying the collection while iterating
            var toClose = new List<Gump>();
            foreach (Gump g in UIManager.Gumps)
            {
                if (!g.IsDisposed && g.CanBeSaved)
                    toClose.Add(g);
            }
            foreach (Gump g in toClose)
                g.Dispose();

            List<Gump> restored = ProfileManager.CurrentProfile.ReadGumps(world, ProfileManager.ProfilePath);
            if (restored != null)
            {
                foreach (Gump g in restored)
                    UIManager.Add(g);
            }
        }

        private static void ParseResolution(string res, out int width, out int height)
        {
            width  = 0;
            height = 0;

            if (string.IsNullOrEmpty(res)) return;

            int x = res.IndexOf('x');
            if (x < 0) return;

            int.TryParse(res.AsSpan(0, x), out width);
            int.TryParse(res.AsSpan(x + 1), out height);
        }
    }
}
