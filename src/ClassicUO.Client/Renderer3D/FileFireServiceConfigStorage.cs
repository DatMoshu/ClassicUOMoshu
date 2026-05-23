// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IFireServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileFireServiceConfigStorage : IFireServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/fire-defaults.json";

        public FireServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] FireServiceConfig: {fullPath} missing — falling back to defaults");
                return new FireServiceConfigLoadResult(false, "file missing", fullPath, FireServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                FireServiceConfig def = FireServiceConfig.Default;
                FireServiceConfig cfg = new FireServiceConfig
                {
                    InitialEnabled     = JsonConfigReader.ReadBool (r, "InitialEnabled",     def.InitialEnabled),
                    DefaultRadius      = JsonConfigReader.ReadFloat(r, "DefaultRadius",      def.DefaultRadius),
                    DefaultLifetime    = JsonConfigReader.ReadFloat(r, "DefaultLifetime",    def.DefaultLifetime),
                    EmberRatePerSec    = JsonConfigReader.ReadFloat(r, "EmberRatePerSec",    def.EmberRatePerSec),
                    SmokeRatePerSec    = JsonConfigReader.ReadFloat(r, "SmokeRatePerSec",    def.SmokeRatePerSec),
                    MaxFires           = JsonConfigReader.ReadInt  (r, "MaxFires",           def.MaxFires),
                    WindAdvectionScale = JsonConfigReader.ReadFloat(r, "WindAdvectionScale", def.WindAdvectionScale),
                    MinIgniteRadius    = JsonConfigReader.ReadFloat(r, "MinIgniteRadius",    def.MinIgniteRadius),
                    MinIgniteLifetime  = JsonConfigReader.ReadFloat(r, "MinIgniteLifetime",  def.MinIgniteLifetime),
                    MaxDeltaSeconds    = JsonConfigReader.ReadFloat(r, "MaxDeltaSeconds",    def.MaxDeltaSeconds),
                    RandomSeed         = JsonConfigReader.ReadInt  (r, "RandomSeed",         def.RandomSeed),
                };
                Console.WriteLine($"[3DCUO] FireServiceConfig loaded from {fullPath}");
                return new FireServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] FireServiceConfig load FAILED: {ex} — falling back to defaults");
                return new FireServiceConfigLoadResult(false, ex.Message, fullPath, FireServiceConfig.Default);
            }
        }
    }
}
