// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Configuration
{
    /// <summary>
    /// Bitmask of categories used for selective profile export and import.
    /// Each flag corresponds to a logical grouping of Profile properties.
    /// When importing a .uowprofile package, the player chooses which categories
    /// to apply — properties in unchecked categories are left unchanged on the
    /// active profile.
    /// </summary>
    [System.Flags]
    internal enum ProfileCategory
    {
        None           = 0,
        SoundSettings  = 1 << 0,   // Volume, music, footsteps, proximity chat
        HueSettings    = 1 << 1,   // All colour customisations (speech, mobiles, spells)
        GumpLayout     = 1 << 2,   // Window positions, sizes, gumps.xml, anchor state
        VisualSettings = 1 << 3,   // Graphics options, lighting, effects, rendering
        CombatSettings = 1 << 4,   // Targeting, warmode, pathfinding, hotkeys, looting
        TooltipSettings = 1 << 5,  // Delay, zoom, opacity, font
        MacroBindings  = 1 << 6,   // macros.xml (WalkAction stripped on import)
        ChatSettings   = 1 << 7,   // Fonts, delays, journal tabs, filter options
        WorldMap       = 1 << 8,   // World map size, overlays, display preferences
        All            = ~None
    }
}
