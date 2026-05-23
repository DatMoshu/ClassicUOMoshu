// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for ISeasonServiceConfigStorage backed by
// Data/renderer3d/season-defaults.json. Session 68 of ADR-012 Phase 4.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileSeasonServiceConfigStorage : ISeasonServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/season-defaults.json";

        public SeasonServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"season-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] SeasonServiceConfig: {err} — falling back to defaults");
                return new SeasonServiceConfigLoadResult(false, err, fullPath, SeasonServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                SeasonServiceConfig def = SeasonServiceConfig.Default;
                SeasonServiceConfig cfg = new SeasonServiceConfig
                {
                    InitialEnabled                = JsonConfigReader.ReadBool (root, "InitialEnabled",                def.InitialEnabled),
                    SecondsPerYear                = JsonConfigReader.ReadFloat(root, "SecondsPerYear",                def.SecondsPerYear),
                    ShowcaseSecondsPerYear        = JsonConfigReader.ReadFloat(root, "ShowcaseSecondsPerYear",        def.ShowcaseSecondsPerYear),
                    WindEaseSeconds               = JsonConfigReader.ReadFloat(root, "WindEaseSeconds",               def.WindEaseSeconds),
                    SwayAmpEaseSeconds            = JsonConfigReader.ReadFloat(root, "SwayAmpEaseSeconds",            def.SwayAmpEaseSeconds),
                    SwayAmpStormDeg               = JsonConfigReader.ReadFloat(root, "SwayAmpStormDeg",               def.SwayAmpStormDeg),
                    LightningFlashHalfLifeSeconds = JsonConfigReader.ReadFloat(root, "LightningFlashHalfLifeSeconds", def.LightningFlashHalfLifeSeconds),
                    LightningFlashEpsilon         = JsonConfigReader.ReadFloat(root, "LightningFlashEpsilon",         def.LightningFlashEpsilon),
                    DriveWeatherParticles         = JsonConfigReader.ReadBool (root, "DriveWeatherParticles",         def.DriveWeatherParticles),
                    RegrowInSpring                = JsonConfigReader.ReadBool (root, "RegrowInSpring",                def.RegrowInSpring),
                    DriveSwayFromWeather          = JsonConfigReader.ReadBool (root, "DriveSwayFromWeather",          def.DriveSwayFromWeather),
                    DriveFoliageShaderTint        = JsonConfigReader.ReadBool (root, "DriveFoliageShaderTint",        def.DriveFoliageShaderTint),
                };

                Console.WriteLine($"[3DCUO] SeasonServiceConfig loaded from {fullPath}");
                return new SeasonServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] SeasonServiceConfig load FAILED: {ex} — falling back to defaults");
                return new SeasonServiceConfigLoadResult(false, ex.Message, fullPath, SeasonServiceConfig.Default);
            }
        }
    }
}
