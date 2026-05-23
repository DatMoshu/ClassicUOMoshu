// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for ITreeStaticRegistryStorage backed by
// tree-statics.json. Reuses the legacy multi-step path resolution (output dir → dev
// fallback → exe-adjacent) so existing JSON files load identically.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// File-backed <see cref="ITreeStaticRegistryStorage"/>. JSON shape preserved bit-for-bit
    /// from the legacy <c>TreeStaticRegistry.Load</c>.
    /// </summary>
    internal sealed class FileTreeStaticRegistryStorage : ITreeStaticRegistryStorage
    {
        public string LastSource { get; private set; }
        public string LastError { get; private set; }

        private static string ResolvePath()
        {
            string baseDir = AppContext.BaseDirectory;
            string primary = Path.Combine(baseDir, "Data", "tree-statics.json");
            if (File.Exists(primary)) return primary;
            string dev = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Data", "tree-statics.json"));
            if (File.Exists(dev)) return dev;
            return Path.Combine(baseDir, "tree-statics.json");
        }

        public bool TryLoad(IDictionary<ushort, TreeStaticEntry> destination)
        {
            try
            {
                string path = ResolvePath();
                if (!File.Exists(path))
                {
                    LastError = $"tree-statics.json not found at {path}";
                    Console.WriteLine($"[TreeStaticRegistry] {LastError}");
                    return false;
                }
                LastSource = path;

                using FileStream fs = File.OpenRead(path);
                using JsonDocument doc = JsonDocument.Parse(fs);
                if (!doc.RootElement.TryGetProperty("graphics", out JsonElement graphics))
                    return false;

                foreach (JsonProperty prop in graphics.EnumerateObject())
                {
                    if (!ushort.TryParse(prop.Name, out ushort gid)) continue;
                    destination[gid] = ParseEntry(prop.Value);
                }

                Console.WriteLine($"[TreeStaticRegistry] loaded {destination.Count} entries from {path}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[TreeStaticRegistry] load failed: {ex}");
                return false;
            }
        }

        private static TreeStaticEntry ParseEntry(JsonElement v)
        {
            TreeStaticKind kind = ParseKind(v.GetProperty("kind").GetString());
            int pair = v.TryGetProperty("pairWith", out JsonElement p) && p.ValueKind == JsonValueKind.Number
                ? p.GetInt32() : 0;
            int td = v.TryGetProperty("tdHeight", out JsonElement th) && th.ValueKind == JsonValueKind.Number
                ? th.GetInt32() : 0;
            string name = v.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
            string tree = v.TryGetProperty("tree", out JsonElement t) ? t.GetString() ?? "" : "";
            // Default true: missing field → deciduous; only explicit "false" makes it evergreen.
            bool deciduous = !v.TryGetProperty("deciduous", out JsonElement d) || d.ValueKind != JsonValueKind.False;
            float fHue = v.TryGetProperty("fallHueDeg", out JsonElement fh) && fh.ValueKind == JsonValueKind.Number
                ? (float)fh.GetDouble() : 0f;
            float fSat = v.TryGetProperty("fallSatBoost", out JsonElement fsBoost) && fsBoost.ValueKind == JsonValueKind.Number
                ? (float)fsBoost.GetDouble() : 1f;

            return new TreeStaticEntry
            {
                Kind = kind, Name = name, TreeType = tree,
                PairWith = pair, TdHeight = td,
                Deciduous = deciduous,
                FallHueDeg = fHue,
                FallSatBoost = fSat,
            };
        }

        private static TreeStaticKind ParseKind(string s) => s switch
        {
            "WholeTree"   => TreeStaticKind.WholeTree,
            "TrunkOnly"   => TreeStaticKind.TrunkOnly,
            "LeafOverlay" => TreeStaticKind.LeafOverlay,
            "Bush"        => TreeStaticKind.Bush,
            _             => TreeStaticKind.Unknown,
        };
    }
}
