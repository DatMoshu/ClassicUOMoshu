using System;
using System.IO;
using ClassicUO.IO;
using ClassicUO.Utility;

namespace ClassicUO.Assets
{
    public class LegacyAssetProvider : IAssetProvider
    {
        private readonly UOFile _file;
        private const int MAX_LAND_DATA_INDEX_COUNT = 0x4000;

        public LegacyAssetProvider(UOFile file)
        {
            _file = file;
        }

        public event Action<int, AssetType> AssetChanged;

        public bool TryGetAsset(int assetId, AssetType type, out AssetData asset)
        {
            asset = default;
            int fileIndex;

            switch (type)
            {
                case AssetType.Land:
                    fileIndex = assetId;
                    break;
                case AssetType.Static:
                    fileIndex = assetId + MAX_LAND_DATA_INDEX_COUNT;
                    break;
                default:
                    return false;
            }

            if (fileIndex < 0 || fileIndex >= _file.Entries.Length)
            {
                return false;
            }

            ref var entry = ref _file.GetValidRefEntry(fileIndex);

            if (entry.Equals(UOFileIndex.Invalid))
            {
                return false;
            }

            short width, height;
            uint[] pixels;

            if (type == AssetType.Land)
            {
                pixels = LoadLand(_file, in entry, out width, out height);
            }
            else
            {
                pixels = LoadArt(_file, in entry, out width, out height);
            }

            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            asset = new AssetData
            {
                Pixels = pixels,
                Width = width,
                Height = height
            };

            return true;
        }

        public bool HasAsset(int assetId, AssetType type)
        {
            int fileIndex;

            switch (type)
            {
                case AssetType.Land:
                    fileIndex = assetId;
                    break;
                case AssetType.Static:
                    fileIndex = assetId + MAX_LAND_DATA_INDEX_COUNT;
                    break;
                default:
                    return false;
            }

            if (fileIndex < 0 || fileIndex >= _file.Entries.Length)
            {
                return false;
            }

            ref var entry = ref _file.GetValidRefEntry(fileIndex);
            return !entry.Equals(UOFileIndex.Invalid) && entry.Length > 0;
        }

        private static uint[] LoadLand(UOFile file, ref readonly UOFileIndex entry, out short width, out short height)
        {
            if (entry.Length == 0)
            {
                width = 0;
                height = 0;

                return Array.Empty<uint>();
            }

            width = 44;
            height = 44;

            if (entry.File != null)
                file = entry.File;

            file.Seek(entry.Offset, SeekOrigin.Begin);

            var data = new uint[width * height];

            for (int i = 0; i < 22; ++i)
            {
                int start = 22 - (i + 1);
                int pos = i * 44 + start;
                int end = start + ((i + 1) << 1);

                for (int j = start; j < end; ++j)
                {
                    data[pos++] = HuesHelper.Color16To32(file.ReadUInt16()) | 0xFF_00_00_00;
                }
            }

            for (int i = 0; i < 22; ++i)
            {
                int pos = (i + 22) * 44 + i;
                int end = i + ((22 - i) << 1);

                for (int j = i; j < end; ++j)
                {
                    data[pos++] = HuesHelper.Color16To32(file.ReadUInt16()) | 0xFF_00_00_00;
                }
            }

            return data;
        }

        private static unsafe uint[] LoadArt(UOFile file, ref readonly UOFileIndex entry, out short width, out short height)
        {
            if (entry.Length == 0)
            {
                width = 0;
                height = 0;

                return Array.Empty<uint>();
            }

            if (entry.File != null)
                file = entry.File;

            file.Seek(entry.Offset, SeekOrigin.Begin);

            var flags = file.ReadUInt32();
            width = file.ReadInt16();
            height = file.ReadInt16();

            var buf = new byte[entry.Length];
            file.Read(buf);

            var data = new uint[width * height];

            fixed (byte* startPtr = buf)
            {
                ushort* lineoffsets = (ushort*)startPtr;
                byte* datastart = (byte*)startPtr + height * 2;
                int x = 0;
                int y = 0;
                var ptr = (ushort*)(datastart + lineoffsets[0] * 2);

                while (y < height)
                {
                    ushort xoffs = *ptr++;
                    ushort run = *ptr++;

                    if (xoffs + run >= 2048)
                    {
                        break;
                    }

                    if (xoffs + run != 0)
                    {
                        x += xoffs;
                        int pos = y * width + x;

                        for (int j = 0; j < run; ++j, ++pos)
                        {
                            ushort val = *ptr++;

                            if (val != 0)
                            {
                                data[pos] = HuesHelper.Color16To32(val) | 0xFF_00_00_00;
                            }
                        }

                        x += run;
                    }
                    else
                    {
                        x = 0;
                        ++y;
                        ptr = (ushort*)(datastart + lineoffsets[y] * 2);
                    }
                }
            }

            return data;
        }
    }
}
