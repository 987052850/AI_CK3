using UnityEngine;

namespace CK3Map
{
    [ExecuteAlways]
    public sealed class CK3RiverSurfaceBinding : MonoBehaviour
    {
        private static readonly int RiverColorId = Shader.PropertyToID("_RiverColor");
        private static readonly int RiverOpacityId = Shader.PropertyToID("_RiverOpacity");
        private static readonly int RiverHeightOffsetId = Shader.PropertyToID("_RiverHeightOffset");
        private static readonly int TextureUvScaleId = Shader.PropertyToID("_TextureUvScale");
        private static readonly int FlowNormalUvScaleId = Shader.PropertyToID("_FlowNormalUvScale");
        private static readonly int FlowNormalSpeedId = Shader.PropertyToID("_FlowNormalSpeed");
        private static readonly int OceanFadeRateId = Shader.PropertyToID("_OceanFadeRate");

        [Header("资源")]
        [SerializeField, InspectorName("河面材质")]
        [Tooltip("阶段 11 生成并保存的 CK3 河面材质资产。")]
        private Material riverMaterial;

        [Header("河面贴地")]
        [SerializeField, InspectorName("河面离地高度"), Min(0.0f)]
        [Tooltip("只用于解决 Unity 深度精度造成的共面闪烁，不参与河网高度计算。默认 0.04。")]
        private float riverHeightOffset = 0.04f;

        [Header("河面外观")]
        [SerializeField, InspectorName("河面颜色倍率")]
        private Color riverColor = new Color(0.72f, 0.88f, 0.92f, 1.0f);

        [SerializeField, InspectorName("河面透明度"), Range(0.0f, 1.0f)]
        private float riverOpacity = 0.82f;

        [SerializeField, InspectorName("纵向纹理缩放"), Min(0.0001f)]
        private float textureUvScale = 0.8f;

        [SerializeField, InspectorName("流动法线缩放"), Min(0.0001f)]
        private float flowNormalUvScale = 0.4f;

        [SerializeField, InspectorName("流动速度")]
        private float flowNormalSpeed = 0.075f;

        [SerializeField, InspectorName("入海淡出率"), Min(0.0f)]
        private float oceanFadeRate = 0.8f;

        [Header("预览")]
        [SerializeField, InspectorName("运行时持续应用参数")]
        [Tooltip("开启后可在 Play 模式实时调整上述参数。")]
        private bool applyEveryFrame = true;

        public void Configure(Material material)
        {
            riverMaterial = material;
            ApplyParameters();
        }

        public void ApplyParameters()
        {
            if (riverMaterial == null)
            {
                return;
            }

            riverMaterial.SetColor(RiverColorId, riverColor);
            riverMaterial.SetFloat(RiverOpacityId, riverOpacity);
            riverMaterial.SetFloat(RiverHeightOffsetId, Mathf.Max(0.0f, riverHeightOffset));
            riverMaterial.SetFloat(TextureUvScaleId, Mathf.Max(0.0001f, textureUvScale));
            riverMaterial.SetFloat(FlowNormalUvScaleId, Mathf.Max(0.0001f, flowNormalUvScale));
            riverMaterial.SetFloat(FlowNormalSpeedId, flowNormalSpeed);
            riverMaterial.SetFloat(OceanFadeRateId, Mathf.Max(0.0f, oceanFadeRate));
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
