// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IWindServiceConfigStorage backed by
// Data/renderer3d/wind-defaults.json. Hand-parses via JsonDocument to stay AOT-clean
// per BLOCKER-05/-06 of the original code review.

using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IWindServiceConfigStorage"/>. Reads the JSON config relative
    /// to the build output. Missing file / malformed JSON → <see cref="WindServiceConfigLoadResult.Success"/>
    /// false and an explanatory <see cref="WindServiceConfigLoadResult.ErrorMessage"/>;
    /// <see cref="WindServiceConfigLoadResult.Config"/> is always populated (legacy defaults
    /// on failure) so callers don't have to null-check.
    /// </summary>
    internal sealed class FileWindServiceConfigStorage : IWindServiceConfigStorage
    {
        public string ConfigPath { get; set; } = "Data/renderer3d/wind-defaults.json";

        public WindServiceConfigLoadResult Load()
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, ConfigPath);
            if (!File.Exists(fullPath))
            {
                string err = $"wind-defaults.json not found at {fullPath}";
                Console.WriteLine($"[3DCUO] WindServiceConfig: {err} — falling back to defaults");
                return new WindServiceConfigLoadResult(false, err, fullPath, WindServiceConfig.Default);
            }

            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;

                WindServiceConfig def = WindServiceConfig.Default;
                WindServiceConfig cfg = new WindServiceConfig
                {
                    InitialStrength          = ReadFloat(root, "InitialStrength",          def.InitialStrength),
                    InitialDirectionDeg      = ReadFloat(root, "InitialDirectionDeg",      def.InitialDirectionDeg),
                    BaseFrequencyHz          = ReadFloat(root, "BaseFrequencyHz",          def.BaseFrequencyHz),
                    WeatherParticleAdvection = ReadFloat(root, "WeatherParticleAdvection", def.WeatherParticleAdvection),
                    LinkToWeather            = ReadBool (root, "LinkToWeather",            def.LinkToWeather),
                    GustChangeMin            = ReadFloat(root, "GustChangeMin",            def.GustChangeMin),
                    GustChangeMax            = ReadFloat(root, "GustChangeMax",            def.GustChangeMax),
                    GustStrengthMin          = ReadFloat(root, "GustStrengthMin",          def.GustStrengthMin),
                    GustStrengthMax          = ReadFloat(root, "GustStrengthMax",          def.GustStrengthMax),
                    GustDirectionRangeDeg    = ReadFloat(root, "GustDirectionRangeDeg",    def.GustDirectionRangeDeg),
                    GustLerpSpeed            = ReadFloat(root, "GustLerpSpeed",            def.GustLerpSpeed),
                    StormStrengthFloor       = ReadFloat(root, "StormStrengthFloor",       def.StormStrengthFloor),
                    StormStrengthCeiling     = ReadFloat(root, "StormStrengthCeiling",     def.StormStrengthCeiling),
                    StormCadenceMultiplier   = ReadFloat(root, "StormCadenceMultiplier",   def.StormCadenceMultiplier),
                    StormCadenceFloorSeconds = ReadFloat(root, "StormCadenceFloorSeconds", def.StormCadenceFloorSeconds),
                    RandomSeed               = ReadInt  (root, "RandomSeed",               def.RandomSeed),
                };

                Console.WriteLine($"[3DCUO] WindServiceConfig loaded from {fullPath}");
                return new WindServiceConfigLoadResult(true, null, fullPath, cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] WindServiceConfig load FAILED: {ex} — falling back to defaults");
                return new WindServiceConfigLoadResult(false, ex.Message, fullPath, WindServiceConfig.Default);
            }
        }

        private static float ReadFloat(JsonElement root, string name, float fallback)
            => root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Number
                ? el.GetSingle()
                : fallback;

        private static int ReadInt(JsonElement root, string name, int fallback)
            => root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Number
                ? el.GetInt32()
                : fallback;

        private static bool ReadBool(JsonElement root, string name, bool fallback)
            => root.TryGetProperty(name, out JsonElement el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
                ? el.GetBoolean()
                : fallback;
    }
}
