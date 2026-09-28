using UnityEngine;

namespace CK3Map
{
    [ExecuteAlways]
    public sealed class CK3WaterSurfaceBinding : MonoBehaviour
    {
        private static readonly int WaterHeightId = Shader.PropertyToID("_WaterHeight");
        private static readonly int FadeDepthId = Shader.PropertyToID("_WaterFadeShoreMaskDepth");
        private static readonly int FadeSharpnessId = Shader.PropertyToID("_WaterFadeShoreMaskSharpness");
        private static readonly int FlatMapWaterOffsetId = Shader.PropertyToID("_UnityFlatMapWaterOffset");

        [Header("资源")]
        [SerializeField, InspectorName("原版水面材质")]
        [Tooltip("Editor 构建并保存的 CK3 水面材质资产。")]
        private Material waterMaterial;

        [Header("原版水面参数")]
        [SerializeField, InspectorName("海平面高度")]
        [Tooltip("game/common/defines/00_defines.txt：NJominiMap.WATERLEVEL，原版值 3。")]
        private float waterHeight = 3.0f;

        [SerializeField, InspectorName("岸边淡出深度")]
        [Tooltip("water.settings：WaterFadeShoreMaskDepth，原版值 0.5。")]
        private float shoreFadeDepth = 0.5f;

        [SerializeField, InspectorName("岸边淡出锐度"), Min(0.0f)]
        [Tooltip("water.settings：WaterFadeShoreMaskSharpness，原版值 5。")]
        private float shoreFadeSharpness = 5.0f;

        [Header("Unity 深度载体")]
        [SerializeField, InspectorName("平面地图水面抬升"), Range(0.0f, 0.2f)]
        [Tooltip("仅用于补偿 CK3 原生 DepthBias=-100 无法直接换算到 Unity 的差异；不改变真实海平面和岸边深度。默认 0.02。")]
        private float flatMapWaterOffset = 0.02f;

        [Header("预览")]
        [SerializeField, InspectorName("运行时持续应用参数")]
        [Tooltip("开启后可在 Play 模式修改 Inspector 并立即查看水面参数。")]
        private bool applyEveryFrame = true;

        public Material WaterMaterial => waterMaterial;

        public void Configure(Material originalWaterMaterial)
        {
            waterMaterial = originalWaterMaterial;
            ApplyParameters();
        }

        public void ApplyParameters()
        {
            if (waterMaterial == null)
            {
                return;
            }

            waterMaterial.SetFloat(WaterHeightId, waterHeight);
            waterMaterial.SetFloat(FadeDepthId, shoreFadeDepth);
            waterMaterial.SetFloat(FadeSharpnessId, Mathf.Max(0.0f, shoreFadeSharpness));
            waterMaterial.SetFloat(FlatMapWaterOffsetId, Mathf.Max(0.0f, flatMapWaterOffset));
        }

        private void OnEnable()
        {
            ApplyParameters();
        }

        private void OnValidate()
        {
            ApplyParameters();
        }

        private void Update()
        {
            if (applyEveryFrame)
            {
                ApplyParameters();
            }
        }
    }
}
