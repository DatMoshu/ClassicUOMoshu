// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for ILeafFallServiceConfigStorage backed by
// Data/renderer3d/leaf-fall-defaults.json. Session 68 of ADR-012 Phase 4.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.World;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileLeafFallServiceConfigStorage : ILeafFallServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/leaf-fall-defaults.json";

        public LeafFallServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"leaf-fall-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] LeafFallServiceConfig: {err} — falling back to defaults");
                return new LeafFallServiceConfigLoadResult(false, err, fullPath, LeafFallServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                LeafFallServiceConfig def = LeafFallServiceConfig.Default;
                LeafFallServiceConfig cfg = new LeafFallServiceConfig
                {
                    MaxLeaves          = JsonConfigReader.ReadInt  (root, "MaxLeaves",          def.MaxLeaves),
                    MaxSpawnPerSecond  = JsonConfigReader.ReadFloat(root, "MaxSpawnPerSecond",  def.MaxSpawnPerSecond),
                    SpawnRadius        = JsonConfigReader.ReadFloat(root, "SpawnRadius",        def.SpawnRadius),
                    SpawnHeight        = JsonConfigReader.ReadFloat(root, "SpawnHeight",        def.SpawnHeight),
                    WindAdvectionScale = JsonConfigReader.ReadFloat(root, "WindAdvectionScale", def.WindAdvectionScale),
                    WindDriftFraction  = JsonConfigReader.ReadFloat(root, "WindDriftFraction",  def.WindDriftFraction),
                    SwayAmplitude      = JsonConfigReader.ReadFloat(root, "SwayAmplitude",      def.SwayAmplitude),
                    SwayFrequencyRad   = JsonConfigReader.ReadFloat(root, "SwayFrequencyRad",   def.SwayFrequencyRad),
                    InitialEnabled     = JsonConfigReader.ReadBool (root, "InitialEnabled",     def.InitialEnabled),
                    RandomSeed         = JsonConfigReader.ReadInt  (root, "RandomSeed",         def.RandomSeed),
                    MaxDeltaSeconds    = JsonConfigReader.ReadFloat(root, "MaxDeltaSeconds",    def.MaxDeltaSeconds),
                };

                Console.WriteLine($"[3DCUO] LeafFallServiceConfig loaded from {fullPath}");
                return new LeafFallServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] LeafFallServiceConfig load FAILED: {ex} — falling back to defaults");
                return new LeafFallServiceConfigLoadResult(false, ex.Message, fullPath, LeafFallServiceConfig.Default);
            }
        }
    }
}
