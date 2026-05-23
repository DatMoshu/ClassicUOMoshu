// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for ITreeSeasonServiceConfigStorage backed by
// Data/renderer3d/tree-season-defaults.json. Session 68 of ADR-012 Phase 4.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.World;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileTreeSeasonServiceConfigStorage : ITreeSeasonServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/tree-season-defaults.json";

        public TreeSeasonServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"tree-season-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] TreeSeasonServiceConfig: {err} — falling back to defaults");
                return new TreeSeasonServiceConfigLoadResult(false, err, fullPath, TreeSeasonServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                TreeSeasonServiceConfig def = TreeSeasonServiceConfig.Default;
                TreeSeasonServiceConfig cfg = new TreeSeasonServiceConfig
                {
                    SpringSummerBoundary = JsonConfigReader.ReadFloat(root, "SpringSummerBoundary", def.SpringSummerBoundary),
                    SummerAutumnBoundary = JsonConfigReader.ReadFloat(root, "SummerAutumnBoundary", def.SummerAutumnBoundary),
                    AutumnWinterBoundary = JsonConfigReader.ReadFloat(root, "AutumnWinterBoundary", def.AutumnWinterBoundary),
                    SnowRiseStart        = JsonConfigReader.ReadFloat(root, "SnowRiseStart",        def.SnowRiseStart),
                    SnowRiseEnd          = JsonConfigReader.ReadFloat(root, "SnowRiseEnd",          def.SnowRiseEnd),
                    SnowFallEnd          = JsonConfigReader.ReadFloat(root, "SnowFallEnd",          def.SnowFallEnd),
                    SnowFadeStart        = JsonConfigReader.ReadFloat(root, "SnowFadeStart",        def.SnowFadeStart),
                    SnowFadeEnd          = JsonConfigReader.ReadFloat(root, "SnowFadeEnd",          def.SnowFadeEnd),
                    InitialEnabled       = JsonConfigReader.ReadBool (root, "InitialEnabled",       def.InitialEnabled),
                    InitialYearProgress  = JsonConfigReader.ReadFloat(root, "InitialYearProgress",  def.InitialYearProgress),
                    InitialSeason        = JsonConfigReader.ReadEnum<TreeSeasonKind>(root, "InitialSeason", def.InitialSeason),
                    InitialSnowLineFrac  = JsonConfigReader.ReadFloat(root, "InitialSnowLineFrac",  def.InitialSnowLineFrac),
                    FallColorSharpness   = JsonConfigReader.ReadFloat(root, "FallColorSharpness",   def.FallColorSharpness),
                    InitialAutoFromYear  = JsonConfigReader.ReadBool (root, "InitialAutoFromYear",  def.InitialAutoFromYear),
                };

                Console.WriteLine($"[3DCUO] TreeSeasonServiceConfig loaded from {fullPath}");
                return new TreeSeasonServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] TreeSeasonServiceConfig load FAILED: {ex} — falling back to defaults");
                return new TreeSeasonServiceConfigLoadResult(false, ex.Message, fullPath, TreeSeasonServiceConfig.Default);
            }
        }
    }
}
