// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — runtime loader for Data/mobile-3d-index.json (built by
// tools/scripts/build-mobile-3d-index.py from custom/Data/npcs/scan-initial.json).
// Maps an NPC type-name OR body graphic to a Synty pack + render kind so the
// renderer knows whether to compose a 3D humanoid for it (and which pack to
// pull parts from) or fall back to the 2D sprite.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Game.GameObjects;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum Mobile3DKind
    {
        Unsupported2D = 0,
        ComposedHumanoid = 1,
        SingleGlb = 2,
    }

    internal readonly struct Mobile3DEntry
    {
        public readonly string Pack;     // null when Kind == Unsupported2D
        public readonly Mobile3DKind Kind;
        public readonly int Body;        // 0 when not applicable

        public Mobile3DEntry(string pack, Mobile3DKind kind, int body)
        {
            Pack = pack;
            Kind = kind;
            Body = body;
        }

        public bool IsComposedHumanoid => Kind == Mobile3DKind.ComposedHumanoid && !string.IsNullOrEmpty(Pack);
    }

    internal static class Mobile3DIndex
    {
        // Master toggle. When false, every TryGet returns false → renderer
        // falls back to the legacy single-shared-vendor mesh path.
        public static bool Enabled = true;

        private static readonly Dictionary<string, Mobile3DEntry> _byType = new(StringComparer.Ordinal);
        private static readonly Dictionary<int, Mobile3DEntry> _byBody = new();
        private static bool _loaded;
        public static string LastError;
        public static int LoadedTypeCount;
        public static int LoadedBodyCount;
        public static string LoadedFromPath;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string path = ResolveJsonPath();
                if (path == null)
                {
                    LastError = "mobile-3d-index.json not found";
                    Console.WriteLine($"[3DCUO] Mobile3DIndex: {LastError}");
                    return;
                }
                LoadedFromPath = path;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                if (root.TryGetProperty("by_type", out var byTypeEl))
                {
                    foreach (var prop in byTypeEl.EnumerateObject())
                    {
                        if (TryParseEntry(prop.Value, out var entry))
                            _byType[prop.Name] = entry;
                    }
                }
                if (root.TryGetProperty("by_body", out var byBodyEl))
                {
                    foreach (var prop in byBodyEl.EnumerateObject())
                    {
                        if (int.TryParse(prop.Name, out int body) && TryParseEntry(prop.Value, out var entry))
                            _byBody[body] = entry;
                    }
                }
                LoadedTypeCount = _byType.Count;
                LoadedBodyCount = _byBody.Count;
                Console.WriteLine($"[3DCUO] Mobile3DIndex loaded: {LoadedTypeCount} types, {LoadedBodyCount} bodies from {path}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO] Mobile3DIndex load FAILED: {ex}");
            }
        }

        public static void Invalidate()
        {
            _loaded = false;
            _byType.Clear();
            _byBody.Clear();
            LoadedTypeCount = 0;
            LoadedBodyCount = 0;
            LastError = null;
            LoadedFromPath = null;
        }

        // Lookup: type-name first (when known), body fallback. Returns false
        // when no entry exists OR the entry is Unsupported2D — callers treat
        // both the same way: skip the 3D path and let the 2D sprite render.
        public static bool TryGet(string typeName, ushort body, out Mobile3DEntry entry)
        {
            entry = default;
            if (!Enabled) return false;
            EnsureLoaded();

            if (!string.IsNullOrEmpty(typeName) && _byType.TryGetValue(typeName, out var e1) && e1.IsComposedHumanoid)
            {
                entry = e1;
                return true;
            }
            if (_byBody.TryGetValue(body, out var e2) && e2.IsComposedHumanoid)
            {
                entry = e2;
                return true;
            }
            return false;
        }

        // Heuristic mapping from a runtime Mobile to the type name the index
        // was keyed on. Client doesn't see server class names; we approximate
        // from the visible Title (vendor titles like "the smith"), Name
        // (guards include "guard"), and body. The same heuristic is duplicated
        // in mobile-3d-rules.yaml so by_type and by_body lookups agree.
        public static string ResolveTypeFromMobile(Mobile m)
        {
            if (m == null) return null;
            string name  = m.Name ?? string.Empty;
            string title = m.Title ?? string.Empty;

            // Guards — name typically includes "guard", "warrior guard",
            // "archer guard", or the title is "the guard".
            if (Contains(name, "guard") || Contains(title, "guard"))
                return "WarriorGuard";

            // Common vendor titles → existing index keys.
            if (Contains(title, "smith"))         return "Blacksmith";
            if (Contains(title, "alchemist"))     return "Alchemist";
            if (Contains(title, "tailor"))        return "Tailor";
            if (Contains(title, "tavern"))        return "Tavernkeeper";
            if (Contains(title, "innkeeper") || Contains(title, "inn keeper")) return "InnKeeper";
            if (Contains(title, "banker"))        return "Banker";
            if (Contains(title, "baker"))         return "Baker";
            if (Contains(title, "cook"))          return "Cook";
            if (Contains(title, "healer"))        return "Healer";
            if (Contains(title, "mage"))          return "Mage";
            if (Contains(title, "provisioner"))   return "Provisioner";
            if (Contains(title, "fisher"))        return "Fisherman";
            if (Contains(title, "weaver"))        return "Weaver";
            if (Contains(title, "carpenter"))     return "Carpenter";
            if (Contains(title, "bowyer"))        return "Bowyer";
            if (Contains(title, "armorer") || Contains(title, "armourer")) return "Armorer";
            if (Contains(title, "jeweler") || Contains(title, "jeweller")) return "Jeweler";
            if (Contains(title, "animal trainer")) return "AnimalTrainer";

            // Body-only → null tells TryGet to fall through to by_body lookup.
            return null;
        }

        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool TryParseEntry(JsonElement el, out Mobile3DEntry entry)
        {
            entry = default;
            if (el.ValueKind != JsonValueKind.Object) return false;
            string pack = null;
            string kindStr = null;
            int body = 0;
            if (el.TryGetProperty("pack", out var p) && p.ValueKind == JsonValueKind.String)
                pack = p.GetString();
            if (el.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String)
                kindStr = k.GetString();
            if (el.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.Number)
                body = b.GetInt32();

            Mobile3DKind kind = kindStr switch
            {
                "composed_humanoid" => Mobile3DKind.ComposedHumanoid,
                "single_glb"        => Mobile3DKind.SingleGlb,
                _                   => Mobile3DKind.Unsupported2D,
            };
            entry = new Mobile3DEntry(pack, kind, body);
            return true;
        }

        private static string ResolveJsonPath()
        {
            // Same probing pattern as EquipmentSidekickMap: build-output mirror
            // first, then walk up to the repo Data folder.
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Data", "mobile-3d-index.json"),
                Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Data", "mobile-3d-index.json"),
                Path.Combine(baseDir, "..", "..", "..", "..", "Data", "mobile-3d-index.json"),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return null;
        }
    }
}
