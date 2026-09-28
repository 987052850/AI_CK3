using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    internal static class CK3TerrainMaterialBuilder
    {
        internal const string OutputRoot = "Assets/CK3Map/Data/Generated/Terrain";
        internal const string LibraryPath = OutputRoot + "/CK3原版地表材质库.asset";

        private const string SourceTerrainRoot = "Assets/CK3Map/Data/Source/game/gfx/map/terrain";
        private const string MaterialsSettingsPath = SourceTerrainRoot + "/materials.settings";
        private const string TerrainSettingsPath = SourceTerrainRoot + "/settings.terrain";
        private const string DetailSettingsPath = SourceTerrainRoot + "/detail_data.settings";
        private const string DetailIndexPath = SourceTerrainRoot + "/detail_index.tga";
        private const string DetailMaskPath = SourceTerrainRoot + "/detail_intensity.tga";
        private const string ColorMapPath = SourceTerrainRoot + "/colormap.dds";
        private const string TerrainSunnyEnvironmentPath =
            "Assets/CK3Map/Data/Source/game/gfx/map/environment/environment_terrain_sunny.dds";
        private const string TerrainShaderName = "CK3Map/Terrain Surface Pre-Lighting";
        private const string TerrainMaterialPath = OutputRoot + "/CK3原版地表合成_光照前.mat";
        private const string DetailIndexAssetPath = OutputRoot + "/CK3地表_四层材质索引_BGRA32.asset";
        private const string DetailMaskAssetPath = OutputRoot + "/CK3地表_四层材质权重_BGRA32.asset";
        private const string ColorMapAssetPath = OutputRoot + "/CK3地表_宏观颜色_DXT5.asset";
        private const int OriginalMaterialCount = 105;
        private const int TextureSize = 1024;
        private const int MipmapCount = 11;
        private const int DetailTextureWidth = 9216;
        private const int DetailTextureHeight = 4608;
        private const int ColorMapMipmapCount = 14;
        private const float WorldExtentX = 9215.0f;
        private const float WorldExtentZ = 4607.0f;
        private const uint DdsMagic = 0x20534444;
        private const uint Dxt5FourCc = 0x35545844;

        private static readonly string[] DynamicMaterialIds =
        {
            "drought",
            "drought_cracks",
            "flood",
            "summer_grass",
            "winter_effect"
        };

        internal static CK3TerrainMaterialLibrary Build()
        {
            string materialsAbsolute = ToAbsolutePath(MaterialsSettingsPath);
            string terrainSettingsAbsolute = ToAbsolutePath(TerrainSettingsPath);
            string detailSettingsAbsolute = ToAbsolutePath(DetailSettingsPath);

            RequireFile(materialsAbsolute, MaterialsSettingsPath);
            RequireFile(terrainSettingsAbsolute, TerrainSettingsPath);
            RequireFile(detailSettingsAbsolute, DetailSettingsPath);

            List<SourceEntry> sourceEntries = ParseMaterialEntries(File.ReadAllText(materialsAbsolute));
            ValidateEntries(sourceEntries);

            float defaultTileFactor = ParseSettingFloat(File.ReadAllText(terrainSettingsAbsolute), "detail_tile_factor");
            float detailBlendRange = ParseSettingFloat(File.ReadAllText(terrainSettingsAbsolute), "detail_blend_range");
            float tileOffsetX = ParseSettingFloat(File.ReadAllText(terrainSettingsAbsolute), "detail_tile_offset_x");
            float tileOffsetY = ParseSettingFloat(File.ReadAllText(terrainSettingsAbsolute), "detail_tile_offset_y");
            float intensityBias = ParseJsonNumber(File.ReadAllText(detailSettingsAbsolute), "material_intensity_bias");
            int materialLimit = Mathf.RoundToInt(ParseJsonNumber(File.ReadAllText(detailSettingsAbsolute), "materials_limit"));
            if (materialLimit != 4)
            {
                throw new InvalidDataException($"CK3 detail_data.settings 的 materials_limit 应为 4，实际为 {materialLimit}。");
            }

            EnsureAssetFolder(OutputRoot);

            Texture2DArray diffuseArray = BuildArray(
                sourceEntries,
                entry => entry.Diffuse,
                OutputRoot + "/CK3地表_漫反射高度_DXT5.asset",
                "CK3 地表漫反射与高度",
                false);
            Texture2DArray normalArray = BuildArray(
                sourceEntries,
                entry => entry.Normal,
                OutputRoot + "/CK3地表_RRxG法线_DXT5.asset",
                "CK3 地表 RRxG 法线",
                true);
            Texture2DArray materialArray = BuildArray(
                sourceEntries,
                entry => entry.Material,
                OutputRoot + "/CK3地表_材质属性_DXT5.asset",
                "CK3 地表材质属性",
                true);
            Texture2D detailIndexTexture = BuildTgaBgraTexture(
                DetailIndexPath,
                DetailIndexAssetPath,
                "CK3 四层材质索引",
                FilterMode.Point);
            Texture2D detailMaskTexture = BuildTgaBgraTexture(
                DetailMaskPath,
                DetailMaskAssetPath,
                "CK3 四层材质权重",
                FilterMode.Point);
            Texture2D colorMapTexture = BuildDxt5Texture(
                ColorMapPath,
                ColorMapAssetPath,
                "CK3 地表宏观颜色",
                DetailTextureWidth,
                DetailTextureHeight,
                ColorMapMipmapCount,
                true);

            List<CK3TerrainMaterialEntry> entries = new List<CK3TerrainMaterialEntry>(sourceEntries.Count);
            for (int index = 0; index < sourceEntries.Count; index++)
            {
                SourceEntry source = sourceEntries[index];
                float tileFactor = source.TileFactor.HasValue ? source.TileFactor.Value : defaultTileFactor;
                entries.Add(new CK3TerrainMaterialEntry(
                    index,
                    source.Name,
                    source.Id,
                    index < DynamicMaterialIds.Length,
                    tileFactor,
                    source.Diffuse,
                    source.Normal,
                    source.Material,
                    source.Mask));
            }

            CK3TerrainMaterialLibrary library = AssetDatabase.LoadAssetAtPath<CK3TerrainMaterialLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CK3TerrainMaterialLibrary>();
                library.name = "CK3 原版地表材质库";
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.ReplaceFromOriginal(
                entries,
                diffuseArray,
                normalArray,
                materialArray,
                detailBlendRange,
                defaultTileFactor,
                new Vector2(tileOffsetX, tileOffsetY),
                intensityBias);

            Material terrainSurfaceMaterial = BuildTerrainSurfaceMaterial(
                sourceEntries,
                diffuseArray,
                normalArray,
                materialArray,
                detailIndexTexture,
                detailMaskTexture,
                colorMapTexture,
                detailBlendRange,
                defaultTileFactor,
                new Vector2(tileOffsetX, tileOffsetY));
            library.ReplaceSurfaceAssets(
                detailIndexTexture,
                detailMaskTexture,
                colorMapTexture,
                terrainSurfaceMaterial,
                new Vector2(WorldExtentX, WorldExtentZ));
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            return library;
        }

        private static Texture2DArray BuildArray(
            IReadOnlyList<SourceEntry> entries,
            Func<SourceEntry, string> pathSelector,
            string assetPath,
            string objectName,
            bool linear)
        {
            Texture2DArray previous = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
            if (previous != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            Texture2DArray array = new Texture2DArray(
                TextureSize,
                TextureSize,
                entries.Count,
                TextureFormat.DXT5,
                true,
                linear)
            {
                name = objectName,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 1
            };

            try
            {
                for (int slice = 0; slice < entries.Count; slice++)
                {
                    string relative = pathSelector(entries[slice]);
                    string sourcePath = SourceTerrainRoot + "/" + relative.Replace('\\', '/');
                    string absolute = ToAbsolutePath(sourcePath);
                    EditorUtility.DisplayProgressBar(
                        "构建 CK3 原版地表纹理数组",
                        $"{objectName}  {slice + 1}/{entries.Count}  {relative}",
                        (float)slice / entries.Count);
                    CopyDxt5MipChain(absolute, sourcePath, array, slice);
                }

                array.Apply(false, true);
                AssetDatabase.CreateAsset(array, assetPath);
                AssetDatabase.SaveAssets();
                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Texture2D BuildTgaBgraTexture(
            string sourceAssetPath,
            string outputAssetPath,
            string objectName,
            FilterMode filterMode)
        {
            string absolutePath = ToAbsolutePath(sourceAssetPath);
            RequireFile(absolutePath, sourceAssetPath);

            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);
            byte idLength = reader.ReadByte();
            byte colorMapType = reader.ReadByte();
            byte imageType = reader.ReadByte();
            reader.ReadBytes(9);
            int width = reader.ReadUInt16();
            int height = reader.ReadUInt16();
            byte bitsPerPixel = reader.ReadByte();
            byte descriptor = reader.ReadByte();

            if (idLength != 0 || colorMapType != 0 || imageType != 2 ||
                width != DetailTextureWidth || height != DetailTextureHeight ||
                bitsPerPixel != 32 || descriptor != 0x08)
            {
                throw new InvalidDataException(
                    $"TGA 格式与 CK3 完整世界控制图不一致：{sourceAssetPath}，" +
                    $"类型 {imageType}，尺寸 {width}x{height}，位深 {bitsPerPixel}，描述 0x{descriptor:X2}。");
            }

            int pixelByteCount = checked(width * height * 4);
            byte[] pixelData = reader.ReadBytes(pixelByteCount);
            if (pixelData.Length != pixelByteCount || stream.Length != 18L + pixelByteCount + 26L)
            {
                throw new InvalidDataException($"TGA 像素数据或 TRUEVISION 页脚长度不正确：{sourceAssetPath}");
            }

            byte[] footer = reader.ReadBytes(26);
            string signature = System.Text.Encoding.ASCII.GetString(footer, 8, 18);
            if (!string.Equals(signature, "TRUEVISION-XFILE.\0", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"TGA 缺少 CK3 原文件的 TRUEVISION 页脚：{sourceAssetPath}");
            }

            Texture2D previous = AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
            if (previous != null)
            {
                AssetDatabase.DeleteAsset(outputAssetPath);
            }

            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.BGRA32,
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
                texture.SetPixelData(pixelData, 0, 0);
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

        private static Texture2D BuildDxt5Texture(
            string sourceAssetPath,
            string outputAssetPath,
            string objectName,
            int expectedWidth,
            int expectedHeight,
            int expectedMipmapCount,
            bool linear)
        {
            string absolutePath = ToAbsolutePath(sourceAssetPath);
            RequireFile(absolutePath, sourceAssetPath);
            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != DdsMagic || reader.ReadUInt32() != 124)
            {
                throw new InvalidDataException($"不是 CK3 预期的 DDS 文件：{sourceAssetPath}");
            }

            reader.ReadUInt32();
            int height = reader.ReadInt32();
            int width = reader.ReadInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            int mipCount = reader.ReadInt32();
            stream.Position = 84;
            uint fourCc = reader.ReadUInt32();
            if (width != expectedWidth || height != expectedHeight ||
                mipCount != expectedMipmapCount || fourCc != Dxt5FourCc)
            {
                throw new InvalidDataException(
                    $"DDS 格式与 CK3 完整世界纹理不一致：{sourceAssetPath}，" +
                    $"尺寸 {width}x{height}，Mip {mipCount}，FourCC 0x{fourCc:X8}。");
            }

            Texture2D previous = AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
            if (previous != null)
            {
                AssetDatabase.DeleteAsset(outputAssetPath);
            }

            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.DXT5,
                true,
                linear)
            {
                name = objectName,
                filterMode = FilterMode.Trilinear,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                anisoLevel = 1
            };

            try
            {
                stream.Position = 128;
                int mipWidth = width;
                int mipHeight = height;
                for (int mip = 0; mip < mipCount; mip++)
                {
                    int blockWidth = Math.Max(1, (mipWidth + 3) / 4);
                    int blockHeight = Math.Max(1, (mipHeight + 3) / 4);
                    int byteCount = checked(blockWidth * blockHeight * 16);
                    byte[] data = reader.ReadBytes(byteCount);
                    if (data.Length != byteCount)
                    {
                        throw new EndOfStreamException(
                            $"DDS Mip 数据不完整：{sourceAssetPath}，Mip {mip}。");
                    }

                    texture.SetPixelData(data, mip, 0);
                    mipWidth = Math.Max(1, mipWidth / 2);
                    mipHeight = Math.Max(1, mipHeight / 2);
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException($"DDS 存在未登记的尾部数据：{sourceAssetPath}。");
                }

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

        private static Material BuildTerrainSurfaceMaterial(
            IReadOnlyList<SourceEntry> sourceEntries,
            Texture2DArray diffuseArray,
            Texture2DArray normalArray,
            Texture2DArray materialArray,
            Texture2D detailIndexTexture,
            Texture2D detailMaskTexture,
            Texture2D colorMapTexture,
            float detailBlendRange,
            float defaultTileFactor,
            Vector2 detailTileOffset)
        {
            Shader shader = Shader.Find(TerrainShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "找不到 CK3 地表 Shader，或 Shader 尚未通过 Unity 编译：" + TerrainShaderName);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "CK3 原版地表合成（光照前）"
                };
                AssetDatabase.CreateAsset(material, TerrainMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_DetailTextures", diffuseArray);
            material.SetTexture("_NormalTextures", normalArray);
            material.SetTexture("_MaterialTextures", materialArray);
            material.SetTexture("_DetailIndexTexture", detailIndexTexture);
            material.SetTexture("_DetailMaskTexture", detailMaskTexture);
            material.SetTexture("_ColorTexture", colorMapTexture);
            material.SetFloat("_DetailBlendRange", detailBlendRange);
            Cubemap terrainSunnyEnvironment =
                AssetDatabase.LoadAssetAtPath<Cubemap>(TerrainSunnyEnvironmentPath);
            if (terrainSunnyEnvironment != null)
            {
                material.SetTexture("_CK3TerrainSunnyEnvironmentMap", terrainSunnyEnvironment);
            }
            material.SetFloat("_CK3MapLightingEnabled", 1.0f);
            material.SetColor("_CK3TerrainSunnySunColor", new Color(1.0f, 0.9f, 0.8f, 1.0f));
            material.SetFloat("_CK3TerrainSunnySunIntensity", 8.0f);
            material.SetFloat("_CK3TerrainSunnyIblScale", 0.25f);
            material.SetFloat("_CK3TerrainSunnySpecularFactor", 1.0f);

            float defaultPackedFactor = defaultTileFactor / WorldExtentX;
            material.SetVector(
                "_DetailTileFactor",
                new Vector4(defaultPackedFactor, -defaultPackedFactor, 0.0f, 0.0f));
            material.SetVector(
                "_DetailTileOffset",
                new Vector4(detailTileOffset.x, detailTileOffset.y, 0.0f, 0.0f));
            material.SetVector(
                "_WorldSpaceToDetail",
                new Vector4(
                    1.0f / DetailTextureWidth,
                    1.0f / DetailTextureHeight,
                    0.0f,
                    0.0f));
            material.SetVector(
                "_DetailTexelSize",
                new Vector4(
                    1.0f / DetailTextureWidth,
                    1.0f / DetailTextureHeight,
                    0.0f,
                    0.0f));
            material.SetVector(
                "_DetailTextureSize",
                new Vector4(DetailTextureWidth, DetailTextureHeight, 0.0f, 0.0f));
            material.SetVector(
                "_WorldSpaceToTerrain01",
                new Vector4(1.0f / WorldExtentX, 1.0f / WorldExtentZ, 0.0f, 0.0f));

            Vector4[] packedFactors = new Vector4[sourceEntries.Count];
            for (int index = 0; index < sourceEntries.Count; index++)
            {
                float tileFactor = sourceEntries[index].TileFactor ?? defaultTileFactor;
                float packed = tileFactor / WorldExtentX;
                packedFactors[index] = new Vector4(packed, -packed, 0.0f, 0.0f);
            }

            material.SetVectorArray("_CK3PackedDetailTileFactors", packedFactors);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void CopyDxt5MipChain(string absolutePath, string displayPath, Texture2DArray target, int slice)
        {
            RequireFile(absolutePath, displayPath);
            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != DdsMagic || reader.ReadUInt32() != 124)
            {
                throw new InvalidDataException($"不是 CK3 预期的 DDS 文件：{displayPath}");
            }

            reader.ReadUInt32();
            int height = reader.ReadInt32();
            int width = reader.ReadInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            int mipCount = reader.ReadInt32();
            stream.Position = 84;
            uint fourCc = reader.ReadUInt32();
            if (width != TextureSize || height != TextureSize || mipCount != MipmapCount || fourCc != Dxt5FourCc)
            {
                throw new InvalidDataException(
                    $"DDS 格式与 CK3 地表数组不一致：{displayPath}，" +
                    $"尺寸 {width}x{height}，Mip {mipCount}，FourCC 0x{fourCc:X8}。");
            }

            stream.Position = 128;
            int mipWidth = width;
            int mipHeight = height;
            for (int mip = 0; mip < mipCount; mip++)
            {
                int blockWidth = Math.Max(1, (mipWidth + 3) / 4);
                int blockHeight = Math.Max(1, (mipHeight + 3) / 4);
                int byteCount = checked(blockWidth * blockHeight * 16);
                byte[] data = reader.ReadBytes(byteCount);
                if (data.Length != byteCount)
                {
                    throw new EndOfStreamException($"DDS Mip 数据不完整：{displayPath}，Mip {mip}。");
                }

                target.SetPixelData(data, mip, slice, 0);
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
            }

            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"DDS 存在未登记的尾部数据：{displayPath}。");
            }
        }

        private static List<SourceEntry> ParseMaterialEntries(string text)
        {
            string noComments = Regex.Replace(text, @"#.*$", string.Empty, RegexOptions.Multiline);
            MatchCollection blocks = Regex.Matches(noComments, @"\{(?<body>[^{}]+)\}", RegexOptions.Singleline);
            List<SourceEntry> result = new List<SourceEntry>();
            foreach (Match block in blocks)
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
                MatchCollection fields = Regex.Matches(
                    block.Groups["body"].Value,
                    @"(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:""(?<quoted>[^""]*)""|(?<bare>[^\s}]+))");
                foreach (Match field in fields)
                {
                    string value = field.Groups["quoted"].Success
                        ? field.Groups["quoted"].Value
                        : field.Groups["bare"].Value;
                    values[field.Groups["key"].Value] = value;
                }

                if (!values.ContainsKey("id"))
                {
                    continue;
                }

                result.Add(new SourceEntry
                {
                    Name = RequireValue(values, "name"),
                    Id = RequireValue(values, "id"),
                    Diffuse = RequireValue(values, "diffuse"),
                    Normal = RequireValue(values, "normal"),
                    Material = RequireValue(values, "material"),
                    Mask = RequireValue(values, "mask"),
                    TileFactor = values.TryGetValue("tile_factor", out string tileFactor)
                        ? float.Parse(tileFactor, NumberStyles.Float, CultureInfo.InvariantCulture)
                        : (float?)null
                });
            }

            return result;
        }

        private static void ValidateEntries(IReadOnlyList<SourceEntry> entries)
        {
            if (entries.Count != OriginalMaterialCount)
            {
                throw new InvalidDataException($"CK3 materials.settings 应包含 105 个材质，实际解析到 {entries.Count} 个。");
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                SourceEntry entry = entries[index];
                if (!ids.Add(entry.Id))
                {
                    throw new InvalidDataException("CK3 材质 id 重复：" + entry.Id);
                }

                if (index < DynamicMaterialIds.Length && entry.Id != DynamicMaterialIds[index])
                {
                    throw new InvalidDataException(
                        $"CK3 动态材质顺序改变：索引 {index} 应为 {DynamicMaterialIds[index]}，实际为 {entry.Id}。");
                }

                RequireFile(ToAbsolutePath(SourceTerrainRoot + "/" + entry.Diffuse), entry.Diffuse);
                RequireFile(ToAbsolutePath(SourceTerrainRoot + "/" + entry.Normal), entry.Normal);
                RequireFile(ToAbsolutePath(SourceTerrainRoot + "/" + entry.Material), entry.Material);
                RequireFile(ToAbsolutePath(SourceTerrainRoot + "/" + entry.Mask), entry.Mask);
            }
        }

        private static float ParseSettingFloat(string text, string key)
        {
            Match match = Regex.Match(
                text,
                @"^\s*" + Regex.Escape(key) + @"\s*=\s*(?<value>[-+0-9.eE]+)",
                RegexOptions.Multiline);
            if (!match.Success)
            {
                throw new InvalidDataException("CK3 settings.terrain 缺少字段：" + key);
            }

            return float.Parse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static float ParseJsonNumber(string text, string key)
        {
            Match match = Regex.Match(
                text,
                "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(?<value>[-+0-9.eE]+)");
            if (!match.Success)
            {
                throw new InvalidDataException("CK3 detail_data.settings 缺少字段：" + key);
            }

            return float.Parse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string RequireValue(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("CK3 materials.settings 材质条目缺少字段：" + key);
            }

            return value;
        }

        private static void RequireFile(string absolutePath, string displayPath)
        {
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("缺少 CK3 原版源文件：" + displayPath, absolutePath);
            }
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

        private sealed class SourceEntry
        {
            internal string Name = string.Empty;
            internal string Id = string.Empty;
            internal string Diffuse = string.Empty;
            internal string Normal = string.Empty;
            internal string Material = string.Empty;
            internal string Mask = string.Empty;
            internal float? TileFactor;
        }
    }
}
