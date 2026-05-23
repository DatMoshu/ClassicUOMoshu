// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IParticleServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileParticleServiceConfigStorage : IParticleServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/particle-defaults.json";

        public ParticleServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] ParticleServiceConfig: {fullPath} missing — falling back to defaults");
                return new ParticleServiceConfigLoadResult(false, "file missing", fullPath, ParticleServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                ParticleServiceConfig def = ParticleServiceConfig.Default;
                ParticleServiceConfig cfg = new ParticleServiceConfig
                {
                    InitialEnabled    = JsonConfigReader.ReadBool(r, "InitialEnabled",    def.InitialEnabled),
                    InitialVerboseLog = JsonConfigReader.ReadBool(r, "InitialVerboseLog", def.InitialVerboseLog),
                };
                Console.WriteLine($"[3DCUO] ParticleServiceConfig loaded from {fullPath}");
                return new ParticleServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] ParticleServiceConfig load FAILED: {ex} — falling back to defaults");
                return new ParticleServiceConfigLoadResult(false, ex.Message, fullPath, ParticleServiceConfig.Default);
            }
        }
    }
}
