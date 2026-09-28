using System;
using UnityEngine;

namespace CK3Map
{
    [CreateAssetMenu(fileName = "CK3原版高度页表", menuName = "CK3 地图/原版高度页表")]
    public sealed class CK3TerrainHeightData : ScriptableObject
    {
        [Header("原版高度纹理")]
        [SerializeField, InspectorName("压缩高度图")]
        [Tooltip("game/map_data/packed_heightmap.png 解码得到的 3185×4061 R16 纹理。")]
        private Texture2D packedHeightTexture;

        [SerializeField, InspectorName("高度间接寻址图")]
        [Tooltip("game/map_data/indirection_heightmap.png 解码得到的 288×144 RGBA32 页表。")]
        private Texture2D indirectionTexture;

        [Header("原版尺寸与层级")]
        [SerializeField, InspectorName("原始高度图尺寸")]
        [Tooltip("heightmap.heightmap: original_heightmap_size")]
        private Vector2Int originalHeightmapSize;

        [SerializeField, InspectorName("间接寻址图尺寸")]
        [Tooltip("原始高度图按 64×64 有效瓦片划分后的页表尺寸。")]
        private Vector2Int indirectionSize;

        [SerializeField, InspectorName("压缩高度图尺寸")]
        [Tooltip("原版 packed_heightmap.png 的像素尺寸。")]
        private Vector2Int packedHeightmapSize;

        [SerializeField, InspectorName("基础瓦片尺寸")]
        [Tooltip("heightmap.heightmap: tile_size；65 像素包含 64 像素有效区和共享边界。")]
        private int baseTileSize;

        [SerializeField, InspectorName("最大压缩层级")]
        [Tooltip("heightmap.heightmap: max_compress_level")]
        private int maxCompressionLevel;

        [SerializeField, InspectorName("层级像素偏移")]
        [Tooltip("heightmap.heightmap: level_offsets；在压缩高度图中的原版像素偏移。")]
        private Vector2Int[] levelOffsets = Array.Empty<Vector2Int>();

        [SerializeField, InspectorName("瓦片到压缩高度图常量")]
        [Tooltip("PdxHeightmapConstants.TileToHeightMapScaleAndOffset，按五个压缩层级排列。")]
        private Vector4[] tileToHeightmapScaleAndOffset = Array.Empty<Vector4>();

        [Header("原版 PdxHeightmapConstants")]
        [SerializeField, InspectorName("世界坐标到页表坐标")]
        private Vector2 worldSpaceToLookup;

        [SerializeField, InspectorName("原始高度像素到世界坐标")]
        private Vector2 originalHeightmapToWorldSpace;

        [SerializeField, InspectorName("世界平面范围")]
        private Vector2 worldExtents;

        [SerializeField, InspectorName("高度缩放")]
        [Tooltip("game/common/defines/00_defines.txt: WORLD_EXTENTS_Y")]
        private float heightScale;

        public Texture2D PackedHeightTexture => packedHeightTexture;
        public Texture2D IndirectionTexture => indirectionTexture;
        public Vector2Int OriginalHeightmapSize => originalHeightmapSize;
        public Vector2Int IndirectionSize => indirectionSize;
        public Vector2Int PackedHeightmapSize => packedHeightmapSize;
        public int BaseTileSize => baseTileSize;
        public int MaxCompressionLevel => maxCompressionLevel;
        public Vector2Int[] LevelOffsets => levelOffsets;
        public Vector4[] TileToHeightmapScaleAndOffset => tileToHeightmapScaleAndOffset;
        public Vector2 WorldSpaceToLookup => worldSpaceToLookup;
        public Vector2 OriginalHeightmapToWorldSpace => originalHeightmapToWorldSpace;
        public Vector2 WorldExtents => worldExtents;
        public float HeightScale => heightScale;

        public void ReplaceData(
            Texture2D packedHeight,
            Texture2D indirection,
            Vector2Int originalSize,
            Vector2Int lookupSize,
            Vector2Int packedSize,
            int tileSize,
            int compressionLevel,
            Vector2Int[] offsets,
            Vector4[] tileConstants,
            Vector2 worldToLookup,
            Vector2 originalToWorld,
            Vector2 extents,
            float yScale)
        {
            packedHeightTexture = packedHeight;
            indirectionTexture = indirection;
            originalHeightmapSize = originalSize;
            indirectionSize = lookupSize;
            packedHeightmapSize = packedSize;
            baseTileSize = tileSize;
            maxCompressionLevel = compressionLevel;
            levelOffsets = offsets;
            tileToHeightmapScaleAndOffset = tileConstants;
            worldSpaceToLookup = worldToLookup;
            originalHeightmapToWorldSpace = originalToWorld;
            worldExtents = extents;
            heightScale = yScale;
        }
    }
}
