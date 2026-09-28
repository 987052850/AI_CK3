using UnityEngine;

namespace CK3Map
{
    [ExecuteAlways]
    public sealed class CK3TerrainNodeBinding : MonoBehaviour
    {
        private static readonly int NodeOffsetScaleLerpId =
            Shader.PropertyToID("_CK3NodeOffsetScaleLerp");
        private static readonly int SkirtId = Shader.PropertyToID("_CK3IsSkirt");

        [SerializeField, InspectorName("原版节点偏移与缩放")]
        [Tooltip("CK3 Terrain 实例数据：XY 为四叉树节点偏移，Z 为节点缩放输入，W 为 LOD 插值。")]
        private Vector4 nodeOffsetScaleLerp;

        [SerializeField, InspectorName("是否为原版裙边")]
        [Tooltip("启用时执行 CK3 FixPositionForSkirt 顶点奇偶偏移。")]
        private bool isSkirt;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock propertyBlock;

        public void Configure(Vector4 originalNodeData, bool originalSkirt)
        {
            nodeOffsetScaleLerp = originalNodeData;
            isSkirt = originalSkirt;
            Apply();
        }

        public void Apply()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(NodeOffsetScaleLerpId, nodeOffsetScaleLerp);
            propertyBlock.SetFloat(SkirtId, isSkirt ? 1.0f : 0.0f);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }
    }
}
