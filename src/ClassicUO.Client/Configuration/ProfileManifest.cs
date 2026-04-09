// SPDX-License-Identifier: BSD-2-Clause

using System;

namespace ClassicUO.Configuration
{
    /// <summary>
    /// Manifest embedded as manifest.json inside every .uowprofile ZIP.
    /// Serialised with SnakeCaseNamingPolicy via <see cref="ProfileJsonContext"/>.
    /// </summary>
    internal sealed class ProfileManifest
    {
        /// <summary>Package format version. Increment only on breaking changes.</summary>
        public int Version { get; set; } = 1;

        public string ProfileName { get; set; } = string.Empty;

        /// <summary>Display name of the exporting character.</summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>Serial of the authoring PlayerMobile. 0 when unknown.</summary>
        public uint AuthorSerial { get; set; }

        /// <summary>Faction name at export time. Informational only.</summary>
        public string Faction { get; set; } = string.Empty;

        public string CreatedUtc { get; set; } = string.Empty;
        public string UpdatedUtc { get; set; } = string.Empty;

        public string[] Tags { get; set; } = Array.Empty<string>();

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Base32(SHA256(package_bytes)[0:20]) — 32 chars.
        /// Empty for local-only exports; populated during Phase 2 server registration.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        public ProfileManifestIncludes Includes { get; set; } = new ProfileManifestIncludes();

        public string ClientVersionMin { get; set; } = "0.1.0";

        /// <summary>Screen resolution at export time — used for gump scaling on import.</summary>
        public string ResolutionCreated { get; set; } = string.Empty;

        /// <summary>
        /// Builds a manifest from the active profile, export categories, and author metadata.
        /// </summary>
        public static ProfileManifest Build(
            Profile profile,
            ProfileCategory categories,
            ProfileExportMetadata metadata)
        {
            string timestamp = DateTime.UtcNow.ToString("o");

            return new ProfileManifest
            {
                ProfileName      = metadata.ProfileName,
                Author           = profile.CharacterName ?? string.Empty,
                AuthorSerial     = metadata.AuthorSerial,
                Faction          = metadata.Faction,
                CreatedUtc       = timestamp,
                UpdatedUtc       = timestamp,
                Tags             = metadata.Tags ?? Array.Empty<string>(),
                Description      = metadata.Description,
                Includes         = ProfileManifestIncludes.FromCategories(categories),
                ResolutionCreated = $"{profile.GameWindowSize.X}x{profile.GameWindowSize.Y}",
            };
        }
    }

    /// <summary>
    /// Tracks which category files are present in the .uowprofile package.
    /// Import UI uses this to grey out unavailable category checkboxes.
    /// </summary>
    internal sealed class ProfileManifestIncludes
    {
        public bool SoundSettings   { get; set; }
        public bool HueSettings     { get; set; }
        public bool GumpLayout      { get; set; }
        public bool VisualSettings  { get; set; }
        public bool CombatSettings  { get; set; }
        public bool TooltipSettings { get; set; }
        public bool MacroBindings   { get; set; }
        public bool ChatSettings    { get; set; }
        public bool WorldMap        { get; set; }

        public static ProfileManifestIncludes FromCategories(ProfileCategory cats) => new ProfileManifestIncludes
        {
            SoundSettings   = cats.HasFlag(ProfileCategory.SoundSettings),
            HueSettings     = cats.HasFlag(ProfileCategory.HueSettings),
            GumpLayout      = cats.HasFlag(ProfileCategory.GumpLayout),
            VisualSettings  = cats.HasFlag(ProfileCategory.VisualSettings),
            CombatSettings  = cats.HasFlag(ProfileCategory.CombatSettings),
            TooltipSettings = cats.HasFlag(ProfileCategory.TooltipSettings),
            MacroBindings   = cats.HasFlag(ProfileCategory.MacroBindings),
            ChatSettings    = cats.HasFlag(ProfileCategory.ChatSettings),
            WorldMap        = cats.HasFlag(ProfileCategory.WorldMap),
        };

        public ProfileCategory ToCategories()
        {
            ProfileCategory cats = ProfileCategory.None;
            if (SoundSettings)   cats |= ProfileCategory.SoundSettings;
            if (HueSettings)     cats |= ProfileCategory.HueSettings;
            if (GumpLayout)      cats |= ProfileCategory.GumpLayout;
            if (VisualSettings)  cats |= ProfileCategory.VisualSettings;
            if (CombatSettings)  cats |= ProfileCategory.CombatSettings;
            if (TooltipSettings) cats |= ProfileCategory.TooltipSettings;
            if (MacroBindings)   cats |= ProfileCategory.MacroBindings;
            if (ChatSettings)    cats |= ProfileCategory.ChatSettings;
            if (WorldMap)        cats |= ProfileCategory.WorldMap;
            return cats;
        }
    }
}
