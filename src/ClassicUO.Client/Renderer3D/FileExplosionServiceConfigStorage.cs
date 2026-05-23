// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IExplosionServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileExplosionServiceConfigStorage : IExplosionServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/explosion-defaults.json";

        public ExplosionServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] ExplosionServiceConfig: {fullPath} missing — falling back to defaults");
                return new ExplosionServiceConfigLoadResult(false, "file missing", fullPath, ExplosionServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                ExplosionServiceConfig def = ExplosionServiceConfig.Default;
                ExplosionServiceConfig cfg = new ExplosionServiceConfig
                {
                    InitialEnabled        = JsonConfigReader.ReadBool (r, "InitialEnabled",        def.InitialEnabled),
                    MaxEvents             = JsonConfigReader.ReadInt  (r, "MaxEvents",             def.MaxEvents),
                    BendAttackSeconds     = JsonConfigReader.ReadFloat(r, "BendAttackSeconds",     def.BendAttackSeconds),
                    BendDurationSeconds   = JsonConfigReader.ReadFloat(r, "BendDurationSeconds",   def.BendDurationSeconds),
                    BendDecayRate         = JsonConfigReader.ReadFloat(r, "BendDecayRate",         def.BendDecayRate),
                    LeavesHiddenSeconds   = JsonConfigReader.ReadFloat(r, "LeavesHiddenSeconds",   def.LeavesHiddenSeconds),
                    BendStrengthPx        = JsonConfigReader.ReadFloat(r, "BendStrengthPx",        def.BendStrengthPx),
                    CoreFalloffFraction   = JsonConfigReader.ReadFloat(r, "CoreFalloffFraction",   def.CoreFalloffFraction),
                    MinEventRadius        = JsonConfigReader.ReadFloat(r, "MinEventRadius",        def.MinEventRadius),
                    MinEventStrength      = JsonConfigReader.ReadFloat(r, "MinEventStrength",      def.MinEventStrength),
                    ZeroDirectionDistance = JsonConfigReader.ReadFloat(r, "ZeroDirectionDistance", def.ZeroDirectionDistance),
                };
                Console.WriteLine($"[3DCUO] ExplosionServiceConfig loaded from {fullPath}");
                return new ExplosionServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] ExplosionServiceConfig load FAILED: {ex} — falling back to defaults");
                return new ExplosionServiceConfigLoadResult(false, ex.Message, fullPath, ExplosionServiceConfig.Default);
            }
        }
    }
}
