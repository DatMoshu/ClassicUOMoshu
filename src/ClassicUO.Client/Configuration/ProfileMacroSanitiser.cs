// SPDX-License-Identifier: BSD-2-Clause

// PP.A5 — Razor macro whitelist enforcement
// -----------------------------------------
// Applies UOWW's macro import policy to a macros.xml file before it is loaded
// by MacroManager. Operates on the XML file directly (no MacroManager dependency).
//
// POLICY (from design/gdd/player-profile.md — Razor Macro Whitelist):
//   BLOCKED:    Walk (MacroType 5) — automated movement enables farming bots.
//               Replaced with Delay (MacroType 27, 200 ms) on import.
//   SANITISED:  Say/Emote/Whisper/Yell (MacroType 1–4) — strip leading '-', '/', '['
//               to prevent command injection.
//   ALL OTHERS: Permitted unchanged.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal static class ProfileMacroSanitiser
    {
        // MacroType enum values from ClassicUO.Client/Game/Managers/MacroManager.cs
        private const int Walk    = 5;
        private const int Delay   = 27;
        private const int Say     = 1;
        private const int Emote   = 2;
        private const int Whisper = 3;
        private const int Yell    = 4;

        /// <summary>
        /// Reads <paramref name="macrosXmlPath"/>, strips blocked actions and
        /// sanitises speech text in-place, then writes the result back to the same path.
        /// Returns the number of actions modified or removed.
        /// </summary>
        public static int Sanitise(string macrosXmlPath)
        {
            if (!File.Exists(macrosXmlPath))
                return 0;

            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(macrosXmlPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[ProfileMacroSanitiser] Failed to load {macrosXmlPath}: {ex.Message}");
                return 0;
            }

            XmlNodeList macros = doc.SelectNodes("//macro");
            if (macros == null)
                return 0;

            int modifications = 0;

            foreach (XmlNode macro in macros)
            {
                string macroName = macro.Attributes?["name"]?.Value ?? "(unnamed)";
                XmlNodeList actions = macro.SelectNodes("action");
                if (actions == null) continue;

                // Single pass: collect walk replacements and sanitise speech in one loop.
                List<XmlNode> walkActions = new List<XmlNode>();

                foreach (XmlNode action in actions)
                {
                    if (!int.TryParse(action.Attributes?["type"]?.Value, out int actionType))
                        continue;

                    if (actionType == Walk)
                    {
                        walkActions.Add(action);
                        continue;
                    }

                    if (actionType == Say || actionType == Emote ||
                        actionType == Whisper || actionType == Yell)
                    {
                        XmlAttribute param1 = action.Attributes?["param1"];
                        if (param1 == null || param1.Value.Length == 0) continue;

                        char first = param1.Value[0];
                        if (first == '-' || first == '/' || first == '[')
                        {
                            string original = param1.Value;
                            param1.Value = param1.Value.TrimStart('-', '/', '[');
                            Log.Trace($"[ProfileMacroSanitiser] '{macroName}': speech prefix stripped '{original}' -> '{param1.Value}'");
                            modifications++;
                        }
                    }
                }

                foreach (XmlNode walkAction in walkActions)
                {
                    XmlElement delay = doc.CreateElement("action");
                    delay.SetAttribute("type",   Delay.ToString());
                    delay.SetAttribute("param1", "200");
                    delay.SetAttribute("param2", "0");
                    delay.SetAttribute("param3", "0");
                    delay.SetAttribute("param4", string.Empty);
                    delay.SetAttribute("param5", string.Empty);

                    macro.ReplaceChild(delay, walkAction);
                    Log.Trace($"[ProfileMacroSanitiser] '{macroName}': Walk replaced with Delay(200ms)");
                    modifications++;
                }
            }

            if (modifications > 0)
            {
                using XmlTextWriter writer = new XmlTextWriter(macrosXmlPath, Encoding.UTF8)
                {
                    Formatting  = Formatting.Indented,
                    IndentChar  = '\t',
                    Indentation = 1
                };
                doc.Save(writer);
            }

            return modifications;
        }
    }
}
