// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IMobileOutfitServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileMobileOutfitServiceConfigStorage : IMobileOutfitServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/mobile-outfit-defaults.json";

        public MobileOutfitServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] MobileOutfitServiceConfig: {fullPath} missing — falling back to defaults");
                return new MobileOutfitServiceConfigLoadResult(false, "file missing", fullPath, MobileOutfitServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                MobileOutfitServiceConfig def = MobileOutfitServiceConfig.Default;
                // OutfitSlots array stays at Default — array loader deferred.
                uint mixer = def.SerialSeedMixer;
                if (r.TryGetProperty("SerialSeedMixer", out JsonElement m) && m.ValueKind == JsonValueKind.Number)
                {
                    mixer = m.GetUInt32();
                }
                MobileOutfitServiceConfig cfg = new MobileOutfitServiceConfig { SerialSeedMixer = mixer };
                Console.WriteLine($"[3DCUO] MobileOutfitServiceConfig loaded from {fullPath}");
                return new MobileOutfitServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] MobileOutfitServiceConfig load FAILED: {ex} — falling back to defaults");
                return new MobileOutfitServiceConfigLoadResult(false, ex.Message, fullPath, MobileOutfitServiceConfig.Default);
            }
        }
    }
}
