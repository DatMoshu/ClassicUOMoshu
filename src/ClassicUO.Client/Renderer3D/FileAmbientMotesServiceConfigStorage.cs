// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IAmbientMotesServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileAmbientMotesServiceConfigStorage : IAmbientMotesServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/ambient-motes-defaults.json";

        public AmbientMotesServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] AmbientMotesServiceConfig: {fullPath} missing — falling back to defaults");
                return new AmbientMotesServiceConfigLoadResult(false, "file missing", fullPath, AmbientMotesServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                AmbientMotesServiceConfig def = AmbientMotesServiceConfig.Default;
                AmbientMotesServiceConfig cfg = new AmbientMotesServiceConfig
                {
                    InitialEnabled     = JsonConfigReader.ReadBool  (r, "InitialEnabled",     def.InitialEnabled),
                    InitialPalette     = JsonConfigReader.ReadString(r, "InitialPalette",     def.InitialPalette),
                    UseSoftGlow        = JsonConfigReader.ReadBool  (r, "UseSoftGlow",        def.UseSoftGlow),
                    Radius             = JsonConfigReader.ReadFloat (r, "Radius",             def.Radius),
                    MinHeight          = JsonConfigReader.ReadFloat (r, "MinHeight",          def.MinHeight),
                    MaxHeight          = JsonConfigReader.ReadFloat (r, "MaxHeight",          def.MaxHeight),
                    TargetAlive        = JsonConfigReader.ReadInt   (r, "TargetAlive",        def.TargetAlive),
                    SpawnRatePerSecond = JsonConfigReader.ReadFloat (r, "SpawnRatePerSecond", def.SpawnRatePerSecond),
                    PerTickSpawnCap    = JsonConfigReader.ReadInt   (r, "PerTickSpawnCap",    def.PerTickSpawnCap),
                    MaxDeltaSeconds    = JsonConfigReader.ReadFloat (r, "MaxDeltaSeconds",    def.MaxDeltaSeconds),
                    DriftUp            = JsonConfigReader.ReadFloat (r, "DriftUp",            def.DriftUp),
                    SwayHorizontalMax  = JsonConfigReader.ReadFloat (r, "SwayHorizontalMax",  def.SwayHorizontalMax),
                    LifetimeMin        = JsonConfigReader.ReadFloat (r, "LifetimeMin",        def.LifetimeMin),
                    LifetimeMax        = JsonConfigReader.ReadFloat (r, "LifetimeMax",        def.LifetimeMax),
                    SizeStart          = JsonConfigReader.ReadFloat (r, "SizeStart",          def.SizeStart),
                    SizeEnd            = JsonConfigReader.ReadFloat (r, "SizeEnd",            def.SizeEnd),
                    RandomSeed         = JsonConfigReader.ReadInt   (r, "RandomSeed",         def.RandomSeed),
                };
                Console.WriteLine($"[3DCUO] AmbientMotesServiceConfig loaded from {fullPath}");
                return new AmbientMotesServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] AmbientMotesServiceConfig load FAILED: {ex} — falling back to defaults");
                return new AmbientMotesServiceConfigLoadResult(false, ex.Message, fullPath, AmbientMotesServiceConfig.Default);
            }
        }
    }
}
