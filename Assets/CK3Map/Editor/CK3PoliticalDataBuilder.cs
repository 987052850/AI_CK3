using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    public readonly struct CK3PoliticalDataBuildResult
    {
        public CK3ProvinceDatabase ProvinceDatabase { get; }
        public CK3DeJureTitleHierarchy TitleHierarchy { get; }

        public CK3PoliticalDataBuildResult(
            CK3ProvinceDatabase provinceDatabase,
            CK3DeJureTitleHierarchy titleHierarchy)
        {
            ProvinceDatabase = provinceDatabase;
            TitleHierarchy = titleHierarchy;
        }
    }

    public static class CK3PoliticalDataBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Political";
        public const string ProvinceDatabasePath = OutputRoot + "/CK3原版省份数据库.asset";
        public const string ProvinceTexturePath = OutputRoot + "/CK3省份颜色编号_RGB24.asset";
        public const string TitleHierarchyPath = OutputRoot + "/CK3原版法理头衔层级.asset";

        private const string MapSourceRoot = "Assets/CK3Map/Data/Source/game/map_data";
        private const string ProvincePngPath = MapSourceRoot + "/provinces.png";
        private const string DefinitionPath = MapSourceRoot + "/definition.csv";
        private const string DefaultMapPath = MapSourceRoot + "/default.map";
        private const string AdjacenciesPath = MapSourceRoot + "/adjacencies.csv";
        private const string LandedTitlesRoot =
            "Assets/CK3Map/Data/Source/game/common/landed_titles";

        private static readonly Vector2Int ExpectedProvinceSize = new Vector2Int(9216, 4608);

        public static CK3PoliticalDataBuildResult Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("省份与法理行政数据只能在非 Play 模式下构建。");
            }

            EnsureAssetFolder(OutputRoot);
            try
            {
                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解析 definition.csv", 0.05f);
                CK3ProvinceDefinition[] definitions = ParseDefinitions(out int maxDefinitionId);

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解析 default.map 省份分类", 0.12f);
                byte[] categories = ParseProvinceCategories(maxDefinitionId, out int maxClassifiedId);

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解析 adjacencies.csv", 0.17f);
                CK3ProvinceAdjacency[] adjacencies = ParseAdjacencies();

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解码并核验完整 provinces.png", 0.22f);
                Texture2D provinceTexture = BuildProvinceTexture(definitions);

                CK3ProvinceDatabase database = LoadOrCreate<CK3ProvinceDatabase>(ProvinceDatabasePath);
                database.ReplaceData(
                    provinceTexture,
                    ExpectedProvinceSize,
                    Math.Max(maxDefinitionId, maxClassifiedId),
                    definitions,
                    categories,
                    adjacencies);
                EditorUtility.SetDirty(database);

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解析 landed_titles 法理层级", 0.72f);
                CK3DeJureTitleHierarchy hierarchy = BuildTitleHierarchy(
                    Math.Max(maxDefinitionId, maxClassifiedId));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new CK3PoliticalDataBuildResult(database, hierarchy);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static CK3ProvinceDefinition[] ParseDefinitions(out int maximumId)
        {
            List<CK3ProvinceDefinition> definitions = new List<CK3ProvinceDefinition>();
            HashSet<int> ids = new HashSet<int>();
            maximumId = -1;

            foreach (string rawLine in File.ReadLines(ToAbsolutePath(DefinitionPath), Encoding.UTF8))
            {
                if (rawLine.Length == 0 || rawLine[0] == '#')
                {
                    continue;
                }

                string[] columns = rawLine.Split(';');
                if (columns.Length < 5 ||
                    !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    continue;
                }

                int red = ParseByte(columns[1], DefinitionPath);
                int green = ParseByte(columns[2], DefinitionPath);
                int blue = ParseByte(columns[3], DefinitionPath);
                if (!ids.Add(id))
                {
                    throw new InvalidDataException($"definition.csv 出现重复省份编号：{id}");
                }

                definitions.Add(new CK3ProvinceDefinition(
                    id,
                    new Color32((byte)red, (byte)green, (byte)blue, 255),
                    columns[4]));
                maximumId = Math.Max(maximumId, id);
            }

            definitions.Sort((left, right) => left.Id.CompareTo(right.Id));
            if (definitions.Count != 13270 || maximumId != 13269)
            {
                throw new InvalidDataException(
                    $"definition.csv 与当前 CK3 本体不一致：{definitions.Count} 条，最大编号 {maximumId}。");
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].Id != index)
                {
                    throw new InvalidDataException("definition.csv 的有效省份编号不连续。缺失编号：" + index);
                }
            }

            return definitions.ToArray();
        }

        private static byte[] ParseProvinceCategories(int maxDefinitionId, out int maxClassifiedId)
        {
            string text = File.ReadAllText(ToAbsolutePath(DefaultMapPath), Encoding.UTF8);
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Dictionary<int, byte> flags = new Dictionary<int, byte>();
            maxClassifiedId = maxDefinitionId;
            Regex entry = new Regex(
                @"^\s*(?<key>sea_zones|river_provinces|lakes|impassable_mountains|impassable_seas)\s*=\s*(?<mode>RANGE|LIST)\s*\{(?<ids>[^}]*)\}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (string rawLine in lines)
            {
                string line = StripComment(rawLine);
                Match match = entry.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                CK3ProvinceCategory category = CategoryForKey(match.Groups["key"].Value);
                int[] numbers = Regex.Matches(match.Groups["ids"].Value, @"-?\d+")
                    .Cast<Match>()
                    .Select(value => int.Parse(value.Value, CultureInfo.InvariantCulture))
                    .ToArray();
                bool range = string.Equals(
                    match.Groups["mode"].Value,
                    "RANGE",
                    StringComparison.OrdinalIgnoreCase);
                if (range && numbers.Length != 2)
                {
                    throw new InvalidDataException("default.map 的 RANGE 不是两个端点：" + rawLine);
                }

                IEnumerable<int> provinceIds = range
                    ? Enumerable.Range(numbers[0], checked(numbers[1] - numbers[0] + 1))
                    : numbers;
                foreach (int provinceId in provinceIds)
                {
                    if (provinceId < 0)
                    {
                        throw new InvalidDataException("default.map 出现负省份编号：" + provinceId);
                    }

                    flags.TryGetValue(provinceId, out byte current);
                    flags[provinceId] = (byte)(current | (byte)category);
                    maxClassifiedId = Math.Max(maxClassifiedId, provinceId);
                }
            }

            byte[] result = new byte[maxClassifiedId + 1];
            foreach (KeyValuePair<int, byte> pair in flags)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static CK3ProvinceAdjacency[] ParseAdjacencies()
        {
            List<CK3ProvinceAdjacency> result = new List<CK3ProvinceAdjacency>();
            foreach (string rawLine in File.ReadLines(ToAbsolutePath(AdjacenciesPath), Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("From;", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] columns = line.Split(';');
                if (columns.Length < 8)
                {
                    throw new InvalidDataException("adjacencies.csv 行格式不完整：" + rawLine);
                }

                int from = ParseInteger(columns[0], AdjacenciesPath);
                int to = ParseInteger(columns[1], AdjacenciesPath);
                if (from == -1 && to == -1)
                {
                    break;
                }

                result.Add(new CK3ProvinceAdjacency(
                    from,
                    to,
                    columns[2],
                    ParseInteger(columns[3], AdjacenciesPath),
                    new Vector2Int(
                        ParseInteger(columns[4], AdjacenciesPath),
                        ParseInteger(columns[5], AdjacenciesPath)),
                    new Vector2Int(
                        ParseInteger(columns[6], AdjacenciesPath),
                        ParseInteger(columns[7], AdjacenciesPath)),
                    columns.Length > 8 ? columns[8] : string.Empty));
            }

            return result.ToArray();
        }

        private static Texture2D BuildProvinceTexture(CK3ProvinceDefinition[] definitions)
        {
            RgbPng image = DecodeRgb8Png(ToAbsolutePath(ProvincePngPath));
            if (image.Width != ExpectedProvinceSize.x || image.Height != ExpectedProvinceSize.y)
            {
                throw new InvalidDataException(
                    $"provinces.png 尺寸错误：{image.Width}×{image.Height}。");
            }

            int[] provinceByColor = new int[1 << 24];
            foreach (CK3ProvinceDefinition definition in definitions)
            {
                Color32 color = definition.Color;
                int key = color.r | (color.g << 8) | (color.b << 16);
                provinceByColor[key] = provinceByColor[key] == 0
                    ? definition.Id + 1
                    : -1;
            }

            bool[] used = new bool[definitions.Length];
            for (int offset = 0; offset < image.UnityRgbPixels.Length; offset += 3)
            {
                int key = image.UnityRgbPixels[offset] |
                    (image.UnityRgbPixels[offset + 1] << 8) |
                    (image.UnityRgbPixels[offset + 2] << 16);
                int encodedId = provinceByColor[key];
                if (encodedId == 0)
                {
                    throw new InvalidDataException(
                        $"provinces.png 使用了 definition.csv 中不存在的 RGB：" +
                        $"{image.UnityRgbPixels[offset]},{image.UnityRgbPixels[offset + 1]}," +
                        $"{image.UnityRgbPixels[offset + 2]}。");
                }

                if (encodedId < 0)
                {
                    throw new InvalidDataException(
                        $"provinces.png 实际使用了 definition.csv 中对应多个省份的重复 RGB：" +
                        $"{image.UnityRgbPixels[offset]},{image.UnityRgbPixels[offset + 1]}," +
                        $"{image.UnityRgbPixels[offset + 2]}。");
                }

                used[encodedId - 1] = true;
            }

            Texture2D oldTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ProvinceTexturePath);
            if (oldTexture != null)
            {
                AssetDatabase.DeleteAsset(ProvinceTexturePath);
            }

            Texture2D texture = new Texture2D(
                image.Width,
                image.Height,
                TextureFormat.RGB24,
                false,
                true)
            {
                name = "CK3 省份颜色编号（RGB24）",
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            try
            {
                texture.SetPixelData(image.UnityRgbPixels, 0, 0);
                texture.Apply(false, true);
                AssetDatabase.CreateAsset(texture, ProvinceTexturePath);
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static CK3DeJureTitleHierarchy BuildTitleHierarchy(int maximumProvinceId)
        {
            List<MutableTitle> mutableTitles = new List<MutableTitle>();
            string absoluteRoot = ToAbsolutePath(LandedTitlesRoot);
            string[] files = Directory.GetFiles(absoluteRoot, "*.txt", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                TitleParser parser = new TitleParser(File.ReadAllText(file, Encoding.UTF8), mutableTitles);
                parser.ParseRoot();
            }

            CK3DeJureTitle[] titles = mutableTitles.Select(value => value.ToRecord()).ToArray();
            int[] baronies = CreateEmptyTitleIndexArray(maximumProvinceId);
            int[] counties = CreateEmptyTitleIndexArray(maximumProvinceId);
            int[] duchies = CreateEmptyTitleIndexArray(maximumProvinceId);
            int[] kingdoms = CreateEmptyTitleIndexArray(maximumProvinceId);
            int[] empires = CreateEmptyTitleIndexArray(maximumProvinceId);

            for (int titleIndex = 0; titleIndex < titles.Length; titleIndex++)
            {
                int provinceId = titles[titleIndex].ProvinceId;
                if (provinceId < 0)
                {
                    continue;
                }

                if (provinceId > maximumProvinceId)
                {
                    throw new InvalidDataException(
                        $"头衔 {titles[titleIndex].Key} 引用了超出完整省份数据范围的编号 {provinceId}。");
                }

                int current = titleIndex;
                while (current >= 0)
                {
                    AssignRankIndex(
                        provinceId,
                        current,
                        titles[current].Rank,
                        baronies,
                        counties,
                        duchies,
                        kingdoms,
                        empires);
                    current = titles[current].ParentIndex;
                }
            }

            CK3DeJureTitleHierarchy hierarchy = LoadOrCreate<CK3DeJureTitleHierarchy>(TitleHierarchyPath);
            hierarchy.ReplaceData(titles, baronies, counties, duchies, kingdoms, empires);
            EditorUtility.SetDirty(hierarchy);
            return hierarchy;
        }

        private static void AssignRankIndex(
            int provinceId,
            int titleIndex,
            CK3TitleRank rank,
            int[] baronies,
            int[] counties,
            int[] duchies,
            int[] kingdoms,
            int[] empires)
        {
            if (rank == CK3TitleRank.霸权)
            {
                // CK3 1.17+ uses h_china as a real landed-title parent above its
                // de-jure empires. It belongs in the title graph, but it is not one
                // of the e/k/d/c/b province index layers.
                return;
            }

            int[] target = rank switch
            {
                CK3TitleRank.男爵领 => baronies,
                CK3TitleRank.伯爵领 => counties,
                CK3TitleRank.公国 => duchies,
                CK3TitleRank.王国 => kingdoms,
                CK3TitleRank.帝国 => empires,
                _ => throw new ArgumentOutOfRangeException(nameof(rank))
            };
            if (target[provinceId] >= 0 && target[provinceId] != titleIndex)
            {
                throw new InvalidDataException($"省份 {provinceId} 对应多个 {rank} 头衔。");
            }

            target[provinceId] = titleIndex;
        }

        private static int[] CreateEmptyTitleIndexArray(int maximumProvinceId)
        {
            int[] result = new int[maximumProvinceId + 1];
            Array.Fill(result, -1);
            return result;
        }

        private static CK3ProvinceCategory CategoryForKey(string key)
        {
            return key.ToLowerInvariant() switch
            {
                "sea_zones" => CK3ProvinceCategory.海区,
                "river_provinces" => CK3ProvinceCategory.大河省份,
                "lakes" => CK3ProvinceCategory.湖泊,
                "impassable_mountains" => CK3ProvinceCategory.不可通行山地,
                "impassable_seas" => CK3ProvinceCategory.不可通行海域,
                _ => throw new InvalidDataException("未知 default.map 省份分类：" + key)
            };
        }

        private static int ParseByte(string value, string source)
        {
            int result = ParseInteger(value, source);
            if (result < 0 || result > 255)
            {
                throw new InvalidDataException(source + " 出现超出 8-bit 范围的颜色值：" + value);
            }

            return result;
        }

        private static int ParseInteger(string value, string source)
        {
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                throw new InvalidDataException(source + " 出现无效整数：" + value);
            }

            return result;
        }

        private static string StripComment(string line)
        {
            int index = line.IndexOf('#');
            return index >= 0 ? line.Substring(0, index) : line;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("无法确定 Unity 项目根目录。");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        internal static RgbPng DecodeRgb8Png(string absolutePath)
        {
            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);
            byte[] expectedSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!reader.ReadBytes(8).SequenceEqual(expectedSignature))
            {
                throw new InvalidDataException("不是 PNG 文件：" + absolutePath);
            }

            int width = 0;
            int height = 0;
            using MemoryStream idat = new MemoryStream();
            while (stream.Position < stream.Length)
            {
                int length = checked((int)ReadBigEndianUInt32(reader));
                string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                byte[] data = reader.ReadBytes(length);
                if (data.Length != length)
                {
                    throw new EndOfStreamException("PNG 数据块不完整：" + absolutePath);
                }

                reader.ReadUInt32();
                if (type == "IHDR")
                {
                    width = checked((int)ReadBigEndianUInt32(data, 0));
                    height = checked((int)ReadBigEndianUInt32(data, 4));
                    if (data[8] != 8 || data[9] != 2 || data[10] != 0 ||
                        data[11] != 0 || data[12] != 0)
                    {
                        throw new InvalidDataException(
                            "provinces.png 不是已确认的 RGB8、无交错 PNG。路径：" + absolutePath);
                    }
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, 0, data.Length);
                }
                else if (type == "IEND")
                {
                    break;
                }
            }

            const int bytesPerPixel = 3;
            int stride = checked(width * bytesPerPixel);
            byte[] filtered = InflateZlib(idat.ToArray(), checked((stride + 1) * height));
            byte[] unityPixels = new byte[checked(stride * height)];
            byte[] previous = new byte[stride];
            byte[] current = new byte[stride];
            int sourceOffset = 0;
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                byte filter = filtered[sourceOffset++];
                for (int x = 0; x < stride; x++)
                {
                    int left = x >= bytesPerPixel ? current[x - bytesPerPixel] : 0;
                    int above = previous[x];
                    int upperLeft = x >= bytesPerPixel ? previous[x - bytesPerPixel] : 0;
                    int value = filtered[sourceOffset++];
                    value += filter switch
                    {
                        0 => 0,
                        1 => left,
                        2 => above,
                        3 => (left + above) >> 1,
                        4 => Paeth(left, above, upperLeft),
                        _ => throw new InvalidDataException("PNG 使用未知行过滤器：" + filter)
                    };
                    current[x] = (byte)value;
                }

                Buffer.BlockCopy(current, 0, unityPixels, (height - 1 - sourceY) * stride, stride);
                byte[] swap = previous;
                previous = current;
                current = swap;
            }

            return new RgbPng(width, height, unityPixels);
        }

        private static byte[] InflateZlib(byte[] zlib, int expectedLength)
        {
            if (zlib.Length < 6 || (zlib[0] & 0x0F) != 8 ||
                ((zlib[0] << 8) + zlib[1]) % 31 != 0 || (zlib[1] & 0x20) != 0)
            {
                throw new InvalidDataException("provinces.png 的 zlib 头无效。");
            }

            byte[] result = new byte[expectedLength];
            using MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6, false);
            using DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress);
            int offset = 0;
            while (offset < result.Length)
            {
                int read = deflate.Read(result, offset, result.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            if (offset != result.Length || deflate.ReadByte() != -1)
            {
                throw new InvalidDataException("provinces.png 解压后的字节数量不正确。");
            }

            uint expectedAdler = ReadBigEndianUInt32(zlib, zlib.Length - 4);
            if (Adler32(result) != expectedAdler)
            {
                throw new InvalidDataException("provinces.png 的 Adler-32 校验失败。");
            }

            return result;
        }

        private static uint Adler32(byte[] data)
        {
            const uint modulus = 65521;
            uint a = 1;
            uint b = 0;
            for (int offset = 0; offset < data.Length;)
            {
                int end = Math.Min(offset + 5552, data.Length);
                for (; offset < end; offset++)
                {
                    a += data[offset];
                    b += a;
                }

                a %= modulus;
                b %= modulus;
            }

            return (b << 16) | a;
        }

        private static int Paeth(int left, int above, int upperLeft)
        {
            int prediction = left + above - upperLeft;
            int leftDistance = Math.Abs(prediction - left);
            int aboveDistance = Math.Abs(prediction - above);
            int upperLeftDistance = Math.Abs(prediction - upperLeft);
            return leftDistance <= aboveDistance && leftDistance <= upperLeftDistance
                ? left
                : aboveDistance <= upperLeftDistance ? above : upperLeft;
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
            {
                throw new EndOfStreamException();
            }

            return ReadBigEndianUInt32(bytes, 0);
        }

        private static uint ReadBigEndianUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) |
                ((uint)bytes[offset + 1] << 16) |
                ((uint)bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }

        internal readonly struct RgbPng
        {
            public int Width { get; }
            public int Height { get; }
            public byte[] UnityRgbPixels { get; }

            public RgbPng(int width, int height, byte[] pixels)
            {
                Width = width;
                Height = height;
                UnityRgbPixels = pixels;
            }
        }

        private sealed class MutableTitle
        {
            public string Key = string.Empty;
            public CK3TitleRank Rank;
            public int ParentIndex = -1;
            public int ProvinceId = -1;
            public string CapitalKey = string.Empty;
            public CK3TitleColorEncoding ColorEncoding;
            public Vector3 ColorValues;

            public CK3DeJureTitle ToRecord()
            {
                return new CK3DeJureTitle(
                    Key,
                    Rank,
                    ParentIndex,
                    ProvinceId,
                    CapitalKey,
                    ColorEncoding,
                    ColorValues);
            }
        }

        private sealed class TitleParser
        {
            private readonly List<string> tokens;
            private readonly List<MutableTitle> titles;
            private int position;

            public TitleParser(string text, List<MutableTitle> destination)
            {
                tokens = Tokenize(text);
                titles = destination;
            }

            public void ParseRoot()
            {
                while (position < tokens.Count)
                {
                    string key = Read();
                    if (!TryConsume("="))
                    {
                        continue;
                    }

                    if (IsTitleKey(key) && Peek() == "{")
                    {
                        ParseTitle(key, -1);
                    }
                    else
                    {
                        SkipValue();
                    }
                }
            }

            private void ParseTitle(string key, int parentIndex)
            {
                Consume("{");
                MutableTitle title = new MutableTitle
                {
                    Key = key,
                    Rank = RankForKey(key),
                    ParentIndex = parentIndex
                };
                int titleIndex = titles.Count;
                titles.Add(title);

                while (position < tokens.Count && Peek() != "}")
                {
                    string property = Read();
                    if (!TryConsume("="))
                    {
                        continue;
                    }

                    if (IsTitleKey(property) && Peek() == "{")
                    {
                        ParseTitle(property, titleIndex);
                    }
                    else if (property == "province")
                    {
                        title.ProvinceId = ParseTokenInteger(Read(), key + ".province");
                    }
                    else if (property == "capital")
                    {
                        title.CapitalKey = Read();
                    }
                    else if (property == "color")
                    {
                        ParseColor(title);
                    }
                    else
                    {
                        SkipValue();
                    }
                }

                Consume("}");
            }

            private void ParseColor(MutableTitle title)
            {
                CK3TitleColorEncoding encoding;
                if (Peek() == "{")
                {
                    encoding = CK3TitleColorEncoding.RGB255;
                }
                else
                {
                    string mode = Read();
                    encoding = mode switch
                    {
                        "hsv" => CK3TitleColorEncoding.HSV01,
                        "hsv360" => CK3TitleColorEncoding.HSV360,
                        _ => throw new InvalidDataException("未知 landed_titles 颜色编码：" + mode)
                    };
                }

                Consume("{");
                float x = ParseTokenFloat(Read(), "color");
                float y = ParseTokenFloat(Read(), "color");
                float z = ParseTokenFloat(Read(), "color");
                while (position < tokens.Count && Peek() != "}")
                {
                    Read();
                }

                Consume("}");
                title.ColorEncoding = encoding;
                title.ColorValues = new Vector3(x, y, z);
            }

            private void SkipValue()
            {
                if (position >= tokens.Count)
                {
                    return;
                }

                if ((Peek() == "hsv" || Peek() == "hsv360") && Peek(1) == "{")
                {
                    Read();
                }

                if (Peek() != "{")
                {
                    Read();
                    return;
                }

                int depth = 0;
                do
                {
                    string token = Read();
                    if (token == "{")
                    {
                        depth++;
                    }
                    else if (token == "}")
                    {
                        depth--;
                    }
                }
                while (position < tokens.Count && depth > 0);
            }

            private string Peek(int offset = 0)
            {
                int index = position + offset;
                return index < tokens.Count ? tokens[index] : string.Empty;
            }

            private string Read()
            {
                return position < tokens.Count ? tokens[position++] : string.Empty;
            }

            private bool TryConsume(string expected)
            {
                if (Peek() != expected)
                {
                    return false;
                }

                position++;
                return true;
            }

            private void Consume(string expected)
            {
                string actual = Read();
                if (actual != expected)
                {
                    throw new InvalidDataException($"landed_titles 语法错误：期望 {expected}，实际 {actual}。");
                }
            }

            private static List<string> Tokenize(string text)
            {
                List<string> result = new List<string>();
                for (int index = 0; index < text.Length;)
                {
                    char current = text[index];
                    if (char.IsWhiteSpace(current))
                    {
                        index++;
                        continue;
                    }

                    if (current == '#')
                    {
                        while (index < text.Length && text[index] != '\n')
                        {
                            index++;
                        }

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
                        while (index < text.Length && text[index] != '"')
                        {
                            index++;
                        }

                        result.Add(text.Substring(start, index - start));
                        if (index < text.Length)
                        {
                            index++;
                        }

                        continue;
                    }

                    int tokenStart = index;
                    while (index < text.Length && !char.IsWhiteSpace(text[index]) &&
                        text[index] != '#' && text[index] != '{' && text[index] != '}' &&
                        text[index] != '=')
                    {
                        index++;
                    }

                    result.Add(text.Substring(tokenStart, index - tokenStart));
                }

                return result;
            }

            private static bool IsTitleKey(string key)
            {
                return key.Length > 2 && key[1] == '_' &&
                    (key[0] == 'h' || key[0] == 'e' || key[0] == 'k' || key[0] == 'd' ||
                     key[0] == 'c' || key[0] == 'b');
            }

            private static CK3TitleRank RankForKey(string key)
            {
                return key[0] switch
                {
                    'h' => CK3TitleRank.霸权,
                    'e' => CK3TitleRank.帝国,
                    'k' => CK3TitleRank.王国,
                    'd' => CK3TitleRank.公国,
                    'c' => CK3TitleRank.伯爵领,
                    'b' => CK3TitleRank.男爵领,
                    _ => throw new InvalidDataException("未知头衔等级：" + key)
                };
            }

            private static int ParseTokenInteger(string token, string field)
            {
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    throw new InvalidDataException(field + " 不是整数：" + token);
                }

                return value;
            }

            private static float ParseTokenFloat(string token, string field)
            {
                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    throw new InvalidDataException(field + " 不是浮点数：" + token);
                }

                return value;
            }
        }
    }
}
