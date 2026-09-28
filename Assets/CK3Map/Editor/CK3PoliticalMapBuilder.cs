using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CK3Map.Editor
{
    public readonly struct CK3PoliticalMapBuildResult
    {
        public int DeJureColoredProvinceCount { get; }
        public int ActualColoredProvinceCount { get; }

        public CK3PoliticalMapBuildResult(int deJureCount, int actualCount)
        {
            DeJureColoredProvinceCount = deJureCount;
            ActualColoredProvinceCount = actualCount;
        }
    }

    public static class CK3PoliticalMapBuilder
    {
        public const string MapDataPath = CK3PoliticalDataBuilder.OutputRoot + "/CK3政治地图数据.asset";
        public const string ProvinceIdPath = CK3PoliticalDataBuilder.OutputRoot + "/CK3省份编号间接寻址_RG16.asset";
        public const string DeJurePalettePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3法理王国颜色表_RGBA32.asset";
        public const string ActualPalettePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3实际领地颜色表_1066_9_15_RGBA32.asset";
        public const string DeJureDistancePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3法理王国边界距离场_R8.asset";
        public const string ActualDistancePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3实际领地边界距离场_1066_9_15_R8.asset";
        public const string DeJureDistanceWithoutShorelinePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3法理王国无水岸边界距离场_R8.asset";
        public const string ActualDistanceWithoutShorelinePath = CK3PoliticalDataBuilder.OutputRoot + "/CK3实际领地无水岸边界距离场_1066_9_15_R8.asset";
        private const string HistoryRoot = "Assets/CK3Map/Data/Source/game/history/titles";
        private const string ProvincePngPath = "Assets/CK3Map/Data/Source/game/map_data/provinces.png";
        private const string TerrainMaterialPath = CK3TerrainWorldBuilder.TerrainMaterialPath;
        private const string ActualDateText = "1066.9.15";
        private const int ActualDate = 10660915;
        private const int LargeImpassableProvincePixels = 100000;
        // NMapColors.OCEAN_MAP_COLOR={0,0,0.1} and WATER_MAP_COLOR={0.67,0.6,1}.
        // Alpha stays zero because these sentinels are used only by the distance-field wildcard pass.
        private static readonly Color32 OceanWildCard = new Color32(0, 0, 26, 0);
        private static readonly Color32 InlandWaterWildCard = new Color32(171, 153, 255, 0);

        public static CK3PoliticalMapBuildResult BuildAndBind()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("政治地图静态资产只能在非 Play 模式构建。");
            }

            CK3ProvinceDatabase database = AssetDatabase.LoadAssetAtPath<CK3ProvinceDatabase>(
                CK3PoliticalDataBuilder.ProvinceDatabasePath);
            CK3DeJureTitleHierarchy hierarchy = AssetDatabase.LoadAssetAtPath<CK3DeJureTitleHierarchy>(
                CK3PoliticalDataBuilder.TitleHierarchyPath);
            Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (database == null || hierarchy == null || terrainMaterial == null)
            {
                throw new InvalidOperationException("请先完成阶段 2、阶段 5 和阶段 6 的构建。");
            }

            try
            {
                EditorUtility.DisplayProgressBar("CK3 政治地图", "生成原版省份编号间接寻址图", 0.08f);
                Texture2D indirection = BuildProvinceIdIndirection(database, out ProvinceTopology topology);

                EditorUtility.DisplayProgressBar("CK3 政治地图", "生成法理王国颜色表", 0.52f);
                Texture2D deJurePalette = BuildDeJurePalette(database, hierarchy, topology, out int deJureCount, out Color32[] deJureColors);

                EditorUtility.DisplayProgressBar("CK3 政治地图", "解析 1066.9.15 原版头衔历史", 0.62f);
                Dictionary<string, TitleHistoryState> history = ParseTitleHistory();
                Texture2D actualPalette = BuildActualPalette(
                    database,
                    hierarchy,
                    history,
                    topology,
                    out int actualCount,
                    out Color32[] actualColors);

                EditorUtility.DisplayProgressBar("CK3 政治地图", "生成法理王国原版边界距离场", 0.88f);
                Texture2D deJureDistance = CK3ProvinceDistanceFieldBuilder.Build(
                    indirection,
                    deJurePalette,
                    DeJureDistancePath,
                    "CK3 法理王国边界距离场");
                EditorUtility.DisplayProgressBar("CK3 政治地图", "生成实际领地原版边界距离场", 0.94f);
                Texture2D actualDistance = CK3ProvinceDistanceFieldBuilder.Build(
                    indirection,
                    actualPalette,
                    ActualDistancePath,
                    "CK3 实际领地边界距离场（1066.9.15）");

                EditorUtility.DisplayProgressBar("CK3 政治地图", "生成原版无水岸边界距离场", 0.97f);
                Texture2D deJureDistancePalette = CreateWaterWildcardPalette(database, deJureColors);
                Texture2D actualDistancePalette = CreateWaterWildcardPalette(database, actualColors);
                Color32[] waterWildCards = { OceanWildCard, InlandWaterWildCard };
                Texture2D deJureDistanceWithoutShoreline;
                Texture2D actualDistanceWithoutShoreline;
                try
                {
                    deJureDistanceWithoutShoreline = CK3ProvinceDistanceFieldBuilder.Build(
                        indirection,
                        deJureDistancePalette,
                        DeJureDistanceWithoutShorelinePath,
                        "CK3 法理王国无水岸边界距离场",
                        waterWildCards);
                    actualDistanceWithoutShoreline = CK3ProvinceDistanceFieldBuilder.Build(
                        indirection,
                        actualDistancePalette,
                        ActualDistanceWithoutShorelinePath,
                        "CK3 实际领地无水岸边界距离场（1066.9.15）",
                        waterWildCards);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(deJureDistancePalette);
                    UnityEngine.Object.DestroyImmediate(actualDistancePalette);
                }

                CK3PoliticalMapData mapData = LoadOrCreate<CK3PoliticalMapData>(MapDataPath);
                mapData.ReplaceData(
                    indirection,
                    deJurePalette,
                    actualPalette,
                    deJureDistance,
                    actualDistance,
                    deJureDistanceWithoutShoreline,
                    actualDistanceWithoutShoreline,
                    ActualDateText);
                EditorUtility.SetDirty(mapData);

                terrainMaterial.SetTexture("_ProvinceColorIndirectionTexture", indirection);
                terrainMaterial.SetTexture("_ProvinceColorTexture", deJurePalette);
                terrainMaterial.SetTexture("_BorderDistanceFieldTexture", deJureDistance);
                terrainMaterial.SetFloat("_CK3ProvinceOverlayEnabled", 1.0f);
                terrainMaterial.SetFloat("_CK3ProvinceOverlayBlend", 0.57f);
                EditorUtility.SetDirty(terrainMaterial);

                ConfigureScene(mapData, terrainMaterial);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new CK3PoliticalMapBuildResult(deJureCount, actualCount);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Texture2D BuildProvinceIdIndirection(
            CK3ProvinceDatabase database,
            out ProvinceTopology topology)
        {
            CK3PoliticalDataBuilder.RgbPng image = CK3PoliticalDataBuilder.DecodeRgb8Png(
                ToAbsolutePath(ProvincePngPath));
            int[] provinceByColor = new int[1 << 24];
            foreach (CK3ProvinceDefinition definition in database.Definitions)
            {
                Color32 color = definition.Color;
                int key = color.r | (color.g << 8) | (color.b << 16);
                int encoded = definition.Id + 1;
                provinceByColor[key] = provinceByColor[key] == 0 ? encoded : -1;
            }

            byte[] pixels = new byte[checked(image.Width * image.Height * 2)];
            int provinceCount = database.MaxProvinceId + 1;
            int[] pixelCounts = new int[provinceCount];
            int[] previousRow = new int[image.Width];
            int[] currentRow = new int[image.Width];
            HashSet<ulong> geometricConnections = new HashSet<ulong>();
            for (int y = 0, source = 0, destination = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++, source += 3, destination += 2)
                {
                    int key = image.UnityRgbPixels[source] |
                        (image.UnityRgbPixels[source + 1] << 8) |
                        (image.UnityRgbPixels[source + 2] << 16);
                    int encoded = provinceByColor[key];
                    if (encoded <= 0)
                    {
                        throw new InvalidDataException("provinces.png 存在无法唯一映射的省份颜色。");
                    }

                    int provinceId = encoded - 1;
                    currentRow[x] = provinceId;
                    pixelCounts[provinceId]++;
                    pixels[destination] = (byte)(provinceId & 255);
                    pixels[destination + 1] = (byte)((provinceId >> 8) & 255);

                    if (x > 0)
                    {
                        AddGeometricConnection(geometricConnections, currentRow[x - 1], provinceId);
                    }
                    if (y > 0)
                    {
                        AddGeometricConnection(geometricConnections, previousRow[x], provinceId);
                    }
                }

                int[] swap = previousRow;
                previousRow = currentRow;
                currentRow = swap;
            }

            topology = BuildProvinceTopology(database, pixelCounts, geometricConnections);

            DeleteOldAsset(ProvinceIdPath);
            Texture2D texture = new Texture2D(image.Width, image.Height, TextureFormat.RG16, false, true)
            {
                name = "CK3 省份编号间接寻址（RG16）",
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            texture.SetPixelData(pixels, 0, 0);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, ProvinceIdPath);
            return texture;
        }

        private static Texture2D BuildDeJurePalette(
            CK3ProvinceDatabase database,
            CK3DeJureTitleHierarchy hierarchy,
            ProvinceTopology topology,
            out int coloredCount,
            out Color32[] generatedColors)
        {
            Color32[] colors = new Color32[256 * 256];
            coloredCount = 0;
            for (int provinceId = 0; provinceId < hierarchy.KingdomByProvince.Length; provinceId++)
            {
                int titleIndex = hierarchy.KingdomByProvince[provinceId];
                if (titleIndex < 0)
                {
                    continue;
                }

                if (TryGetTitleColor(hierarchy.Titles[titleIndex], out Color32 color))
                {
                    colors[provinceId] = color;
                    coloredCount++;
                }
            }
            ApplyFillInImpassable(database, topology, colors);
            coloredCount = CountColored(colors);
            generatedColors = colors;
            return CreatePalette(DeJurePalettePath, "CK3 法理王国颜色表", colors);
        }

        private static Texture2D BuildActualPalette(
            CK3ProvinceDatabase database,
            CK3DeJureTitleHierarchy hierarchy,
            Dictionary<string, TitleHistoryState> history,
            ProvinceTopology topology,
            out int coloredCount,
            out Color32[] generatedColors)
        {
            Dictionary<string, int> titleIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < hierarchy.Titles.Length; i++)
            {
                if (!titleIndexByKey.ContainsKey(hierarchy.Titles[i].Key))
                {
                    titleIndexByKey.Add(hierarchy.Titles[i].Key, i);
                }
            }

            Color32[] colors = new Color32[256 * 256];
            coloredCount = 0;
            for (int provinceId = 0; provinceId < hierarchy.CountyByProvince.Length; provinceId++)
            {
                int countyIndex = hierarchy.CountyByProvince[provinceId];
                if (countyIndex < 0)
                {
                    continue;
                }

                string countyKey = hierarchy.Titles[countyIndex].Key;
                if (!history.TryGetValue(countyKey, out TitleHistoryState countyState) || countyState.Holder == "0" ||
                    string.IsNullOrEmpty(countyState.Holder))
                {
                    continue;
                }

                string realmTitle = ResolveTopLiegeTitle(countyKey, history);
                Color32 color = default;
                bool hasColor = history.TryGetValue(realmTitle, out TitleHistoryState realmState) &&
                    realmState.HasHistoricalColor;
                if (hasColor)
                {
                    color = realmState.HistoricalColor;
                }
                else if (titleIndexByKey.TryGetValue(realmTitle, out int realmIndex) &&
                    TryGetTitleColor(hierarchy.Titles[realmIndex], out color))
                {
                    hasColor = true;
                }

                if (hasColor)
                {
                    colors[provinceId] = color;
                    coloredCount++;
                }
            }
            ApplyFillInImpassable(database, topology, colors);
            coloredCount = CountColored(colors);
            generatedColors = colors;
            return CreatePalette(ActualPalettePath, "CK3 实际领地颜色表（1066.9.15）", colors);
        }

        private static void AddGeometricConnection(HashSet<ulong> connections, int left, int right)
        {
            if (left <= 0 || right <= 0 || left == right)
            {
                return;
            }

            int minimum = Math.Min(left, right);
            int maximum = Math.Max(left, right);
            connections.Add(((ulong)(uint)minimum << 32) | (uint)maximum);
        }

        private static ProvinceTopology BuildProvinceTopology(
            CK3ProvinceDatabase database,
            int[] pixelCounts,
            HashSet<ulong> geometricConnections)
        {
            List<int>[] neighbors = new List<int>[pixelCounts.Length];
            foreach (ulong connection in geometricConnections)
            {
                int left = (int)(connection >> 32);
                int right = (int)(connection & uint.MaxValue);
                GetNeighborList(neighbors, left).Add(right);
                GetNeighborList(neighbors, right).Add(left);
            }

            for (int provinceId = 0; provinceId < neighbors.Length; provinceId++)
            {
                neighbors[provinceId]?.Sort();
            }

            HashSet<ulong> allConnections = new HashSet<ulong>(geometricConnections);
            foreach (CK3ProvinceAdjacency adjacency in database.Adjacencies)
            {
                int from = adjacency.From;
                int to = adjacency.To;
                if (from < 0 || from >= neighbors.Length || to < 0 || to >= neighbors.Length)
                {
                    throw new InvalidDataException(
                        $"adjacencies.csv 引用了定义范围外的省份：{from} -> {to}");
                }
                if (from == to)
                {
                    continue;
                }

                int minimum = Math.Min(from, to);
                int maximum = Math.Max(from, to);
                ulong key = ((ulong)(uint)minimum << 32) | (uint)maximum;
                if (!allConnections.Add(key))
                {
                    continue;
                }

                GetNeighborList(neighbors, from).Add(to);
                GetNeighborList(neighbors, to).Add(from);
            }

            int[][] orderedNeighbors = new int[neighbors.Length][];
            for (int provinceId = 0; provinceId < neighbors.Length; provinceId++)
            {
                orderedNeighbors[provinceId] = neighbors[provinceId]?.ToArray() ?? Array.Empty<int>();
            }
            return new ProvinceTopology(pixelCounts, orderedNeighbors);
        }

        private static List<int> GetNeighborList(List<int>[] neighbors, int provinceId)
        {
            List<int> result = neighbors[provinceId];
            if (result == null)
            {
                result = new List<int>();
                neighbors[provinceId] = result;
            }
            return result;
        }

        private static void ApplyFillInImpassable(
            CK3ProvinceDatabase database,
            ProvinceTopology topology,
            Color32[] colors)
        {
            int provinceCount = Math.Min(topology.PixelCounts.Length, colors.Length);
            for (int provinceId = 0; provinceId < provinceCount; provinceId++)
            {
                if (!IsImpassable(database, provinceId) ||
                    topology.PixelCounts[provinceId] >= LargeImpassableProvincePixels)
                {
                    continue;
                }

                List<ColorCandidate> candidates = new List<ColorCandidate>();
                int eligibleNeighborCount = 0;
                int[] neighbors = topology.OrderedNeighbors[provinceId];
                for (int index = 0; index < neighbors.Length; index++)
                {
                    int neighborId = neighbors[index];
                    if (IsImpassable(database, neighborId) || !IsLand(database, neighborId))
                    {
                        continue;
                    }

                    eligibleNeighborCount++;
                    uint value = PackColor(colors[neighborId]);
                    int candidateIndex = -1;
                    for (int candidate = 0; candidate < candidates.Count; candidate++)
                    {
                        if (candidates[candidate].Value == value)
                        {
                            candidateIndex = candidate;
                            break;
                        }
                    }

                    if (candidateIndex >= 0)
                    {
                        ColorCandidate candidate = candidates[candidateIndex];
                        candidate.Count++;
                        candidates[candidateIndex] = candidate;
                    }
                    else
                    {
                        candidates.Add(new ColorCandidate(value, 1));
                    }
                }

                int bestIndex = -1;
                int bestCount = 0;
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (bestIndex < 0 || candidates[index].Count > bestCount)
                    {
                        bestIndex = index;
                        bestCount = candidates[index].Count;
                    }
                }

                if (bestIndex >= 0 && bestCount > eligibleNeighborCount / 2)
                {
                    colors[provinceId] = UnpackColor(candidates[bestIndex].Value);
                }
            }
        }

        private static bool IsImpassable(CK3ProvinceDatabase database, int provinceId)
        {
            CK3ProvinceCategory category = database.GetCategory(provinceId);
            if ((category & (CK3ProvinceCategory.不可通行山地 | CK3ProvinceCategory.不可通行海域)) != 0)
            {
                return true;
            }

            if (provinceId < 0 || provinceId >= database.Definitions.Length)
            {
                return false;
            }
            Color32 sourceColor = database.Definitions[provinceId].Color;
            return (sourceColor.r == 0 && sourceColor.g == 0 && sourceColor.b == 0) ||
                (sourceColor.r == 255 && sourceColor.g == 255 && sourceColor.b == 255);
        }

        private static bool IsLand(CK3ProvinceDatabase database, int provinceId)
        {
            CK3ProvinceCategory category = database.GetCategory(provinceId);
            CK3ProvinceCategory nonLand = CK3ProvinceCategory.海区 |
                CK3ProvinceCategory.大河省份 |
                CK3ProvinceCategory.湖泊 |
                CK3ProvinceCategory.不可通行海域;
            return (category & nonLand) == 0;
        }

        private static uint PackColor(Color32 color)
        {
            return color.r | ((uint)color.g << 8) | ((uint)color.b << 16) | ((uint)color.a << 24);
        }

        private static Color32 UnpackColor(uint value)
        {
            return new Color32(
                (byte)(value & 255),
                (byte)((value >> 8) & 255),
                (byte)((value >> 16) & 255),
                (byte)((value >> 24) & 255));
        }

        private static int CountColored(Color32[] colors)
        {
            int count = 0;
            for (int index = 0; index < colors.Length; index++)
            {
                if (colors[index].a != 0)
                {
                    count++;
                }
            }
            return count;
        }

        internal static string ResolveTopLiegeTitle(string start, Dictionary<string, TitleHistoryState> history)
        {
            string current = start;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(current) && history.TryGetValue(current, out TitleHistoryState state) &&
                !string.IsNullOrEmpty(state.Liege) && state.Liege != "0")
            {
                current = state.Liege;
            }

            return current;
        }

        private static bool TryGetTitleColor(CK3DeJureTitle title, out Color32 color)
        {
            Vector3 value = title.ColorValues;
            Vector3 rgb;
            switch (title.ColorEncoding)
            {
                case CK3TitleColorEncoding.RGB255:
                    rgb = value / 255.0f;
                    break;
                case CK3TitleColorEncoding.HSV01:
                    rgb = HsvToRgb(value);
                    break;
                case CK3TitleColorEncoding.HSV360:
                    rgb = HsvToRgb(new Vector3(value.x / 360.0f, value.y / 100.0f, value.z / 100.0f));
                    break;
                default:
                    color = default;
                    return false;
            }

            color = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(rgb.x) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(rgb.y) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(rgb.z) * 255.0f),
                255);
            return true;
        }

        private static Vector3 HsvToRgb(Vector3 hsv)
        {
            Vector3 k = new Vector3(1.0f, 2.0f / 3.0f, 1.0f / 3.0f);
            Vector3 p = new Vector3(
                Mathf.Abs(Mathf.Repeat(hsv.x + k.x, 1.0f) * 6.0f - 3.0f),
                Mathf.Abs(Mathf.Repeat(hsv.x + k.y, 1.0f) * 6.0f - 3.0f),
                Mathf.Abs(Mathf.Repeat(hsv.x + k.z, 1.0f) * 6.0f - 3.0f));
            return hsv.z * Vector3.Lerp(Vector3.one, new Vector3(
                Mathf.Clamp01(p.x - 1.0f),
                Mathf.Clamp01(p.y - 1.0f),
                Mathf.Clamp01(p.z - 1.0f)), hsv.y);
        }

        private static Texture2D CreateWaterWildcardPalette(CK3ProvinceDatabase database, Color32[] sourceColors)
        {
            Color32[] colors = (Color32[])sourceColors.Clone();
            int count = Math.Min(colors.Length, database.MaxProvinceId + 1);
            for (int province = 0; province < count; province++)
            {
                CK3ProvinceCategory category = database.GetCategory(province);
                if ((category & (CK3ProvinceCategory.海区 | CK3ProvinceCategory.不可通行海域)) != 0)
                    colors[province] = OceanWildCard;
                else if ((category & (CK3ProvinceCategory.湖泊 | CK3ProvinceCategory.大河省份)) != 0)
                    colors[province] = InlandWaterWildCard;
            }

            Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, true)
            {
                name = "CK3 距离场水域 Wildcard 颜色表",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreatePalette(string path, string name, Color32[] colors)
        {
            DeleteOldAsset(path);
            Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        internal static Dictionary<string, TitleHistoryState> ParseTitleHistory()
        {
            Dictionary<string, TitleHistoryState> result = new Dictionary<string, TitleHistoryState>(StringComparer.Ordinal);
            string[] files = Directory.GetFiles(ToAbsolutePath(HistoryRoot), "*.txt", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
            {
                if ((index & 31) == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "CK3 政治地图",
                        $"解析 1066.9.15 头衔历史 {index}/{files.Length}",
                        0.62f + 0.22f * index / Math.Max(1.0f, files.Length));
                }
                new HistoryParser(File.ReadAllText(files[index], Encoding.UTF8), result).Parse();
            }
            return result;
        }

        private static void ConfigureScene(CK3PoliticalMapData data, Material material)
        {
            GameObject root = GameObject.Find(CK3TerrainWorldBuilder.RootName);
            if (root == null)
            {
                throw new InvalidOperationException("场景中没有完整世界地形，请先完成阶段 5。");
            }

            CK3MapModeController controller = root.GetComponent<CK3MapModeController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CK3MapModeController>(root);
            }
            Undo.RecordObject(controller, "绑定 CK3 政治地图模式");
            controller.Configure(data, material);
            EditorUtility.SetDirty(controller);

            CK3TerrainSurfaceBinding surfaceBinding = root.GetComponent<CK3TerrainSurfaceBinding>();
            if (surfaceBinding == null)
            {
                surfaceBinding = Undo.AddComponent<CK3TerrainSurfaceBinding>(root);
            }
            CK3TerrainMaterialLibrary materialLibrary =
                AssetDatabase.LoadAssetAtPath<CK3TerrainMaterialLibrary>(
                    CK3TerrainMaterialBuilder.LibraryPath);
            if (materialLibrary != null)
            {
                surfaceBinding.Configure(materialLibrary, material);
                EditorUtility.SetDirty(surfaceBinding);
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                CK3StrategicMapCamera cameraController = camera.GetComponent<CK3StrategicMapCamera>();
                if (cameraController == null)
                {
                    cameraController = Undo.AddComponent<CK3StrategicMapCamera>(camera.gameObject);
                }
                cameraController.ResetToCK3Start();
                EditorUtility.SetDirty(cameraController);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void DeleteOldAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("无法确定 Unity 项目根目录。");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private sealed class ProvinceTopology
        {
            public int[] PixelCounts { get; }
            public int[][] OrderedNeighbors { get; }

            public ProvinceTopology(int[] pixelCounts, int[][] orderedNeighbors)
            {
                PixelCounts = pixelCounts;
                OrderedNeighbors = orderedNeighbors;
            }
        }

        private struct ColorCandidate
        {
            public uint Value;
            public int Count;

            public ColorCandidate(uint value, int count)
            {
                Value = value;
                Count = count;
            }
        }

        internal sealed class TitleHistoryState
        {
            public string Holder = string.Empty;
            public string Liege = string.Empty;
            public string HistoricalNameKey = string.Empty;
            public Color32 HistoricalColor;
            public bool HasHistoricalColor;
            public int HolderDate = int.MinValue;
            public int LiegeDate = int.MinValue;
            public int HistoricalNameDate = int.MinValue;
            public int HistoricalColorDate = int.MinValue;
        }

        private sealed class HistoryParser
        {
            private readonly List<string> tokens;
            private readonly Dictionary<string, TitleHistoryState> destination;
            private int position;

            public HistoryParser(string text, Dictionary<string, TitleHistoryState> output)
            {
                tokens = Tokenize(text);
                destination = output;
            }

            public void Parse()
            {
                while (position < tokens.Count)
                {
                    string title = Read();
                    if (!TryConsume("=") || !IsTitleKey(title) || Peek() != "{")
                    {
                        SkipValue();
                        continue;
                    }

                    ParseTitle(title);
                }
            }

            private void ParseTitle(string title)
            {
                Consume("{");
                if (!destination.TryGetValue(title, out TitleHistoryState state))
                {
                    state = new TitleHistoryState();
                    destination.Add(title, state);
                }

                while (position < tokens.Count && Peek() != "}")
                {
                    string property = Read();
                    if (!TryConsume("=")) continue;
                    if (TryParseDate(property, out int date) && Peek() == "{")
                    {
                        if (date <= ActualDate) ParseDateBlock(state, date);
                        else SkipValue();
                    }
                    else
                    {
                        SkipValue();
                    }
                }
                Consume("}");
            }

            private void ParseDateBlock(TitleHistoryState state, int date)
            {
                Consume("{");
                while (position < tokens.Count && Peek() != "}")
                {
                    string property = Read();
                    if (!TryConsume("=")) continue;
                    if ((property == "holder" || property == "liege") && Peek() != "{")
                    {
                        string value = Read();
                        if (property == "holder" && date >= state.HolderDate)
                        {
                            state.Holder = value;
                            state.HolderDate = date;
                        }
                        else if (property == "liege" && date >= state.LiegeDate)
                        {
                            state.Liege = value;
                            state.LiegeDate = date;
                        }
                    }
                    else if (property == "effect" && Peek() == "{")
                    {
                        ParseEffectBlock(state, date);
                    }
                    else
                    {
                        SkipValue();
                    }
                }
                Consume("}");
            }

            private void ParseEffectBlock(TitleHistoryState state, int date)
            {
                Consume("{");
                while (position < tokens.Count && Peek() != "}")
                {
                    string effect = Read();
                    if (!TryConsume("="))
                    {
                        continue;
                    }

                    if (effect == "set_title_color" && Peek() == "{")
                    {
                        Consume("{");
                        float red = ParseColorComponent(Read());
                        float green = ParseColorComponent(Read());
                        float blue = ParseColorComponent(Read());
                        while (position < tokens.Count && Peek() != "}") Read();
                        Consume("}");
                        if (date >= state.HistoricalColorDate)
                        {
                            state.HistoricalColor = new Color32(
                                (byte)Mathf.Clamp(Mathf.RoundToInt(red), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt(green), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt(blue), 0, 255),
                                255);
                            state.HasHistoricalColor = true;
                            state.HistoricalColorDate = date;
                        }
                    }
                    else if (effect == "set_title_name" && Peek() != "{")
                    {
                        string nameKey = Read();
                        if (date >= state.HistoricalNameDate)
                        {
                            state.HistoricalNameKey = nameKey;
                            state.HistoricalNameDate = date;
                        }
                    }
                    else
                    {
                        SkipValue();
                    }
                }
                Consume("}");
            }

            private static float ParseColorComponent(string token)
            {
                if (!float.TryParse(
                    token,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
                {
                    throw new InvalidDataException("title history 出现无效 set_title_color 数值：" + token);
                }
                return value;
            }

            private void SkipValue()
            {
                if (position >= tokens.Count) return;
                if (Peek() != "{")
                {
                    Read();
                    return;
                }
                int depth = 0;
                do
                {
                    string token = Read();
                    if (token == "{") depth++;
                    else if (token == "}") depth--;
                } while (position < tokens.Count && depth > 0);
            }

            private string Peek() => position < tokens.Count ? tokens[position] : string.Empty;
            private string Read() => position < tokens.Count ? tokens[position++] : string.Empty;
            private bool TryConsume(string value)
            {
                if (Peek() != value) return false;
                position++;
                return true;
            }
            private void Consume(string value)
            {
                if (Read() != value) throw new InvalidDataException("title history 语法错误。");
            }

            private static bool TryParseDate(string token, out int date)
            {
                date = 0;
                string[] parts = token.Split('.');
                if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) ||
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) return false;
                date = year * 10000 + month * 100 + day;
                return true;
            }

            private static bool IsTitleKey(string key)
            {
                return key.Length > 2 && key[1] == '_' && "hekdcb".IndexOf(key[0]) >= 0;
            }

            private static List<string> Tokenize(string text)
            {
                List<string> result = new List<string>();
                for (int index = 0; index < text.Length;)
                {
                    char current = text[index];
                    if (char.IsWhiteSpace(current)) { index++; continue; }
                    if (current == '#')
                    {
                        while (index < text.Length && text[index] != '\n') index++;
                        continue;
                    }
                    if (current == '{' || current == '}' || current == '=')
                    {
                        result.Add(current.ToString());
                        index++;
                        continue;
                    }
                    if (current == '"')
                    {
                        int start = ++index;
                        while (index < text.Length && text[index] != '"') index++;
                        result.Add(text.Substring(start, index - start));
                        if (index < text.Length) index++;
                        continue;
                    }
                    int startToken = index;
                    while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '#' &&
                        text[index] != '{' && text[index] != '}' && text[index] != '=') index++;
                    result.Add(text.Substring(startToken, index - startToken));
                }
                return result;
            }
        }
    }
}
