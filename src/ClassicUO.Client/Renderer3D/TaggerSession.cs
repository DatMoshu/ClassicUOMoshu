// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Tagger session for the equipment-model registry.
//
// Loads Data/model-index.json, walks untagged entries one at a time, and shows
// each on the live player rig by setting the matching AttachmentRenderer slot's
// CurrentIndex. On confirm/discard/duplicate, writes a row to
// Data/equipment-models.json (or .discarded.json) and flips the model's
// `tagged` flag in model-index.json so the next session resumes where this one
// left off.
//
// This is throwaway prototype code — JSON parsing is hand-rolled to avoid
// pulling System.Text.Json into the prototype's NativeAOT trim graph.
//
// All paths are repo-relative with forward slashes. Repo root is resolved by
// walking up from AppContext.BaseDirectory until we find a "Data" directory.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class TaggerSession
    {
        public sealed class Model
        {
            public string Path;        // repo-relative
            public string Pack;
            public string Slot;        // AttachSlotKind name
            public string AttachCode;
            public string Tribe;
            public string Outfit;
            public string Variant;
            public string HueCode;
            public bool Tagged;

            // Suggestions (graphic + layer + name) loaded from suggestions JSON.
            public readonly List<Suggestion> Suggestions = new();
        }

        public sealed class Suggestion
        {
            public string Layer;
            public string Graphic;
            public string Name;
            public int Score;
        }

        public sealed class Item
        {
            public string Graphic;
            public string Layer;
            public string Name;
        }

        public static readonly List<Model> Models = new();
        public static readonly Dictionary<string, List<Item>> ItemsByLayer = new();
        public static int Cursor = -1;
        public static string LastError;
        public static string LastInfo;
        private static string _repoRoot;

        public static bool Loaded => Models.Count > 0;
        public static int UntaggedCount
        {
            get
            {
                int n = 0;
                foreach (var m in Models)
                    if (!m.Tagged) n++;
                return n;
            }
        }

        public static Model Current
            => Cursor >= 0 && Cursor < Models.Count ? Models[Cursor] : null;

        public static bool LoadIfNeeded()
        {
            if (Loaded) return true;
            try
            {
                _repoRoot = ResolveRepoRoot();
                if (_repoRoot == null)
                {
                    LastError = "could not locate repo root from " + AppContext.BaseDirectory;
                    return false;
                }

                LoadModelIndex(Path.Combine(_repoRoot, "Data", "model-index.json"));
                LoadSuggestions(Path.Combine(_repoRoot, "Data", "equipment-models.suggestions.json"));
                LoadItemIndex(Path.Combine(_repoRoot, "Data", "item-index.json"));

                Cursor = FindNextUntagged(-1);
                LastInfo = $"loaded {Models.Count} models, {UntaggedCount} untagged";
                return true;
            }
            catch (Exception ex)
            {
                LastError = "load failed: " + ex.Message;
                return false;
            }
        }

        public static void Next()    { if (Loaded) Cursor = FindNextUntagged(Cursor); ApplyCurrent(); }
        public static void Prev()    { if (Loaded) Cursor = FindPrevUntagged(Cursor); ApplyCurrent(); }
        public static void NextAny() { if (Loaded) Cursor = (Cursor + 1) % Models.Count; ApplyCurrent(); }
        public static void PrevAny() { if (Loaded) Cursor = (Cursor - 1 + Models.Count) % Models.Count; ApplyCurrent(); }

        public static void Skip()    => Next();   // equivalent: just advance, don't change tagged flag

        // Apply the current model to the matching AttachmentRenderer slot so the
        // user sees it on the rig. Clears every other attach slot to avoid
        // visual confusion (one model at a time).
        public static void ApplyCurrent()
        {
            var m = Current;
            if (m == null) return;
            if (!Enum.TryParse<AttachSlotKind>(m.Slot, out var kind))
            {
                LastError = $"unknown slot '{m.Slot}'";
                return;
            }

            // Clear other slots, set current.
            foreach (var s in AttachmentRenderer.Slots)
            {
                if (s.Kind != kind) s.CurrentIndex = -1;
            }

            AttachSlot slot = null;
            foreach (var s in AttachmentRenderer.Slots)
            {
                if (s.Kind == kind) { slot = s; break; }
            }
            if (slot == null) { LastError = "no AttachmentRenderer slot for " + m.Slot; return; }

            // Convert repo-rel path to slot-rel path
            //  "prototypes/3DCUO/Models/Hats/Pack/foo.glb" -> "Hats/Pack/foo.glb"
            string needle = m.Path;
            int idx = needle.IndexOf("/Models/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) needle = needle.Substring(idx + "/Models/".Length);

            int found = slot.Available.FindIndex(p =>
                string.Equals(p.Replace('\\', '/'), needle, StringComparison.OrdinalIgnoreCase));
            slot.CurrentIndex = found;
            if (found < 0) LastError = "model not in slot's Available list: " + needle;
            else LastInfo = $"showing [{Cursor + 1}/{Models.Count}] {m.Path}";
        }

        public static void Confirm(string graphic, string layer)
        {
            var m = Current;
            if (m == null) return;
            graphic = NormalizeGraphic(graphic);
            if (graphic == null) { LastError = "invalid graphic — expected hex (e.g. 0x1408)"; return; }

            var entry = new Dictionary<string, string>
            {
                ["layer"] = layer ?? "(unknown)",
                ["graphic"] = graphic,
                ["glb"] = m.Path,
                ["slot"] = m.Slot,
                ["pack"] = m.Pack ?? "",
                ["confidence"] = "manual",
            };
            AppendRegistryRow(Path.Combine(_repoRoot, "Data", "equipment-models.json"), entry);
            MarkTagged(m);
            Cursor = FindNextUntagged(Cursor);
            ApplyCurrent();
            LastInfo = $"tagged {m.Path} → {layer} {graphic}";
        }

        public static void MarkDuplicate()
        {
            var m = Current;
            if (m == null) return;
            // No registry row — just mark tagged. Future runs skip it.
            MarkTagged(m);
            Cursor = FindNextUntagged(Cursor);
            ApplyCurrent();
            LastInfo = $"marked duplicate: {m.Path}";
        }

        public static void Discard()
        {
            var m = Current;
            if (m == null) return;
            var entry = new Dictionary<string, string>
            {
                ["glb"] = m.Path,
                ["slot"] = m.Slot,
                ["pack"] = m.Pack ?? "",
                ["reason"] = "discarded",
            };
            AppendRegistryRow(Path.Combine(_repoRoot, "Data", "equipment-models.discarded.json"), entry);
            MarkTagged(m);
            Cursor = FindNextUntagged(Cursor);
            ApplyCurrent();
            LastInfo = $"discarded {m.Path}";
        }

        // ---------------- helpers ----------------

        private static int FindNextUntagged(int from)
        {
            if (Models.Count == 0) return -1;
            for (int i = 1; i <= Models.Count; i++)
            {
                int j = (from + i) % Models.Count;
                if (!Models[j].Tagged) return j;
            }
            return from < 0 ? 0 : from;
        }

        private static int FindPrevUntagged(int from)
        {
            if (Models.Count == 0) return -1;
            for (int i = 1; i <= Models.Count; i++)
            {
                int j = (from - i + Models.Count * 2) % Models.Count;
                if (!Models[j].Tagged) return j;
            }
            return from < 0 ? 0 : from;
        }

        private static void MarkTagged(Model m)
        {
            m.Tagged = true;
            // Persist by rewriting the model-index.json's "tagged" booleans only.
            // To avoid a full JSON re-emit, we do a targeted text replacement:
            // find the model's path string and flip the next "tagged": false -> true.
            string idxFile = Path.Combine(_repoRoot, "Data", "model-index.json");
            try
            {
                string text = File.ReadAllText(idxFile);
                int p = text.IndexOf("\"path\": \"" + m.Path + "\"", StringComparison.Ordinal);
                if (p < 0) return;
                int t = text.IndexOf("\"tagged\":", p, StringComparison.Ordinal);
                if (t < 0) return;
                int colon = text.IndexOf(':', t);
                int valStart = colon + 1;
                while (valStart < text.Length && char.IsWhiteSpace(text[valStart])) valStart++;
                if (valStart + 5 <= text.Length && text.Substring(valStart, 5) == "false")
                {
                    text = text.Substring(0, valStart) + "true " + text.Substring(valStart + 5);
                    File.WriteAllText(idxFile, text);
                }
            }
            catch (Exception ex)
            {
                LastError = "failed to persist tagged flag: " + ex.Message;
            }
        }

        private static string NormalizeGraphic(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (!int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v)) return null;
            if (v < 0 || v > 0xFFFF) return null;
            return $"0x{v:X4}";
        }

        private static void AppendRegistryRow(string file, Dictionary<string, string> entry)
        {
            // The registry is a JSON array. Maintain it manually: read whole
            // file, insert before the last ']', write back.
            string body;
            if (!File.Exists(file))
            {
                body = "[\n]\n";
                File.WriteAllText(file, body);
            }
            body = File.ReadAllText(file);
            int last = body.LastIndexOf(']');
            if (last < 0) { body = "[\n]\n"; last = body.LastIndexOf(']'); }
            string before = body.Substring(0, last).TrimEnd();
            bool hasContent = before.Length > 1 && before[before.Length - 1] != '[';
            var sb = new StringBuilder();
            sb.Append(before);
            if (hasContent) sb.Append(",");
            sb.Append("\n  ");
            sb.Append(SerializeFlatObject(entry));
            sb.Append("\n]\n");
            File.WriteAllText(file, sb.ToString());
        }

        private static string SerializeFlatObject(Dictionary<string, string> entry)
        {
            var sb = new StringBuilder();
            sb.Append("{ ");
            bool first = true;
            foreach (var kv in entry)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append('"'); sb.Append(EscapeJson(kv.Key)); sb.Append("\": \"");
                sb.Append(EscapeJson(kv.Value)); sb.Append('"');
            }
            sb.Append(" }");
            return sb.ToString();
        }

        private static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string ResolveRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Data")) &&
                    Directory.Exists(Path.Combine(dir, "tools")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static void LoadModelIndex(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException(file);
            string text = File.ReadAllText(file);
            // Hand-rolled extractor: scan for each `{ ... "path": "...", ... }`
            // object inside the "models" array. Regex would suffice but we avoid
            // a dependency for prototype code.
            Models.Clear();
            int idx = text.IndexOf("\"models\"", StringComparison.Ordinal);
            if (idx < 0) throw new InvalidDataException("no \"models\" key");
            idx = text.IndexOf('[', idx);
            if (idx < 0) throw new InvalidDataException("no models array");

            while (true)
            {
                int objStart = text.IndexOf('{', idx);
                if (objStart < 0) break;
                int objEnd = MatchBrace(text, objStart);
                if (objEnd < 0) break;
                string obj = text.Substring(objStart, objEnd - objStart + 1);

                var m = new Model
                {
                    Path = ExtractString(obj, "path"),
                    Pack = ExtractString(obj, "pack"),
                    Slot = ExtractString(obj, "slot"),
                    AttachCode = ExtractString(obj, "attach_code"),
                    Tribe = ExtractString(obj, "tribe"),
                    Outfit = ExtractString(obj, "outfit"),
                    Variant = ExtractString(obj, "variant"),
                    HueCode = ExtractString(obj, "hue_code"),
                    Tagged = ExtractBool(obj, "tagged"),
                };
                if (!string.IsNullOrEmpty(m.Path)) Models.Add(m);

                idx = objEnd + 1;
                int closing = text.IndexOf(']', idx);
                int next = text.IndexOf('{', idx);
                if (closing >= 0 && (next < 0 || closing < next)) break;
            }
        }

        private static void LoadSuggestions(string file)
        {
            if (!File.Exists(file)) return;
            string text = File.ReadAllText(file);
            var byPath = new Dictionary<string, Model>();
            foreach (var m in Models) byPath[m.Path] = m;

            int idx = text.IndexOf("\"suggestions\"", StringComparison.Ordinal);
            if (idx < 0) return;
            idx = text.IndexOf('[', idx);
            if (idx < 0) return;

            while (true)
            {
                int objStart = text.IndexOf('{', idx);
                if (objStart < 0) break;
                int objEnd = MatchBrace(text, objStart);
                if (objEnd < 0) break;
                string obj = text.Substring(objStart, objEnd - objStart + 1);
                string mPath = ExtractString(obj, "model");
                if (byPath.TryGetValue(mPath ?? "", out var m))
                {
                    int candIdx = obj.IndexOf("\"candidates\"", StringComparison.Ordinal);
                    if (candIdx >= 0)
                    {
                        int candStart = obj.IndexOf('[', candIdx);
                        int candEnd = MatchBracket(obj, candStart);
                        if (candStart >= 0 && candEnd >= 0)
                        {
                            string arr = obj.Substring(candStart, candEnd - candStart + 1);
                            int p = 0;
                            while (true)
                            {
                                int s = arr.IndexOf('{', p);
                                if (s < 0) break;
                                int e = MatchBrace(arr, s);
                                if (e < 0) break;
                                string c = arr.Substring(s, e - s + 1);
                                m.Suggestions.Add(new Suggestion
                                {
                                    Layer = ExtractString(c, "layer"),
                                    Graphic = ExtractString(c, "graphic"),
                                    Name = ExtractString(c, "name"),
                                    Score = ExtractInt(c, "score"),
                                });
                                p = e + 1;
                            }
                        }
                    }
                }
                idx = objEnd + 1;
                int closing = text.IndexOf(']', idx);
                int next = text.IndexOf('{', idx);
                if (closing >= 0 && (next < 0 || closing < next)) break;
            }
        }

        private static void LoadItemIndex(string file)
        {
            if (!File.Exists(file)) return;
            string text = File.ReadAllText(file);
            ItemsByLayer.Clear();
            int idx = text.IndexOf("\"items\"", StringComparison.Ordinal);
            if (idx < 0) return;
            idx = text.IndexOf('[', idx);
            if (idx < 0) return;

            while (true)
            {
                int objStart = text.IndexOf('{', idx);
                if (objStart < 0) break;
                int objEnd = MatchBrace(text, objStart);
                if (objEnd < 0) break;
                string obj = text.Substring(objStart, objEnd - objStart + 1);
                var it = new Item
                {
                    Graphic = ExtractString(obj, "graphic"),
                    Layer = ExtractString(obj, "layer"),
                    Name = ExtractString(obj, "name"),
                };
                if (!string.IsNullOrEmpty(it.Layer))
                {
                    if (!ItemsByLayer.TryGetValue(it.Layer, out var list))
                        ItemsByLayer[it.Layer] = list = new List<Item>();
                    list.Add(it);
                }
                idx = objEnd + 1;
                int closing = text.IndexOf(']', idx);
                int next = text.IndexOf('{', idx);
                if (closing >= 0 && (next < 0 || closing < next)) break;
            }
        }

        // ---- minimal JSON parsers (objects only, string/bool/int values) ----

        private static int MatchBrace(string s, int open)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static int MatchBracket(string s, int open)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '[') depth++;
                else if (c == ']') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string ExtractString(string obj, string key)
        {
            string token = "\"" + key + "\"";
            int k = obj.IndexOf(token, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = obj.IndexOf(':', k);
            int start = obj.IndexOf('"', colon + 1);
            if (start < 0) return null;
            var sb = new StringBuilder();
            for (int i = start + 1; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c == '\\' && i + 1 < obj.Length) { sb.Append(obj[i + 1]); i++; continue; }
                if (c == '"') return sb.ToString();
                sb.Append(c);
            }
            return null;
        }

        private static bool ExtractBool(string obj, string key)
        {
            string token = "\"" + key + "\"";
            int k = obj.IndexOf(token, StringComparison.Ordinal);
            if (k < 0) return false;
            int colon = obj.IndexOf(':', k);
            int i = colon + 1;
            while (i < obj.Length && char.IsWhiteSpace(obj[i])) i++;
            return i + 4 <= obj.Length && obj.Substring(i, 4) == "true";
        }

        private static int ExtractInt(string obj, string key)
        {
            string token = "\"" + key + "\"";
            int k = obj.IndexOf(token, StringComparison.Ordinal);
            if (k < 0) return 0;
            int colon = obj.IndexOf(':', k);
            int i = colon + 1;
            while (i < obj.Length && char.IsWhiteSpace(obj[i])) i++;
            int start = i;
            while (i < obj.Length && (char.IsDigit(obj[i]) || obj[i] == '-')) i++;
            int.TryParse(obj.Substring(start, i - start), out int v);
            return v;
        }
    }
}
