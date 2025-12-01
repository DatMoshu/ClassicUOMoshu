using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClassicUO.Utility.Logging;
using StbImageSharp;

namespace ClassicUO.Assets
{
    public partial class FileSystemAssetProvider : IAssetProvider, IDisposable
    {
        private readonly string _overridesPath;
        private readonly Dictionary<long, string> _assetMap = new Dictionary<long, string>();
        private FileSystemWatcher _watcher;

        public FileSystemAssetProvider(string basePath)
        {
          _overridesPath = Path.Combine(basePath, "Overrides");
            IndexFiles();
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            if (!Directory.Exists(_overridesPath))
            {
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(_overridesPath);
                _watcher.IncludeSubdirectories = true;
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                _watcher.Filter = "*.*"; // Watch all, filter in event

                _watcher.Created += OnFileChanged;
                _watcher.Changed += OnFileChanged;
                _watcher.Deleted += OnFileDeleted;
                _watcher.Renamed += OnFileRenamed;

                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to setup FileSystemWatcher: {ex.Message}");
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            HandleFileChange(e.OldFullPath, true);
            HandleFileChange(e.FullPath, false);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath, true);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath, false);
        }

        public event Action<int, AssetType> AssetChanged;

        private void HandleFileChange(string fullPath, bool isDelete)
        {
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (ext != ".png" && ext != ".tga" && ext != ".json")
            {
                return;
            }

            var fileName = Path.GetFileNameWithoutExtension(fullPath);
            if (fileName.StartsWith("0x") && int.TryParse(fileName.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int id))
            {
                // Determine type based on path
                AssetType? type = null;
                if (fullPath.Contains(Path.Combine("Art", "Land")))
                {
                    type = AssetType.Land;
                }
                else if (fullPath.Contains(Path.Combine("Art", "Statics")))
                {
                    type = AssetType.Static;
                }

                if (type.HasValue)
                {
                    // If it's a JSON file, we don't update the map, but we still fire the event
                    // to trigger a reload (which will read the new JSON).
                    if (ext != ".json")
                    {
                        long key = GetKey(type.Value, id);
                        lock (_assetMap)
                        {
                            if (isDelete)
                            {
                                if (_assetMap.ContainsKey(key) && _assetMap[key] == fullPath)
                                {
                                    _assetMap.Remove(key);
                                }
                            }
                            else
                            {
                                _assetMap[key] = fullPath;
                            }
                        }
                    }

                    AssetChanged?.Invoke(id, type.Value);
                }
            }
        }

        private void IndexFiles()
        {
            if (!Directory.Exists(_overridesPath))
            {
                return;
            }

            var artPath = Path.Combine(_overridesPath, "Art");
            if (Directory.Exists(artPath))
            {
                IndexDirectory(Path.Combine(artPath, "Land"), AssetType.Land);
                IndexDirectory(Path.Combine(artPath, "Statics"), AssetType.Static);
            }

            // Add other types as needed
        }

        private void IndexDirectory(string path, AssetType type)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.png", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("0x") && int.TryParse(fileName.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int id))
                {
                    long key = GetKey(type, id);
                    lock (_assetMap)
                    {
                        _assetMap[key] = file;
                    }
                }
            }

            // Also support .tga? Plan says PNG/TGA.
            foreach (var file in Directory.EnumerateFiles(path, "*.tga", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("0x") && int.TryParse(fileName.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int id))
                {
                    long key = GetKey(type, id);
                    lock (_assetMap)
                    {
                        if (!_assetMap.ContainsKey(key)) // PNG takes precedence? Or TGA? Let's say PNG.
                        {
                            _assetMap[key] = file;
                        }
                    }
                }
            }
        }

        private long GetKey(AssetType type, int id)
        {
            return ((long)type << 32) | (uint)id;
        }

        public bool TryGetAsset(int assetId, AssetType type, out AssetData asset)
        {
            asset = default;
            long key = GetKey(type, assetId);
            string path;

            lock (_assetMap)
            {
                if (!_assetMap.TryGetValue(key, out path))
                {
                    return false;
                }
            }

            try
            {
                // Retry loop for file locking issues (common with FileSystemWatcher)
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using (var stream = File.OpenRead(path))
                        {
                            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                            var pixels = new uint[image.Width * image.Height];
                            var srcSpan = MemoryMarshal.Cast<byte, uint>(image.Data);
                            srcSpan.CopyTo(pixels);

                            asset = new AssetData
                            {
                                Pixels = pixels,
                                Width = image.Width,
                                Height = image.Height
                            };

                            // Try load JSON metadata
                            string jsonPath = Path.ChangeExtension(path, ".json");
                            if (File.Exists(jsonPath))
                            {
                                try
                                {
                                    string jsonString = File.ReadAllText(jsonPath);
                                    var metadata = JsonSerializer.Deserialize(jsonString, AssetMetadataContext.Default.AssetMetadata);

                                    if (metadata != null && metadata.Rendering != null)
                                    {
                                        if (metadata.Rendering.Pivot != null)
                                        {
                                            asset.PivotX = metadata.Rendering.Pivot.X;
                                            asset.PivotY = metadata.Rendering.Pivot.Y;
                                        }
                                        
                                        if (metadata.Rendering.Scale > 0)
                                        {
                                            asset.Scale = metadata.Rendering.Scale;
                                        }
                                    }

                                    if (metadata != null && metadata.Lighting != null)
                                    {
                                        asset.HasNormalMap = !string.IsNullOrEmpty(metadata.Lighting.NormalMap);
                                        asset.IsEmissive = !string.IsNullOrEmpty(metadata.Lighting.EmissionMap);
                                    }
                                }
                                catch
                                {
                                    // Ignore JSON errors
                                }
                            }

                            return true;
                        }
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(50); // Wait a bit
                    }
                }
            }
            catch (Exception)
            {
                // Log error?
                return false;
            }

            return false;
        }

        public bool HasAsset(int assetId, AssetType type)
        {
            long key = GetKey(type, assetId);
            lock (_assetMap)
            {
                return _assetMap.ContainsKey(key);
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }

        [System.Text.Json.Serialization.JsonSerializable(typeof(AssetMetadata))]
        private partial class AssetMetadataContext : System.Text.Json.Serialization.JsonSerializerContext
        {
        }

        private class AssetMetadata
        {
            public RenderingMetadata Rendering { get; set; }
            public LightingMetadata Lighting { get; set; }
        }

        private class RenderingMetadata
        {
            public PointMetadata Pivot { get; set; }
            public float Scale { get; set; }
        }

        private class PointMetadata
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        private class LightingMetadata
        {
            public string NormalMap { get; set; }
            public string EmissionMap { get; set; }
        }
    }
}
