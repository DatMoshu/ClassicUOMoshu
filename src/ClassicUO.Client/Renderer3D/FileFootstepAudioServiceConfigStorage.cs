// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IFootstepAudioServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Audio;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileFootstepAudioServiceConfigStorage : IFootstepAudioServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/footstep-audio-defaults.json";

        public FootstepAudioServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] FootstepAudioServiceConfig: {fullPath} missing — falling back to defaults");
                return new FootstepAudioServiceConfigLoadResult(false, "file missing", fullPath, FootstepAudioServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                FootstepAudioServiceConfig def = FootstepAudioServiceConfig.Default;
                // FolderNames stays at Default — dictionary deferred.
                FootstepAudioServiceConfig cfg = new FootstepAudioServiceConfig
                {
                    InitialEnabled            = JsonConfigReader.ReadBool (r, "InitialEnabled",            def.InitialEnabled),
                    InitialVolume             = JsonConfigReader.ReadFloat(r, "InitialVolume",             def.InitialVolume),
                    VerboseLog                = JsonConfigReader.ReadBool (r, "VerboseLog",                def.VerboseLog),
                    InitialAutoUseSnowInWinter= JsonConfigReader.ReadBool (r, "InitialAutoUseSnowInWinter",def.InitialAutoUseSnowInWinter),
                    InitialOverrideMaterial   = JsonConfigReader.ReadEnum<FootstepMaterial>(r, "InitialOverrideMaterial", def.InitialOverrideMaterial),
                    InitialDefaultMaterial    = JsonConfigReader.ReadEnum<FootstepMaterial>(r, "InitialDefaultMaterial",  def.InitialDefaultMaterial),
                    InitialFootwear           = JsonConfigReader.ReadEnum<FootwearKind>    (r, "InitialFootwear",         def.InitialFootwear),
                    Variants                  = JsonConfigReader.ReadInt  (r, "Variants",                  def.Variants),
                    PitchJitter               = JsonConfigReader.ReadFloat(r, "PitchJitter",               def.PitchJitter),
                    RandomSeed                = JsonConfigReader.ReadInt  (r, "RandomSeed",                def.RandomSeed),
                    PackSubPath               = JsonConfigReader.ReadString(r, "PackSubPath",              def.PackSubPath),
                };
                Console.WriteLine($"[3DCUO] FootstepAudioServiceConfig loaded from {fullPath}");
                return new FootstepAudioServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] FootstepAudioServiceConfig load FAILED: {ex} — falling back to defaults");
                return new FootstepAudioServiceConfigLoadResult(false, ex.Message, fullPath, FootstepAudioServiceConfig.Default);
            }
        }
    }
}
