using UnityEngine;

namespace CK3Map
{
    [System.Serializable]
    public sealed class CK3GradientBorderPreset
    {
        [SerializeField, InspectorName("渐变内部透明度"), Range(0.0f, 1.0f)]
        private float gradientAlphaInside;

        [SerializeField, InspectorName("渐变外部透明度"), Range(0.0f, 1.0f)]
        private float gradientAlphaOutside;

        [SerializeField, InspectorName("渐变宽度"), Min(0.0f)]
        private float gradientWidth;

        [SerializeField, InspectorName("渐变颜色倍率"), Min(0.0f)]
        private float gradientColorMultiplier;

        [SerializeField, InspectorName("深色边缘宽度"), Min(0.0f)]
        private float edgeWidth;

        [SerializeField, InspectorName("深色边缘柔和度"), Min(0.0f)]
        private float edgeSmoothness;

        [SerializeField, InspectorName("深色边缘透明度"), Range(0.0f, 1.0f)]
        private float edgeAlpha;

        [SerializeField, InspectorName("深色边缘颜色倍率"), Min(0.0f)]
        private float edgeColorMultiplier;

        [SerializeField, InspectorName("光照前混合"), Range(0.0f, 1.0f)]
        private float preLightingBlend;

        [SerializeField, InspectorName("光照后混合"), Range(0.0f, 1.0f)]
        private float postLightingBlend;

        public float GradientAlphaInside => gradientAlphaInside;
        public float GradientAlphaOutside => gradientAlphaOutside;
        public float GradientWidth => gradientWidth;
        public float GradientColorMultiplier => gradientColorMultiplier;
        public float EdgeWidth => edgeWidth;
        public float EdgeSmoothness => edgeSmoothness;
        public float EdgeAlpha => edgeAlpha;
        public float EdgeColorMultiplier => edgeColorMultiplier;
        public float PreLightingBlend => preLightingBlend;
        public float PostLightingBlend => postLightingBlend;

        public CK3GradientBorderPreset()
        {
        }

        public CK3GradientBorderPreset(
            float inside,
            float outside,
            float width,
            float gradientMultiplier,
            float edgeWidthValue,
            float edgeSoftness,
            float edgeAlphaValue,
            float edgeMultiplier,
            float preBlend,
            float postBlend)
        {
            gradientAlphaInside = inside;
            gradientAlphaOutside = outside;
            gradientWidth = width;
            gradientColorMultiplier = gradientMultiplier;
            edgeWidth = edgeWidthValue;
            edgeSmoothness = edgeSoftness;
            edgeAlpha = edgeAlphaValue;
            edgeColorMultiplier = edgeMultiplier;
            preLightingBlend = preBlend;
            postLightingBlend = postBlend;
        }
    }

    public enum CK3VisibleMapMode
    {
        法理王国地图,
        实际领地地图
    }

    [ExecuteAlways]
    public sealed class CK3MapModeController : MonoBehaviour
    {
        [Header("地图资源")]
        [SerializeField, InspectorName("政治地图数据")]
        private CK3PoliticalMapData mapData;

        [SerializeField, InspectorName("地形共享材质")]
        private Material terrainMaterial;

        [SerializeField, InspectorName("水面共享材质")]
        private Material waterMaterial;

        [SerializeField, InspectorName("世界外侧共享材质")]
        private Material surroundMaterial;

        [SerializeField, InspectorName("当前地图模式")]
        private CK3VisibleMapMode currentMode = CK3VisibleMapMode.法理王国地图;

        [Header("显示控制")]
        [SerializeField, InspectorName("启用政治填色")]
        private bool politicalOverlayEnabled = true;

        [SerializeField, InspectorName("显示游戏内切换按钮")]
        private bool showGameButtons = true;

        [SerializeField, InspectorName("法理王国曲线名称")]
        private GameObject deJureKingdomMapNames;

        [SerializeField, InspectorName("实际领地曲线名称")]
        private GameObject actualRealmMapNames;

        [SerializeField, InspectorName("政治填色强度")]
        [Range(0.0f, 1.0f)]
        [Tooltip("CK3 原版 map_modes.txt 的 Pre/Post Lighting 混合由缩放档位驱动；此值是整体预览倍率。")]
        private float overlayStrength = 1.0f;

        [SerializeField, InspectorName("距离场五点采样偏移"), Range(0.0f, 2.0f)]
        [Tooltip("CK3 游戏覆盖版使用中心点和四个对角点，原版偏移值为 0.75。增大后边缘更柔和，减小后更锐利。")]
        private float distanceFieldSampleOffset = 0.75f;

        [Header("缩放档位分界")]
        [SerializeField, InspectorName("近景档位")]
        private float nearZoomStep = 2.0f;

        [SerializeField, InspectorName("中景档位")]
        private float middleZoomStep = 9.0f;

        [SerializeField, InspectorName("远景档位")]
        private float farZoomStep = 15.0f;

        [SerializeField, InspectorName("平面地图前一档")]
        private float preFlatZoomStep = 20.0f;

        [SerializeField, InspectorName("平面地图档位")]
        private float flatZoomStep = 21.0f;

        [SerializeField, InspectorName("关闭水岸政治边缘档位")]
        [Tooltip("CK3 原版 00_graphics.txt：WATER_BORDERS_ZOOM_STEP = 8。达到该档位后，距离场使用水域 Wildcard，不再把湖岸和海岸当成国家边界。")]
        private float waterBordersZoomStep = 8.0f;

        [Header("近景政治边缘（原版档位 2）")]
        [SerializeField, InspectorName("近景参数")]
        private CK3GradientBorderPreset nearPreset = new CK3GradientBorderPreset(
            0.4f, 0.8f, 0.4f, 1.0f, 0.01f, 0.0f, 1.0f, 0.3f, 0.5f, 0.8f);

        [Header("中景政治边缘（原版档位 9）")]
        [SerializeField, InspectorName("中景参数")]
        private CK3GradientBorderPreset middlePreset = new CK3GradientBorderPreset(
            0.4f, 0.8f, 0.4f, 1.0f, 0.04f, 0.015f, 1.0f, 0.65f, 0.5f, 0.95f);

        [Header("远景政治边缘（原版档位 15）")]
        [SerializeField, InspectorName("远景参数")]
        private CK3GradientBorderPreset farPreset = new CK3GradientBorderPreset(
            1.0f, 1.0f, 0.4f, 1.0f, 0.04f, 0.0f, 1.0f, 0.65f, 0.5f, 1.0f);

        [Header("平面地图前政治边缘（原版档位 20）")]
        [SerializeField, InspectorName("平面地图前参数")]
        private CK3GradientBorderPreset preFlatPreset = new CK3GradientBorderPreset(
            1.0f, 1.0f, 0.4f, 1.0f, 0.04f, 0.0f, 1.0f, 0.65f, 0.0f, 1.0f);

        [Header("平面地图政治边缘（原版档位 21）")]
        [SerializeField, InspectorName("平面地图参数")]
        private CK3GradientBorderPreset flatPreset = new CK3GradientBorderPreset(
            0.6f, 1.0f, 0.4f, 1.0f, 0.05f, 0.0f, 1.0f, 0.7f, 0.0f, 0.95f);

        private static readonly int IndirectionId = Shader.PropertyToID("_ProvinceColorIndirectionTexture");
        private static readonly int PaletteId = Shader.PropertyToID("_ProvinceColorTexture");
        private static readonly int DistanceFieldId = Shader.PropertyToID("_BorderDistanceFieldTexture");
        private static readonly int EnabledId = Shader.PropertyToID("_CK3ProvinceOverlayEnabled");
        private static readonly int BlendId = Shader.PropertyToID("_CK3ProvinceOverlayBlend");
        private static readonly int GradientAlphaInsideId = Shader.PropertyToID("_GB_GradientAlphaInside");
        private static readonly int GradientAlphaOutsideId = Shader.PropertyToID("_GB_GradientAlphaOutside");
        private static readonly int GradientWidthId = Shader.PropertyToID("_GB_GradientWidth");
        private static readonly int GradientColorMultiplierId = Shader.PropertyToID("_GB_GradientColorMul");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_GB_EdgeWidth");
        private static readonly int EdgeSmoothnessId = Shader.PropertyToID("_GB_EdgeSmoothness");
        private static readonly int EdgeAlphaId = Shader.PropertyToID("_GB_EdgeAlpha");
        private static readonly int EdgeColorMultiplierId = Shader.PropertyToID("_GB_EdgeColorMul");
        private static readonly int PreLightingBlendId = Shader.PropertyToID("_GB_PreLightingBlend");
        private static readonly int PostLightingBlendId = Shader.PropertyToID("_GB_PostLightingBlend");
        private static readonly int DistanceSampleOffsetId = Shader.PropertyToID("_CK3DistanceSampleOffset");
        private static readonly int WaterZoomFactorId = Shader.PropertyToID("_CK3WaterZoomedInZoomedOutFactor");
        private static readonly int FlatMapHeightId = Shader.PropertyToID("_FlatMapHeight");
        private static readonly int FlatMapLerpId = Shader.PropertyToID("_FlatMapLerp");

        [Header("原版平面地图顶点过渡")]
        [SerializeField, InspectorName("平面地图高度")]
        [Tooltip("CK3 原版 00_graphics.txt：FLAT_MAP_HEIGHT = 3.92。地形与地图名称必须共用此高度。")]
        private float flatMapHeight = 3.92f;

        [Header("原版远近景光照联动")]
        [SerializeField, InspectorName("水面近景高度"), Min(0.0f)]
        [Tooltip("CK3 原版 water.settings 的 WaterZoomedInHeight，默认 100。政治色光照后混合也读取同一远近景因子。")]
        private float waterZoomedInHeight = 100.0f;

        [SerializeField, InspectorName("水面远景高度"), Min(0.0f)]
        [Tooltip("CK3 原版 water.settings 的 WaterZoomedOutHeight，默认 750。")]
        private float waterZoomedOutHeight = 750.0f;

        public CK3VisibleMapMode CurrentMode => currentMode;

        public void Configure(CK3PoliticalMapData data, Material material)
        {
            mapData = data;
            terrainMaterial = material;
            ApplyMode();
        }

        public void ConfigureMapNames(GameObject deJureNames)
        {
            deJureKingdomMapNames = deJureNames;
            ApplyMode();
        }

        public void ConfigureWater(Material material)
        {
            waterMaterial = material;
            ApplyBlendForCurrentZoom();
        }

        public void ConfigureSurround(Material material)
        {
            surroundMaterial = material;
            ApplyBlendForCurrentZoom();
        }

        public void ConfigureActualRealmMapNames(GameObject actualNames)
        {
            actualRealmMapNames = actualNames;
            ApplyMode();
        }

        public void SetMode(CK3VisibleMapMode mode)
        {
            currentMode = mode;
            ApplyMode();
        }

        private void OnEnable()
        {
            ApplyMode();
        }

        private void OnValidate()
        {
            ApplyMode();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (Input.GetKeyDown(KeyCode.F1)) SetMode(CK3VisibleMapMode.法理王国地图);
                if (Input.GetKeyDown(KeyCode.F2)) SetMode(CK3VisibleMapMode.实际领地地图);
            }

            ApplyBlendForCurrentZoom();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showGameButtons)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16, 16, 230, 92), GUI.skin.box);
            GUILayout.Label("CK3 地图模式");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentMode == CK3VisibleMapMode.法理王国地图, "F1 法理地图", GUI.skin.button))
            {
                SetMode(CK3VisibleMapMode.法理王国地图);
            }
            if (GUILayout.Toggle(currentMode == CK3VisibleMapMode.实际领地地图, "F2 实际地图", GUI.skin.button))
            {
                SetMode(CK3VisibleMapMode.实际领地地图);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ApplyMode()
        {
            if (terrainMaterial == null || mapData == null)
            {
                return;
            }

            Texture2D palette = currentMode == CK3VisibleMapMode.法理王国地图
                ? mapData.DeJureKingdomPalette
                : mapData.ActualRealmPalette;
            Texture2D distanceField = currentMode == CK3VisibleMapMode.法理王国地图
                ? mapData.DeJureKingdomDistanceField
                : mapData.ActualRealmDistanceField;
            terrainMaterial.SetTexture(IndirectionId, mapData.ProvinceIdIndirection);
            terrainMaterial.SetTexture(PaletteId, palette);
            terrainMaterial.SetTexture(DistanceFieldId, distanceField);
            terrainMaterial.SetFloat(EnabledId,
                politicalOverlayEnabled && palette != null && distanceField != null ? 1.0f : 0.0f);
            if (deJureKingdomMapNames != null)
                deJureKingdomMapNames.SetActive(currentMode == CK3VisibleMapMode.法理王国地图);
            if (actualRealmMapNames != null)
                actualRealmMapNames.SetActive(currentMode == CK3VisibleMapMode.实际领地地图);
            ApplyBlendForCurrentZoom();
        }

        private void ApplyBlendForCurrentZoom()
        {
            if (terrainMaterial == null)
            {
                return;
            }

            EnsurePresets();

            CK3StrategicMapCamera cameraController = Camera.main != null
                ? Camera.main.GetComponent<CK3StrategicMapCamera>()
                : null;
            float zoomStep = cameraController != null ? cameraController.VisualZoomStep : 21.0f;
            float zoomDistance = cameraController != null
                ? cameraController.CurrentZoomDistance
                : waterZoomedOutHeight;
            SetDistanceFieldForZoom(zoomStep);
            float flatMapLerp = Mathf.InverseLerp(preFlatZoomStep, flatZoomStep, zoomStep);
            terrainMaterial.SetFloat(FlatMapHeightId, flatMapHeight);
            terrainMaterial.SetFloat(FlatMapLerpId, flatMapLerp);
            if (waterMaterial != null)
            {
                waterMaterial.SetFloat(FlatMapHeightId, flatMapHeight);
                waterMaterial.SetFloat(FlatMapLerpId, flatMapLerp);
            }
            if (surroundMaterial != null)
            {
                surroundMaterial.SetFloat(FlatMapHeightId, flatMapHeight);
                surroundMaterial.SetFloat(FlatMapLerpId, flatMapLerp);
            }
            ApplyFlatMapToNames(flatMapLerp);
            terrainMaterial.SetFloat(GradientAlphaInsideId, SamplePresetValue(zoomStep, preset => preset.GradientAlphaInside));
            terrainMaterial.SetFloat(GradientAlphaOutsideId, SamplePresetValue(zoomStep, preset => preset.GradientAlphaOutside));
            terrainMaterial.SetFloat(GradientWidthId, SamplePresetValue(zoomStep, preset => preset.GradientWidth));
            terrainMaterial.SetFloat(GradientColorMultiplierId, SamplePresetValue(zoomStep, preset => preset.GradientColorMultiplier));
            terrainMaterial.SetFloat(EdgeWidthId, SamplePresetValue(zoomStep, preset => preset.EdgeWidth));
            terrainMaterial.SetFloat(EdgeSmoothnessId, SamplePresetValue(zoomStep, preset => preset.EdgeSmoothness));
            terrainMaterial.SetFloat(EdgeAlphaId, SamplePresetValue(zoomStep, preset => preset.EdgeAlpha));
            terrainMaterial.SetFloat(EdgeColorMultiplierId, SamplePresetValue(zoomStep, preset => preset.EdgeColorMultiplier));
            terrainMaterial.SetFloat(PreLightingBlendId, SamplePresetValue(zoomStep, preset => preset.PreLightingBlend));
            terrainMaterial.SetFloat(PostLightingBlendId, SamplePresetValue(zoomStep, preset => preset.PostLightingBlend));
            terrainMaterial.SetFloat(DistanceSampleOffsetId, distanceFieldSampleOffset);
            terrainMaterial.SetFloat(BlendId, overlayStrength);
            terrainMaterial.SetFloat(
                WaterZoomFactorId,
                Mathf.InverseLerp(
                    waterZoomedInHeight,
                    Mathf.Max(waterZoomedInHeight + 0.0001f, waterZoomedOutHeight),
                    zoomDistance));
        }

        private void ApplyFlatMapToNames(float flatMapLerp)
        {
            ApplyFlatMapToNameObject(deJureKingdomMapNames, flatMapLerp);
            ApplyFlatMapToNameObject(actualRealmMapNames, flatMapLerp);
        }

        private void SetDistanceFieldForZoom(float zoomStep)
        {
            if (mapData == null || terrainMaterial == null)
                return;

            bool withoutShoreline = zoomStep >= waterBordersZoomStep;
            Texture2D distanceField;
            if (currentMode == CK3VisibleMapMode.法理王国地图)
            {
                distanceField = withoutShoreline && mapData.DeJureKingdomDistanceFieldWithoutShoreline != null
                    ? mapData.DeJureKingdomDistanceFieldWithoutShoreline
                    : mapData.DeJureKingdomDistanceField;
            }
            else
            {
                distanceField = withoutShoreline && mapData.ActualRealmDistanceFieldWithoutShoreline != null
                    ? mapData.ActualRealmDistanceFieldWithoutShoreline
                    : mapData.ActualRealmDistanceField;
            }
            terrainMaterial.SetTexture(DistanceFieldId, distanceField);
        }

        private void ApplyFlatMapToNameObject(GameObject nameObject, float flatMapLerp)
        {
            if (nameObject == null)
                return;
            MeshRenderer renderer = nameObject.GetComponent<MeshRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material != null)
            {
                material.SetFloat(FlatMapHeightId, flatMapHeight);
                material.SetFloat(FlatMapLerpId, flatMapLerp);
            }
        }

        private void EnsurePresets()
        {
            if (nearPreset == null)
                nearPreset = new CK3GradientBorderPreset(
                    0.4f, 0.8f, 0.4f, 1.0f, 0.01f, 0.0f, 1.0f, 0.3f, 0.5f, 0.8f);
            if (middlePreset == null)
                middlePreset = new CK3GradientBorderPreset(
                    0.4f, 0.8f, 0.4f, 1.0f, 0.04f, 0.015f, 1.0f, 0.65f, 0.5f, 0.95f);
            if (farPreset == null)
                farPreset = new CK3GradientBorderPreset(
                    1.0f, 1.0f, 0.4f, 1.0f, 0.04f, 0.0f, 1.0f, 0.65f, 0.5f, 1.0f);
            if (preFlatPreset == null)
                preFlatPreset = new CK3GradientBorderPreset(
                    1.0f, 1.0f, 0.4f, 1.0f, 0.04f, 0.0f, 1.0f, 0.65f, 0.0f, 1.0f);
            if (flatPreset == null)
                flatPreset = new CK3GradientBorderPreset(
                    0.6f, 1.0f, 0.4f, 1.0f, 0.05f, 0.0f, 1.0f, 0.7f, 0.0f, 0.95f);
        }

        private float SamplePresetValue(float zoom, System.Func<CK3GradientBorderPreset, float> selector)
        {
            if (zoom <= nearZoomStep) return selector(nearPreset);
            if (zoom <= middleZoomStep) return Mathf.Lerp(
                selector(nearPreset), selector(middlePreset),
                Mathf.InverseLerp(nearZoomStep, middleZoomStep, zoom));
            if (zoom <= farZoomStep) return Mathf.Lerp(
                selector(middlePreset), selector(farPreset),
                Mathf.InverseLerp(middleZoomStep, farZoomStep, zoom));
            if (zoom <= preFlatZoomStep) return Mathf.Lerp(
                selector(farPreset), selector(preFlatPreset),
                Mathf.InverseLerp(farZoomStep, preFlatZoomStep, zoom));
            return Mathf.Lerp(
                selector(preFlatPreset), selector(flatPreset),
                Mathf.InverseLerp(preFlatZoomStep, flatZoomStep, zoom));
        }
    }
}
