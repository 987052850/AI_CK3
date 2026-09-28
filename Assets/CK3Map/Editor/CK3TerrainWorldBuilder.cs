using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CK3Map.Editor
{
    public static class CK3TerrainWorldBuilder
    {
        public const string RootName = "CK3原版完整世界地形";
        public const string SourceNodesPath = "Assets/CK3Map/Data/Source/game/map_data/nodes.dat";
        public const string HeightDataPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版高度页表.asset";
        public const string TerrainMaterialPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版地表合成_光照前.mat";

        private const int RecordSize = 32;
        private const int ExpectedRecordCount = 1398101;
        private const float NormQuadtreeToWorld = 16384.0f;

        public static int Build(int forceLodLevel)
        {
            if (forceLodLevel < 0 || forceLodLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(forceLodLevel));
            }

            Mesh terrainMesh = CK3TerrainNodeMeshBuilder.Build();
            Mesh skirtMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CK3TerrainNodeMeshBuilder.SkirtMeshPath);
            CK3TerrainHeightData heightData = AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(HeightDataPath);
            Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (terrainMesh == null || skirtMesh == null || heightData == null || terrainMaterial == null)
            {
                throw new InvalidOperationException("缺少原版节点网格、裙边网格、高度页表或地表材质资产。");
            }

            BindHeightData(terrainMaterial, heightData);

            string absoluteNodesPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "CK3Map/Data/Source/game/map_data/nodes.dat"));
            byte[] bytes = File.ReadAllBytes(absoluteNodesPath);
            if (bytes.Length != ExpectedRecordCount * RecordSize)
            {
                throw new InvalidDataException($"nodes.dat 尺寸错误：{bytes.Length}。");
            }

            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "构建 CK3 原版完整世界地形");
            CK3TerrainMaterialLibrary materialLibrary =
                AssetDatabase.LoadAssetAtPath<CK3TerrainMaterialLibrary>(
                    CK3TerrainMaterialBuilder.LibraryPath);
            if (materialLibrary == null)
            {
                throw new InvalidOperationException("缺少 CK3 原版地表材质库，请先完成阶段 2。");
            }
            CK3TerrainSurfaceBinding surfaceBinding =
                Undo.AddComponent<CK3TerrainSurfaceBinding>(root);
            surfaceBinding.Configure(materialLibrary, terrainMaterial);
            int firstNode = GetLevelStart(forceLodLevel);
            int levelNodeCount = 1 << (forceLodLevel * 2);
            int created = 0;

            try
            {
                for (int localIndex = 0; localIndex < levelNodeCount; localIndex++)
                {
                    if ((localIndex & 63) == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "CK3 完整世界地形",
                            $"构建原版 ForceLodLevel {forceLodLevel}：{localIndex}/{levelNodeCount}",
                            localIndex / (float)levelNodeCount);
                    }

                    int recordIndex = firstNode + localIndex;
                    int offset = recordIndex * RecordSize;
                    bool outsideOrEmpty = bytes[offset + 28] != 0;
                    if (outsideOrEmpty)
                    {
                        continue;
                    }

                    float nodeX = ReadSingle(bytes, offset);
                    float nodeY = ReadSingle(bytes, offset + 4);
                    float storedScale = ReadSingle(bytes, offset + 8);
                    if (!(storedScale > 0.0f))
                    {
                        throw new InvalidDataException($"nodes.dat 节点 {recordIndex} 缩放无效。");
                    }

                    // CK3's CalcTerrainVertex inverts NodeScale, then multiplies
                    // NodeOffset by that scale. nodes.dat stores normalized absolute
                    // offsets, so the instance stream must carry offsets in node-grid
                    // coordinates for the shader to reconstruct:
                    // nodeX + WithinNodePos.x * storedScale (same for Y).
                    Vector4 nodeData = new Vector4(
                        nodeX / storedScale,
                        nodeY / storedScale,
                        1.0f / storedScale,
                        0.0f);
                    string nodeName = $"节点_{recordIndex}_{localIndex}";
                    CreateRenderer(root.transform, nodeName, terrainMesh, terrainMaterial, nodeData, storedScale, false);
                    CreateRenderer(root.transform, nodeName + "_裙边", skirtMesh, terrainMaterial, nodeData, storedScale, true);
                    created++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            Selection.activeGameObject = root;
            ConfigureMainCamera();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            FocusSceneView();
            return created;
        }

        private static void ConfigureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // CK3 NCamera values from game/common/defines/graphic/00_graphics.txt:
            // FOV=60, ZNEAR=10, ZFAR=100000, START_LOOK_AT=(5000, 0, 2300),
            // START_ZOOM_STEP=33 -> distance 5464 and tilt 85 degrees.
            const float fieldOfView = 60.0f;
            const float nearClip = 10.0f;
            const float farClip = 100000.0f;
            const float startDistance = 5464.0f;
            const float startTilt = 85.0f;
            Vector3 lookAt = new Vector3(5000.0f, 0.0f, 2300.0f);
            Quaternion rotation = Quaternion.Euler(startTilt, 0.0f, 0.0f);

            Undo.RecordObject(camera, "应用 CK3 原版地图相机参数");
            Undo.RecordObject(camera.transform, "应用 CK3 原版地图相机参数");
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = nearClip;
            camera.farClipPlane = farClip;
            camera.orthographic = false;
            camera.transform.SetPositionAndRotation(
                lookAt - rotation * Vector3.forward * startDistance,
                rotation);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
        }

        public static void FocusSceneView()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            // Editor inspection only: this does not define or modify the CK3 game camera.
            Vector3 worldCenter = new Vector3(9215.0f * 0.5f, 25.0f, 4607.0f * 0.5f);
            sceneView.LookAt(
                worldCenter,
                Quaternion.Euler(90.0f, 0.0f, 0.0f),
                5200.0f,
                true,
                true);
            sceneView.Repaint();
        }

        private static void CreateRenderer(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Vector4 nodeData,
            float storedScale,
            bool skirt)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            float nodeWorldSize = storedScale * NormQuadtreeToWorld;
            gameObject.transform.localPosition = new Vector3(
                nodeData.x * storedScale * NormQuadtreeToWorld,
                25.0f,
                nodeData.y * storedScale * NormQuadtreeToWorld);
            gameObject.transform.localScale = new Vector3(nodeWorldSize, 1.0f, nodeWorldSize);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            CK3TerrainNodeBinding binding = gameObject.AddComponent<CK3TerrainNodeBinding>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            binding.Configure(nodeData, skirt);
        }

        private static void BindHeightData(Material material, CK3TerrainHeightData data)
        {
            material.SetTexture("_CK3HeightLookupTexture", data.IndirectionTexture);
            material.SetTexture("_CK3PackedHeightTexture", data.PackedHeightTexture);
            material.SetVector("_CK3WorldSpaceToLookup", data.WorldSpaceToLookup);
            material.SetVector(
                "_CK3IndirectionSize",
                new Vector4(data.IndirectionSize.x, data.IndirectionSize.y, 0.0f, 0.0f));
            material.SetVector(
                "_CK3PackedHeightMapSize",
                new Vector4(data.PackedHeightmapSize.x, data.PackedHeightmapSize.y, 0.0f, 0.0f));
            material.SetFloat("_CK3BaseTileSize", data.BaseTileSize);
            material.SetFloat("_CK3HeightScale", data.HeightScale);
            material.SetFloat("_CK3NormQuadtreeToWorld", NormQuadtreeToWorld);
            material.SetFloat("_CK3SkirtSize", -data.HeightScale * 0.1f);
            material.SetVector("_CK3WorldExtents", data.WorldExtents);
            Vector4[] constants = data.TileToHeightmapScaleAndOffset;
            for (int i = 0; i < constants.Length; i++)
            {
                material.SetVector($"_CK3TileToHeightMap{i}", constants[i]);
            }
            EditorUtility.SetDirty(material);
        }

        private static int GetLevelStart(int level)
        {
            return ((1 << (level * 2)) - 1) / 3;
        }

        private static float ReadSingle(byte[] bytes, int offset)
        {
            return BitConverter.ToSingle(bytes, offset);
        }
    }
}
