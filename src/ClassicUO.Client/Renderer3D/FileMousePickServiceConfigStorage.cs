// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IMousePickServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileMousePickServiceConfigStorage : IMousePickServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/mouse-pick-defaults.json";

        public MousePickServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] MousePickServiceConfig: {fullPath} missing — falling back to defaults");
                return new MousePickServiceConfigLoadResult(false, "file missing", fullPath, MousePickServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                MousePickServiceConfig def = MousePickServiceConfig.Default;
                float tile = JsonConfigReader.ReadFloat(r, "TileSize",        def.TileSize);
                float zS  = JsonConfigReader.ReadFloat(r, "ZScale",          def.ZScale);
                float eps = JsonConfigReader.ReadFloat(r, "ParallelEpsilon", def.ParallelEpsilon);
                MousePickServiceConfig cfg = new MousePickServiceConfig(tile, zS, eps);
                Console.WriteLine($"[3DCUO] MousePickServiceConfig loaded from {fullPath}");
                return new MousePickServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] MousePickServiceConfig load FAILED: {ex} — falling back to defaults");
                return new MousePickServiceConfigLoadResult(false, ex.Message, fullPath, MousePickServiceConfig.Default);
            }
        }
    }
}
