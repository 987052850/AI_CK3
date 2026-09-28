using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace CK3Map
{
    [Serializable]
    public sealed class CK3DecorationBatch
    {
        [SerializeField, InspectorName("原版 PDX Mesh 名称")]
        private string originalPdxMeshName;

        [SerializeField, InspectorName("Unity 共享网格")]
        private Mesh mesh;

        [SerializeField, InspectorName("材质")]
        private Material[] materials = Array.Empty<Material>();

        [SerializeField, InspectorName("原版实例变换数据")]
        private TextAsset transformData;

        [SerializeField, InspectorName("实例数量")]
        private int instanceCount;

        public string OriginalPdxMeshName => originalPdxMeshName;
        public Mesh Mesh => mesh;
        public Material[] Materials => materials;
        public TextAsset TransformData => transformData;
        public int InstanceCount => instanceCount;

        public void Replace(string pdxMeshName, Mesh sharedMesh, Material[] sharedMaterials, TextAsset transforms, int count)
        {
            originalPdxMeshName = pdxMeshName;
            mesh = sharedMesh;
            materials = sharedMaterials ?? Array.Empty<Material>();
            transformData = transforms;
            instanceCount = count;
        }
    }

    [CreateAssetMenu(fileName = "CK3原版近景装饰数据", menuName = "CK3 地图/原版近景装饰数据")]
    public sealed class CK3DecorationData : ScriptableObject
    {
        [SerializeField, InspectorName("原版装饰批次")]
        private CK3DecorationBatch[] batches = Array.Empty<CK3DecorationBatch>();

        public CK3DecorationBatch[] Batches => batches;

        public void ReplaceData(CK3DecorationBatch[] value)
        {
            batches = value ?? Array.Empty<CK3DecorationBatch>();
        }
    }

    // Unity carrier adapter: original CK3 instance transforms remain unchanged in the generated
    // binary asset. Unity GPU instancing is only the replaceable Scene/Game display carrier.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CK3DecorationRenderer : MonoBehaviour
    {
        private const int MaximumInstancesPerDraw = 1023;
        private const int TransformFloatCount = 10;
        private const int BinaryMagic = 0x334B4344; // DCK3

        [Header("原版装饰数据")]
        [SerializeField, InspectorName("装饰数据库")]
        [Tooltip("由 CK3 地图构建器在 Editor 中生成的原版 Mesh、材质和实例变换。")]
        private CK3DecorationData data;

        [Header("显示")]
        [SerializeField, InspectorName("显示近景装饰")]
        private bool showDecorations = true;

        [SerializeField, InspectorName("每批最多显示数量")]
        [Tooltip("0 表示显示该批次的全部原版实例；可临时限制数量来诊断画面。")]
        [Min(0)] private int maximumVisiblePerBatch;

        [SerializeField, InspectorName("投射阴影")]
        private bool castShadows;

        [SerializeField, InspectorName("接收阴影")]
        private bool receiveShadows = true;

        [SerializeField, InspectorName("运行时持续绘制")]
        [Tooltip("保持开启可让同一批持久资产同时出现在 Scene 和 Game。")]
        private bool drawEveryFrame = true;

        [SerializeField, InspectorName("最大水平显示距离")]
        [Tooltip("Unity 载体的性能裁剪距离。0 表示不按距离裁剪；原版实例数据不受修改。")]
        [Min(0.0f)] private float maximumHorizontalDistance = 1200.0f;

        [SerializeField, InspectorName("空间分块尺寸")]
        [Tooltip("仅用于 Unity 视锥裁剪的空间分块，不改变 CK3 原版坐标。")]
        [Min(32.0f)] private float spatialChunkSize = 256.0f;

        [SerializeField, InspectorName("分块包围盒扩展")]
        [Tooltip("为树冠和建筑尺寸扩展裁剪包围盒，避免边缘闪烁。")]
        [Min(0.0f)] private float boundsPadding = 32.0f;

        [SerializeField, InspectorName("使用原版缩放可见性")]
        [Tooltip("启用 game_object layers.txt 的树木与建筑缩放档位隐藏规则。")]
        private bool useOriginalZoomVisibility = true;

        [SerializeField, InspectorName("树木与建筑淡出档位")]
        [Tooltip("原版 game/gfx/map/map_object_data/layers.txt 中 tree_high_layer 与 building_layer 的 fade_out 均为 9。")]
        [Range(0, 34)] private int decorationFadeOutZoomStep = 9;

        [SerializeField, InspectorName("Unity 档位切换淡出时间")]
        [Tooltip("原版透明度和抖动渲染已确认；此时间仅平滑 Unity 相机的离散档位跳变，不声称为原版 CPU 曲线。")]
        [Min(0.01f)] private float unityZoomFadeDuration = 0.25f;

        [NonSerialized] private CK3DecorationData loadedData;
        [NonSerialized] private float loadedChunkSize;
        [NonSerialized] private float loadedBoundsPadding;
        [NonSerialized] private readonly List<LoadedBatch> loadedBatches = new List<LoadedBatch>();
        [NonSerialized] private readonly Dictionary<int, float> cameraOpacities = new Dictionary<int, float>();
        [NonSerialized] private MaterialPropertyBlock drawProperties;

        public CK3DecorationData Data
        {
            get => data;
            set
            {
                data = value;
                Reload();
            }
        }

        private void OnEnable()
        {
            EnsureDrawingResources();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Reload();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            cameraOpacities.Clear();
        }

        private void OnValidate()
        {
            if (loadedData != data ||
                !Mathf.Approximately(loadedChunkSize, spatialChunkSize) ||
                !Mathf.Approximately(loadedBoundsPadding, boundsPadding))
                Reload();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!drawEveryFrame || !showDecorations)
                return;
            if (camera == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;
            if (loadedData != data)
                Reload();
            EnsureDrawingResources();
            Draw(camera);
        }

        private void EnsureDrawingResources()
        {
            // MaterialPropertyBlock owns a native Unity object internally and therefore must not
            // be constructed by a MonoBehaviour field initializer/constructor during serialization.
            if (drawProperties == null)
                drawProperties = new MaterialPropertyBlock();
        }

        private void Reload()
        {
            loadedData = data;
            loadedChunkSize = spatialChunkSize;
            loadedBoundsPadding = boundsPadding;
            loadedBatches.Clear();
            if (data == null || data.Batches == null)
                return;

            foreach (CK3DecorationBatch source in data.Batches)
            {
                if (source == null || source.Mesh == null || source.TransformData == null || source.InstanceCount <= 0)
                    continue;
                Matrix4x4[] matrices = ReadMatrices(source.TransformData.bytes, source.InstanceCount);
                loadedBatches.Add(new LoadedBatch(source, matrices, spatialChunkSize, boundsPadding));
            }
        }

        private void Draw(Camera camera)
        {
            float decorationOpacity = 1.0f;
            if (useOriginalZoomVisibility)
            {
                CK3StrategicMapCamera strategicCamera = camera.GetComponent<CK3StrategicMapCamera>();
                if (strategicCamera != null)
                {
                    float targetOpacity = strategicCamera.ZoomStep < decorationFadeOutZoomStep ? 1.0f : 0.0f;
                    int cameraId = camera.GetInstanceID();
                    if (!cameraOpacities.TryGetValue(cameraId, out decorationOpacity))
                        decorationOpacity = targetOpacity;
                    else
                    {
                        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 1.0f / 120.0f);
                        decorationOpacity = Mathf.MoveTowards(
                            decorationOpacity,
                            targetOpacity,
                            deltaTime / Mathf.Max(0.01f, unityZoomFadeDuration));
                    }
                    cameraOpacities[cameraId] = decorationOpacity;
                    if (decorationOpacity <= 0.0001f)
                        return;
                }
            }
            drawProperties.Clear();
            drawProperties.SetFloat("_GlobalOpacity", decorationOpacity);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            Vector3 cameraPosition = camera.transform.position;
            foreach (LoadedBatch batch in loadedBatches)
            {
                Mesh mesh = batch.Source.Mesh;
                Material[] materials = batch.Source.Materials;
                if (mesh == null || materials == null || materials.Length == 0)
                    continue;

                int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
                int drawnForBatch = 0;
                foreach (LoadedChunk chunk in batch.Chunks)
                {
                    if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.Bounds))
                        continue;
                    if (maximumHorizontalDistance > 0.0f)
                    {
                        float dx = chunk.Bounds.center.x - cameraPosition.x;
                        float dz = chunk.Bounds.center.z - cameraPosition.z;
                        float allowance = Mathf.Max(chunk.Bounds.extents.x, chunk.Bounds.extents.z);
                        float allowed = maximumHorizontalDistance + allowance;
                        if (dx * dx + dz * dz > allowed * allowed)
                            continue;
                    }

                    int count = chunk.Matrices.Length;
                    if (maximumVisiblePerBatch > 0)
                    {
                        count = Mathf.Min(count, maximumVisiblePerBatch - drawnForBatch);
                        if (count <= 0)
                            break;
                    }
                    for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    {
                        Material material = materials[subMesh];
                        if (material == null)
                            continue;
                        Graphics.DrawMeshInstanced(
                            mesh,
                            subMesh,
                            material,
                            chunk.Matrices,
                            count,
                            drawProperties,
                            castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                            receiveShadows,
                            gameObject.layer,
                            camera,
                            LightProbeUsage.Off,
                            null);
                    }
                    drawnForBatch += count;
                }
            }
        }

        private static Matrix4x4[] ReadMatrices(byte[] bytes, int expectedCount)
        {
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int magic = reader.ReadInt32();
                int version = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (magic != BinaryMagic || version != 1 || count < 0 || count != expectedCount)
                    throw new InvalidDataException("CK3 近景装饰实例数据头无效或数量不一致。");

                long requiredLength = 12L + count * TransformFloatCount * sizeof(float);
                if (stream.Length != requiredLength)
                    throw new InvalidDataException("CK3 近景装饰实例数据长度不正确。");

                List<Matrix4x4> result = new List<Matrix4x4>(count);
                for (int index = 0; index < count; index++)
                {
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float rotationLengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                                  rotation.z * rotation.z + rotation.w * rotation.w;
                    if (!IsFinite(position) || !IsFinite(rotationLengthSquared) ||
                        rotationLengthSquared < 0.5f || rotationLengthSquared > 1.5f ||
                        !IsFinite(scale) || scale.x <= 0.0f || scale.y <= 0.0f || scale.z <= 0.0f)
                        continue;
                    rotation = Normalize(rotation, rotationLengthSquared);
                    result.Add(Matrix4x4.TRS(position, rotation, scale));
                }
                return result.ToArray();
            }
        }

        private static Quaternion Normalize(Quaternion value, float lengthSquared)
        {
            float inverseLength = 1.0f / Mathf.Sqrt(lengthSquared);
            return new Quaternion(
                value.x * inverseLength,
                value.y * inverseLength,
                value.z * inverseLength,
                value.w * inverseLength);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private sealed class LoadedBatch
        {
            public readonly CK3DecorationBatch Source;
            public readonly LoadedChunk[] Chunks;

            public LoadedBatch(CK3DecorationBatch source, Matrix4x4[] matrices, float chunkSize, float padding)
            {
                Source = source;
                Dictionary<Vector2Int, List<Matrix4x4>> cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
                float safeChunkSize = Mathf.Max(32.0f, chunkSize);
                foreach (Matrix4x4 matrix in matrices)
                {
                    Vector3 position = matrix.GetColumn(3);
                    Vector2Int key = new Vector2Int(
                        Mathf.FloorToInt(position.x / safeChunkSize),
                        Mathf.FloorToInt(position.z / safeChunkSize));
                    if (!cells.TryGetValue(key, out List<Matrix4x4> cell))
                    {
                        cell = new List<Matrix4x4>();
                        cells.Add(key, cell);
                    }
                    cell.Add(matrix);
                }

                List<LoadedChunk> chunks = new List<LoadedChunk>();
                foreach (List<Matrix4x4> cell in cells.Values)
                {
                    for (int offset = 0; offset < cell.Count; offset += MaximumInstancesPerDraw)
                    {
                        int count = Mathf.Min(MaximumInstancesPerDraw, cell.Count - offset);
                        Matrix4x4[] chunkMatrices = new Matrix4x4[count];
                        cell.CopyTo(offset, chunkMatrices, 0, count);
                        chunks.Add(new LoadedChunk(chunkMatrices, padding));
                    }
                }
                Chunks = chunks.ToArray();
            }
        }

        private sealed class LoadedChunk
        {
            public readonly Matrix4x4[] Matrices;
            public readonly Bounds Bounds;

            public LoadedChunk(Matrix4x4[] matrices, float padding)
            {
                Matrices = matrices;
                Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                foreach (Matrix4x4 matrix in matrices)
                {
                    Vector3 position = matrix.GetColumn(3);
                    minimum = Vector3.Min(minimum, position);
                    maximum = Vector3.Max(maximum, position);
                }
                Bounds bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
                bounds.Expand(Mathf.Max(0.0f, padding) * 2.0f);
                Bounds = bounds;
            }
        }
    }
}
