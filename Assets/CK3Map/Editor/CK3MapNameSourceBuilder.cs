using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CK3Map.Editor
{
    public readonly struct CK3MapNameSourceBuildResult
    {
        public int RegionCount { get; }
        public int GlyphCount { get; }
        public CK3MapNameSourceBuildResult(int regions, int glyphs) { RegionCount = regions; GlyphCount = glyphs; }
    }

    public static class CK3MapNameSourceBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/MapNames";
        public const string SourceDataPath = OutputRoot + "/CK3原版地图名称源数据.asset";
        public const string FontAssetPath = OutputRoot + "/CK3原版MapFont_SDF.asset";
        private const string FontPath = "Assets/CK3Map/Data/Source/game/fonts/mapfont/Paradox_King_Script.otf";
        private const string ProvincePngPath = "Assets/CK3Map/Data/Source/game/map_data/provinces.png";
        private const string LocalizationRoot = "Assets/CK3Map/Data/Source/game/localization/english";
        private static readonly Regex LocalizationLine = new Regex(
            "^\\s*([^#\\s][^:]*):(?:\\d+)?\\s+\"(.*)\"\\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static CK3MapNameSourceBuildResult BuildDeJureKingdomSources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("地图名称静态资产只能在非 Play 模式构建。");
            CK3ProvinceDatabase database = AssetDatabase.LoadAssetAtPath<CK3ProvinceDatabase>(CK3PoliticalDataBuilder.ProvinceDatabasePath);
            CK3DeJureTitleHierarchy hierarchy = AssetDatabase.LoadAssetAtPath<CK3DeJureTitleHierarchy>(CK3PoliticalDataBuilder.TitleHierarchyPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (database == null || hierarchy == null || sourceFont == null)
                throw new InvalidOperationException("缺少阶段 6 行政数据或原版 Paradox_King_Script.otf。");
            EnsureFolder(OutputRoot);
            try
            {
                EditorUtility.DisplayProgressBar("CK3 原版地图名称", "读取英文原版本地化", 0.05f);
                Dictionary<string, string> localization = ParseLocalization();
                CK3MapNameRegion[] regions = BuildConnectedRegions(database, hierarchy, localization);
                Dictionary<string, CK3PoliticalMapBuilder.TitleHistoryState> history = CK3PoliticalMapBuilder.ParseTitleHistory();
                CK3MapNameRegion[] actualRegions = BuildConnectedActualRegions(database, hierarchy, history, localization);
                string characters = new string(string.Concat(
                    regions.Select(region => region.DisplayName)
                        .Concat(actualRegions.Select(region => region.DisplayName)))
                    .Distinct().ToArray());
                EditorUtility.DisplayProgressBar("CK3 原版地图名称", "由 Paradox_King_Script.otf 生成 SDF 图集", 0.90f);
                TMP_FontAsset fontAsset = BuildFontAsset(sourceFont, characters);
                CK3MapNameSourceData data = AssetDatabase.LoadAssetAtPath<CK3MapNameSourceData>(SourceDataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CK3MapNameSourceData>();
                    data.name = "CK3 原版地图名称源数据";
                    AssetDatabase.CreateAsset(data, SourceDataPath);
                }
                data.ReplaceData("dejure_kingdoms;realms", fontAsset, regions, actualRegions);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUIUtility.PingObject(data);
                return new CK3MapNameSourceBuildResult(regions.Length + actualRegions.Length, characters.Length);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static CK3MapNameRegion[] BuildConnectedRegions(CK3ProvinceDatabase database, CK3DeJureTitleHierarchy hierarchy, Dictionary<string, string> localization)
        {
            CK3PoliticalDataBuilder.RgbPng image = CK3PoliticalDataBuilder.DecodeRgb8Png(ToAbsolute(ProvincePngPath));
            int provinceCount = database.MaxProvinceId + 1;
            int[] provinceByColor = new int[1 << 24];
            Array.Fill(provinceByColor, -1);
            foreach (CK3ProvinceDefinition definition in database.Definitions)
            {
                Color32 color = definition.Color;
                provinceByColor[color.r | color.g << 8 | color.b << 16] = definition.Id;
            }
            int[] parent = Enumerable.Range(0, provinceCount).ToArray();
            int[] previous = new int[image.Width];
            int[] current = new int[image.Width];
            Array.Fill(previous, -1);
            int source = 0;
            for (int y = 0; y < image.Height; y++)
            {
                if ((y & 127) == 0)
                    EditorUtility.DisplayProgressBar("CK3 原版地图名称", $"扫描省份实体邻接 {y}/{image.Height}", 0.12f + 0.55f * y / image.Height);
                for (int x = 0; x < image.Width; x++, source += 3)
                {
                    int color = image.UnityRgbPixels[source] | image.UnityRgbPixels[source + 1] << 8 | image.UnityRgbPixels[source + 2] << 16;
                    int province = provinceByColor[color];
                    current[x] = province;
                    if (x > 0) UnionIfSameKingdom(current[x - 1], province, hierarchy, parent);
                    if (y > 0) UnionIfSameKingdom(previous[x], province, hierarchy, parent);
                }
                int[] swap = previous; previous = current; current = swap;
            }
            foreach (CK3ProvinceAdjacency adjacency in database.Adjacencies)
                UnionIfSameKingdom(adjacency.From, adjacency.To, hierarchy, parent);

            Dictionary<int, RegionAccumulator> accumulators = new Dictionary<int, RegionAccumulator>();
            source = 0;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++, source += 3)
            {
                int color = image.UnityRgbPixels[source] | image.UnityRgbPixels[source + 1] << 8 | image.UnityRgbPixels[source + 2] << 16;
                int province = provinceByColor[color];
                if (!HasKingdom(province, hierarchy)) continue;
                int root = Find(parent, province);
                if (!accumulators.TryGetValue(root, out RegionAccumulator region))
                {
                    region = new RegionAccumulator(); accumulators.Add(root, region);
                }
                region.AddPixel(x, y, province);
            }

            List<CK3MapNameRegion> result = new List<CK3MapNameRegion>(accumulators.Count);
            foreach (KeyValuePair<int, RegionAccumulator> pair in accumulators.OrderBy(item => item.Key))
            {
                int kingdomIndex = hierarchy.KingdomByProvince[pair.Key];
                string key = hierarchy.Titles[kingdomIndex].Key;
                string displayName = localization.TryGetValue(key, out string localized) ? localized : key;
                RegionAccumulator region = pair.Value;
                result.Add(new CK3MapNameRegion(key, displayName, region.Provinces.OrderBy(value => value).ToArray(), region.Bounds, region.PixelCount));
            }
            return result.ToArray();
        }

        private static CK3MapNameRegion[] BuildConnectedActualRegions(
            CK3ProvinceDatabase database,
            CK3DeJureTitleHierarchy hierarchy,
            Dictionary<string, CK3PoliticalMapBuilder.TitleHistoryState> history,
            Dictionary<string, string> localization)
        {
            CK3PoliticalDataBuilder.RgbPng image = CK3PoliticalDataBuilder.DecodeRgb8Png(ToAbsolute(ProvincePngPath));
            int provinceCount = database.MaxProvinceId + 1;
            string[] realmByProvince = new string[provinceCount];
            for (int province = 0; province < Math.Min(provinceCount, hierarchy.CountyByProvince.Length); province++)
            {
                int countyIndex = hierarchy.CountyByProvince[province];
                if (countyIndex < 0)
                    continue;

                string countyKey = hierarchy.Titles[countyIndex].Key;
                if (!history.TryGetValue(countyKey, out CK3PoliticalMapBuilder.TitleHistoryState countyState) ||
                    string.IsNullOrEmpty(countyState.Holder) || countyState.Holder == "0")
                    continue;

                realmByProvince[province] = CK3PoliticalMapBuilder.ResolveTopLiegeTitle(countyKey, history);
            }

            int[] provinceByColor = new int[1 << 24];
            Array.Fill(provinceByColor, -1);
            foreach (CK3ProvinceDefinition definition in database.Definitions)
            {
                Color32 color = definition.Color;
                provinceByColor[color.r | color.g << 8 | color.b << 16] = definition.Id;
            }

            int[] parent = Enumerable.Range(0, provinceCount).ToArray();
            int[] previous = new int[image.Width];
            int[] current = new int[image.Width];
            Array.Fill(previous, -1);
            int source = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++, source += 3)
                {
                    int color = image.UnityRgbPixels[source] |
                        image.UnityRgbPixels[source + 1] << 8 |
                        image.UnityRgbPixels[source + 2] << 16;
                    int province = provinceByColor[color];
                    current[x] = province;
                    if (x > 0) UnionIfSameRealm(current[x - 1], province, realmByProvince, parent);
                    if (y > 0) UnionIfSameRealm(previous[x], province, realmByProvince, parent);
                }
                int[] swap = previous; previous = current; current = swap;
            }
            foreach (CK3ProvinceAdjacency adjacency in database.Adjacencies)
                UnionIfSameRealm(adjacency.From, adjacency.To, realmByProvince, parent);

            Dictionary<int, RegionAccumulator> accumulators = new Dictionary<int, RegionAccumulator>();
            source = 0;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++, source += 3)
            {
                int color = image.UnityRgbPixels[source] |
                    image.UnityRgbPixels[source + 1] << 8 |
                    image.UnityRgbPixels[source + 2] << 16;
                int province = provinceByColor[color];
                if (province < 0 || province >= realmByProvince.Length || string.IsNullOrEmpty(realmByProvince[province]))
                    continue;
                int root = Find(parent, province);
                if (!accumulators.TryGetValue(root, out RegionAccumulator region))
                {
                    region = new RegionAccumulator();
                    accumulators.Add(root, region);
                }
                region.AddPixel(x, y, province);
            }

            List<CK3MapNameRegion> result = new List<CK3MapNameRegion>(accumulators.Count);
            foreach (KeyValuePair<int, RegionAccumulator> pair in accumulators.OrderBy(item => item.Key))
            {
                string realmKey = realmByProvince[pair.Key];
                string nameKey = realmKey;
                if (history.TryGetValue(realmKey, out CK3PoliticalMapBuilder.TitleHistoryState realmState) &&
                    !string.IsNullOrEmpty(realmState.HistoricalNameKey))
                    nameKey = realmState.HistoricalNameKey;
                string displayName = localization.TryGetValue(nameKey, out string localized) ? localized : nameKey;
                RegionAccumulator region = pair.Value;
                result.Add(new CK3MapNameRegion(
                    realmKey,
                    displayName,
                    region.Provinces.OrderBy(value => value).ToArray(),
                    region.Bounds,
                    region.PixelCount));
            }
            return result.ToArray();
        }

        private static void UnionIfSameRealm(int left, int right, string[] realmByProvince, int[] parent)
        {
            if (left < 0 || right < 0 || left == right ||
                left >= realmByProvince.Length || right >= realmByProvince.Length)
                return;
            string leftRealm = realmByProvince[left];
            if (string.IsNullOrEmpty(leftRealm) || !string.Equals(leftRealm, realmByProvince[right], StringComparison.Ordinal))
                return;
            int a = Find(parent, left);
            int b = Find(parent, right);
            if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
        }

        private static TMP_FontAsset BuildFontAsset(Font sourceFont, string characters)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(FontAssetPath) != null) AssetDatabase.DeleteAsset(FontAssetPath);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(sourceFont, 50, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("原版 MapFont SDF 图集生成失败。");
            if (!asset.TryAddCharacters(characters, out string missing, true) && !string.IsNullOrEmpty(missing))
                throw new InvalidOperationException("原版 MapFont 缺少字符：" + missing);
            asset.name = "CK3 原版 MapFont SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            foreach (Texture2D atlas in asset.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) { atlas.name = asset.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, asset); }
            if (asset.material != null && !AssetDatabase.Contains(asset.material)) { asset.material.name = asset.name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset); }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Dictionary<string, string> ParseLocalization()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] files = Directory.GetFiles(ToAbsolute(LocalizationRoot), "*.yml", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            foreach (string line in File.ReadLines(file, Encoding.UTF8))
            {
                Match match = LocalizationLine.Match(line);
                if (match.Success) result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Replace("\\\"", "\"");
            }
            return result;
        }

        private static void UnionIfSameKingdom(int left, int right, CK3DeJureTitleHierarchy hierarchy, int[] parent)
        {
            if (left < 0 || right < 0 || left == right || !HasKingdom(left, hierarchy) || !HasKingdom(right, hierarchy)) return;
            if (hierarchy.KingdomByProvince[left] != hierarchy.KingdomByProvince[right]) return;
            int a = Find(parent, left), b = Find(parent, right);
            if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
        }
        private static bool HasKingdom(int province, CK3DeJureTitleHierarchy hierarchy) => province >= 0 && province < hierarchy.KingdomByProvince.Length && hierarchy.KingdomByProvince[province] >= 0;
        private static int Find(int[] parent, int value)
        {
            int root = value;
            while (parent[root] != root) root = parent[root];
            while (parent[value] != value) { int next = parent[value]; parent[value] = root; value = next; }
            return root;
        }

        private sealed class RegionAccumulator
        {
            public readonly HashSet<int> Provinces = new HashSet<int>();
            public int PixelCount;
            public RectInt Bounds;
            private int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            public void AddPixel(int x, int y, int province)
            {
                Provinces.Add(province); PixelCount++;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
        private static string ToAbsolute(string assetPath)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("无法确定 Unity 项目根目录。");
            return Path.GetFullPath(Path.Combine(root, assetPath));
        }
    }
}
