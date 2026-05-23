// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Client-only character sheet bound to an RpgTable piece. The sheet is a
    /// schema-less collection of typed fields (int/string/bool/counter) plus a
    /// scrollable inventory list, configurable art, and a free-form notes
    /// panel. Persisted as XML under the active profile so a player's sheets
    /// follow them across sessions without burdening the server.
    /// </summary>
    internal sealed class RpgCharacterSheet
    {
        public string SheetId;
        public uint TableSerial;
        public int PieceNumber = -1;

        public string CharacterName = string.Empty;

        public ushort BackgroundGumpId = 9270;
        public ushort PortraitGumpId;

        public string Notes = string.Empty;

        public readonly List<SheetField> Fields = new();
        public readonly List<InventoryItem> Inventory = new();

        public RpgCharacterSheet()
        {
            SheetId = Guid.NewGuid().ToString("N");
        }

        public SheetField AddField(SheetFieldType type, string name)
        {
            var f = new SheetField
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name ?? string.Empty,
                Type = type
            };
            if (type == SheetFieldType.Counter)
            {
                f.IntValue = 10;
                f.CounterMax = 10;
            }
            Fields.Add(f);
            return f;
        }

        public void RemoveField(string id)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (Fields[i].Id == id) { Fields.RemoveAt(i); return; }
            }
        }

        public InventoryItem AddInventoryItem(string name = "")
        {
            var item = new InventoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name ?? string.Empty,
                Quantity = 1
            };
            Inventory.Add(item);
            return item;
        }

        public void RemoveInventoryItem(string id)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (Inventory[i].Id == id) { Inventory.RemoveAt(i); return; }
            }
        }

        public void Save(XmlWriter w)
        {
            w.WriteStartElement("sheet");
            w.WriteAttributeString("id", SheetId);
            w.WriteAttributeString("table", TableSerial.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("piece", PieceNumber.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("name", CharacterName ?? string.Empty);
            w.WriteAttributeString("bg", BackgroundGumpId.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("portrait", PortraitGumpId.ToString(CultureInfo.InvariantCulture));

            w.WriteStartElement("notes");
            w.WriteCData(Notes ?? string.Empty);
            w.WriteEndElement();

            w.WriteStartElement("fields");
            foreach (var f in Fields) f.Save(w);
            w.WriteEndElement();

            w.WriteStartElement("inventory");
            foreach (var it in Inventory) it.Save(w);
            w.WriteEndElement();

            w.WriteEndElement(); // sheet
        }

        public static RpgCharacterSheet Load(XmlElement el)
        {
            if (el == null) return null;
            var s = new RpgCharacterSheet
            {
                SheetId = AttrOrNew(el, "id"),
                TableSerial = ParseUInt(el.GetAttribute("table")),
                PieceNumber = ParseInt(el.GetAttribute("piece"), -1),
                CharacterName = el.GetAttribute("name") ?? string.Empty,
                BackgroundGumpId = ParseUShort(el.GetAttribute("bg"), 9270),
                PortraitGumpId = ParseUShort(el.GetAttribute("portrait"), 0)
            };

            var notesEl = el["notes"];
            if (notesEl != null) s.Notes = notesEl.InnerText ?? string.Empty;

            var fieldsEl = el["fields"];
            if (fieldsEl != null)
            {
                foreach (XmlNode n in fieldsEl.ChildNodes)
                {
                    if (n is XmlElement fe && fe.LocalName == "field")
                    {
                        var f = SheetField.Load(fe);
                        if (f != null) s.Fields.Add(f);
                    }
                }
            }

            var invEl = el["inventory"];
            if (invEl != null)
            {
                foreach (XmlNode n in invEl.ChildNodes)
                {
                    if (n is XmlElement ie && ie.LocalName == "item")
                    {
                        var it = InventoryItem.Load(ie);
                        if (it != null) s.Inventory.Add(it);
                    }
                }
            }

            return s;
        }

        private static string AttrOrNew(XmlElement el, string name)
        {
            var v = el.GetAttribute(name);
            return string.IsNullOrEmpty(v) ? Guid.NewGuid().ToString("N") : v;
        }

        internal static int ParseInt(string s, int def = 0)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

        internal static uint ParseUInt(string s, uint def = 0)
            => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

        internal static ushort ParseUShort(string s, ushort def = 0)
            => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

        internal static bool ParseBool(string s, bool def = false)
            => bool.TryParse(s, out var v) ? v : def;
    }

    internal enum SheetFieldType : byte
    {
        Int = 0,
        String = 1,
        Bool = 2,
        Counter = 3
    }

    internal sealed class SheetField
    {
        public string Id;
        public string Name = string.Empty;
        public SheetFieldType Type;

        public int IntValue;
        public string StringValue = string.Empty;
        public bool BoolValue;
        public int CounterMax;

        public void Save(XmlWriter w)
        {
            w.WriteStartElement("field");
            w.WriteAttributeString("id", Id);
            w.WriteAttributeString("name", Name ?? string.Empty);
            w.WriteAttributeString("type", ((int)Type).ToString(CultureInfo.InvariantCulture));
            switch (Type)
            {
                case SheetFieldType.Int:
                case SheetFieldType.Counter:
                    w.WriteAttributeString("value", IntValue.ToString(CultureInfo.InvariantCulture));
                    if (Type == SheetFieldType.Counter)
                        w.WriteAttributeString("max", CounterMax.ToString(CultureInfo.InvariantCulture));
                    break;
                case SheetFieldType.String:
                    w.WriteAttributeString("value", StringValue ?? string.Empty);
                    break;
                case SheetFieldType.Bool:
                    w.WriteAttributeString("value", BoolValue ? "true" : "false");
                    break;
            }
            w.WriteEndElement();
        }

        public static SheetField Load(XmlElement el)
        {
            if (el == null) return null;
            var f = new SheetField
            {
                Id = el.GetAttribute("id"),
                Name = el.GetAttribute("name") ?? string.Empty,
                Type = (SheetFieldType)RpgCharacterSheet.ParseInt(el.GetAttribute("type"))
            };
            if (string.IsNullOrEmpty(f.Id)) f.Id = Guid.NewGuid().ToString("N");
            var raw = el.GetAttribute("value") ?? string.Empty;
            switch (f.Type)
            {
                case SheetFieldType.Int:
                case SheetFieldType.Counter:
                    f.IntValue = RpgCharacterSheet.ParseInt(raw);
                    f.CounterMax = RpgCharacterSheet.ParseInt(el.GetAttribute("max"));
                    break;
                case SheetFieldType.String:
                    f.StringValue = raw;
                    break;
                case SheetFieldType.Bool:
                    f.BoolValue = RpgCharacterSheet.ParseBool(raw);
                    break;
            }
            return f;
        }
    }

    internal sealed class InventoryItem
    {
        public string Id;
        public string Name = string.Empty;
        public int Quantity = 1;
        public string Notes = string.Empty;

        public void Save(XmlWriter w)
        {
            w.WriteStartElement("item");
            w.WriteAttributeString("id", Id);
            w.WriteAttributeString("name", Name ?? string.Empty);
            w.WriteAttributeString("qty", Quantity.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Notes))
            {
                w.WriteStartElement("notes");
                w.WriteCData(Notes);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        public static InventoryItem Load(XmlElement el)
        {
            if (el == null) return null;
            var it = new InventoryItem
            {
                Id = el.GetAttribute("id"),
                Name = el.GetAttribute("name") ?? string.Empty,
                Quantity = RpgCharacterSheet.ParseInt(el.GetAttribute("qty"), 1)
            };
            if (string.IsNullOrEmpty(it.Id)) it.Id = Guid.NewGuid().ToString("N");
            var notesEl = el["notes"];
            if (notesEl != null) it.Notes = notesEl.InnerText ?? string.Empty;
            return it;
        }
    }
}
