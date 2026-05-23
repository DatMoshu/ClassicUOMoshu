// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — dumps a JSONL classification report for every unique
// Static graphic currently in view. Drives iteration on StaticClassifier rules.
//
// Output: prototypes/3DCUO/dumps/static_classify_<timestamp>.jsonl
// Each line: {"graphic":..,"name":"..","height":..,"flags":[..],"isTree":..,
//             "isVegetation":..,"isRock":..,"kind":"..","count":..}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class StaticClassifyDumper
    {
        public static string LastDumpFile;
        public static string LastError;

        private static string ResolveDumpRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "dumps"));
            Directory.CreateDirectory(candidate);
            return candidate;
        }

        private struct Bucket
        {
            public ushort Graphic;
            public string Name;
            public byte Height;
            public TileFlag Flags;
            public bool IsTree;
            public bool IsVegetation;
            public bool IsRock;
            public StaticKind Kind;
            public int Count;
            // Registry fields (tree-statics.json) — empty/0 if no entry.
            public string RegistryKind;
            public string TreeType;
            public int    PairWith;
            public int    ArtHeightPx;   // actual sprite pixel height (ArtLoader)
        }

        public static string Dump()
        {
            try
            {
                var chunks = Static3DRenderer.LastVisibleChunks;
                if (chunks == null || chunks.Count == 0)
                {
                    Console.WriteLine("[StaticClassify] no visible chunks captured — enable Full 3D and orbit the camera once before dumping.");
                    return null;
                }

                var buckets = new Dictionary<ushort, Bucket>();
                int totalInstances = 0;

                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    var chunk = chunks[ci];
                    if (chunk == null) continue;
                    for (int ty = 0; ty < 8; ty++)
                    for (int tx = 0; tx < 8; tx++)
                    {
                        for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                        {
                            if (obj.IsDestroyed) continue;
                            if (obj is not Static s) continue;
                            if (!s.AllowedToDraw) continue;
                            totalInstances++;

                            if (!buckets.TryGetValue(s.Graphic, out var b))
                            {
                                ref readonly var td = ref s.ItemData;
                                string regKind = "";
                                string regTree = "";
                                int    regPair = 0;
                                if (TreeStaticRegistry.TryGet(s.Graphic, out var entry))
                                {
                                    regKind = entry.Kind.ToString();
                                    regTree = entry.TreeType ?? "";
                                    regPair = entry.PairWith;
                                }

                                int artH = 0;
                                try
                                {
                                    ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(s.Graphic);
                                    if (artInfo.Texture != null) artH = artInfo.UV.Height;
                                }
                                catch { /* art not loaded — leave 0 */ }

                                b = new Bucket
                                {
                                    Graphic = s.Graphic,
                                    Name = td.Name ?? "",
                                    Height = td.Height,
                                    Flags = td.Flags,
                                    IsTree = StaticFilters.IsTree(s.Graphic, out _),
                                    IsVegetation = StaticFilters.IsVegetation(s.Graphic),
                                    IsRock = StaticFilters.IsRock(s.Graphic),
                                    Kind = StaticClassifier.Classify(s.Graphic, in td),
                                    Count = 0,
                                    RegistryKind = regKind,
                                    TreeType = regTree,
                                    PairWith = regPair,
                                    ArtHeightPx = artH
                                };
                            }
                            b.Count++;
                            buckets[s.Graphic] = b;
                        }
                    }
                }

                string root = ResolveDumpRoot();
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outPath = Path.Combine(root, $"static_classify_{ts}.jsonl");

                using (var sw = new StreamWriter(outPath, false, Encoding.UTF8))
                {
                    foreach (var b in buckets.Values)
                    {
                        sw.WriteLine(BuildJsonLine(in b));
                    }
                }

                LastDumpFile = outPath;
                Console.WriteLine($"[StaticClassify] Wrote {buckets.Count} unique graphics ({totalInstances} instances in view) to {outPath}");
                return outPath;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[StaticClassify] dump failed: {ex}");
                return null;
            }
        }

        private static string BuildJsonLine(in Bucket b)
        {
            var sb = new StringBuilder(192);
            sb.Append('{');
            sb.Append("\"graphic\":").Append(b.Graphic);
            sb.Append(",\"name\":\"").Append(JsonEscape(b.Name)).Append('"');
            sb.Append(",\"height\":").Append(b.Height);
            sb.Append(",\"flags\":[");
            bool first = true;
            foreach (TileFlag f in Enum.GetValues(typeof(TileFlag)))
            {
                if (f == TileFlag.None) continue;
                ulong fv = (ulong)f;
                if ((fv & (fv - 1)) != 0) continue; // single-bit only
                if (((ulong)b.Flags & fv) == 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(f).Append('"');
            }
            sb.Append(']');
            sb.Append(",\"isTree\":").Append(b.IsTree ? "true" : "false");
            sb.Append(",\"isVegetation\":").Append(b.IsVegetation ? "true" : "false");
            sb.Append(",\"isRock\":").Append(b.IsRock ? "true" : "false");
            sb.Append(",\"kind\":\"").Append(b.Kind).Append('"');
            sb.Append(",\"registryKind\":\"").Append(JsonEscape(b.RegistryKind)).Append('"');
            sb.Append(",\"treeType\":\"").Append(JsonEscape(b.TreeType)).Append('"');
            sb.Append(",\"pairWith\":").Append(b.PairWith);
            sb.Append(",\"artHeightPx\":").Append(b.ArtHeightPx);
            sb.Append(",\"count\":").Append(b.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
