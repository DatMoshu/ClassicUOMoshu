// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWeatherGroundOverlayMapStorage backed by
// Data/renderer3d/weather-ground-overlay.json. JsonDocument-based hand parsing keeps
// the loader AOT-clean per BLOCKER-05/-06 of the original code review.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.EnvRender;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IWeatherGroundOverlayMapStorage"/>. Reads the pilot Phase 4
    /// config file relative to the build output. Missing file → empty mapping table
    /// (renderer degrades to no-overlay fade); malformed entries are skipped with a
    /// console warning rather than aborting the load.
    /// </summary>
    internal sealed class FileWeatherGroundOverlayMapStorage : IWeatherGroundOverlayMapStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/weather-ground-overlay.json";

        public WeatherGroundOverlayMapLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"weather-ground-overlay.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] WeatherGroundOverlayMap: {err}");
                return new WeatherGroundOverlayMapLoadResult(false, err, fullPath, null);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                var table = new Dictionary<WeatherKind, WeatherGroundOverlayEntry>(8);
                if (doc.RootElement.TryGetProperty("mappings", out JsonElement mappings) &&
                    mappings.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty p in mappings.EnumerateObject())
                    {
                        if (!Enum.TryParse(p.Name, ignoreCase: true, out WeatherKind kind))
                        {
                            Console.WriteLine($"[3DCUO] WeatherGroundOverlayMap: unknown weather kind '{p.Name}' — skipped");
                            continue;
                        }
                        if (p.Value.ValueKind != JsonValueKind.Object) continue;
                        string modeStr = p.Value.TryGetProperty("mode", out JsonElement mEl) ? mEl.GetString() : null;
                        if (string.IsNullOrEmpty(modeStr) ||
                            !Enum.TryParse(modeStr, ignoreCase: true, out GroundEffectMode mode))
                        {
                            Console.WriteLine($"[3DCUO] WeatherGroundOverlayMap: '{p.Name}' has invalid mode '{modeStr}' — skipped");
                            continue;
                        }
                        float mult = 1.0f;
                        if (p.Value.TryGetProperty("strength_multiplier", out JsonElement sEl) &&
                            sEl.ValueKind == JsonValueKind.Number)
                        {
                            mult = sEl.GetSingle();
                        }
                        table[kind] = new WeatherGroundOverlayEntry(mode, mult);
                    }
                }
                Console.WriteLine($"[3DCUO] WeatherGroundOverlayMap loaded: {table.Count} mappings from {fullPath}");
                return new WeatherGroundOverlayMapLoadResult(true, null, fullPath, table);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] WeatherGroundOverlayMap load FAILED: {ex}");
                return new WeatherGroundOverlayMapLoadResult(false, ex.Message, fullPath, null);
            }
        }
    }
}
