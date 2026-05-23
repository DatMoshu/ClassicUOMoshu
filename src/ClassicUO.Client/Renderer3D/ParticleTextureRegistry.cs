// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — name → Texture2D registry for particle billboards.
// Loads PNGs from {AppContext.BaseDirectory}/Textures/Particles/ on first
// access and caches them. The csproj's CopyParticleTextures target ships
// the source PNGs from prototypes/3DCUO/src/ClassicUO.Renderer/textures/
// particles/ into that output folder.
//
// Curated set as of import (sourced from D:\_repos\Particles\):
//   rain, snow, dust, rain_splash               — Enviro 3 weather pack
//   lightning_bolt, lightning_flash, lightning_glow — Enviro 3 lightning
//   fog, fog_alt, smoke_dense, smoke_anim_sheet  — Hovl Studio smoke
//   leaf                                         — Hovl Studio (autumn drop)
//   halo, flash                                  — Hovl Studio (lightning camera)
//   beam                                         — ARTnGAME RAIN/BEAMS
//   fire_sheet                                   — ARTnGAME 14-frame fire
//   snowflake                                    — Cartoon Coffee 2D Deluxe
//   lightning_toon                               — ARTnGAME toon lightning
//
// Lookup is case-insensitive; missing keys return null and log once.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class ParticleTextureRegistry
    {
        private static readonly Dictionary<string, Texture2D> _cache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _missingLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static GraphicsDevice _gd;

        public static int CachedCount => _cache.Count;
        public static string LastError;

        public static void SetDevice(GraphicsDevice gd) => _gd = gd;

        // Resolve a particle texture by short name (no extension, no path).
        // Returns null if the file is missing — callers should fall back.
        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var tex)) return tex;
            if (_gd == null) _gd = Client.Game.GraphicsDevice;
            if (_gd == null) return null;

            var path = Path.Combine(AppContext.BaseDirectory, "Textures", "Particles", name + ".png");
            if (!File.Exists(path))
            {
                if (_missingLogged.Add(name))
                    Console.WriteLine($"[ParticleTextureRegistry] missing: {path}");
                _cache[name] = null;
                return null;
            }

            try
            {
                using var fs = File.OpenRead(path);
                tex = Texture2D.FromStream(_gd, fs);
                _cache[name] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[ParticleTextureRegistry] load failed for {name}: {ex.Message}");
                _cache[name] = null;
                return null;
            }
        }

        // Drop everything (e.g. on graphics device reset). The next Get() call
        // re-loads on demand.
        public static void Clear()
        {
            foreach (var t in _cache.Values) t?.Dispose();
            _cache.Clear();
            _missingLogged.Clear();
        }

        // Iterate available names — used by debug gumps.
        public static IEnumerable<string> KnownNames => _cache.Keys;
    }
}
