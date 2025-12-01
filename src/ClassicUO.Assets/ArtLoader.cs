// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Threading.Tasks;
using ClassicUO.IO;
using ClassicUO.Utility;

namespace ClassicUO.Assets
{
    public sealed class ArtLoader : UOFileLoader
    {
        private UOFile _file;
        private IAssetProvider[] _providers;
        public const int MAX_LAND_DATA_INDEX_COUNT = 0x4000;
        public const int MAX_STATIC_DATA_INDEX_COUNT = 0x14000;

        public ArtLoader(UOFileManager fileManager) : base(fileManager)
        {
        }


        public UOFile File => _file;


        public override void Load()
        {
            string filePath = FileManager.GetUOFilePath("artLegacyMUL.uop");

            if (FileManager.IsUOPInstallation && System.IO.File.Exists(filePath))
            {
                _file = new UOFileUop(filePath, "build/artlegacymul/{0:D8}.tga");
            }
            else
            {
                filePath = FileManager.GetUOFilePath("art.mul");
                string idxPath = FileManager.GetUOFilePath("artidx.mul");

                if (System.IO.File.Exists(filePath) && System.IO.File.Exists(idxPath))
                {
                    _file = new UOFileMul(filePath, idxPath);
                }
            }

            _file.FillEntries();

            _providers = new IAssetProvider[]
            {
                new FileSystemAssetProvider(FileManager.BasePath),
                new LegacyAssetProvider(_file)
            };

            foreach (var provider in _providers)
            {
                provider.AssetChanged += OnAssetChanged;
            }
        }

        public event Action<uint> AssetChanged;

        private void OnAssetChanged(int id, AssetType type)
        {
            uint idx = (uint)id;
            if (type == AssetType.Static)
            {
                idx += MAX_LAND_DATA_INDEX_COUNT;
            }

            AssetChanged?.Invoke(idx);
        }

        // public Rectangle GetRealArtBounds(int index) =>
        //     index + 0x4000 >= _spriteInfos.Length
        //         ? Rectangle.Empty
        //         : _spriteInfos[index + 0x4000].ArtBounds;

        private static void AddBlackBorder(Span<uint> pixels, int width, int height)
        {
            for (int yy = 0; yy < height; yy++)
            {
                int startY = yy != 0 ? -1 : 0;
                int endY = yy + 1 < height ? 2 : 1;

                for (int xx = 0; xx < width; xx++)
                {
                    ref uint pixel = ref pixels[yy * width + xx];

                    if (pixel == 0)
                    {
                        continue;
                    }

                    int startX = xx != 0 ? -1 : 0;
                    int endX = xx + 1 < width ? 2 : 1;

                    for (int i = startY; i < endY; i++)
                    {
                        int currentY = yy + i;

                        for (int j = startX; j < endX; j++)
                        {
                            int currentX = xx + j;

                            ref uint currentPixel = ref pixels[currentY * width + currentX];

                            if (currentPixel == 0u)
                            {
                                pixel = 0xFF_00_00_00;
                            }
                        }
                    }
                }
            }
        }

        public ArtInfo GetArt(uint idx)
        {
            if (_providers == null)
            {
                return default;
            }

            AssetType type;
            int id;

            if (idx < MAX_LAND_DATA_INDEX_COUNT)
            {
                type = AssetType.Land;
                id = (int)idx;
            }
            else
            {
                type = AssetType.Static;
                id = (int)(idx - MAX_LAND_DATA_INDEX_COUNT);
            }

            foreach (var provider in _providers)
            {
                if (provider.TryGetAsset(id, type, out var asset))
                {
                    return new ArtInfo()
                    {
                        Pixels = asset.Pixels,
                        Width = asset.Width,
                        Height = asset.Height,
                        PivotX = asset.PivotX,
                        PivotY = asset.PivotY,
                        Scale = asset.Scale
                    };
                }
            }

            return new ArtInfo()
            {
                Pixels = Array.Empty<uint>(),
                Width = 0,
                Height = 0
            };
        }
    }

    public ref struct ArtInfo
    {
        public Span<uint> Pixels;
        public int Width;
        public int Height;
        public int PivotX;
        public int PivotY;
        public float Scale;
    }
}
