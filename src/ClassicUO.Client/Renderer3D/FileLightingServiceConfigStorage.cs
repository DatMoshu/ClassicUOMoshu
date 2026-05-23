// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for ILightingServiceConfigStorage backed by
// Data/renderer3d/lighting-defaults.json. Session 68 of ADR-012 Phase 4.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileLightingServiceConfigStorage : ILightingServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/lighting-defaults.json";

        public LightingServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"lighting-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] LightingServiceConfig: {err} — falling back to defaults");
                return new LightingServiceConfigLoadResult(false, err, fullPath, LightingServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                LightingServiceConfig def = LightingServiceConfig.Default;
                LightingServiceConfig cfg = new LightingServiceConfig
                {
                    LegacyHardcodedDir        = JsonConfigReader.ReadVector3(root, "LegacyHardcodedDir",        def.LegacyHardcodedDir),
                    MaxElevation              = JsonConfigReader.ReadFloat  (root, "MaxElevation",              def.MaxElevation),
                    InitialEnabled            = JsonConfigReader.ReadBool   (root, "InitialEnabled",            def.InitialEnabled),
                    InitialAutoCycle          = JsonConfigReader.ReadBool   (root, "InitialAutoCycle",          def.InitialAutoCycle),
                    InitialTimeOfDay          = JsonConfigReader.ReadFloat  (root, "InitialTimeOfDay",          def.InitialTimeOfDay),
                    InitialCyclePeriodSeconds = JsonConfigReader.ReadFloat  (root, "InitialCyclePeriodSeconds", def.InitialCyclePeriodSeconds),
                };

                Console.WriteLine($"[3DCUO] LightingServiceConfig loaded from {fullPath}");
                return new LightingServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] LightingServiceConfig load FAILED: {ex} — falling back to defaults");
                return new LightingServiceConfigLoadResult(false, ex.Message, fullPath, LightingServiceConfig.Default);
            }
        }
    }
}
