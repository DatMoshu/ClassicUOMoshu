// SPDX-License-Identifier: BSD-2-Clause

// PP.A8 — Gump layout resolution scaler
// ----------------------------------------
// Scales all gump X/Y positions in gumps.xml from the export resolution to the
// import resolution. Called when the profile was created at a different screen
// resolution than the current one.
//
// Each <gump> element has x/y attributes for its top-left position. We apply a
// linear scale: newX = round(oldX * (newW / origW)), newY = round(oldY * (newH / origH)).
// Gumps that are fully off-screen after scaling are clamped to (0, 0).

using System;
using System.IO;
using System.Text;
using System.Xml;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal static class ProfileResolutionScaler
    {
        /// <summary>
        /// Scales gump X/Y positions in <paramref name="gumpsXmlPath"/> from
        /// (<paramref name="origW"/>x<paramref name="origH"/>) to
        /// (<paramref name="newW"/>x<paramref name="newH"/>).
        /// Writes the result back to the same file.
        /// Returns the number of gumps scaled.
        /// </summary>
        public static int Scale(string gumpsXmlPath, int origW, int origH, int newW, int newH)
        {
            if (!File.Exists(gumpsXmlPath)) return 0;
            if (origW <= 0 || origH <= 0 || newW <= 0 || newH <= 0) return 0;

            float scaleX = (float)newW / origW;
            float scaleY = (float)newH / origH;

            XmlDocument doc = new XmlDocument();

            try { doc.Load(gumpsXmlPath); }
            catch (Exception ex)
            {
                Log.Error($"[ProfileResolutionScaler] Failed to load {gumpsXmlPath}: {ex.Message}");
                return 0;
            }

            XmlNodeList gumps = doc.SelectNodes("//gump");
            if (gumps == null || gumps.Count == 0) return 0;

            int scaled = 0;

            foreach (XmlNode gump in gumps)
            {
                XmlAttribute xAttr = gump.Attributes?["x"];
                XmlAttribute yAttr = gump.Attributes?["y"];

                if (xAttr == null || yAttr == null) continue;

                if (!int.TryParse(xAttr.Value, out int oldX)) continue;
                if (!int.TryParse(yAttr.Value, out int oldY)) continue;

                int newX = Math.Max(0, (int)MathF.Round(oldX * scaleX));
                int newY = Math.Max(0, (int)MathF.Round(oldY * scaleY));

                xAttr.Value = newX.ToString();
                yAttr.Value = newY.ToString();
                scaled++;
            }

            if (scaled > 0)
            {
                using XmlTextWriter writer = new XmlTextWriter(gumpsXmlPath, Encoding.UTF8)
                {
                    Formatting  = Formatting.Indented,
                    IndentChar  = '\t',
                    Indentation = 1
                };
                doc.Save(writer);

                Log.Trace($"[ProfileResolutionScaler] Scaled {scaled} gumps " +
                          $"from {origW}x{origH} to {newW}x{newH}");
            }

            return scaled;
        }
    }
}
