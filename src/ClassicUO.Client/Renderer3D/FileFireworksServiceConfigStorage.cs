// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IFireworksServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileFireworksServiceConfigStorage : IFireworksServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/fireworks-defaults.json";

        public FireworksServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] FireworksServiceConfig: {fullPath} missing — falling back to defaults");
                return new FireworksServiceConfigLoadResult(false, "file missing", fullPath, FireworksServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                FireworksServiceConfig def = FireworksServiceConfig.Default;
                FireworksServiceConfig cfg = new FireworksServiceConfig
                {
                    InitialEnabled            = JsonConfigReader.ReadBool  (r, "InitialEnabled",            def.InitialEnabled),
                    InitialLoop               = JsonConfigReader.ReadBool  (r, "InitialLoop",               def.InitialLoop),
                    InitialClimaxText         = JsonConfigReader.ReadString(r, "InitialClimaxText",         def.InitialClimaxText),
                    ShowDurationSeconds       = JsonConfigReader.ReadFloat (r, "ShowDurationSeconds",       def.ShowDurationSeconds),
                    ClimaxStartSeconds        = JsonConfigReader.ReadFloat (r, "ClimaxStartSeconds",        def.ClimaxStartSeconds),
                    ClimaxEndSeconds          = JsonConfigReader.ReadFloat (r, "ClimaxEndSeconds",          def.ClimaxEndSeconds),
                    ClimaxEmitIntervalSeconds = JsonConfigReader.ReadFloat (r, "ClimaxEmitIntervalSeconds", def.ClimaxEmitIntervalSeconds),
                    ClimaxCellSize            = JsonConfigReader.ReadFloat (r, "ClimaxCellSize",            def.ClimaxCellSize),
                    ClimaxYOffset             = JsonConfigReader.ReadFloat (r, "ClimaxYOffset",             def.ClimaxYOffset),
                    ClimaxZOffset             = JsonConfigReader.ReadFloat (r, "ClimaxZOffset",             def.ClimaxZOffset),
                    ClimaxLifetimeSeconds     = JsonConfigReader.ReadFloat (r, "ClimaxLifetimeSeconds",     def.ClimaxLifetimeSeconds),
                    ClimaxSizeStart           = JsonConfigReader.ReadFloat (r, "ClimaxSizeStart",           def.ClimaxSizeStart),
                    ClimaxSizeEnd             = JsonConfigReader.ReadFloat (r, "ClimaxSizeEnd",             def.ClimaxSizeEnd),
                    MaxDeltaSeconds           = JsonConfigReader.ReadFloat (r, "MaxDeltaSeconds",           def.MaxDeltaSeconds),
                    RandomSeed                = JsonConfigReader.ReadInt   (r, "RandomSeed",                def.RandomSeed),
                };
                Console.WriteLine($"[3DCUO] FireworksServiceConfig loaded from {fullPath}");
                return new FireworksServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] FireworksServiceConfig load FAILED: {ex} — falling back to defaults");
                return new FireworksServiceConfigLoadResult(false, ex.Message, fullPath, FireworksServiceConfig.Default);
            }
        }
    }
}
