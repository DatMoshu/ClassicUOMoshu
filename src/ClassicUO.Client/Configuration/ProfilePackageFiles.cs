// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Configuration
{
    /// <summary>
    /// Entry names used inside a .uowprofile ZIP archive.
    /// Single source of truth for both <see cref="ProfilePackageWriter"/> and
    /// <see cref="ProfilePackageReader"/>.
    /// </summary>
    internal static class ProfilePackageFiles
    {
        public const string Manifest    = "manifest.json";
        public const string Profile     = "profile.json";
        public const string Gumps       = "gumps.xml";
        public const string Macros      = "macros.xml";
        public const string SkillGroups = "skillsgroups.xml";
    }
}
