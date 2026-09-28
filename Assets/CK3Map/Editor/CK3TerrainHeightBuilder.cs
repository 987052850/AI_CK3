using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    public static class CK3TerrainHeightBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Terrain";
        public const string HeightDataPath = OutputRoot + "/CK3原版高度页表.asset";

        private const string SourceRoot = "Assets/CK3Map/Data/Source/game/map_data";
        private const string HeightmapSettingsPath = SourceRoot + "/heightmap.heightmap";
        private const string PackedHeightSourcePath = SourceRoot + "/packed_heightmap.png";
        private const string IndirectionSourcePath = SourceRoot + "/indirection_heightmap.png";
        private const string PackedHeightAssetPath = OutputRoot + "/CK3高度_压缩页_R16.asset";
        private const string IndirectionAssetPath = OutputRoot + "/CK3高度_间接寻址_RGBA32.asset";

        private static readonly Vector2Int ExpectedOriginalSize = new Vector2Int(18432, 9216);
        private static readonly Vector2Int ExpectedPackedSize = new Vector2Int(3185, 4061);
        private static readonly Vector2Int ExpectedIndirectionSize = new Vector2Int(288, 144);
        private static readonly Vector2 ExpectedWorldExtents = new Vector2(9215.0f, 4607.0f);
        private static readonly Vector2Int[] ExpectedLevelOffsets =
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, 1397),
            new Vector2Int(0, 3129),
            new Vector2Int(0, 3690),
            new Vector2Int(0, 3861)
        };

        public static CK3TerrainHeightData Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("原版高度页表只能在非 Play 模式下构建。");
            }

            EnsureAssetFolder(OutputRoot);
            string settings = File.ReadAllText(ToAbsolutePath(HeightmapSettingsPath));
            ValidateSettings(settings);

            EditorUtility.DisplayProgressBar("CK3 地图构建器", "解码原版 R16 压缩高度图", 0.15f);
            Texture2D packedHeight = BuildPngTexture(
                PackedHeightSourcePath,
                PackedHeightAssetPath,
                "CK3 高度压缩页（R16）",
                ExpectedPackedSize,
                16,
                0,
                TextureFormat.R16,
                FilterMode.Bilinear);

            EditorUtility.DisplayProgressBar("CK3 地图构建器", "解码原版 RGBA32 高度间接寻址图", 0.65f);
            Texture2D indirection = BuildPngTexture(
                IndirectionSourcePath,
                IndirectionAssetPath,
                "CK3 高度间接寻址（RGBA32）",
                ExpectedIndirectionSize,
                8,
                6,
                TextureFormat.RGBA32,
                FilterMode.Point);

            Vector4[] tileConstants = new Vector4[ExpectedLevelOffsets.Length];
            for (int level = 0; level < tileConstants.Length; level++)
            {
                int compressionFactor = 1 << level;
                float currentTileSize = 64.0f / compressionFactor + 1.0f;
                Vector2Int offset = ExpectedLevelOffsets[level];
                tileConstants[level] = new Vector4(
                    currentTileSize / ExpectedPackedSize.x,
                    currentTileSize / ExpectedPackedSize.y,
                    offset.x / (float)ExpectedPackedSize.x,
                    offset.y / (float)ExpectedPackedSize.y);
            }

            CK3TerrainHeightData data = AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(HeightDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CK3TerrainHeightData>();
                data.name = "CK3 原版高度页表";
                AssetDatabase.CreateAsset(data, HeightDataPath);
            }

            data.ReplaceData(
                packedHeight,
                indirection,
                ExpectedOriginalSize,
                ExpectedIndirectionSize,
                ExpectedPackedSize,
                65,
                4,
                (Vector2Int[])ExpectedLevelOffsets.Clone(),
                tileConstants,
                new Vector2(1.0f / 9216.0f, 1.0f / 4608.0f),
                new Vector2(0.5f, 0.5f),
                ExpectedWorldExtents,
                50.0f);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(data);
            EditorUtility.ClearProgressBar();
            return data;
        }

        private static Texture2D BuildPngTexture(
            string sourceAssetPath,
            string outputAssetPath,
            string objectName,
            Vector2Int expectedSize,
            byte expectedBitDepth,
            byte expectedColorType,
            TextureFormat textureFormat,
            FilterMode filterMode)
        {
            PngImage image = DecodePng(ToAbsolutePath(sourceAssetPath));
            if (image.Width != expectedSize.x || image.Height != expectedSize.y ||
                image.BitDepth != expectedBitDepth || image.ColorType != expectedColorType)
            {
                throw new InvalidDataException(
                    $"PNG 格式与 CK3 原版不一致：{sourceAssetPath}，" +
                    $"实际 {image.Width}×{image.Height} / {image.BitDepth} bit / color type {image.ColorType}。");
            }

            Texture2D previous = AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
            if (previous != null)
            {
                AssetDatabase.DeleteAsset(outputAssetPath);
            }

            Texture2D texture = new Texture2D(
                image.Width,
                image.Height,
                textureFormat,
                false,
                true)
            {
                name = objectName,
                filterMode = filterMode,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                anisoLevel = 0
            };

            try
            {
                texture.SetPixelData(image.UnityPixelData, 0, 0);
                texture.Apply(false, true);
                AssetDatabase.CreateAsset(texture, outputAssetPath);
                AssetDatabase.SaveAssets();
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        internal static PngImage DecodePng(string absolutePath)
        {
            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);
            byte[] signature = reader.ReadBytes(8);
            byte[] expectedSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!ByteArraysEqual(signature, expectedSignature))
            {
                throw new InvalidDataException("不是 PNG 文件：" + absolutePath);
            }

            int width = 0;
            int height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            using MemoryStream idat = new MemoryStream();
            while (stream.Position < stream.Length)
            {
                int length = checked((int)ReadBigEndianUInt32(reader));
                string type = new string(reader.ReadChars(4));
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
                    bitDepth = data[8];
                    colorType = data[9];
                    if (data[10] != 0 || data[11] != 0 || data[12] != 0)
                    {
                        throw new InvalidDataException("CK3 高度 PNG 使用了未登记的压缩、过滤或交错格式。");
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

            int bytesPerPixel = colorType == 0 && bitDepth == 16
                ? 2
                : colorType == 6 && bitDepth == 8
                    ? 4
                    : throw new InvalidDataException("CK3 高度 PNG 像素格式未登记：" + absolutePath);
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
                    switch (filter)
                    {
                        case 0:
                            break;
                        case 1:
                            value += left;
                            break;
                        case 2:
                            value += above;
                            break;
                        case 3:
                            value += (left + above) >> 1;
                            break;
                        case 4:
                            value += Paeth(left, above, upperLeft);
                            break;
                        default:
                            throw new InvalidDataException("PNG 使用了未知行过滤器：" + filter);
                    }

                    current[x] = (byte)value;
                }

                int destinationY = height - 1 - sourceY;
                int destinationOffset = destinationY * stride;
                if (bytesPerPixel == 2)
                {
                    for (int x = 0; x < width; x++)
                    {
                        unityPixels[destinationOffset + x * 2] = current[x * 2 + 1];
                        unityPixels[destinationOffset + x * 2 + 1] = current[x * 2];
                    }
                }
                else
                {
                    Buffer.BlockCopy(current, 0, unityPixels, destinationOffset, stride);
                }

                byte[] swap = previous;
                previous = current;
                current = swap;
            }

            return new PngImage(width, height, bitDepth, colorType, unityPixels);
        }

        private static byte[] InflateZlib(byte[] zlib, int expectedLength)
        {
            if (zlib.Length < 6 || (zlib[0] & 0x0F) != 8 || ((zlib[0] << 8) + zlib[1]) % 31 != 0 ||
                (zlib[1] & 0x20) != 0)
            {
                throw new InvalidDataException("CK3 PNG 的 zlib 头无效。");
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
                throw new InvalidDataException("CK3 PNG 解压后的字节数量不正确。");
            }

            uint expectedAdler = ReadBigEndianUInt32(zlib, zlib.Length - 4);
            if (Adler32(result) != expectedAdler)
            {
                throw new InvalidDataException("CK3 PNG 的 Adler-32 校验失败。");
            }

            return result;
        }

        private static void ValidateSettings(string text)
        {
            RequireSetting(text, "original_heightmap_size", "18432 9216");
            RequireSetting(text, "tile_size", "65");
            RequireSetting(text, "should_wrap_x", "no");
            RequireSetting(text, "max_compress_level", "4");
            RequireSetting(text, "empty_tile_offset", "225 39");
            Match match = Regex.Match(
                text,
                @"^\s*level_offsets\s*=\s*(?<value>.+?)\s*$",
                RegexOptions.Multiline);
            if (!match.Success)
            {
                throw new InvalidDataException("heightmap.heightmap 缺少原版 level_offsets。");
            }

            MatchCollection numbers = Regex.Matches(match.Groups["value"].Value, @"-?\d+");
            if (numbers.Count != ExpectedLevelOffsets.Length * 2)
            {
                throw new InvalidDataException("heightmap.heightmap 的 level_offsets 数量不正确。");
            }

            for (int index = 0; index < ExpectedLevelOffsets.Length; index++)
            {
                int x = int.Parse(numbers[index * 2].Value, CultureInfo.InvariantCulture);
                int y = int.Parse(numbers[index * 2 + 1].Value, CultureInfo.InvariantCulture);
                if (x != ExpectedLevelOffsets[index].x || y != ExpectedLevelOffsets[index].y)
                {
                    throw new InvalidDataException("heightmap.heightmap 的 level_offsets 与当前 CK3 本体不一致。");
                }
            }
        }

        private static void RequireSetting(string text, string key, string expectedNumbers)
        {
            string pattern = @"^\s*" + Regex.Escape(key) + @"\s*=\s*\{?\s*" +
                Regex.Escape(expectedNumbers).Replace("\\ ", @"\s+") + @"\s*\}?\s*$";
            if (!Regex.IsMatch(text, pattern, RegexOptions.Multiline))
            {
                throw new InvalidDataException($"heightmap.heightmap 的 {key} 与当前 CK3 本体不一致。");
            }
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

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new DirectoryNotFoundException("无法取得 Unity 项目根目录。");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
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

        internal sealed class PngImage
        {
            internal readonly int Width;
            internal readonly int Height;
            internal readonly byte BitDepth;
            internal readonly byte ColorType;
            internal readonly byte[] UnityPixelData;

            internal PngImage(int width, int height, byte bitDepth, byte colorType, byte[] unityPixelData)
            {
                Width = width;
                Height = height;
                BitDepth = bitDepth;
                ColorType = colorType;
                UnityPixelData = unityPixelData;
            }
        }
    }
}
