using UnityEngine;

namespace CK3Map
{
    [ExecuteAlways]
    public sealed class CK3TerrainSurfaceBinding : MonoBehaviour
    {
        private static readonly int PackedDetailTileFactorsId =
            Shader.PropertyToID("_CK3PackedDetailTileFactors");
        private static readonly int HeightScaleId = Shader.PropertyToID("_CK3HeightScale");
        private static readonly int TerrainVerticalOffsetId = Shader.PropertyToID("_CK3TerrainVerticalOffset");
        private static readonly int SkirtSizeId = Shader.PropertyToID("_CK3SkirtSize");
        private static readonly int DetailBlendRangeId = Shader.PropertyToID("_DetailBlendRange");
        private static readonly int MacroColorStrengthId = Shader.PropertyToID("_CK3MacroColorStrength");
        private static readonly int NormalHeightScaleId = Shader.PropertyToID("_CK3NormalHeightScale");
        private static readonly int NormalStepSizeId = Shader.PropertyToID("_CK3NormalStepSize");
        private static readonly int MapLightingEnabledId = Shader.PropertyToID("_CK3MapLightingEnabled");
        private static readonly int TerrainSunnySunColorId = Shader.PropertyToID("_CK3TerrainSunnySunColor");
        private static readonly int TerrainSunnySunIntensityId = Shader.PropertyToID("_CK3TerrainSunnySunIntensity");
        private static readonly int TerrainSunnyIblScaleId = Shader.PropertyToID("_CK3TerrainSunnyIblScale");
        private static readonly int TerrainSunnySpecularFactorId = Shader.PropertyToID("_CK3TerrainSunnySpecularFactor");

        [Header("资源")]
        [SerializeField, InspectorName("原版地表材质库")]
        [Tooltip("Editor 构建并保存的 CK3 原版 105 材质库。")]
        private CK3TerrainMaterialLibrary materialLibrary;

        [SerializeField, InspectorName("地表合成材质")]
        [Tooltip("需要接收 CK3 PdxTerrainConstants 材质平铺数组的持久化材质资产。")]
        private Material terrainSurfaceMaterial;

        [Header("运行时更新")]
        [SerializeField, InspectorName("运行时持续应用参数")]
        [Tooltip("开启后可以在 Play 模式拖动 Inspector 并立即查看效果。")]
        private bool applyEveryFrame = true;

        [Header("地形几何")]
        [SerializeField, InspectorName("地形高度缩放"), Min(0.0f)]
        [Tooltip("CK3 原版默认值为 50。")]
        private float heightScale = 50.0f;

        [SerializeField, InspectorName("地形整体高度偏移")]
        private float terrainVerticalOffset;

        [SerializeField, InspectorName("分块裙边深度")]
        [Tooltip("CK3 原版默认值为 -5；越负，分块接缝向下延伸越深。")]
        private float skirtSize = -5.0f;

        [Header("地表材质混合")]
        [SerializeField, InspectorName("四层材质高度混合范围"), Min(0.0001f)]
        [Tooltip("CK3 原版 detail_data.settings 的默认值为 0.25。")]
        private float detailBlendRange = 0.25f;

        [SerializeField, InspectorName("宏观颜色影响强度"), Range(0.0f, 2.0f)]
        [Tooltip("控制 colormap 对近景细节材质的影响。1 为当前原版移植值。")]
        private float macroColorStrength = 1.0f;

        [SerializeField, InspectorName("地形法线高度倍率"), Min(0.0f)]
        [Tooltip("CK3 原版 settings.terrain 的 normal_height_scale，默认 0.8。")]
        private float normalHeightScale = 0.8f;

        [SerializeField, InspectorName("地形法线采样步长"), Min(0.0001f)]
        [Tooltip("CK3 原版 settings.terrain 的 normal_step_size，默认 1.6。")]
        private float normalStepSize = 1.6f;

        [Header("CK3 地图光照")]
        [SerializeField, InspectorName("启用 CK3 地图光照")]
        [Tooltip("启用原版 GetMaterialProperties 与 CalculateMapLighting 光照链。")]
        private bool mapLightingEnabled = true;

        [SerializeField, InspectorName("晴天太阳颜色")]
        [Tooltip("原版 TERRAIN_SUNNY_SUN_COLOR。")]
        private Color terrainSunnySunColor = new Color(1.0f, 0.9f, 0.8f, 1.0f);

        [SerializeField, InspectorName("晴天太阳强度"), Min(0.0f)]
        [Tooltip("原版 TERRAIN_SUNNY_SUN_INTENSITY，默认 8。")]
        private float terrainSunnySunIntensity = 8.0f;

        [SerializeField, InspectorName("晴天环境光倍率"), Min(0.0f)]
        [Tooltip("原版 TERRAIN_SUNNY_IBL_SCALE，默认 0.25。")]
        private float terrainSunnyIblScale = 0.25f;

        [SerializeField, InspectorName("晴天高光倍率"), Min(0.0f)]
        [Tooltip("原版 TERRAIN_SUNNY_SPECULAR_FACTOR，默认 1。")]
        private float terrainSunnySpecularFactor = 1.0f;

        public CK3TerrainMaterialLibrary MaterialLibrary => materialLibrary;
        public Material TerrainSurfaceMaterial => terrainSurfaceMaterial;

        public void Configure(
            CK3TerrainMaterialLibrary originalMaterialLibrary,
            Material originalTerrainSurfaceMaterial)
        {
            materialLibrary = originalMaterialLibrary;
            terrainSurfaceMaterial = originalTerrainSurfaceMaterial;
            ApplyOriginalConstants();
        }

        public void ApplyOriginalConstants()
        {
            if (materialLibrary == null || terrainSurfaceMaterial == null)
            {
                return;
            }

            terrainSurfaceMaterial.SetVectorArray(
                PackedDetailTileFactorsId,
                materialLibrary.CreatePackedDetailTileFactors());
            ApplyVisualParameters();
        }

        public void ApplyVisualParameters()
        {
            if (terrainSurfaceMaterial == null)
            {
                return;
            }

            terrainSurfaceMaterial.SetFloat(HeightScaleId, heightScale);
            terrainSurfaceMaterial.SetFloat(TerrainVerticalOffsetId, terrainVerticalOffset);
            terrainSurfaceMaterial.SetFloat(SkirtSizeId, skirtSize);
            terrainSurfaceMaterial.SetFloat(DetailBlendRangeId, Mathf.Max(0.0001f, detailBlendRange));
            terrainSurfaceMaterial.SetFloat(MacroColorStrengthId, macroColorStrength);
            terrainSurfaceMaterial.SetFloat(NormalHeightScaleId, normalHeightScale);
            terrainSurfaceMaterial.SetFloat(NormalStepSizeId, Mathf.Max(0.0001f, normalStepSize));
            terrainSurfaceMaterial.SetFloat(MapLightingEnabledId, mapLightingEnabled ? 1.0f : 0.0f);
            terrainSurfaceMaterial.SetColor(TerrainSunnySunColorId, terrainSunnySunColor);
            terrainSurfaceMaterial.SetFloat(TerrainSunnySunIntensityId, terrainSunnySunIntensity);
            terrainSurfaceMaterial.SetFloat(TerrainSunnyIblScaleId, terrainSunnyIblScale);
            terrainSurfaceMaterial.SetFloat(TerrainSunnySpecularFactorId, terrainSunnySpecularFactor);
        }

        private void OnEnable()
        {
            ApplyOriginalConstants();
        }

        private void OnValidate()
        {
            ApplyOriginalConstants();
        }

        private void Update()
        {
            if (applyEveryFrame)
            {
                ApplyVisualParameters();
            }
        }
    }
}
