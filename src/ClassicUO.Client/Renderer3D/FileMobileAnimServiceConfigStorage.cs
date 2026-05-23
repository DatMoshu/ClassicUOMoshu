// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IMobileAnimServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileMobileAnimServiceConfigStorage : IMobileAnimServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/mobile-anim-defaults.json";

        public MobileAnimServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] MobileAnimServiceConfig: {fullPath} missing — falling back to defaults");
                return new MobileAnimServiceConfigLoadResult(false, "file missing", fullPath, MobileAnimServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                MobileAnimServiceConfig def = MobileAnimServiceConfig.Default;
                MobileAnimServiceConfig cfg = new MobileAnimServiceConfig
                {
                    StaleEntrySeconds             = JsonConfigReader.ReadFloat(r, "StaleEntrySeconds",             def.StaleEntrySeconds),
                    MaxTrackedEntries             = JsonConfigReader.ReadInt  (r, "MaxTrackedEntries",             def.MaxTrackedEntries),
                    EvictionFrameInterval         = JsonConfigReader.ReadInt  (r, "EvictionFrameInterval",         def.EvictionFrameInterval),
                    WalkingMotionThresholdSeconds = JsonConfigReader.ReadFloat(r, "WalkingMotionThresholdSeconds", def.WalkingMotionThresholdSeconds),
                    MotionDeltaSqEpsilon          = JsonConfigReader.ReadFloat(r, "MotionDeltaSqEpsilon",          def.MotionDeltaSqEpsilon),
                };
                Console.WriteLine($"[3DCUO] MobileAnimServiceConfig loaded from {fullPath}");
                return new MobileAnimServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] MobileAnimServiceConfig load FAILED: {ex} — falling back to defaults");
                return new MobileAnimServiceConfigLoadResult(false, ex.Message, fullPath, MobileAnimServiceConfig.Default);
            }
        }
    }
}
