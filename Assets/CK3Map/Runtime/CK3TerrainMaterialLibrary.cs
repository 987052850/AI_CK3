using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK3Map
{
    [Serializable]
    public sealed class CK3TerrainMaterialEntry
    {
        [SerializeField, InspectorName("原版数组索引")]
        [Tooltip("该材质在 CK3 materials.settings 中的原始顺序；detail_index.tga 直接使用这个索引。")]
        private int originalIndex;

        [SerializeField, InspectorName("显示名称")]
        [Tooltip("CK3 materials.settings 的 name 字段。")]
        private string displayName = string.Empty;

        [SerializeField, InspectorName("稳定标识")]
        [Tooltip("CK3 materials.settings 的 id 字段。")]
        private string id = string.Empty;

        [SerializeField, InspectorName("动态覆盖材质")]
        [Tooltip("CK3 文件开头固定顺序的 drought、drought_cracks、flood、summer_grass、winter_effect。")]
        private bool dynamicOverlay;

        [SerializeField, InspectorName("平铺系数")]
        [Tooltip("CK3 的 tile_factor；未单独设置时使用 settings.terrain 的 detail_tile_factor。")]
        private float tileFactor;

        [SerializeField, InspectorName("漫反射源文件")]
        private string diffusePath = string.Empty;

        [SerializeField, InspectorName("RRxG 法线源文件")]
        private string normalPath = string.Empty;

        [SerializeField, InspectorName("属性源文件")]
        private string materialPath = string.Empty;

        [SerializeField, InspectorName("绘制遮罩源文件")]
        private string maskPath = string.Empty;

        public int OriginalIndex => originalIndex;
        public string DisplayName => displayName;
        public string Id => id;
        public bool DynamicOverlay => dynamicOverlay;
        public float TileFactor => tileFactor;
        public string DiffusePath => diffusePath;
        public string NormalPath => normalPath;
        public string MaterialPath => materialPath;
        public string MaskPath => maskPath;

        public CK3TerrainMaterialEntry(
            int originalIndex,
            string displayName,
            string id,
            bool dynamicOverlay,
            float tileFactor,
            string diffusePath,
            string normalPath,
            string materialPath,
            string maskPath)
        {
            this.originalIndex = originalIndex;
            this.displayName = displayName;
            this.id = id;
            this.dynamicOverlay = dynamicOverlay;
            this.tileFactor = tileFactor;
            this.diffusePath = diffusePath;
            this.normalPath = normalPath;
            this.materialPath = materialPath;
            this.maskPath = maskPath;
        }
    }

    public sealed class CK3TerrainMaterialLibrary : ScriptableObject
    {
        [Header("CK3 原版材料表")]
        [SerializeField, InspectorName("材料条目")]
        [Tooltip("严格保持 materials.settings 的 105 个条目及其原始顺序。")]
        private List<CK3TerrainMaterialEntry> materials = new List<CK3TerrainMaterialEntry>();

        [Header("原始 DXT5 纹理数组")]
        [SerializeField, InspectorName("漫反射与高度数组")]
        [Tooltip("RGB 为漫反射颜色，Alpha 为 CK3 高度混合输入；使用 sRGB 采样。")]
        private Texture2DArray diffuseHeightTextures;

        [SerializeField, InspectorName("RRxG 法线数组")]
        [Tooltip("保留 CK3 原始 DXT5 通道；G/A 是法线 XY，不经过 Unity Normal Map 重排。")]
        private Texture2DArray normalTextures;

        [SerializeField, InspectorName("材质属性数组")]
        [Tooltip("CK3 属性通道：R 控制 colormap 覆盖，G 高光，B 金属度，A 粗糙度。")]
        private Texture2DArray materialTextures;

        [Header("完整世界控制图")]
        [SerializeField, InspectorName("四层材质索引图")]
        [Tooltip("CK3 detail_index.tga，9216×4608 BGRA32；四个通道直接保存四个材质数组索引。")]
        private Texture2D detailIndexTexture;

        [SerializeField, InspectorName("四层材质权重图")]
        [Tooltip("CK3 detail_intensity.tga，9216×4608 BGRA32；四个通道分别对应四个材质权重。")]
        private Texture2D detailMaskTexture;

        [SerializeField, InspectorName("宏观颜色图")]
        [Tooltip("CK3 colormap.dds，9216×4608 DXT5；Shader 按原版先翻转 Y，再执行 pow(color, 2.2)。")]
        private Texture2D colorMapTexture;

        [SerializeField, InspectorName("地表合成材质")]
        [Tooltip("使用 CK3 四层聚合、高度混合、RRxG 法线、属性合成和 SoftLight 宏观颜色逻辑的持久化材质。")]
        private Material terrainSurfaceMaterial;

        [Header("原版 Terrain 常量")]
        [SerializeField, InspectorName("高度混合范围")]
        [Tooltip("settings.terrain: detail_blend_range")]
        private float detailBlendRange = 0.25f;

        [SerializeField, InspectorName("默认平铺系数")]
        [Tooltip("settings.terrain: detail_tile_factor")]
        private float defaultTileFactor = 337.5f;

        [SerializeField, InspectorName("平铺起点偏移")]
        [Tooltip("settings.terrain: detail_tile_offset_x / detail_tile_offset_y")]
        private Vector2 detailTileOffset = new Vector2(0.0f, -512.0f);

        [SerializeField, InspectorName("材料强度偏置")]
        [Tooltip("detail_data.settings: material_intensity_bias")]
        private float materialIntensityBias;

        [SerializeField, InspectorName("世界平面范围")]
        [Tooltip("game/common/defines/00_defines.txt: WORLD_EXTENTS_X / WORLD_EXTENTS_Z")]
        private Vector2 worldExtents = new Vector2(9215.0f, 4607.0f);

        public IReadOnlyList<CK3TerrainMaterialEntry> Materials => materials;
        public Texture2DArray DiffuseHeightTextures => diffuseHeightTextures;
        public Texture2DArray NormalTextures => normalTextures;
        public Texture2DArray MaterialTextures => materialTextures;
        public Texture2D DetailIndexTexture => detailIndexTexture;
        public Texture2D DetailMaskTexture => detailMaskTexture;
        public Texture2D ColorMapTexture => colorMapTexture;
        public Material TerrainSurfaceMaterial => terrainSurfaceMaterial;
        public float DetailBlendRange => detailBlendRange;
        public float DefaultTileFactor => defaultTileFactor;
        public Vector2 DetailTileOffset => detailTileOffset;
        public float MaterialIntensityBias => materialIntensityBias;
        public Vector2 WorldExtents => worldExtents;

        public void ReplaceFromOriginal(
            List<CK3TerrainMaterialEntry> originalMaterials,
            Texture2DArray diffuseArray,
            Texture2DArray normalArray,
            Texture2DArray materialArray,
            float blendRange,
            float tileFactor,
            Vector2 tileOffset,
            float intensityBias)
        {
            materials = originalMaterials;
            diffuseHeightTextures = diffuseArray;
            normalTextures = normalArray;
            materialTextures = materialArray;
            detailBlendRange = blendRange;
            defaultTileFactor = tileFactor;
            detailTileOffset = tileOffset;
            materialIntensityBias = intensityBias;
        }

        public void ReplaceSurfaceAssets(
            Texture2D originalDetailIndexTexture,
            Texture2D originalDetailMaskTexture,
            Texture2D originalColorMapTexture,
            Material originalTerrainSurfaceMaterial,
            Vector2 originalWorldExtents)
        {
            detailIndexTexture = originalDetailIndexTexture;
            detailMaskTexture = originalDetailMaskTexture;
            colorMapTexture = originalColorMapTexture;
            terrainSurfaceMaterial = originalTerrainSurfaceMaterial;
            worldExtents = originalWorldExtents;
        }

        public Vector4[] CreatePackedDetailTileFactors()
        {
            Vector4[] result = new Vector4[materials.Count];
            float inverseWorldWidth = 1.0f / worldExtents.x;
            for (int index = 0; index < materials.Count; index++)
            {
                float packed = materials[index].TileFactor * inverseWorldWidth;
                result[index] = new Vector4(packed, -packed, 0.0f, 0.0f);
            }

            return result;
        }
    }
}
