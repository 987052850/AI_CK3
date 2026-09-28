using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    public readonly struct CK3RiverNetworkBuildResult
    {
        public readonly CK3RiverNetworkData Data;
        public readonly int MainRiverCount;
        public readonly int SecondaryRiverCount;
        public readonly int IgnoredSecondarySourceCount;

        public CK3RiverNetworkBuildResult(
            CK3RiverNetworkData data,
            int mainRiverCount,
            int secondaryRiverCount,
            int ignoredSecondarySourceCount)
        {
            Data = data;
            MainRiverCount = mainRiverCount;
            SecondaryRiverCount = secondaryRiverCount;
            IgnoredSecondarySourceCount = ignoredSecondarySourceCount;
        }
    }

    public static class CK3RiverNetworkBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Rivers";
        public const string RiverDataPath = OutputRoot + "/CK3原版位图河网.asset";

        private const string RiverBitmapPath =
            "Assets/CK3Map/Data/Source/game/map_data/rivers.png";
        private const int ExpectedWidth = 9216;
        private const int ExpectedHeight = 4608;
        private const int WidthPixelValueCount = 13;
        private const float MinimumWidth = 1.0f;
        private const float MaximumWidth = 4.0f;
        private const float SourceWidth = 0.01f;

        private static readonly int[] NeighborX = { -1, 1, 0, 0 };
        private static readonly int[] NeighborY = { 0, 0, -1, 1 };

        public static CK3RiverNetworkBuildResult Build()
        {
            EnsureAssetFolder(OutputRoot);
            try
            {
                EditorUtility.DisplayProgressBar("CK3 地图构建器", "解码原版 rivers.png", 0.05f);
                IndexedPng image = DecodeIndexed8Png(ToAbsolutePath(RiverBitmapPath));
                ValidateImage(image);

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "扫描原版河源标记", 0.20f);
                List<int> mainSources = new List<int>(1024);
                List<int> secondarySources = new List<int>(1024);
                for (int offset = 0; offset < image.Indices.Length; offset++)
                {
                    byte value = image.Indices[offset];
                    if (value == 0)
                    {
                        mainSources.Add(offset);
                    }
                    else if (value == 1 || value == 2)
                    {
                        secondarySources.Add(offset);
                    }
                }

                int[] riverOwner = new int[image.Indices.Length];
                for (int index = 0; index < riverOwner.Length; index++)
                {
                    riverOwner[index] = -1;
                }
                List<CK3RiverRecord> rivers = new List<CK3RiverRecord>(
                    mainSources.Count + secondarySources.Count);
                List<CK3RiverPixelPoint> points = new List<CK3RiverPixelPoint>(300000);

                for (int index = 0; index < mainSources.Count; index++)
                {
                    TraceRiver(image, mainSources[index], -1, riverOwner, rivers, points);
                    if ((index & 63) == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "CK3 地图构建器",
                            $"追踪主河流 {index + 1}/{mainSources.Count}",
                            Mathf.Lerp(0.25f, 0.58f, (index + 1.0f) / mainSources.Count));
                    }
                }

                int secondaryBuilt = 0;
                while (secondarySources.Count > 0)
                {
                    List<PendingSecondary> pending = new List<PendingSecondary>();
                    List<int> remaining = new List<int>();
                    for (int index = 0; index < secondarySources.Count; index++)
                    {
                        int source = secondarySources[index];
                        int parentPoint = FindOwnedNeighbor(
                            source, image.Width, image.Height, riverOwner);
                        if (parentPoint < 0)
                        {
                            remaining.Add(source);
                            continue;
                        }

                        pending.Add(new PendingSecondary(source, parentPoint));
                    }

                    for (int index = 0; index < pending.Count; index++)
                    {
                        PendingSecondary item = pending[index];
                        TraceRiver(
                            image,
                            item.Source,
                            item.ParentPoint,
                            riverOwner,
                            rivers,
                            points);
                        secondaryBuilt++;
                    }

                    secondarySources = remaining;

                    EditorUtility.DisplayProgressBar(
                        "CK3 地图构建器",
                        $"连接支流，剩余 {secondarySources.Count}",
                        Mathf.Lerp(0.60f, 0.88f,
                            secondaryBuilt / (float)Math.Max(1, secondaryBuilt + secondarySources.Count)));
                    if (pending.Count == 0)
                    {
                        break;
                    }
                }

                EditorUtility.DisplayProgressBar("CK3 地图构建器", "保存原版河网资产", 0.94f);
                CK3RiverNetworkData data =
                    AssetDatabase.LoadAssetAtPath<CK3RiverNetworkData>(RiverDataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CK3RiverNetworkData>();
                    data.name = "CK3 原版位图河网";
                    AssetDatabase.CreateAsset(data, RiverDataPath);
                }

                data.ReplaceData(
                    new Vector2Int(image.Width, image.Height),
                    WidthPixelValueCount,
                    MinimumWidth,
                    MaximumWidth,
                    SourceWidth,
                    rivers.ToArray(),
                    points.ToArray());
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                return new CK3RiverNetworkBuildResult(
                    data,
                    mainSources.Count,
                    secondaryBuilt,
                    secondarySources.Count);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void TraceRiver(
            IndexedPng image,
            int source,
            int parentPoint,
            int[] riverOwner,
            List<CK3RiverRecord> rivers,
            List<CK3RiverPixelPoint> points)
        {
            int riverIndex = rivers.Count;
            int pointStart = points.Count;
            int parentRiver = parentPoint >= 0 ? riverOwner[parentPoint] : -1;
            if (parentPoint >= 0)
            {
                AddPoint(image, parentPoint, points);
            }

            AddPoint(image, source, points);
            riverOwner[source] = riverIndex;
            int current = source;
            while (true)
            {
                int next = FindUnvisitedRiverNeighbor(
                    current, image.Width, image.Height, image.Indices, riverOwner);
                if (next < 0)
                {
                    break;
                }

                AddPoint(image, next, points);
                riverOwner[next] = riverIndex;
                current = next;
            }

            rivers.Add(new CK3RiverRecord(
                image.Indices[source],
                parentRiver,
                pointStart,
                points.Count - pointStart));
        }

        private static void AddPoint(
            IndexedPng image,
            int offset,
            List<CK3RiverPixelPoint> points)
        {
            int x = offset % image.Width;
            int y = offset / image.Width;
            byte paletteIndex = image.Indices[offset];
            float width = paletteIndex < 3
                ? SourceWidth
                : (MaximumWidth - MinimumWidth) *
                  (paletteIndex - 3.0f) / WidthPixelValueCount + MinimumWidth;
            points.Add(new CK3RiverPixelPoint(x, y, paletteIndex, width));
        }

        private static int FindOwnedNeighbor(
            int offset,
            int width,
            int height,
            int[] riverOwner)
        {
            int x = offset % width;
            int y = offset / width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + NeighborX[direction];
                int ny = y + NeighborY[direction];
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                {
                    continue;
                }

                int neighbor = ny * width + nx;
                if (riverOwner[neighbor] >= 0)
                {
                    return neighbor;
                }
            }

            return -1;
        }

        private static int FindUnvisitedRiverNeighbor(
            int offset,
            int width,
            int height,
            byte[] pixels,
            int[] riverOwner)
        {
            int x = offset % width;
            int y = offset / width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + NeighborX[direction];
                int ny = y + NeighborY[direction];
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                {
                    continue;
                }

                int neighbor = ny * width + nx;
                byte value = pixels[neighbor];
                if (riverOwner[neighbor] < 0 && value >= 3 && value <= 253)
                {
                    return neighbor;
                }
            }

            return -1;
        }

        private static void ValidateImage(IndexedPng image)
        {
            if (image.Width != ExpectedWidth || image.Height != ExpectedHeight)
            {
                throw new InvalidDataException(
                    $"rivers.png 尺寸错误：{image.Width}×{image.Height}，" +
                    $"原版当前数据应为 {ExpectedWidth}×{ExpectedHeight}。");
            }

            Color32[] expected =
            {
                new Color32(0, 255, 0, 255),
                new Color32(255, 0, 0, 255),
                new Color32(255, 252, 0, 255)
            };
            if (image.Palette.Length < 256)
            {
                throw new InvalidDataException("rivers.png 调色板不足 256 项。");
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (!image.Palette[index].Equals(expected[index]))
                {
                    throw new InvalidDataException(
                        $"rivers.png 的源点调色板索引 {index} 与原版定义不一致。");
                }
            }
        }

        private static IndexedPng DecodeIndexed8Png(string absolutePath)
        {
            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            byte[] actualSignature = reader.ReadBytes(signature.Length);
            if (actualSignature.Length != signature.Length)
            {
                throw new InvalidDataException("rivers.png 文件头不完整。");
            }

            for (int index = 0; index < signature.Length; index++)
            {
                if (actualSignature[index] != signature[index])
                {
                    throw new InvalidDataException("rivers.png 不是 PNG 文件。");
                }
            }

            int width = 0;
            int height = 0;
            Color32[] palette = Array.Empty<Color32>();
            using MemoryStream idat = new MemoryStream();
            while (stream.Position < stream.Length)
            {
                int length = checked((int)ReadBigEndianUInt32(reader));
                string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                byte[] data = reader.ReadBytes(length);
                if (data.Length != length)
                {
                    throw new EndOfStreamException("rivers.png 数据块不完整。");
                }

                reader.ReadUInt32();
                if (type == "IHDR")
                {
                    width = checked((int)ReadBigEndianUInt32(data, 0));
                    height = checked((int)ReadBigEndianUInt32(data, 4));
                    if (data[8] != 8 || data[9] != 3 || data[10] != 0 ||
                        data[11] != 0 || data[12] != 0)
                    {
                        throw new InvalidDataException(
                            "rivers.png 不是原版使用的 8 位索引色、无交错 PNG。");
                    }
                }
                else if (type == "PLTE")
                {
                    if (data.Length % 3 != 0)
                    {
                        throw new InvalidDataException("rivers.png 的 PLTE 长度无效。");
                    }

                    palette = new Color32[data.Length / 3];
                    for (int index = 0; index < palette.Length; index++)
                    {
                        palette[index] = new Color32(
                            data[index * 3], data[index * 3 + 1], data[index * 3 + 2], 255);
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

            int stride = width;
            byte[] filtered = InflateZlib(idat.ToArray(), checked((stride + 1) * height));
            byte[] pixels = new byte[checked(stride * height)];
            byte[] previous = new byte[stride];
            byte[] current = new byte[stride];
            int sourceOffset = 0;
            for (int y = 0; y < height; y++)
            {
                byte filter = filtered[sourceOffset++];
                for (int x = 0; x < stride; x++)
                {
                    int left = x > 0 ? current[x - 1] : 0;
                    int above = previous[x];
                    int upperLeft = x > 0 ? previous[x - 1] : 0;
                    int value = filtered[sourceOffset++];
                    value += filter switch
                    {
                        0 => 0,
                        1 => left,
                        2 => above,
                        3 => (left + above) >> 1,
                        4 => Paeth(left, above, upperLeft),
                        _ => throw new InvalidDataException(
                            "rivers.png 使用未知行过滤器：" + filter)
                    };
                    current[x] = (byte)value;
                }

                // Keep PNG source row order. Jomini traces integer bitmap Y in this order;
                // conversion to world Z happens after graph construction.
                Buffer.BlockCopy(current, 0, pixels, y * stride, stride);
                byte[] swap = previous;
                previous = current;
                current = swap;
            }

            return new IndexedPng(width, height, palette, pixels);
        }

        private static byte[] InflateZlib(byte[] zlib, int expectedLength)
        {
            if (zlib.Length < 6 || (zlib[0] & 0x0F) != 8 ||
                ((zlib[0] << 8) + zlib[1]) % 31 != 0 || (zlib[1] & 0x20) != 0)
            {
                throw new InvalidDataException("rivers.png 的 zlib 头无效。");
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
                throw new InvalidDataException("rivers.png 解压后的字节数量不正确。");
            }

            uint expectedAdler = ReadBigEndianUInt32(zlib, zlib.Length - 4);
            if (Adler32(result) != expectedAdler)
            {
                throw new InvalidDataException("rivers.png 的 Adler-32 校验失败。");
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

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("无法定位 Unity 项目目录。");
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
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

        private readonly struct IndexedPng
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Color32[] Palette;
            public readonly byte[] Indices;

            public IndexedPng(int width, int height, Color32[] palette, byte[] indices)
            {
                Width = width;
                Height = height;
                Palette = palette;
                Indices = indices;
            }
        }

        private readonly struct PendingSecondary
        {
            public readonly int Source;
            public readonly int ParentPoint;

            public PendingSecondary(int source, int parentPoint)
            {
                Source = source;
                ParentPoint = parentPoint;
            }
        }
    }
}
