// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWeatherServiceConfigStorage backed by
// Data/renderer3d/weather-defaults.json. Session 68 of ADR-012 Phase 4.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileWeatherServiceConfigStorage : IWeatherServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/weather-defaults.json";

        public WeatherServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"weather-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] WeatherServiceConfig: {err} — falling back to defaults");
                return new WeatherServiceConfigLoadResult(false, err, fullPath, WeatherServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                WeatherServiceConfig def = WeatherServiceConfig.Default;
                WeatherServiceConfig cfg = new WeatherServiceConfig
                {
                    InitialType                  = JsonConfigReader.ReadEnum<WeatherKind>(root, "InitialType",  def.InitialType),
                    InitialIntensity             = JsonConfigReader.ReadFloat(root, "InitialIntensity",             def.InitialIntensity),
                    Radius                       = JsonConfigReader.ReadFloat(root, "Radius",                       def.Radius),
                    Height                       = JsonConfigReader.ReadFloat(root, "Height",                       def.Height),
                    InitialAutoLightning         = JsonConfigReader.ReadBool (root, "InitialAutoLightning",         def.InitialAutoLightning),
                    InitialOcclusionCheckEnabled = JsonConfigReader.ReadBool (root, "InitialOcclusionCheckEnabled", def.InitialOcclusionCheckEnabled),
                    InitialApplyProfileOnSetType = JsonConfigReader.ReadBool (root, "InitialApplyProfileOnSetType", def.InitialApplyProfileOnSetType),
                    LightningEveryMin            = JsonConfigReader.ReadFloat(root, "LightningEveryMin",            def.LightningEveryMin),
                    LightningEveryMax            = JsonConfigReader.ReadFloat(root, "LightningEveryMax",            def.LightningEveryMax),
                };

                Console.WriteLine($"[3DCUO] WeatherServiceConfig loaded from {fullPath}");
                return new WeatherServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] WeatherServiceConfig load FAILED: {ex} — falling back to defaults");
                return new WeatherServiceConfigLoadResult(false, ex.Message, fullPath, WeatherServiceConfig.Default);
            }
        }
    }
}
