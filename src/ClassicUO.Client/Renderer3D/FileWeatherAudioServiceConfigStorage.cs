// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWeatherAudioServiceConfigStorage (session 69).

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Audio;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileWeatherAudioServiceConfigStorage : IWeatherAudioServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/weather-audio-defaults.json";

        public WeatherAudioServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[3DCUO] WeatherAudioServiceConfig: {fullPath} missing — falling back to defaults");
                return new WeatherAudioServiceConfigLoadResult(false, "file missing", fullPath, WeatherAudioServiceConfig.Default);
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement r = doc.RootElement;
                WeatherAudioServiceConfig def = WeatherAudioServiceConfig.Default;
                // AmbientLoops + ThunderOneShots stay at Default — collections deferred.
                WeatherAudioServiceConfig cfg = new WeatherAudioServiceConfig
                {
                    InitialEnabled        = JsonConfigReader.ReadBool (r, "InitialEnabled",        def.InitialEnabled),
                    InitialAmbientVolume  = JsonConfigReader.ReadFloat(r, "InitialAmbientVolume",  def.InitialAmbientVolume),
                    InitialThunderVolume  = JsonConfigReader.ReadFloat(r, "InitialThunderVolume",  def.InitialThunderVolume),
                    CrossfadeSeconds      = JsonConfigReader.ReadFloat(r, "CrossfadeSeconds",      def.CrossfadeSeconds),
                    VerboseLog            = JsonConfigReader.ReadBool (r, "VerboseLog",            def.VerboseLog),
                    RandomSeed            = JsonConfigReader.ReadInt  (r, "RandomSeed",            def.RandomSeed),
                    ThunderPitchJitter    = JsonConfigReader.ReadFloat(r, "ThunderPitchJitter",    def.ThunderPitchJitter),
                };
                Console.WriteLine($"[3DCUO] WeatherAudioServiceConfig loaded from {fullPath}");
                return new WeatherAudioServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] WeatherAudioServiceConfig load FAILED: {ex} — falling back to defaults");
                return new WeatherAudioServiceConfigLoadResult(false, ex.Message, fullPath, WeatherAudioServiceConfig.Default);
            }
        }
    }
}
