// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IBuffParticleServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileBuffParticleServiceConfigStorage : IBuffParticleServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/buff-particle-defaults.json";

        public BuffParticleServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] BuffParticleServiceConfig: {fullPath} missing — falling back to defaults");
                return new BuffParticleServiceConfigLoadResult(false, "file missing", fullPath, BuffParticleServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                BuffParticleServiceConfig def = BuffParticleServiceConfig.Default;
                BuffParticleServiceConfig cfg = new BuffParticleServiceConfig
                {
                    InitialEnabled       = JsonConfigReader.ReadBool (r, "InitialEnabled",       def.InitialEnabled),
                    InitialRequire3DMode = JsonConfigReader.ReadBool (r, "InitialRequire3DMode", def.InitialRequire3DMode),
                    BodyLow              = JsonConfigReader.ReadFloat(r, "BodyLow",              def.BodyLow),
                    BodyMid              = JsonConfigReader.ReadFloat(r, "BodyMid",              def.BodyMid),
                    BodyTop              = JsonConfigReader.ReadFloat(r, "BodyTop",              def.BodyTop),
                    AuraRadius           = JsonConfigReader.ReadFloat(r, "AuraRadius",           def.AuraRadius),
                    IntervalFire         = JsonConfigReader.ReadFloat(r, "IntervalFire",         def.IntervalFire),
                    IntervalIce          = JsonConfigReader.ReadFloat(r, "IntervalIce",          def.IntervalIce),
                    IntervalHoly         = JsonConfigReader.ReadFloat(r, "IntervalHoly",         def.IntervalHoly),
                    IntervalCurse        = JsonConfigReader.ReadFloat(r, "IntervalCurse",        def.IntervalCurse),
                    IntervalPoison       = JsonConfigReader.ReadFloat(r, "IntervalPoison",       def.IntervalPoison),
                    IntervalLightning    = JsonConfigReader.ReadFloat(r, "IntervalLightning",    def.IntervalLightning),
                    IntervalStat         = JsonConfigReader.ReadFloat(r, "IntervalStat",         def.IntervalStat),
                    IntervalDefense      = JsonConfigReader.ReadFloat(r, "IntervalDefense",      def.IntervalDefense),
                    IntervalStealth      = JsonConfigReader.ReadFloat(r, "IntervalStealth",      def.IntervalStealth),
                    IntervalFormShift    = JsonConfigReader.ReadFloat(r, "IntervalFormShift",    def.IntervalFormShift),
                    IntervalWind         = JsonConfigReader.ReadFloat(r, "IntervalWind",         def.IntervalWind),
                    IntervalDebuff       = JsonConfigReader.ReadFloat(r, "IntervalDebuff",       def.IntervalDebuff),
                    IntervalDefault      = JsonConfigReader.ReadFloat(r, "IntervalDefault",      def.IntervalDefault),
                    MaxDeltaSeconds      = JsonConfigReader.ReadFloat(r, "MaxDeltaSeconds",      def.MaxDeltaSeconds),
                    RandomSeed           = JsonConfigReader.ReadInt  (r, "RandomSeed",           def.RandomSeed),
                };
                Console.WriteLine($"[3DCUO] BuffParticleServiceConfig loaded from {fullPath}");
                return new BuffParticleServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] BuffParticleServiceConfig load FAILED: {ex} — falling back to defaults");
                return new BuffParticleServiceConfigLoadResult(false, ex.Message, fullPath, BuffParticleServiceConfig.Default);
            }
        }
    }
}
