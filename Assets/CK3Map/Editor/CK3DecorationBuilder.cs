using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CK3Map.Editor
{
    public sealed class CK3DecorationBuildResult
    {
        public CK3DecorationData Data { get; internal set; }
        public int TreePrototypeCount { get; internal set; }
        public int TreeInstanceCount { get; internal set; }
        public int BuildingPrototypeCount { get; internal set; }
        public int BuildingInstanceCount { get; internal set; }
        public Mesh RockPrototype { get; internal set; }
    }

    public static class CK3DecorationBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Decorations";
        public const string DataPath = OutputRoot + "/CK3原版近景装饰数据.asset";
        public const string SceneRootName = "CK3原版近景装饰";

        private const int BinaryMagic = 0x334B4344; // DCK3
        private const string HeightDataPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版高度页表.asset";
        private const string PackedHeightSourcePath = "Assets/CK3Map/Data/Source/game/map_data/packed_heightmap.png";
        private const string IndirectionHeightSourcePath = "Assets/CK3Map/Data/Source/game/map_data/indirection_heightmap.png";
        private const string MirroredGameRoot = "Assets/CK3Map/Data/Source/game";
        private const string TabletopEnvironmentPath =
            MirroredGameRoot + "/gfx/portraits/environments/castle_interior_01_fire.dds";

        private static Dictionary<string, string> mirroredTextureIndex;

        public static CK3DecorationBuildResult BuildAndBind()
        {
            CK3TerrainHeightData heightData = AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(HeightDataPath);
            if (heightData == null)
                throw new InvalidOperationException("请先构建阶段 3 的原版高度页表。");

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Meshes");
            EnsureFolder(OutputRoot + "/Materials");
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/Instances");

            string originalRoot = GetOriginalGameRoot();
            List<TreeDefinition> treeDefinitions = DiscoverTreeDefinitions(originalRoot);
            List<CK3DecorationBatch> batches = new List<CK3DecorationBatch>();
            int totalInstances = 0;
            HeightSampler heightSampler = HeightSampler.Create(heightData);

            try
            {
                Dictionary<string, ImportedModel> importedTrees = new Dictionary<string, ImportedModel>(StringComparer.Ordinal);
                for (int index = 0; index < treeDefinitions.Count; index++)
                {
                    TreeDefinition definition = treeDefinitions[index];
                    EditorUtility.DisplayProgressBar(
                        "CK3 原版近景装饰",
                        $"导入 {definition.PdxMeshName} 并读取原版实例",
                        index / (float)(treeDefinitions.Count + 2));

                    if (!importedTrees.TryGetValue(definition.PdxMeshName, out ImportedModel model))
                    {
                        model = ImportPdxModel(
                            definition.SourceMeshPath,
                            definition.SourceTextureDirectory,
                            definition.OutputName,
                            true,
                            1);
                        importedTrees.Add(definition.PdxMeshName, model);
                    }

                    string instanceSource = Path.Combine(
                        originalRoot,
                        "gfx/map/map_object_data/generated",
                        definition.InstanceFileName);
                    string binaryAssetPath = OutputRoot + "/Instances/" + Path.GetFileNameWithoutExtension(definition.InstanceFileName) + ".bytes";
                    int instanceCount = WriteOriginalTransforms(instanceSource, binaryAssetPath, heightSampler);
                    TextAsset transformData = AssetDatabase.LoadAssetAtPath<TextAsset>(binaryAssetPath);

                    CK3DecorationBatch batch = new CK3DecorationBatch();
                    batch.Replace(definition.PdxMeshName, model.Mesh, model.Materials, transformData, instanceCount);
                    batches.Add(batch);
                    totalInstances += instanceCount;
                }

                string holdingDirectory = Path.Combine(originalRoot, "gfx/models/buildings/holdings");
                ImportedModel castleModel = ImportPdxModel(
                    Path.Combine(holdingDirectory, "building_western_castle_01.mesh"),
                    holdingDirectory,
                    "building_western_castle_01",
                    false,
                    1);
                List<ProvinceLocator> castleLocators = ReadOriginalCastleLocators(originalRoot);
                string castleBinaryPath = OutputRoot + "/Instances/building_western_castle_01_原版位置.bytes";
                WriteHoldingSampleTransforms(castleBinaryPath, heightSampler, castleLocators.ToArray());
                CK3DecorationBatch castleBatch = new CK3DecorationBatch();
                castleBatch.Replace(
                    "building_western_castle_01_mesh",
                    castleModel.Mesh,
                    castleModel.Materials,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(castleBinaryPath),
                    castleLocators.Count);
                batches.Add(castleBatch);

                EditorUtility.DisplayProgressBar(
                    "CK3 原版近景装饰",
                    "导入原版山石原型 cliff_big_01",
                    treeDefinitions.Count / (float)(treeDefinitions.Count + 2));
                string cliffDirectory = Path.Combine(originalRoot, "gfx/models/mapitems/cliffs");
                ImportedModel cliff = ImportPdxModel(
                    Path.Combine(cliffDirectory, "cliff_big_01.mesh"),
                    cliffDirectory,
                    "cliff_big_01",
                    false,
                    0);

                CK3DecorationData data = LoadOrCreate<CK3DecorationData>(DataPath);
                data.ReplaceData(batches.ToArray());
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                BindScene(data);

                return new CK3DecorationBuildResult
                {
                    Data = data,
                    TreePrototypeCount = treeDefinitions.Count,
                    TreeInstanceCount = totalInstances,
                    BuildingPrototypeCount = 1,
                    BuildingInstanceCount = castleLocators.Count,
                    RockPrototype = cliff.Mesh
                };
            }
            finally
            {
                heightSampler.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<TreeDefinition> DiscoverTreeDefinitions(string originalRoot)
        {
            string treeModelRoot = Path.Combine(originalRoot, "gfx/models/mapitems/trees");
            string generatedRoot = Path.Combine(originalRoot, "gfx/map/map_object_data/generated");
            Dictionary<string, TreeModelSource> models = new Dictionary<string, TreeModelSource>(StringComparer.Ordinal);
            foreach (string assetPath in Directory.EnumerateFiles(treeModelRoot, "*.asset", SearchOption.AllDirectories))
            {
                string meshName = null;
                string meshFile = null;
                foreach (string rawLine in File.ReadLines(assetPath))
                {
                    string line = rawLine.Trim();
                    if (meshName == null && line.StartsWith("name", StringComparison.Ordinal))
                        meshName = ReadQuotedValue(line);
                    else if (meshFile == null && line.StartsWith("file", StringComparison.Ordinal))
                        meshFile = ReadQuotedValue(line);
                    if (meshName != null && meshFile != null)
                        break;
                }
                if (!string.IsNullOrEmpty(meshName) && !string.IsNullOrEmpty(meshFile))
                {
                    string directory = Path.GetDirectoryName(assetPath);
                    models[meshName] = new TreeModelSource(
                        Path.Combine(directory, meshFile),
                        directory,
                        Path.GetFileNameWithoutExtension(meshFile));
                }
            }

            List<TreeDefinition> definitions = new List<TreeDefinition>();
            foreach (string instancePath in Directory.EnumerateFiles(generatedRoot, "tree*generator*.txt", SearchOption.TopDirectoryOnly))
            {
                string pdxMeshName = null;
                foreach (string rawLine in File.ReadLines(instancePath))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("pdxmesh", StringComparison.Ordinal))
                    {
                        pdxMeshName = ReadQuotedValue(line);
                        break;
                    }
                }
                if (string.IsNullOrEmpty(pdxMeshName))
                    throw new InvalidDataException("原版树木生成文件缺少 pdxmesh：" + instancePath);
                if (!models.TryGetValue(pdxMeshName, out TreeModelSource source))
                    throw new InvalidDataException("找不到原版树木 PDX Mesh 对应的 .asset/.mesh：" + pdxMeshName);
                definitions.Add(new TreeDefinition(
                    pdxMeshName,
                    source.MeshPath,
                    source.TextureDirectory,
                    source.OutputName,
                    Path.GetFileName(instancePath)));
            }
            definitions.Sort((left, right) => string.CompareOrdinal(left.InstanceFileName, right.InstanceFileName));
            return definitions;
        }

        private static string ReadQuotedValue(string line)
        {
            int first = line.IndexOf('"');
            int last = line.LastIndexOf('"');
            return first >= 0 && last > first ? line.Substring(first + 1, last - first - 1) : null;
        }

        private static List<ProvinceLocator> ReadOriginalCastleLocators(string originalRoot)
        {
            HashSet<int> castleProvinceIds = new HashSet<int>();
            string historyRoot = Path.Combine(originalRoot, "history/provinces");
            Regex provinceStart = new Regex(@"^\s*(\d+)\s*=\s*\{", RegexOptions.CultureInvariant);
            foreach (string file in Directory.EnumerateFiles(historyRoot, "*.txt", SearchOption.TopDirectoryOnly))
            {
                int depth = 0;
                int provinceId = -1;
                foreach (string rawLine in File.ReadLines(file))
                {
                    string line = rawLine;
                    int comment = line.IndexOf('#');
                    if (comment >= 0)
                        line = line.Substring(0, comment);
                    if (provinceId < 0)
                    {
                        Match start = provinceStart.Match(line);
                        if (!start.Success)
                            continue;
                        provinceId = int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
                    }

                    int depthBefore = depth;
                    depth += CountCharacter(line, '{') - CountCharacter(line, '}');
                    if (depthBefore == 1 && line.IndexOf("holding = castle_holding", StringComparison.Ordinal) >= 0)
                        castleProvinceIds.Add(provinceId);
                    if (depth <= 0)
                    {
                        depth = 0;
                        provinceId = -1;
                    }
                }
            }

            string locatorPath = Path.Combine(originalRoot, "gfx/map/map_object_data/building_locators.txt");
            string locatorText = File.ReadAllText(locatorPath, Encoding.UTF8);
            Regex locatorPattern = new Regex(
                @"(?ms)\{\s*id\s*=\s*(\d+)\s*position\s*=\s*\{\s*([-+\d\.]+)\s+([-+\d\.]+)\s+([-+\d\.]+)\s*\}\s*rotation\s*=\s*\{\s*([-+\d\.]+)\s+([-+\d\.]+)\s+([-+\d\.]+)\s+([-+\d\.]+)\s*\}\s*scale\s*=\s*\{\s*([-+\d\.]+)\s+([-+\d\.]+)\s+([-+\d\.]+)",
                RegexOptions.CultureInvariant);
            List<ProvinceLocator> result = new List<ProvinceLocator>();
            foreach (Match match in locatorPattern.Matches(locatorText))
            {
                int provinceId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!castleProvinceIds.Contains(provinceId))
                    continue;
                Quaternion rotation = new Quaternion(
                    float.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture));
                Vector3 scale = new Vector3(
                    float.Parse(match.Groups[9].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[10].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[11].Value, CultureInfo.InvariantCulture));
                float rotationLengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                              rotation.z * rotation.z + rotation.w * rotation.w;
                if (rotationLengthSquared < 0.5f || rotationLengthSquared > 1.5f ||
                    scale.x <= 0.0f || scale.y <= 0.0f || scale.z <= 0.0f)
                    continue;
                rotation = new Quaternion(
                    rotation.x / Mathf.Sqrt(rotationLengthSquared),
                    rotation.y / Mathf.Sqrt(rotationLengthSquared),
                    rotation.z / Mathf.Sqrt(rotationLengthSquared),
                    rotation.w / Mathf.Sqrt(rotationLengthSquared));
                result.Add(new ProvinceLocator(
                    float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture),
                    rotation,
                    scale,
                    float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)));
            }
            if (result.Count == 0)
                throw new InvalidDataException("未能从原版 building_locators.txt 与省份历史的 castle_holding 交集中得到城堡位置。");
            return result;
        }

        private static int CountCharacter(string value, char character)
        {
            int count = 0;
            foreach (char current in value)
            {
                if (current == character)
                    count++;
            }
            return count;
        }

        internal static ImportedModel ImportStaticPdxModel(
            string sourceMeshPath,
            string outputRoot,
            string outputName)
        {
            EnsureFolder(outputRoot);
            EnsureFolder(outputRoot + "/Meshes");
            EnsureFolder(outputRoot + "/Materials");
            EnsureFolder(outputRoot + "/Textures");
            return ImportPdxModel(
                sourceMeshPath,
                Path.GetDirectoryName(sourceMeshPath),
                outputName,
                false,
                0,
                outputRoot,
                true);
        }

        private static ImportedModel ImportPdxModel(
            string sourceMeshPath,
            string sourceTextureDirectory,
            string outputName,
            bool treeMaterial,
            int maximumParts,
            string outputRoot = OutputRoot,
            bool tabletopMaterial = false)
        {
            if (!File.Exists(sourceMeshPath))
                throw new FileNotFoundException("找不到 CK3 原版 PDX Mesh。", sourceMeshPath);

            PdxModel model = PdxBinaryMeshReader.Read(sourceMeshPath, treeMaterial);
            ApplyAssetTextureMetadata(sourceMeshPath, model);
            if (maximumParts > 0 && model.Parts.Count > maximumParts)
                model.Parts.RemoveRange(maximumParts, model.Parts.Count - maximumParts);
            Mesh mesh = BuildUnityMesh(model, outputName);
            string meshPath = outputRoot + "/Meshes/" + outputName + ".asset";
            mesh = SaveMesh(mesh, meshPath);

            Material[] materials = new Material[model.Parts.Count];
            for (int index = 0; index < model.Parts.Count; index++)
            {
                PdxMeshPart part = model.Parts[index];
                Texture2D diffuse = CopyTexture(sourceTextureDirectory, part.DiffuseTexture, outputRoot);
                Texture2D normal = CopyTexture(sourceTextureDirectory, part.NormalTexture, outputRoot);
                Texture2D properties = CopyTexture(
                    sourceTextureDirectory, part.SpecularTexture, outputRoot);
                Texture2D tint = treeMaterial
                    ? CopyTexture(sourceTextureDirectory, FindIndexedTexture(sourceMeshPath, 3), outputRoot)
                    : null;
                if (tabletopMaterial && (diffuse == null || normal == null || properties == null))
                {
                    throw new FileNotFoundException(
                        "CK3 桌面子网格缺少原版材质贴图。请先重新同步原版数据。" +
                        $"\n模型：{Path.GetFileName(sourceMeshPath)}" +
                        $"\n子网格：{index}" +
                        $"\n漫反射：{part.DiffuseTexture}" +
                        $"\n法线：{part.NormalTexture}" +
                        $"\n属性：{part.SpecularTexture}");
                }
                materials[index] = SaveCarrierMaterial(
                    outputRoot + "/Materials/" + outputName + "_" + index + ".mat",
                    diffuse,
                    normal,
                    tint,
                    treeMaterial,
                    properties,
                    tabletopMaterial,
                    part.ShaderName);
            }

            return new ImportedModel(mesh, materials);
        }

        private static Mesh BuildUnityMesh(PdxModel model, string name)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Vector2> uvs = new List<Vector2>();
            List<int[]> subMeshes = new List<int[]>();

            foreach (PdxMeshPart part in model.Parts)
            {
                int baseVertex = vertices.Count;
                vertices.AddRange(part.Positions);
                normals.AddRange(part.Normals);
                tangents.AddRange(part.Tangents);
                uvs.AddRange(part.Uvs);
                int[] indices = new int[part.Indices.Length];
                for (int index = 0; index < indices.Length; index++)
                    indices[index] = part.Indices[index] + baseVertex;
                subMeshes.Add(indices);
            }

            Mesh mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
                mesh.SetNormals(normals);
            if (tangents.Count == vertices.Count)
                mesh.SetTangents(tangents);
            if (uvs.Count == vertices.Count)
                mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subMeshes.Count;
            for (int index = 0; index < subMeshes.Count; index++)
                mesh.SetTriangles(subMeshes[index], index, false);
            if (normals.Count != vertices.Count)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int WriteOriginalTransforms(
            string sourcePath,
            string assetPath,
            HeightSampler heightSampler)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("找不到 CK3 原版生成式树木实例文件。", sourcePath);

            int transformCount = 0;
            bool insideTransforms = false;
            string fullOutputPath = AssetPathToFullPath(assetPath);
            using (StreamReader reader = new StreamReader(sourcePath, Encoding.UTF8, true, 1 << 16))
            using (FileStream stream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(BinaryMagic);
                writer.Write(1);
                writer.Write(0);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!insideTransforms)
                    {
                        int marker = line.IndexOf("transform=\"", StringComparison.Ordinal);
                        if (marker < 0)
                            continue;
                        insideTransforms = true;
                        line = line.Substring(marker + 11);
                    }

                    bool lastLine = line.IndexOf('"') >= 0;
                    if (lastLine)
                        line = line.Substring(0, line.IndexOf('"'));
                    string[] values = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length > 0)
                    {
                        if (values.Length != 10)
                            throw new InvalidDataException($"原版实例变换字段不是 10 个浮点数：{sourcePath}");
                        float[] transform = new float[10];
                        for (int index = 0; index < transform.Length; index++)
                            transform[index] = float.Parse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture);
                        transform[1] = SampleOriginalHeight(heightSampler, new Vector2(transform[0], transform[2]));
                        foreach (float value in transform)
                            writer.Write(value);
                        transformCount++;
                    }
                    if (lastLine)
                        break;
                }
                if (transformCount <= 0)
                    throw new InvalidDataException("原版树木实例文件中没有 transform 数据：" + sourcePath);
                writer.Flush();
                stream.Position = sizeof(int) * 2;
                writer.Write(transformCount);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return transformCount;
        }

        private static void WriteHoldingSampleTransforms(
            string assetPath,
            HeightSampler heightSampler,
            ProvinceLocator[] locators)
        {
            using (FileStream stream = new FileStream(AssetPathToFullPath(assetPath), FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(BinaryMagic);
                writer.Write(1);
                writer.Write(locators.Length);
                foreach (ProvinceLocator locator in locators)
                {
                    float terrainHeight = SampleOriginalHeight(heightSampler, new Vector2(locator.X, locator.Z));
                    writer.Write(locator.X);
                    writer.Write(terrainHeight + locator.Height);
                    writer.Write(locator.Z);
                    writer.Write(locator.Rotation.x);
                    writer.Write(locator.Rotation.y);
                    writer.Write(locator.Rotation.z);
                    writer.Write(locator.Rotation.w);
                    writer.Write(locator.Scale.x);
                    writer.Write(locator.Scale.y);
                    writer.Write(locator.Scale.z);
                }
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static float SampleOriginalHeight(HeightSampler sampler, Vector2 world)
        {
            CK3TerrainHeightData data = sampler.Data;
            Texture2D lookup = sampler.Indirection;
            Texture2D packed = sampler.Packed;

            Vector2 lookupCoordinate = new Vector2(
                Mathf.Clamp(world.x * data.WorldSpaceToLookup.x, 0.0f, 0.999999f),
                Mathf.Clamp(world.y * data.WorldSpaceToLookup.y, 0.0f, 0.999999f));
            int lookupX = Mathf.Clamp(Mathf.FloorToInt(lookupCoordinate.x * data.IndirectionSize.x), 0, data.IndirectionSize.x - 1);
            int lookupY = Mathf.Clamp(Mathf.FloorToInt(lookupCoordinate.y * data.IndirectionSize.y), 0, data.IndirectionSize.y - 1);
            Color32 indirect = lookup.GetPixel(lookupX, lookupY);
            float currentTileSize = (data.BaseTileSize - 1.0f) / indirect.b + 1.0f;
            float tileOffset = 0.5f / currentTileSize;
            float tileScale = (currentTileSize - 1.0f) / currentTileSize;
            Vector2 within = new Vector2(
                Mathf.Repeat(lookupCoordinate.x * data.IndirectionSize.x, 1.0f),
                Mathf.Repeat(lookupCoordinate.y * data.IndirectionSize.y, 1.0f));
            Vector2 tileUv = new Vector2(indirect.r, indirect.g) + Vector2.one * tileOffset + within * tileScale;
            int level = Mathf.Clamp(indirect.a, 0, data.TileToHeightmapScaleAndOffset.Length - 1);
            Vector4 transform = data.TileToHeightmapScaleAndOffset[level];
            Vector2 packedUv = new Vector2(tileUv.x * transform.x + transform.z, tileUv.y * transform.y + transform.w);
            return packed.GetPixelBilinear(packedUv.x, packedUv.y).r * data.HeightScale;
        }

        private static Texture2D CopyTexture(
            string sourceDirectory,
            string fileName,
            string outputRoot = OutputRoot)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;
            string sourcePath = Path.Combine(sourceDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                string textureName = Path.GetFileName(fileName);
                Dictionary<string, string> index = GetMirroredTextureIndex();
                if (!index.TryGetValue(textureName, out sourcePath))
                    return null;
            }
            string assetPath = outputRoot + "/Textures/" + Path.GetFileName(fileName);
            File.Copy(sourcePath, AssetPathToFullPath(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Dictionary<string, string> GetMirroredTextureIndex()
        {
            if (mirroredTextureIndex != null)
                return mirroredTextureIndex;

            string root = AssetPathToFullPath(MirroredGameRoot);
            Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root))
                return index;

            string[] paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string extension = Path.GetExtension(path);
                if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                    continue;
                string name = Path.GetFileName(path);
                if (!index.ContainsKey(name))
                    index.Add(name, path);
            }
            mirroredTextureIndex = index;
            return index;
        }

        private static void ApplyAssetTextureMetadata(string sourceMeshPath, PdxModel model)
        {
            string sourceAssetPath = GetPdxAssetMetadataPath(sourceMeshPath);
            if (!File.Exists(sourceAssetPath) || model == null || model.Parts.Count == 0)
                return;

            string source = File.ReadAllText(sourceAssetPath, Encoding.UTF8);
            MatchCollection settings = Regex.Matches(
                source,
                "meshsettings\\s*=\\s*\\{(?<body>.*?)\\n\\s*\\}",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            int count = Mathf.Min(settings.Count, model.Parts.Count);
            for (int index = 0; index < count; index++)
            {
                string body = settings[index].Groups["body"].Value;
                PdxMeshPart part = model.Parts[index];
                string diffuse = ReadAssetTextureField(body, "texture_diffuse");
                string normal = ReadAssetTextureField(body, "texture_normal");
                string specular = ReadAssetTextureField(body, "texture_specular");
                string shader = ReadAssetTextureField(body, "shader");
                if (!string.IsNullOrWhiteSpace(diffuse)) part.DiffuseTexture = diffuse;
                if (!string.IsNullOrWhiteSpace(normal)) part.NormalTexture = normal;
                if (!string.IsNullOrWhiteSpace(specular)) part.SpecularTexture = specular;
                if (!string.IsNullOrWhiteSpace(shader)) part.ShaderName = shader;
            }
        }

        private static string ReadAssetTextureField(string body, string field)
        {
            Match match = Regex.Match(
                body,
                "(?:^|\\s)" + Regex.Escape(field) + "\\s*=\\s*\\\"([^\\\"]+)\\\"",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string FindIndexedTexture(string sourceMeshPath, int textureIndex)
        {
            string assetPath = GetPdxAssetMetadataPath(sourceMeshPath);
            if (!File.Exists(assetPath))
                return null;
            Regex pattern = new Regex(
                "texture\\s*=\\s*\\{\\s*file\\s*=\\s*\"([^\"]+)\"\\s*index\\s*=\\s*" + textureIndex + "(?:\\s|\\})",
                RegexOptions.CultureInvariant);
            Match match = pattern.Match(File.ReadAllText(assetPath, Encoding.UTF8));
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string GetPdxAssetMetadataPath(string sourceMeshPath)
        {
            const string mirroredMeshSuffix = ".mesh.ck3source";
            if (sourceMeshPath.EndsWith(mirroredMeshSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return sourceMeshPath.Substring(0, sourceMeshPath.Length - mirroredMeshSuffix.Length) +
                       ".asset.ck3source";
            }
            return Path.ChangeExtension(sourceMeshPath, ".asset");
        }

        private static Material SaveCarrierMaterial(
            string path,
            Texture2D diffuse,
            Texture2D normal,
            Texture2D tint,
            bool tree,
            Texture2D properties = null,
            bool tabletop = false,
            string originalShaderName = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(
                tree ? "CK3Map/Tree Surface" :
                tabletop ? "CK3Map/Tabletop Surface" : "CK3Map/Decoration Surface");
            if (shader == null)
                throw new InvalidOperationException("找不到近景装饰所需 Shader：" +
                                                    (tree ? "CK3Map/Tree Surface" : "CK3Map/Decoration Surface"));
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_NormalMap", normal);
            if (material.HasProperty("_PropertiesMap"))
                material.SetTexture("_PropertiesMap", properties);
            material.SetFloat("_GlobalOpacity", 1.0f);
            if (tree)
            {
                material.SetFloat("_Cutoff", 0.4f);
                material.SetTexture("_NormalMap", normal);
                material.SetTexture("_TintMap", tint);
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", 2.0f);
                bool alphaClip = tabletop && !string.IsNullOrEmpty(originalShaderName) &&
                    originalShaderName.IndexOf("alpha_to_coverage", StringComparison.OrdinalIgnoreCase) >= 0;
                material.SetFloat("_AlphaClip", alphaClip ? 1.0f : 0.0f);
                material.SetFloat("_Cutoff", 0.5f);
                if (alphaClip) material.EnableKeyword("_ALPHATEST_ON");
                else material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                if (tabletop)
                {
                    Texture environment = AssetDatabase.LoadAssetAtPath<Texture>(TabletopEnvironmentPath);
                    if (environment == null)
                        throw new FileNotFoundException(
                            "缺少 CK3 原版西式桌面环境 Cubemap，请先重新同步原版数据。",
                            TabletopEnvironmentPath);
                    material.SetTexture("_CK3TabletopEnvironmentMap", environment);
                    material.SetColor("_CK3TabletopSunColor", Color.HSVToRGB(0.08f, 0.15f, 1.0f));
                    material.SetFloat("_CK3TabletopSunIntensity", 5.0f);
                    material.SetFloat("_CK3TabletopIblScale", 1.0f);
                    material.SetFloat("_CK3TabletopSpecularFactor", 1.0f);
                }
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh source, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, path);
                return source;
            }
            EditorUtility.CopySerialized(source, existing);
            Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void BindScene(CK3DecorationData data)
        {
            GameObject root = GameObject.Find(SceneRootName);
            if (root == null)
            {
                root = new GameObject(SceneRootName);
                Undo.RegisterCreatedObjectUndo(root, "创建 CK3 原版近景装饰");
            }
            CK3DecorationRenderer renderer = root.GetComponent<CK3DecorationRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<CK3DecorationRenderer>(root);
            Undo.RecordObject(renderer, "绑定 CK3 原版近景装饰");
            renderer.Data = data;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
        }

        private static string GetOriginalGameRoot()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../game"));
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException("找不到 CK3 原版 game 目录：" + path);
            return path;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private readonly struct TreeDefinition
        {
            public readonly string PdxMeshName;
            public readonly string SourceMeshPath;
            public readonly string SourceTextureDirectory;
            public readonly string OutputName;
            public readonly string InstanceFileName;

            public TreeDefinition(
                string pdxMeshName,
                string sourceMeshPath,
                string sourceTextureDirectory,
                string outputName,
                string instanceFileName)
            {
                PdxMeshName = pdxMeshName;
                SourceMeshPath = sourceMeshPath;
                SourceTextureDirectory = sourceTextureDirectory;
                OutputName = outputName;
                InstanceFileName = instanceFileName;
            }
        }

        private readonly struct TreeModelSource
        {
            public readonly string MeshPath;
            public readonly string TextureDirectory;
            public readonly string OutputName;

            public TreeModelSource(string meshPath, string textureDirectory, string outputName)
            {
                MeshPath = meshPath;
                TextureDirectory = textureDirectory;
                OutputName = outputName;
            }
        }

        private readonly struct ProvinceLocator
        {
            public readonly float X;
            public readonly float Z;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly float Height;

            public ProvinceLocator(float x, float z, Quaternion rotation, Vector3 scale, float height)
            {
                X = x;
                Z = z;
                Rotation = rotation;
                Scale = scale;
                Height = height;
            }
        }

        internal readonly struct ImportedModel
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;

            public ImportedModel(Mesh mesh, Material[] materials)
            {
                Mesh = mesh;
                Materials = materials;
            }
        }

        private sealed class HeightSampler : IDisposable
        {
            public readonly CK3TerrainHeightData Data;
            public readonly Texture2D Packed;
            public readonly Texture2D Indirection;

            private HeightSampler(CK3TerrainHeightData data, Texture2D packed, Texture2D indirection)
            {
                Data = data;
                Packed = packed;
                Indirection = indirection;
            }

            public static HeightSampler Create(CK3TerrainHeightData data)
            {
                CK3TerrainHeightBuilder.PngImage packedImage = CK3TerrainHeightBuilder.DecodePng(
                    AssetPathToFullPath(PackedHeightSourcePath));
                CK3TerrainHeightBuilder.PngImage indirectionImage = CK3TerrainHeightBuilder.DecodePng(
                    AssetPathToFullPath(IndirectionHeightSourcePath));
                Texture2D packed = CreateReadableTexture(packedImage, TextureFormat.R16, FilterMode.Bilinear);
                Texture2D indirection = CreateReadableTexture(indirectionImage, TextureFormat.RGBA32, FilterMode.Point);
                return new HeightSampler(data, packed, indirection);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Packed);
                Object.DestroyImmediate(Indirection);
            }

            private static Texture2D CreateReadableTexture(
                CK3TerrainHeightBuilder.PngImage image,
                TextureFormat format,
                FilterMode filterMode)
            {
                Texture2D texture = new Texture2D(image.Width, image.Height, format, false, true)
                {
                    filterMode = filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixelData(image.UnityPixelData, 0, 0);
                texture.Apply(false, false);
                return texture;
            }
        }

        private sealed class PdxModel
        {
            public readonly List<PdxMeshPart> Parts = new List<PdxMeshPart>();
        }

        private sealed class PdxMeshPart
        {
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uvs;
            public int[] Indices;
            public string DiffuseTexture;
            public string NormalTexture;
            public string SpecularTexture;
            public string ShaderName;
        }

        private static class PdxBinaryMeshReader
        {
            public static PdxModel Read(string path, bool firstLodOnly)
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 6, 8) != "pdxasset")
                    throw new InvalidDataException("不是受支持的 CK3 pdxasset Mesh：" + path);

                int stopOffset = firstLodOnly ? IndexOfAscii(bytes, "LOD_1") : -1;
                if (stopOffset < 0)
                    stopOffset = bytes.Length;

                PdxModel model = new PdxModel();
                PdxMeshPart current = null;
                int offset = 0;
                while (offset < stopOffset - 8)
                {
                    if (bytes[offset] != (byte)'!')
                    {
                        offset++;
                        continue;
                    }

                    int keyLength = bytes[offset + 1];
                    int headerEnd = offset + 2 + keyLength + 1 + 4;
                    if (keyLength <= 0 || keyLength > 32 || headerEnd > stopOffset)
                    {
                        offset++;
                        continue;
                    }
                    string key = Encoding.ASCII.GetString(bytes, offset + 2, keyLength);
                    char type = (char)bytes[offset + 2 + keyLength];
                    int count = BitConverter.ToInt32(bytes, offset + 3 + keyLength);
                    int dataOffset = offset + 7 + keyLength;
                    if (count < 0)
                        throw new InvalidDataException("CK3 PDX Mesh 字段数量无效：" + key);

                    if (type == 'f')
                    {
                        int byteCount = checked(count * sizeof(float));
                        if (dataOffset + byteCount > stopOffset)
                            throw new InvalidDataException("CK3 PDX Mesh 浮点字段越界：" + key);
                        float[] values = new float[count];
                        Buffer.BlockCopy(bytes, dataOffset, values, 0, byteCount);
                        if (key == "p")
                        {
                            if (current != null && current.Positions != null)
                                FinalizeGeometryPart(model, current, path);
                            current = new PdxMeshPart { Positions = ToVector3(values) };
                        }
                        else if (current != null && key == "n") current.Normals = ToVector3(values);
                        else if (current != null && key == "ta") current.Tangents = ToVector4(values);
                        else if (current != null && key == "u0") current.Uvs = ToVector2(values);
                        offset = dataOffset + byteCount;
                        continue;
                    }
                    if (type == 'i')
                    {
                        int byteCount = checked(count * sizeof(int));
                        if (dataOffset + byteCount > stopOffset)
                            throw new InvalidDataException("CK3 PDX Mesh 整数字段越界：" + key);
                        if (current != null && key == "tri")
                        {
                            current.Indices = new int[count];
                            Buffer.BlockCopy(bytes, dataOffset, current.Indices, 0, byteCount);
                        }
                        offset = dataOffset + byteCount;
                        continue;
                    }
                    if (type == 's')
                    {
                        string value = ReadFirstString(bytes, dataOffset, count, stopOffset, out int nextOffset);
                        if (current != null)
                        {
                            if (key == "diff") current.DiffuseTexture = value;
                            else if (key == "n") current.NormalTexture = value;
                            else if (key == "spec") current.SpecularTexture = value;
                        }
                        offset = nextOffset;
                        continue;
                    }
                    offset++;
                }

                if (current != null && current.Positions != null)
                    FinalizeGeometryPart(model, current, path);
                if (model.Parts.Count == 0)
                    throw new InvalidDataException("CK3 PDX Mesh 中没有可用几何：" + path);
                return model;
            }

            private static void FinalizeGeometryPart(PdxModel model, PdxMeshPart part, string path)
            {
                // CK3 tabletop meshes append locator/attachment positions as p[3] records
                // after their real p/n/ta/u0/tri geometry blocks. They are not submeshes.
                // Only a p record paired with tri is renderable geometry.
                if (part.Indices == null || part.Indices.Length == 0)
                    return;
                int vertexCount = part.Positions.Length;
                if (part.Normals == null || part.Normals.Length != vertexCount)
                    part.Normals = Array.Empty<Vector3>();
                if (part.Tangents == null || part.Tangents.Length != vertexCount)
                    part.Tangents = Array.Empty<Vector4>();
                if (part.Uvs == null || part.Uvs.Length != vertexCount)
                    part.Uvs = Array.Empty<Vector2>();
                foreach (int index in part.Indices)
                {
                    if (index < 0 || index >= vertexCount)
                        throw new InvalidDataException("CK3 PDX Mesh 三角形索引越界：" + path);
                }
                model.Parts.Add(part);
            }

            private static string ReadFirstString(byte[] bytes, int offset, int count, int limit, out int nextOffset)
            {
                string first = null;
                for (int index = 0; index < count; index++)
                {
                    if (offset + 4 > limit)
                        throw new InvalidDataException("CK3 PDX Mesh 字符串长度字段越界。");
                    int length = BitConverter.ToInt32(bytes, offset);
                    offset += 4;
                    if (length < 0 || offset + length > limit)
                        throw new InvalidDataException("CK3 PDX Mesh 字符串字段越界。");
                    int textLength = length;
                    while (textLength > 0 && bytes[offset + textLength - 1] == 0)
                        textLength--;
                    string value = Encoding.UTF8.GetString(bytes, offset, textLength);
                    if (first == null)
                        first = value;
                    offset += length;
                }
                nextOffset = offset;
                return first;
            }

            private static Vector3[] ToVector3(float[] values)
            {
                if (values.Length % 3 != 0)
                    throw new InvalidDataException("CK3 PDX Mesh Vector3 字段长度无效。");
                Vector3[] result = new Vector3[values.Length / 3];
                for (int index = 0; index < result.Length; index++)
                    result[index] = new Vector3(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]);
                return result;
            }

            private static Vector4[] ToVector4(float[] values)
            {
                if (values.Length % 4 != 0)
                    throw new InvalidDataException("CK3 PDX Mesh Vector4 字段长度无效。");
                Vector4[] result = new Vector4[values.Length / 4];
                for (int index = 0; index < result.Length; index++)
                    result[index] = new Vector4(values[index * 4], values[index * 4 + 1], values[index * 4 + 2], values[index * 4 + 3]);
                return result;
            }

            private static Vector2[] ToVector2(float[] values)
            {
                if (values.Length % 2 != 0)
                    throw new InvalidDataException("CK3 PDX Mesh Vector2 字段长度无效。");
                Vector2[] result = new Vector2[values.Length / 2];
                for (int index = 0; index < result.Length; index++)
                    result[index] = new Vector2(values[index * 2], values[index * 2 + 1]);
                return result;
            }

            private static int IndexOfAscii(byte[] bytes, string value)
            {
                byte[] needle = Encoding.ASCII.GetBytes(value);
                for (int index = 0; index <= bytes.Length - needle.Length; index++)
                {
                    int matched = 0;
                    while (matched < needle.Length && bytes[index + matched] == needle[matched])
                        matched++;
                    if (matched == needle.Length)
                        return index;
                }
                return -1;
            }
        }
    }
}
