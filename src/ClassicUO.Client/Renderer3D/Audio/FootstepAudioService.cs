// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using System;
using System.IO;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Production implementation of <see cref="IFootstepAudioService"/>. Material resolution
    /// + filename construction; voice management + file I/O delegated to <see cref="IFootstepAudioPlayer"/>.
    /// </summary>
    public sealed class FootstepAudioService : IFootstepAudioService
    {
        private readonly FootstepAudioServiceConfig _config;
        private readonly IFootstepAudioPlayer _player;
        private readonly IWeatherService _weather;
        private readonly Random _rng;

        private bool _enabled;
        private float _volume;
        private bool _autoUseSnowInWinter;
        private FootstepMaterial _overrideMaterial;
        private FootstepMaterial _defaultMaterial;
        private FootwearKind _footwear;
        private FootstepMaterial _lastMaterial = FootstepMaterial.Grass;
        private int _lastPlayCount;

        public FootstepAudioService(
            FootstepAudioServiceConfig config,
            IFootstepAudioPlayer player,
            IWeatherService weather)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _weather = weather ?? throw new ArgumentNullException(nameof(weather));

            _enabled = _config.InitialEnabled;
            _volume = _config.InitialVolume;
            _autoUseSnowInWinter = _config.InitialAutoUseSnowInWinter;
            _overrideMaterial = _config.InitialOverrideMaterial;
            _defaultMaterial = _config.InitialDefaultMaterial;
            _footwear = _config.InitialFootwear;
            _rng = new Random(_config.RandomSeed);
        }

        // ===== IFootstepAudioService — read =====

        public bool Enabled => _enabled;
        public float Volume => _volume;
        public bool AutoUseSnowInWinter => _autoUseSnowInWinter;
        public FootstepMaterial OverrideMaterial => _overrideMaterial;
        public FootstepMaterial DefaultMaterial => _defaultMaterial;
        public FootwearKind Footwear => _footwear;
        public FootstepMaterial LastMaterial => _lastMaterial;
        public int LastPlayCount => _lastPlayCount;

        // ===== IFootstepAudioService — mutate =====

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetVolume(float volume) => _volume = volume < 0f ? 0f : (volume > 1f ? 1f : volume);
        public void SetAutoUseSnowInWinter(bool enabled) => _autoUseSnowInWinter = enabled;
        public void SetOverrideMaterial(FootstepMaterial m) => _overrideMaterial = m;
        public void SetDefaultMaterial(FootstepMaterial m) => _defaultMaterial = m;
        public void SetFootwear(FootwearKind k) => _footwear = k;

        // ===== Material resolution =====

        public FootstepMaterial ResolveMaterial()
        {
            if (_overrideMaterial != FootstepMaterial.Auto) return _overrideMaterial;
            if (_autoUseSnowInWinter && _weather.Type == WeatherKind.Snow) return FootstepMaterial.Snow;
            return _defaultMaterial;
        }

        // ===== TryPlayStep =====

        public bool TryPlayStep(int x, int y, bool running, bool mounted)
        {
            if (!_enabled) return false;
            if (mounted) return false; // mounts use their own sound

            FootstepMaterial mat = ResolveMaterial();
            if (!_config.FolderNames.TryGetValue(mat, out string folder)) return false;

            string path = PickPath(folder, running);
            if (path == null) return false;

            float pitch = ((float)_rng.NextDouble() - 0.5f) * _config.PitchJitter;
            if (!_player.Play(path, _volume, pitch)) return false;

            _lastPlayCount++;
            _lastMaterial = mat;
            return true;
        }

        // ===== Internals =====

        private string PickPath(string folder, bool running)
        {
            string prefix = (_footwear == FootwearKind.Shoe ? "Shoe" : "Bare") + " Step";
            string hardness = running ? "Hard" : ((_rng.Next(2) == 0) ? "Medium" : "Soft");
            char letter = (char)('A' + _rng.Next(Math.Max(1, _config.Variants)));
            string materialNameInFile = FolderToFilenameMaterial(folder);

            // Primary candidate.
            string primary = BuildPath(folder, $"{prefix} {materialNameInFile} {hardness} {letter}.wav");
            if (_player.Exists(primary)) return primary;

            // Fallback 1: opposite footwear.
            string altPrefix = (_footwear == FootwearKind.Shoe ? "Bare" : "Shoe") + " Step";
            string alt1 = BuildPath(folder, $"{altPrefix} {materialNameInFile} {hardness} {letter}.wav");
            if (_player.Exists(alt1)) return alt1;

            // Fallback 2: Hard hardness on the original footwear.
            string alt2 = BuildPath(folder, $"{prefix} {materialNameInFile} Hard {letter}.wav");
            if (_player.Exists(alt2)) return alt2;

            return null;
        }

        private string BuildPath(string folder, string filename)
            => Path.Combine(_config.PackSubPath, folder, filename);

        /// <summary>
        /// The Ovani pack uses the folder name as the material in filenames EXCEPT for
        /// "Water or Mud" which appears as "Water" in the file. Public-static for tests.
        /// </summary>
        public static string FolderToFilenameMaterial(string folder)
            => folder == "Water or Mud" ? "Water" : folder;
    }
}
