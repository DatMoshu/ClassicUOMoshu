// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWallNeighborClassifierConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileWallNeighborClassifierConfigStorage : IWallNeighborClassifierConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/wall-neighbor-classifier-defaults.json";

        public WallNeighborClassifierConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] WallNeighborClassifierConfig: {fullPath} missing — falling back to defaults");
                return new WallNeighborClassifierConfigLoadResult(false, "file missing", fullPath, WallNeighborClassifierConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                WallNeighborClassifierConfig def = WallNeighborClassifierConfig.Default;
                // Struct has positional ctor; reconstruct from parsed values.
                int zTol = JsonConfigReader.ReadInt(r, "ZTolerance", def.ZTolerance);
                bool enabled = JsonConfigReader.ReadBool(r, "Enabled", def.Enabled);
                WallNeighborClassifierConfig cfg = new WallNeighborClassifierConfig(zTol, enabled);
                Console.WriteLine($"[3DCUO] WallNeighborClassifierConfig loaded from {fullPath}");
                return new WallNeighborClassifierConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] WallNeighborClassifierConfig load FAILED: {ex} — falling back to defaults");
                return new WallNeighborClassifierConfigLoadResult(false, ex.Message, fullPath, WallNeighborClassifierConfig.Default);
            }
        }
    }
}
