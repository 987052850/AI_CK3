using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CK3Map.Editor
{
    public readonly struct CK3TabletopFlatMapBuildResult
    {
        public readonly int TabletopObjectCount;
        public readonly Texture2D FlatMap;
        public readonly Texture2D PaperTearMask;
        public readonly GameObject TabletopRoot;
        public readonly GameObject SurroundObject;

        public CK3TabletopFlatMapBuildResult(
            int tabletopObjectCount,
            Texture2D flatMap,
            Texture2D paperTearMask,
            GameObject tabletopRoot,
            GameObject surroundObject)
        {
            TabletopObjectCount = tabletopObjectCount;
            FlatMap = flatMap;
            PaperTearMask = paperTearMask;
            TabletopRoot = tabletopRoot;
            SurroundObject = surroundObject;
        }
    }

    public static class CK3TabletopFlatMapBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Tabletop";
        public const string TabletopRootName = "CK3原版西式地图桌面";
        public const string SurroundObjectName = "CK3原版世界外侧地图";

        private const string SourceRoot = "Assets/CK3Map/Data/Source/game";
        private const string TerrainMaterialPath =
            "Assets/CK3Map/Data/Generated/Terrain/CK3原版地表合成_光照前.mat";
        private const string WaterMaterialPath =
            "Assets/CK3Map/Data/Generated/Water/CK3原版低规格水面.mat";
        private const float FlatMapHeight = 3.92f;
        private const float WorldWidth = 9216.0f;
        private const float WorldHeight = 4608.0f;

        private static readonly TabletopDefinition[] TabletopDefinitions =
        {
            new TabletopDefinition("地图桌面", "tabletop_west_basic", new Vector3(4500, -15, 2560)),
            new TabletopDefinition("桌面蜡烛", "tabletop_west_basic_candles", new Vector3(4500, -1, 2560)),
            new TabletopDefinition("桌布", "tabletop_west_basic_tablecloth", new Vector3(4500, -20, 2560)),
            new TabletopDefinition("桌面道具", "tabletop_west_basic_props", new Vector3(4500, -1, 2560))
        };

        public static CK3TabletopFlatMapBuildResult BuildAndBind()
        {
            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/Meshes");
            EnsureFolder(OutputRoot + "/Materials");

            Texture2D flatMap = ImportDds(
                SourceRoot + "/gfx/map/terrain/flat_maps/flatmap.dds",
                OutputRoot + "/Textures/CK3原版平面地图.asset",
                TextureWrapMode.Clamp);
            Texture2D paperTearMask = ImportDds(
                SourceRoot + "/gfx/map/terrain/flat_maps/paper_tear_mask.dds",
                OutputRoot + "/Textures/CK3原版撕纸遮罩.asset",
                TextureWrapMode.Repeat);
            Texture2D surroundMask = ImportDds(
                SourceRoot + "/gfx/map/surround_map/surround_mask.dds",
                OutputRoot + "/Textures/CK3原版外侧遮罩.asset",
                TextureWrapMode.Clamp);
            Texture2D surroundFade = ImportDds(
                SourceRoot + "/gfx/map/surround_map/surround_fade.dds",
                OutputRoot + "/Textures/CK3原版外侧淡出.asset",
                TextureWrapMode.Clamp);

            GameObject tabletopRoot = BuildTabletopScene();
            BindFlatMap(flatMap, paperTearMask);
            GameObject surround = BuildSurroundScene(surroundMask, surroundFade);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = tabletopRoot;
            return new CK3TabletopFlatMapBuildResult(
                TabletopDefinitions.Length,
                flatMap,
                paperTearMask,
                tabletopRoot,
                surround);
        }

        private static GameObject BuildTabletopScene()
        {
            GameObject root = GameObject.Find(TabletopRootName);
            if (root == null)
            {
                root = new GameObject(TabletopRootName);
                Undo.RegisterCreatedObjectUndo(root, "创建 CK3 原版西式地图桌面");
            }

            while (root.transform.childCount > 0)
                Undo.DestroyObjectImmediate(root.transform.GetChild(0).gameObject);
            if (root.GetComponent<CK3TabletopVisibility>() == null)
                Undo.AddComponent<CK3TabletopVisibility>(root);

            foreach (TabletopDefinition definition in TabletopDefinitions)
            {
                string meshAssetPath = SourceRoot + "/gfx/models/tabletop/" +
                                       definition.AssetName + ".mesh.ck3source";
                string fullMeshPath = AssetPathToFullPath(meshAssetPath);
                if (!File.Exists(fullMeshPath))
                    throw new FileNotFoundException("请先重新执行原版数据同步，缺少桌面 Mesh。", meshAssetPath);

                CK3DecorationBuilder.ImportedModel imported = CK3DecorationBuilder.ImportStaticPdxModel(
                    fullMeshPath,
                    OutputRoot,
                    definition.AssetName);
                GameObject child = new GameObject(definition.DisplayName);
                Undo.RegisterCreatedObjectUndo(child, "创建 CK3 桌面部件");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = definition.Position;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one * 5.0f;
                MeshFilter filter = Undo.AddComponent<MeshFilter>(child);
                MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(child);
                filter.sharedMesh = imported.Mesh;
                renderer.sharedMaterials = imported.Materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return root;
        }

        private static void BindFlatMap(Texture2D flatMap, Texture2D paperTearMask)
        {
            Material terrain = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (terrain == null)
            {
                CK3TerrainSurfaceBinding binding = UnityEngine.Object.FindObjectOfType<CK3TerrainSurfaceBinding>();
                terrain = binding != null ? binding.TerrainSurfaceMaterial : null;
            }
            Material water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (water == null)
            {
                CK3WaterSurfaceBinding binding = UnityEngine.Object.FindObjectOfType<CK3WaterSurfaceBinding>();
                water = binding != null ? binding.WaterMaterial : null;
            }
            if (terrain == null || water == null)
                throw new InvalidOperationException("请先完成地形与海洋水面的构建。");

            SetFlatMapTextures(terrain, flatMap, paperTearMask);
            SetFlatMapTextures(water, flatMap, paperTearMask);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(water);
        }

        private static void SetFlatMapTextures(Material material, Texture2D flatMap, Texture2D paperTearMask)
        {
            material.SetTexture("_FlatMapTexture", flatMap);
            material.SetTexture("_PaperTearMask", paperTearMask);
            material.SetFloat("_FlatMapHeight", FlatMapHeight);
        }

        private static GameObject BuildSurroundScene(Texture2D surroundMask, Texture2D surroundFade)
        {
            Mesh mesh = BuildOrReplaceQuad(
                OutputRoot + "/Meshes/CK3原版世界外侧地图网格.asset",
                FlatMapHeight);
            Shader shader = Shader.Find("CK3Map/Surround Map");
            if (shader == null)
                throw new InvalidOperationException("找不到 CK3Map/Surround Map Shader，或 Shader 尚未编译。");
            string materialPath = OutputRoot + "/Materials/CK3原版世界外侧地图.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CK3原版世界外侧地图" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else material.shader = shader;
            material.SetTexture("_SurroundMask", surroundMask);
            material.SetTexture("_SurroundFade", surroundFade);
            material.SetFloat("_FlatMapHeight", FlatMapHeight);
            EditorUtility.SetDirty(material);

            GameObject target = GameObject.Find(SurroundObjectName);
            if (target == null)
            {
                target = new GameObject(SurroundObjectName);
                Undo.RegisterCreatedObjectUndo(target, "创建 CK3 原版世界外侧地图");
            }
            MeshFilter filter = GetOrAdd<MeshFilter>(target);
            MeshRenderer renderer = GetOrAdd<MeshRenderer>(target);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;

            CK3MapModeController controller = UnityEngine.Object.FindObjectOfType<CK3MapModeController>();
            if (controller != null)
                controller.ConfigureSurround(material);
            return target;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T value = target.GetComponent<T>();
            return value != null ? value : Undo.AddComponent<T>(target);
        }

        private static Mesh BuildOrReplaceQuad(string path, float height)
        {
            Mesh mesh = new Mesh { name = "CK3原版世界外侧地图网格" };
            mesh.vertices = new[]
            {
                new Vector3(0, height, 0), new Vector3(0, height, WorldHeight),
                new Vector3(WorldWidth, height, 0), new Vector3(WorldWidth, height, WorldHeight)
            };
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0);
            mesh.bounds = new Bounds(
                new Vector3(WorldWidth * 0.5f, height, WorldHeight * 0.5f),
                new Vector3(WorldWidth, 4.0f, WorldHeight));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Texture2D ImportDds(string sourceAssetPath, string outputPath, TextureWrapMode wrap)
        {
            string fullPath = AssetPathToFullPath(sourceAssetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("缺少已同步的 CK3 原版 DDS。", sourceAssetPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length < 128 || BitConverter.ToUInt32(bytes, 0) != 0x20534444 ||
                BitConverter.ToUInt32(bytes, 4) != 124)
                throw new InvalidDataException("不是受支持的 CK3 DDS：" + sourceAssetPath);
            int height = BitConverter.ToInt32(bytes, 12);
            int width = BitConverter.ToInt32(bytes, 16);
            int mipCount = Mathf.Max(1, BitConverter.ToInt32(bytes, 28));
            uint fourCc = BitConverter.ToUInt32(bytes, 84);
            TextureFormat format = fourCc == 0x31545844 ? TextureFormat.DXT1 :
                fourCc == 0x35545844 ? TextureFormat.DXT5 :
                throw new InvalidDataException("当前阶段只接受 CK3 的 DXT1/DXT5 DDS：" + sourceAssetPath);
            byte[] payload = new byte[bytes.Length - 128];
            Buffer.BlockCopy(bytes, 128, payload, 0, payload.Length);
            Texture2D texture = new Texture2D(width, height, format, mipCount > 1, false)
            {
                name = Path.GetFileNameWithoutExtension(outputPath),
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            texture.LoadRawTextureData(payload);
            texture.Apply(false, true);
            if (AssetDatabase.LoadMainAssetAtPath(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);
            AssetDatabase.CreateAsset(texture, outputPath);
            return texture;
        }

        private static string AssetPathToFullPath(string assetPath) =>
            Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct TabletopDefinition
        {
            public readonly string DisplayName;
            public readonly string AssetName;
            public readonly Vector3 Position;
            public TabletopDefinition(string displayName, string assetName, Vector3 position)
            {
                DisplayName = displayName;
                AssetName = assetName;
                Position = position;
            }
        }
    }
}
