// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWeatherDefaultsStorage backed by a JSON file
// at {AppContext.BaseDirectory}/Data/weather-defaults.json.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// File-backed <see cref="IWeatherDefaultsStorage"/>. JSON shape is preserved bit-for-bit
    /// from the legacy <c>WeatherDefaultsStore.Save/Load</c> so existing files load without
    /// migration.
    /// </summary>
    internal sealed class FileWeatherDefaultsStorage : IWeatherDefaultsStorage
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public string LastSavedPath { get; private set; }
        public string LastError { get; private set; }

        private static string ResolvePath()
            => Path.Combine(AppContext.BaseDirectory, "Data", "weather-defaults.json");

        public bool TryLoad(IDictionary<WeatherKind, WeatherOverrideRecord> destination)
        {
            try
            {
                string path = ResolvePath();
                if (!File.Exists(path)) return true; // empty file is "successful empty load"

                using FileStream fs = File.OpenRead(path);
                using JsonDocument doc = JsonDocument.Parse(fs);
                if (!doc.RootElement.TryGetProperty("profiles", out JsonElement profiles))
                    return true;

                foreach (JsonProperty prop in profiles.EnumerateObject())
                {
                    if (!Enum.TryParse<WeatherKind>(prop.Name, ignoreCase: true, out WeatherKind kind))
                        continue;
                    var rec = JsonSerializer.Deserialize<WeatherOverrideRecord>(prop.Value.GetRawText());
                    if (rec != null) destination[kind] = rec;
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[WeatherDefaults] load failed: {ex}");
                return false;
            }
        }

        public bool Save(IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> overrides)
        {
            try
            {
                string path = ResolvePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                StringBuilder sb = new StringBuilder();
                sb.Append("{\n  \"profiles\": {");
                bool first = true;
                foreach (KeyValuePair<WeatherKind, WeatherOverrideRecord> kv in overrides)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("\n    \"").Append(kv.Key).Append("\": ");
                    sb.Append(JsonSerializer.Serialize(kv.Value, WriteOptions));
                }
                sb.Append("\n  }\n}\n");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                LastSavedPath = path;
                Console.WriteLine($"[WeatherDefaults] saved {overrides.Count} overrides to {path}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[WeatherDefaults] save failed: {ex}");
                return false;
            }
        }
    }
}
