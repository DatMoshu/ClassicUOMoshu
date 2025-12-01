using System;

namespace ClassicUO.Assets
{
    public enum AssetType
    {
        Land,
        Static,
        Gump,
        Animation,
        Texture
    }

    public struct AssetData
    {
        public uint[] Pixels;
        public int Width;
        public int Height;
        public int PivotX;
        public int PivotY;
        public bool HasNormalMap;
        public bool IsEmissive;
        public float Scale;
    }

    public interface IAssetProvider
    {
        bool TryGetAsset(int assetId, AssetType type, out AssetData asset);
        bool HasAsset(int assetId, AssetType type);
        event Action<int, AssetType> AssetChanged;
    }
}
