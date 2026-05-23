// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Named palette pair for <see cref="AmbientMotesService"/>. Each entry is a start/end
    /// color pair — start drives the spawn alpha, end drives the fade-out.
    /// </summary>
    public readonly struct AmbientMotesPalette
    {
        public readonly Color Start;
        public readonly Color End;

        public AmbientMotesPalette(Color start, Color end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Built-in named palettes. Operators select by name (e.g., from a console command);
    /// the service applies the corresponding start/end pair. Case-insensitive lookup.
    /// </summary>
    public static class AmbientMotesPaletteLibrary
    {
        private static readonly Dictionary<string, AmbientMotesPalette> _palettes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "kokiri",    new AmbientMotesPalette(new Color(220, 255, 130, (byte)255), new Color( 80, 160,  60, (byte)0)) },
            { "lostwoods", new AmbientMotesPalette(new Color(160, 255, 170, (byte)255), new Color( 40, 120,  60, (byte)0)) },
            { "spirit",    new AmbientMotesPalette(new Color(220, 235, 255, (byte)255), new Color(120, 150, 200, (byte)0)) },
            { "embers",    new AmbientMotesPalette(new Color(255, 200, 100, (byte)255), new Color(200,  80,  20, (byte)0)) },
            { "bloodmoon", new AmbientMotesPalette(new Color(255, 120, 120, (byte)255), new Color(140,  20,  20, (byte)0)) },
            { "fairy",     new AmbientMotesPalette(new Color(255, 180, 220, (byte)255), new Color(180,  90, 160, (byte)0)) },
        };

        /// <summary>Default palette — Kokiri Forest yellow-green.</summary>
        public static AmbientMotesPalette Default => _palettes["kokiri"];

        /// <summary>Look up a named palette. Returns false when the name is unknown.</summary>
        public static bool TryGet(string name, out AmbientMotesPalette palette)
            => _palettes.TryGetValue(name ?? string.Empty, out palette);

        /// <summary>All built-in palette names. Useful for UI dropdowns and tests.</summary>
        public static IEnumerable<string> Names => _palettes.Keys;
    }
}
