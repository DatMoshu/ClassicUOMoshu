// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Loads and saves <see cref="RpgCharacterSheet"/> XML under the active
    /// profile. Sheets are keyed by (TableSerial, PieceNumber) so a piece in a
    /// specific table always opens the same sheet across sessions.
    ///
    /// Storage layout (per profile):
    ///   &lt;ProfilePath&gt;/rpg-sheets/&lt;sheetId&gt;.xml
    ///   &lt;ProfilePath&gt;/rpg-sheets/index.xml   (binding map)
    ///
    /// Sheets live on disk only — the server never sees them. This deliberately
    /// keeps DM-authored content (potentially large notes, custom field lists)
    /// off the server's serialization path.
    /// </summary>
    internal static class RpgCharacterSheetManager
    {
        private const string FolderName = "rpg-sheets";
        private const string IndexFile = "index.xml";

        private static readonly Dictionary<long, string> _index = new();
        private static bool _indexLoaded;

        private static string Folder
            => Path.Combine(ProfileManager.ProfilePath ?? ".", FolderName);

        private static long Key(uint table, int piece) => ((long)table << 32) | (uint)piece;

        public static RpgCharacterSheet GetOrCreate(uint tableSerial, int pieceNumber)
        {
            EnsureIndex();
            if (_index.TryGetValue(Key(tableSerial, pieceNumber), out var id))
            {
                var loaded = LoadById(id);
                if (loaded != null) return loaded;
            }
            var sheet = new RpgCharacterSheet
            {
                TableSerial = tableSerial,
                PieceNumber = pieceNumber
            };
            return sheet;
        }

        public static void Save(RpgCharacterSheet sheet)
        {
            if (sheet == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, sheet.SheetId + ".xml");
                using (var writer = new XmlTextWriter(path, Encoding.UTF8)
                {
                    Formatting = Formatting.Indented,
                    IndentChar = '\t',
                    Indentation = 1
                })
                {
                    writer.WriteStartDocument(true);
                    sheet.Save(writer);
                    writer.WriteEndDocument();
                }

                EnsureIndex();
                _index[Key(sheet.TableSerial, sheet.PieceNumber)] = sheet.SheetId;
                SaveIndex();
            }
            catch (Exception ex)
            {
                Log.Error("[RpgCharacterSheetManager] Save failed: " + ex);
            }
        }

        public static void DeleteBinding(uint tableSerial, int pieceNumber)
        {
            EnsureIndex();
            _index.Remove(Key(tableSerial, pieceNumber));
            SaveIndex();
        }

        private static RpgCharacterSheet LoadById(string sheetId)
        {
            try
            {
                var path = Path.Combine(Folder, sheetId + ".xml");
                if (!File.Exists(path)) return null;
                var doc = new XmlDocument();
                doc.Load(path);
                return RpgCharacterSheet.Load(doc["sheet"]);
            }
            catch (Exception ex)
            {
                Log.Error("[RpgCharacterSheetManager] Load failed for " + sheetId + ": " + ex);
                return null;
            }
        }

        private static void EnsureIndex()
        {
            if (_indexLoaded) return;
            _indexLoaded = true;
            _index.Clear();
            try
            {
                var path = Path.Combine(Folder, IndexFile);
                if (!File.Exists(path)) return;
                var doc = new XmlDocument();
                doc.Load(path);
                var root = doc["bindings"];
                if (root == null) return;
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (n is XmlElement el && el.LocalName == "bind")
                    {
                        var t = RpgCharacterSheet.ParseUInt(el.GetAttribute("table"));
                        var p = RpgCharacterSheet.ParseInt(el.GetAttribute("piece"), -1);
                        var id = el.GetAttribute("sheet");
                        if (!string.IsNullOrEmpty(id))
                            _index[Key(t, p)] = id;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[RpgCharacterSheetManager] Index load failed: " + ex);
            }
        }

        private static void SaveIndex()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, IndexFile);
                using var writer = new XmlTextWriter(path, Encoding.UTF8)
                {
                    Formatting = Formatting.Indented,
                    IndentChar = '\t',
                    Indentation = 1
                };
                writer.WriteStartDocument(true);
                writer.WriteStartElement("bindings");
                foreach (var kv in _index)
                {
                    writer.WriteStartElement("bind");
                    writer.WriteAttributeString("table", ((uint)(kv.Key >> 32)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("piece", ((int)(kv.Key & 0xFFFFFFFF)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("sheet", kv.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            catch (Exception ex)
            {
                Log.Error("[RpgCharacterSheetManager] Index save failed: " + ex);
            }
        }
    }
}
