// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for INukeShowServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileNukeShowServiceConfigStorage : INukeShowServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/nuke-show-defaults.json";

        public NukeShowServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] NukeShowServiceConfig: {fullPath} missing — falling back to defaults");
                return new NukeShowServiceConfigLoadResult(false, "file missing", fullPath, NukeShowServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                NukeShowServiceConfig def = NukeShowServiceConfig.Default;
                NukeShowServiceConfig cfg = new NukeShowServiceConfig
                {
                    InitialEnabled        = JsonConfigReader.ReadBool  (r, "InitialEnabled",        def.InitialEnabled),
                    InitialVerboseLog     = JsonConfigReader.ReadBool  (r, "InitialVerboseLog",     def.InitialVerboseLog),
                    InitialBarrageCount   = JsonConfigReader.ReadInt   (r, "InitialBarrageCount",   def.InitialBarrageCount),
                    InitialBarrageRadius  = JsonConfigReader.ReadFloat (r, "InitialBarrageRadius",  def.InitialBarrageRadius),
                    InitialStagger        = JsonConfigReader.ReadFloat (r, "InitialStagger",        def.InitialStagger),
                    InitialSingleDistance = JsonConfigReader.ReadFloat (r, "InitialSingleDistance", def.InitialSingleDistance),
                    InitialNukeScale      = JsonConfigReader.ReadFloat (r, "InitialNukeScale",      def.InitialNukeScale),
                    InitialBlastRadius    = JsonConfigReader.ReadFloat (r, "InitialBlastRadius",    def.InitialBlastRadius),
                    UoExplosionSoundId    = JsonConfigReader.ReadInt   (r, "UoExplosionSoundId",    def.UoExplosionSoundId),
                    DDayAudioPath         = JsonConfigReader.ReadString(r, "DDayAudioPath",         def.DDayAudioPath),
                    InitialPlayDDayAudio  = JsonConfigReader.ReadBool  (r, "InitialPlayDDayAudio",  def.InitialPlayDDayAudio),
                    DDayVolume            = JsonConfigReader.ReadFloat (r, "DDayVolume",            def.DDayVolume),
                    FlashTextureName      = JsonConfigReader.ReadString(r, "FlashTextureName",      def.FlashTextureName),
                    MaxDeltaSeconds       = JsonConfigReader.ReadFloat (r, "MaxDeltaSeconds",       def.MaxDeltaSeconds),
                    TailSeconds           = JsonConfigReader.ReadFloat (r, "TailSeconds",           def.TailSeconds),
                    RandomSeed            = JsonConfigReader.ReadInt   (r, "RandomSeed",            def.RandomSeed),
                };
                Console.WriteLine($"[3DCUO] NukeShowServiceConfig loaded from {fullPath}");
                return new NukeShowServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] NukeShowServiceConfig load FAILED: {ex} — falling back to defaults");
                return new NukeShowServiceConfigLoadResult(false, ex.Message, fullPath, NukeShowServiceConfig.Default);
            }
        }
    }
}
