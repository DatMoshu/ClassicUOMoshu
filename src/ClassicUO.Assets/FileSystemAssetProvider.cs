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

        private readonly string _cachePath;

        public FileSystemAssetProvider(string basePath)
        {
            _overridesPath = Path.Combine(basePath, "Overrides");
            _cachePath = Path.Combine(_overridesPath, "Cache");
            
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }

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

            // Check for cached DDS
            string ddsName = $"{Path.GetFileNameWithoutExtension(path)}.dds";
            string ddsPath = Path.Combine(_cachePath, ddsName);

            if (File.Exists(ddsPath))
            {
                // Check if DDS is newer than source
                DateTime ddsTime = File.GetLastWriteTimeUtc(ddsPath);
                DateTime srcTime = File.GetLastWriteTimeUtc(path);

                if (ddsTime >= srcTime)
                {
                    if (TryLoadDDS(ddsPath, out asset))
                    {
                        // Load metadata from source JSON if exists (DDS doesn't store our custom metadata)
                        LoadMetadata(path, ref asset);
                        return true;
                    }
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

                            LoadMetadata(path, ref asset);

                            // Save to DDS Cache
                            SaveToDDS(ddsPath, asset);

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

        private void LoadMetadata(string path, ref AssetData asset)
        {
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
        }

        private bool TryLoadDDS(string path, out AssetData asset)
        {
            asset = default;
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    uint magic = br.ReadUInt32();
                    if (magic != 0x20534444) // 'DDS '
                        return false;

                    // DDS_HEADER
                    uint size = br.ReadUInt32();
                    uint flags = br.ReadUInt32();
                    uint height = br.ReadUInt32();
                    uint width = br.ReadUInt32();
                    uint pitchOrLinearSize = br.ReadUInt32();
                    uint depth = br.ReadUInt32();
                    uint mipMapCount = br.ReadUInt32();
                    fs.Seek(44, SeekOrigin.Current); // Reserved1[11]

                    // DDS_PIXELFORMAT
                    uint pfSize = br.ReadUInt32();
                    uint pfFlags = br.ReadUInt32();
                    uint fourCC = br.ReadUInt32();
                    uint rgbBitCount = br.ReadUInt32();
                    uint rBitMask = br.ReadUInt32();
                    uint gBitMask = br.ReadUInt32();
                    uint bBitMask = br.ReadUInt32();
                    uint aBitMask = br.ReadUInt32();

                    // Caps
                    fs.Seek(16 + 4 + 4 + 4, SeekOrigin.Current); // Caps, Caps2, Caps3, Caps4, Reserved2

                    // We only support uncompressed A8R8G8B8 for now
                    if (pfFlags == 0x41 && rgbBitCount == 32 && rBitMask == 0x00FF0000 && bBitMask == 0x000000FF)
                    {
                         // Standard BGRA (A8R8G8B8)
                    }
                    else
                    {
                        return false; // Unsupported format
                    }

                    int pixelCount = (int)(width * height);
                    var pixels = new uint[pixelCount];
                    var byteData = br.ReadBytes(pixelCount * 4);
                    
                    // Convert BGRA bytes to uint (AABBGGRR in little endian is 0xAABBGGRR, but we need 0xAABBGGRR)
                    // Wait, StbImage returns RGBA. ClassicUO uses uints which are usually 0xAABBGGRR (little endian) -> R G B A in memory?
                    // Let's assume standard XNA/MonoGame Texture2D data order.
                    // Actually, let's just read as uints.
                    
                    MemoryMarshal.Cast<byte, uint>(byteData).CopyTo(pixels);

                    // If the saved data was BGRA, and we read it as uint on little endian:
                    // B G R A -> 0xAARRGGBB. 
                    // StbImage returns RGBA -> 0xAABBGGRR.
                    // We need to match what StbImage returns.
                    // If we save as BGRA, we read as BGRA.
                    // Let's ensure SaveToDDS saves as BGRA (standard DDS).
                    // And StbImage returns RGBA. So we need to swap R and B.

                    for (int i = 0; i < pixels.Length; i++)
                    {
                        uint p = pixels[i];
                        // 0xAARRGGBB (BGRA in memory) -> 0xAABBGGRR (RGBA in memory)
                        // Swap R and B
                        uint a = p & 0xFF000000;
                        uint r = (p & 0x00FF0000) >> 16;
                        uint g = p & 0x0000FF00;
                        uint b = p & 0x000000FF;
                        
                        pixels[i] = a | (b << 16) | g | r;
                    }

                    asset = new AssetData
                    {
                        Pixels = pixels,
                        Width = (int)width,
                        Height = (int)height
                    };

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SaveToDDS(string path, AssetData asset)
        {
            try
            {
                using (var fs = File.Create(path))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(0x20534444); // 'DDS '
                    
                    // DDS_HEADER
                    bw.Write(124); // dwSize
                    bw.Write(0x00001007); // dwFlags (CAPS | HEIGHT | WIDTH | PIXELFORMAT)
                    bw.Write((uint)asset.Height);
                    bw.Write((uint)asset.Width);
                    bw.Write((uint)(asset.Width * 4)); // dwPitchOrLinearSize
                    bw.Write(0); // dwDepth
                    bw.Write(0); // dwMipMapCount
                    for (int i = 0; i < 11; i++) bw.Write(0); // dwReserved1

                    // DDS_PIXELFORMAT
                    bw.Write(32); // dwSize
                    bw.Write(0x00000041); // dwFlags (RGB | ALPHAPIXELS)
                    bw.Write(0); // dwFourCC
                    bw.Write(32); // dwRGBBitCount
                    bw.Write(0x00FF0000); // dwRBitMask (Red)
                    bw.Write(0x0000FF00); // dwGBitMask (Green)
                    bw.Write(0x000000FF); // dwBBitMask (Blue)
                    bw.Write(0xFF000000); // dwABitMask (Alpha)

                    // DDS_CAPS
                    bw.Write(0x00001000); // dwCaps (TEXTURE)
                    bw.Write(0); // dwCaps2
                    bw.Write(0); // dwCaps3
                    bw.Write(0); // dwCaps4
                    bw.Write(0); // dwReserved2

                    // Pixel Data
                    // Convert RGBA (0xAABBGGRR) to BGRA (0xAARRGGBB) for standard DDS
                    var pixels = asset.Pixels;
                    var bytes = new byte[pixels.Length * 4];
                    
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        uint p = pixels[i];
                        // 0xAABBGGRR -> 0xAARRGGBB
                        byte a = (byte)((p & 0xFF000000) >> 24);
                        byte b = (byte)((p & 0x00FF0000) >> 16);
                        byte g = (byte)((p & 0x0000FF00) >> 8);
                        byte r = (byte)(p & 0x000000FF);

                        int idx = i * 4;
                        bytes[idx] = b;
                        bytes[idx + 1] = g;
                        bytes[idx + 2] = r;
                        bytes[idx + 3] = a;
                    }

                    bw.Write(bytes);
                }
            }
            catch
            {
                // Ignore save errors
            }
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
