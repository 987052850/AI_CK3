using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;

namespace CK3Map.Editor
{
    public readonly struct CK3MapNameMeshBuildResult
    {
        public int NameCount { get; }
        public int GlyphCount { get; }

        public CK3MapNameMeshBuildResult(int names, int glyphs)
        {
            NameCount = names;
            GlyphCount = glyphs;
        }
    }

    /// <summary>
    /// countryname.cpp 已确认链路的 Editor 持久化移植：区域像素回归轴、
    /// 十一条平行测试线、最长连续区域段、原版缩放/字距、五控制点和两段
    /// Catmull-Rom。静态 Mesh、R 通道字体图集和材质全部保存到 Assets。
    /// </summary>
    public static class CK3MapNameMeshBuilder
    {
        public const string MeshPath = CK3MapNameSourceBuilder.OutputRoot + "/CK3法理王国曲线名称网格.asset";
        public const string ActualRealmMeshPath = CK3MapNameSourceBuilder.OutputRoot + "/CK3实际领地曲线名称网格.asset";
        public const string MaterialPath = CK3MapNameSourceBuilder.OutputRoot + "/CK3原版地图名称材质.mat";
        public const string FontTexturePath = CK3MapNameSourceBuilder.OutputRoot + "/CK3原版MapFont_R8.asset";
        public const string SceneObjectName = "法理王国曲线名称";
        public const string ActualRealmSceneObjectName = "实际领地曲线名称";

        private const string ProvincePngPath = "Assets/CK3Map/Data/Source/game/map_data/provinces.png";
        private const string RoughOverlayPath = "Assets/CK3Map/Data/Source/game/gfx/map/textures/rough_texture_overlay.dds";
        private const int InitialStride = 4;
        private const int MaximumSamples = 65536;
        private const int NumberOfLineTests = 5;
        private const float TestLineSpacing = 10.0f;
        private const float FinalScale = 0.75f;
        private const float ScaleCapWidth = 0.9f;
        private const float ScaleCapHeight = 0.6f;
        private const float MaximumStretchFactor = 1.2f;
        private const float HorizontalBias = 1.6f;
        private const float SquarenessThreshold = 0.06f;
        private const float SingleCharacterSquarenessThreshold = 0.15f;
        private const int CurveCutoff = 2;

        private struct PixelStats
        {
            public long Count;
            public long SumX;
            public long SumY;
            public long SumXY;
            public long SumX2;
            public long SumY2;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
        }

        private struct LineSegment
        {
            public Vector2 Start;
            public Vector2 End;
            public int PixelCount;
            public float LengthSquared => (End - Start).sqrMagnitude;
        }

        private struct GlyphLayout
        {
            public TMP_Character Character;
            public float Left;
            public float Right;
            public float Bottom;
            public float Top;
        }

        private sealed class RegionMask
        {
            private readonly int[] provinceByPixel;
            private readonly int width;
            private readonly int height;
            private readonly HashSet<int> provinces;
            private readonly CK3ProvinceDatabase database;
            private readonly bool allowWaterCrossing;

            public RegionMask(
                int[] pixels,
                int imageWidth,
                int imageHeight,
                IEnumerable<int> ids,
                CK3ProvinceDatabase provinceDatabase,
                bool allowWater)
            {
                provinceByPixel = pixels;
                width = imageWidth;
                height = imageHeight;
                provinces = new HashSet<int>(ids);
                database = provinceDatabase;
                allowWaterCrossing = allowWater;
            }

            public bool ContainsArea(int x, int y)
            {
                return x >= 0 && y >= 0 && x < width && y < height && provinces.Contains(provinceByPixel[y * width + x]);
            }

            public bool ContainsLine(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    return false;

                int province = provinceByPixel[y * width + x];
                if (provinces.Contains(province))
                    return true;
                if (province < 0 || province > database.MaxProvinceId)
                    return false;

                CK3ProvinceCategory category = database.GetCategory(province);
                if ((category & CK3ProvinceCategory.大河省份) != 0)
                    return true;
                if (!allowWaterCrossing)
                    return false;

                return (category & (CK3ProvinceCategory.海区 |
                    CK3ProvinceCategory.湖泊 |
                    CK3ProvinceCategory.不可通行海域)) != 0;
            }
        }

        public static CK3MapNameMeshBuildResult BuildAndBind()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("曲线地图名称只能在非 Play 模式构建。");

            CK3MapNameMeshBuildResult deJure = BuildAndBindMode(false);
            CK3MapNameMeshBuildResult actual = BuildAndBindMode(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new CK3MapNameMeshBuildResult(
                deJure.NameCount + actual.NameCount,
                deJure.GlyphCount + actual.GlyphCount);
        }

        private static CK3MapNameMeshBuildResult BuildAndBindMode(bool actualRealm)
        {

            CK3MapNameSourceData source = AssetDatabase.LoadAssetAtPath<CK3MapNameSourceData>(CK3MapNameSourceBuilder.SourceDataPath);
            CK3ProvinceDatabase database = AssetDatabase.LoadAssetAtPath<CK3ProvinceDatabase>(CK3PoliticalDataBuilder.ProvinceDatabasePath);
            CK3TerrainHeightData heightData = AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(CK3TerrainWorldBuilder.HeightDataPath);
            Shader shader = Shader.Find("CK3Map/Map Name");
            Texture2D roughOverlay = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughOverlayPath);
            if (source == null || source.MapFont == null || database == null || heightData == null || shader == null)
                throw new InvalidOperationException("缺少地图名称源、原版字体、省份数据库、高度页表或 CK3Map/Map Name Shader。");
            if (roughOverlay == null)
                throw new InvalidOperationException("原版 rough_texture_overlay.dds 未被 Unity 正确导入，不能用替代纹理生成地图名称。");

            source.MapFont.ReadFontAssetDefinition();
            CK3PoliticalDataBuilder.RgbPng image = CK3PoliticalDataBuilder.DecodeRgb8Png(ToAbsolute(ProvincePngPath));
            int[] provinceByPixel = BuildProvinceRaster(image, database);
            List<Vector3> vertices = new List<Vector3>(32768);
            List<Vector2> uvs = new List<Vector2>(32768);
            List<int> triangles = new List<int>(49152);
            int nameCount = 0;
            int glyphCount = 0;

            try
            {
                CK3MapNameRegion[] regions = actualRealm
                    ? source.ActualRealmRegions
                    : source.DeJureKingdomRegions;
                if (actualRealm && (regions == null || regions.Length == 0))
                    throw new InvalidOperationException("实际领地名称源为空，请先重新构建地图名称源与字体图集。");
                for (int i = 0; i < regions.Length; i++)
                {
                    if ((i & 7) == 0)
                        EditorUtility.DisplayProgressBar("CK3 原版曲线地图名称", $"生成 {i}/{regions.Length}", (float)i / Math.Max(1, regions.Length));
                    // countryname.cpp keeps Area membership separate from the province
                    // flags accepted while clipping a candidate line. Realm/de-jure map
                    // names enable the water-crossing Area option: sea/lake pixels may
                    // connect two land masses, but never contribute to area/centroid/size.
                    RegionMask mask = new RegionMask(
                        provinceByPixel,
                        image.Width,
                        image.Height,
                        regions[i].ProvinceIds,
                        database,
                        true);
                    if (AppendRegion(regions[i], source.MapFont, mask, heightData, vertices, uvs, triangles, out int addedGlyphs))
                    {
                        nameCount++;
                        glyphCount += addedGlyphs;
                    }
                }

                string meshPath = actualRealm ? ActualRealmMeshPath : MeshPath;
                string meshName = actualRealm ? "CK3 实际领地曲线名称网格" : "CK3 法理王国曲线名称网格";
                Mesh mesh = LoadOrCreateMesh(meshPath, meshName);
                mesh.Clear();
                mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0, true);
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);

                Texture2D fontTexture = BuildRedChannelFontTexture(source.MapFont);
                Material material = LoadOrCreateMaterial(shader);
                material.SetTexture("_FontAtlas", fontTexture);
                material.SetTexture("_MapNameOverlayTexture", roughOverlay);
                material.SetVector("_TextureSize", new Vector4(fontTexture.width, fontTexture.height, 0.0f, 0.0f));
                material.SetFloat("_Transparency", 1.0f);
                material.SetFloat("_LodFactor", 0.05f);
                material.SetFloat("_ThicknessBias", 0.015f);
                material.SetFloat("_FlatMapLerp", 1.0f);
                material.SetFloat("_FlatMapHeight", 3.92f);
                material.SetTexture("_CK3HeightLookupTexture", heightData.IndirectionTexture);
                material.SetTexture("_CK3PackedHeightTexture", heightData.PackedHeightTexture);
                material.SetVector("_CK3WorldSpaceToLookup", heightData.WorldSpaceToLookup);
                material.SetVector(
                    "_CK3IndirectionSize",
                    new Vector4(heightData.IndirectionSize.x, heightData.IndirectionSize.y, 0.0f, 0.0f));
                material.SetFloat("_CK3BaseTileSize", heightData.BaseTileSize);
                material.SetFloat("_CK3HeightScale", heightData.HeightScale);
                material.SetFloat("_CK3TerrainVerticalOffset", 0.0f);
                Vector4[] heightConstants = heightData.TileToHeightmapScaleAndOffset;
                for (int index = 0; index < heightConstants.Length; index++)
                {
                    material.SetVector($"_CK3TileToHeightMap{index}", heightConstants[index]);
                }
                EditorUtility.SetDirty(material);

                BindScene(
                    mesh,
                    material,
                    actualRealm ? ActualRealmSceneObjectName : SceneObjectName,
                    actualRealm);
                EditorGUIUtility.PingObject(mesh);
                return new CK3MapNameMeshBuildResult(nameCount, glyphCount);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool AppendRegion(
            CK3MapNameRegion region,
            TMP_FontAsset font,
            RegionMask mask,
            CK3TerrainHeightData heightData,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            out int addedGlyphs)
        {
            addedGlyphs = 0;
            if (string.IsNullOrWhiteSpace(region.DisplayName) || region.PixelCount <= 0)
                return false;

            int stride = CalculateStride(region.PixelCount);
            if (!TryCollectStats(region.PixelBounds, mask, stride, out PixelStats stats))
                return false;
            if (!TryBuildRegressionSegments(stats, region.PixelBounds, mask, out LineSegment horizontal, out LineSegment vertical))
                return false;
            LineSegment selected = horizontal.LengthSquared * HorizontalBias > vertical.LengthSquared ? horizontal : vertical;
            if (selected.PixelCount <= 1 || selected.LengthSquared <= 0.0f)
                return false;
            if (selected.Start.x > selected.End.x || (Mathf.Approximately(selected.Start.x, selected.End.x) && selected.Start.y > selected.End.y))
                (selected.Start, selected.End) = (selected.End, selected.Start);

            if (!TryBuildGlyphLayout(region.DisplayName, font, out List<GlyphLayout> glyphs, out float textWidth, out float textHeight))
                return false;

            float lineLength = Mathf.Sqrt(selected.LengthSquared);
            // countryname.cpp receives the exact summed Area pixel count separately from
            // the strided regression samples. The stride only reduces the covariance work;
            // it must not change the height/size fit of the final name.
            float exactArea = region.PixelCount;
            float widthScale = lineLength * ScaleCapWidth / textWidth;
            float heightScale = (exactArea / lineLength) * ScaleCapHeight / textHeight;
            float threshold = glyphs.Count > 1 ? SquarenessThreshold : SingleCharacterSquarenessThreshold;
            float scale = widthScale;
            float extraSpacing = 0.0f;
            float widthToHeightFit = widthScale / Mathf.Max(heightScale, 0.000001f);
            bool heightBalanced =
                (widthScale > heightScale && glyphs.Count > 1) ||
                Mathf.Abs(1.0f - widthToHeightFit) <= threshold;
            if (heightBalanced)
            {
                scale = heightScale;
                float stretch = Mathf.Clamp(widthScale / heightScale, 1.0f, MaximumStretchFactor);
                if (glyphs.Count > 1)
                    extraSpacing = textWidth * (stretch - 1.0f) / (glyphs.Count - 1);
            }
            scale *= FinalScale;
            if (!(scale > 0.0f) || float.IsNaN(scale) || float.IsInfinity(scale))
                return false;

            Vector2 center = (selected.Start + selected.End) * 0.5f;
            Vector2 direction = (selected.End - selected.Start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float expandedWidth = textWidth + extraSpacing * Math.Max(0, glyphs.Count - 1);
            Vector2[] controls = glyphs.Count > CurveCutoff
                ? BuildFiveControlPoints(
                    glyphs, extraSpacing, scale, center, direction, region.PixelBounds, mask)
                : BuildStraightControls(center, direction, expandedWidth * scale);

            float spacingCenterOffset = extraSpacing * Math.Max(0, glyphs.Count - 1) * 0.5f;
            for (int i = 0; i < glyphs.Count; i++)
            {
                GlyphLayout glyph = glyphs[i];
                float spacingOffset = i * extraSpacing - spacingCenterOffset;
                float left = glyph.Left + spacingOffset;
                float right = glyph.Right + spacingOffset;
                float bottom = glyph.Bottom;
                float top = glyph.Top;
                GlyphRect rect = glyph.Character.glyph.glyphRect;
                Texture2D atlas = font.atlasTextures[glyph.Character.glyph.atlasIndex];
                float u0 = (float)rect.x / atlas.width;
                float v0 = (float)rect.y / atlas.height;
                float u1 = (float)(rect.x + rect.width) / atlas.width;
                float v1 = (float)(rect.y + rect.height) / atlas.height;

                int baseVertex = vertices.Count;
                AddCurvedVertex(left, bottom, expandedWidth, scale, center, direction, normal, controls, heightData, new Vector2(u0, v0), vertices, uvs);
                AddCurvedVertex(left, top, expandedWidth, scale, center, direction, normal, controls, heightData, new Vector2(u0, v1), vertices, uvs);
                AddCurvedVertex(right, top, expandedWidth, scale, center, direction, normal, controls, heightData, new Vector2(u1, v1), vertices, uvs);
                AddCurvedVertex(right, bottom, expandedWidth, scale, center, direction, normal, controls, heightData, new Vector2(u1, v0), vertices, uvs);
                triangles.Add(baseVertex + 0);
                triangles.Add(baseVertex + 1);
                triangles.Add(baseVertex + 2);
                triangles.Add(baseVertex + 0);
                triangles.Add(baseVertex + 2);
                triangles.Add(baseVertex + 3);
            }

            addedGlyphs = glyphs.Count;
            return true;
        }

        private static int CalculateStride(int pixelCount)
        {
            int stride = InitialStride;
            while (stride > 1 && (long)stride * stride > pixelCount)
                stride >>= 1;
            while ((long)pixelCount / ((long)stride * stride) > MaximumSamples)
                stride <<= 1;
            return stride;
        }

        private static bool TryCollectStats(RectInt bounds, RegionMask mask, int stride, out PixelStats stats)
        {
            stats = new PixelStats
            {
                MinX = int.MaxValue,
                MinY = int.MaxValue,
                MaxX = int.MinValue,
                MaxY = int.MinValue
            };
            int endX = bounds.xMax;
            int endY = bounds.yMax;
            for (int y = bounds.yMin; y < endY; y += stride)
            for (int x = bounds.xMin; x < endX; x += stride)
            {
                if (!mask.ContainsArea(x, y)) continue;
                stats.Count++;
                stats.SumX += x;
                stats.SumY += y;
                stats.SumXY += (long)x * y;
                stats.SumX2 += (long)x * x;
                stats.SumY2 += (long)y * y;
                stats.MinX = Math.Min(stats.MinX, x);
                stats.MinY = Math.Min(stats.MinY, y);
                stats.MaxX = Math.Max(stats.MaxX, x);
                stats.MaxY = Math.Max(stats.MaxY, y);
            }
            return stats.Count > 0;
        }

        private static bool TryBuildRegressionSegments(PixelStats stats, RectInt bounds, RegionMask mask, out LineSegment horizontal, out LineSegment vertical)
        {
            horizontal = default;
            vertical = default;
            double n = stats.Count;
            double covariance = n * stats.SumXY - (double)stats.SumX * stats.SumY;
            double denominatorX = n * stats.SumX2 - (double)stats.SumX * stats.SumX;
            double denominatorY = n * stats.SumY2 - (double)stats.SumY * stats.SumY;
            if (denominatorX == 0.0 || denominatorY == 0.0)
                return false;
            float slopeYFromX = (float)(covariance / denominatorX);
            float interceptY = (float)(stats.SumY / n - slopeYFromX * (stats.SumX / n));
            float slopeXFromY = (float)(covariance / denominatorY);
            float interceptX = (float)(stats.SumX / n - slopeXFromY * (stats.SumY / n));
            Vector2 h0 = new Vector2(stats.MinX, stats.MinX * slopeYFromX + interceptY);
            Vector2 h1 = new Vector2(stats.MaxX, stats.MaxX * slopeYFromX + interceptY);
            Vector2 v0 = new Vector2(stats.MinY * slopeXFromY + interceptX, stats.MinY);
            Vector2 v1 = new Vector2(stats.MaxY * slopeXFromY + interceptX, stats.MaxY);
            Vector2 target = new Vector2((float)(stats.SumX / n), (float)(stats.SumY / n));
            horizontal = FindBestParallelSegment(h0, h1, bounds, mask, target);
            vertical = FindBestParallelSegment(v0, v1, bounds, mask, target);
            return horizontal.PixelCount > 0 || vertical.PixelCount > 0;
        }

        private static LineSegment FindBestParallelSegment(
            Vector2 start,
            Vector2 end,
            RectInt bounds,
            RegionMask mask,
            Vector2 target)
        {
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude == 0.0f) return default;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            LineSegment best = default;
            for (int test = -NumberOfLineTests; test <= NumberOfLineTests; test++)
            {
                Vector2 offset = perpendicular * (test * TestLineSpacing);
                if (!ClipLineToRect(start + offset, end + offset, bounds, out Vector2 clippedStart, out Vector2 clippedEnd))
                    continue;
                LineSegment candidate = FindTargetInsideRun(clippedStart, clippedEnd, mask, target);
                if (candidate.PixelCount > best.PixelCount)
                    best = candidate;
            }
            return best;
        }

        private static LineSegment FindTargetInsideRun(Vector2 start, Vector2 end, RegionMask mask, Vector2 target)
        {
            List<Vector2Int> pixels = RasterizeLine(Mathf.RoundToInt(start.x), Mathf.RoundToInt(start.y), Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y));
            int bestStart = -1;
            int bestCount = 0;
            float bestDistanceSquared = float.PositiveInfinity;
            int currentStart = -1;
            for (int i = 0; i <= pixels.Count; i++)
            {
                bool inside = i < pixels.Count && mask.ContainsLine(pixels[i].x, pixels[i].y);
                if (inside && currentStart < 0) currentStart = i;
                if (!inside && currentStart >= 0)
                {
                    int count = i - currentStart;
                    float distanceSquared = DistanceToRunSquared(pixels, currentStart, count, target);
                    if (distanceSquared < bestDistanceSquared ||
                        (Mathf.Approximately(distanceSquared, bestDistanceSquared) && count > bestCount))
                    {
                        bestStart = currentStart;
                        bestCount = count;
                        bestDistanceSquared = distanceSquared;
                    }
                    currentStart = -1;
                }
            }
            if (bestCount == 0) return default;
            return new LineSegment { Start = pixels[bestStart], End = pixels[bestStart + bestCount - 1], PixelCount = bestCount };
        }

        private static float DistanceToRunSquared(List<Vector2Int> pixels, int start, int count, Vector2 target)
        {
            float result = float.PositiveInfinity;
            int end = start + count;
            for (int index = start; index < end; index++)
            {
                result = Mathf.Min(result, ((Vector2)pixels[index] - target).sqrMagnitude);
            }
            return result;
        }

        private static List<Vector2Int> RasterizeLine(int x0, int y0, int x1, int y1)
        {
            List<Vector2Int> result = new List<Vector2Int>(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1);
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                result.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int doubled = error << 1;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
            return result;
        }

        private static bool ClipLineToRect(Vector2 start, Vector2 end, RectInt bounds, out Vector2 clippedStart, out Vector2 clippedEnd)
        {
            Vector2 direction = end - start;
            float t0 = float.NegativeInfinity;
            float t1 = float.PositiveInfinity;
            if (!ClipAxis(start.x, direction.x, bounds.xMin, bounds.xMax - 1, ref t0, ref t1) ||
                !ClipAxis(start.y, direction.y, bounds.yMin, bounds.yMax - 1, ref t0, ref t1) || t0 > t1)
            {
                clippedStart = clippedEnd = default;
                return false;
            }
            clippedStart = start + direction * t0;
            clippedEnd = start + direction * t1;
            return true;
        }

        private static bool ClipAxis(float origin, float direction, float minimum, float maximum, ref float t0, ref float t1)
        {
            if (Mathf.Abs(direction) < 1e-6f)
                return origin >= minimum && origin <= maximum;
            float a = (minimum - origin) / direction;
            float b = (maximum - origin) / direction;
            if (a > b) (a, b) = (b, a);
            t0 = Mathf.Max(t0, a);
            t1 = Mathf.Min(t1, b);
            return t0 <= t1;
        }

        private static bool TryBuildGlyphLayout(string text, TMP_FontAsset font, out List<GlyphLayout> glyphs, out float width, out float height)
        {
            glyphs = new List<GlyphLayout>(text.Length);
            width = 0.0f;
            float cursor = 0.0f;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (char value in text)
            {
                if (!font.characterLookupTable.TryGetValue(value, out TMP_Character character) || character.glyph == null)
                    continue;
                GlyphMetrics metrics = character.glyph.metrics;
                float left = cursor + metrics.horizontalBearingX;
                float right = left + metrics.width;
                glyphs.Add(new GlyphLayout
                {
                    Character = character,
                    Left = left,
                    Right = right,
                    Bottom = metrics.horizontalBearingY - metrics.height,
                    Top = metrics.horizontalBearingY
                });
                minimum = Mathf.Min(minimum, metrics.horizontalBearingY - metrics.height);
                maximum = Mathf.Max(maximum, metrics.horizontalBearingY);
                if (metrics.width > 0.0f)
                {
                    minimumX = Mathf.Min(minimumX, left);
                    maximumX = Mathf.Max(maximumX, right);
                }
                cursor += metrics.horizontalAdvance;
            }
            width = maximumX - minimumX;
            height = maximum - minimum;
            if (glyphs.Count == 0 || width <= 0.0f || height <= 0.0f) return false;
            float horizontalCenter = (minimumX + maximumX) * 0.5f;
            float verticalCenter = (minimum + maximum) * 0.5f;
            for (int i = 0; i < glyphs.Count; i++)
            {
                GlyphLayout layout = glyphs[i];
                layout.Left -= horizontalCenter;
                layout.Right -= horizontalCenter;
                layout.Bottom -= verticalCenter;
                layout.Top -= verticalCenter;
                glyphs[i] = layout;
            }
            return true;
        }

        private static Vector2[] BuildFiveControlPoints(
            List<GlyphLayout> glyphs,
            float extraSpacing,
            float scale,
            Vector2 center,
            Vector2 direction,
            RectInt bounds,
            RegionMask mask)
        {
            List<Vector2> centers = new List<Vector2>(glyphs.Count);
            List<Vector2> basePoints = new List<Vector2>(glyphs.Count);
            float spacingCenterOffset = extraSpacing * Math.Max(0, glyphs.Count - 1) * 0.5f;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            for (int index = 0; index < glyphs.Count; index++)
            {
                GlyphLayout glyph = glyphs[index];
                float glyphCenter = (glyph.Left + glyph.Right) * 0.5f +
                                    index * extraSpacing - spacingCenterOffset;
                Vector2 basePoint = center + direction * (glyphCenter * scale);
                basePoints.Add(basePoint);
                if (TryFindRegionCrossSection(basePoint, normal, bounds, mask, out Vector2 sectionCenter))
                    centers.Add(sectionCenter);
                else
                    centers.Add(basePoint);
            }
            Vector2 average = Vector2.zero;
            foreach (Vector2 point in centers) average += point;
            average /= centers.Count;
            Vector2 first = (basePoints[0] + centers[0]) * 0.5f;
            Vector2 last = (basePoints[basePoints.Count - 1] + centers[centers.Count - 1]) * 0.5f;
            Vector2 before = ExtrapolateControl(first, average, last);
            Vector2 after = ExtrapolateControl(last, average, first);
            return new[] { before, first, average, last, after };
        }

        private static Vector2[] BuildStraightControls(Vector2 center, Vector2 direction, float width)
        {
            Vector2 half = direction * (width * 0.5f);
            Vector2 first = center - half;
            Vector2 last = center + half;
            return new[] { first - half, first, center, last, last + half };
        }

        private static Vector2 ExtrapolateControl(Vector2 edge, Vector2 middle, Vector2 opposite)
        {
            Vector2 edgeVector = edge - middle;
            Vector2 oppositeVector = middle - opposite;
            if (edgeVector.sqrMagnitude <= 1e-8f || oppositeVector.sqrMagnitude <= 1e-8f)
                return edge + edgeVector;
            Vector2 edgeNormal = edgeVector.normalized;
            Vector2 oppositeNormal = oppositeVector.normalized;
            float dot = Vector2.Dot(oppositeNormal, edgeVector);
            float cross = oppositeNormal.x * edgeVector.y - oppositeNormal.y * edgeVector.x;
            Vector2 extension = new Vector2(edgeNormal.x * dot - edgeNormal.y * cross, edgeNormal.x * cross + edgeNormal.y * dot);
            return edge + extension;
        }

        private static bool TryFindRegionCrossSection(Vector2 point, Vector2 normal, RectInt bounds, RegionMask mask, out Vector2 sectionCenter)
        {
            float reach = Mathf.Sqrt(bounds.width * bounds.width + bounds.height * bounds.height);
            if (!ClipLineToRect(point - normal * reach, point + normal * reach, bounds, out Vector2 start, out Vector2 end))
            {
                sectionCenter = point;
                return false;
            }
            List<Vector2Int> pixels = RasterizeLine(Mathf.RoundToInt(start.x), Mathf.RoundToInt(start.y), Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y));
            int chosenStart = -1;
            int chosenEnd = -1;
            float chosenDistance = float.PositiveInfinity;
            int runStart = -1;
            for (int i = 0; i <= pixels.Count; i++)
            {
                bool inside = i < pixels.Count && mask.ContainsLine(pixels[i].x, pixels[i].y);
                if (inside && runStart < 0) runStart = i;
                if (!inside && runStart >= 0)
                {
                    int runEnd = i - 1;
                    Vector2 candidate = ((Vector2)pixels[runStart] + pixels[runEnd]) * 0.5f;
                    float distance = (candidate - point).sqrMagnitude;
                    if (distance < chosenDistance)
                    {
                        chosenDistance = distance;
                        chosenStart = runStart;
                        chosenEnd = runEnd;
                    }
                    runStart = -1;
                }
            }
            if (chosenStart < 0)
            {
                sectionCenter = point;
                return false;
            }
            sectionCenter = ((Vector2)pixels[chosenStart] + pixels[chosenEnd]) * 0.5f;
            return true;
        }

        private static void AddCurvedVertex(
            float localX,
            float localY,
            float textWidth,
            float scale,
            Vector2 lineCenter,
            Vector2 lineDirection,
            Vector2 lineNormal,
            Vector2[] controls,
            CK3TerrainHeightData heightData,
            Vector2 uv,
            List<Vector3> vertices,
            List<Vector2> uvs)
        {
            float t = Mathf.Clamp01(localX / textWidth + 0.5f);
            EvaluateCatmullRom(controls, t, out Vector2 baseline, out Vector2 tangent);
            Vector2 normal = tangent.sqrMagnitude > 1e-8f ? new Vector2(-tangent.y, tangent.x).normalized : lineNormal;
            Vector2 position = baseline + normal * (localY * scale);
            // The original map-name pass disables depth testing. Its CPU-generated Position is
            // therefore the sampled terrain position itself; an extra carrier height creates
            // visible parallax while the strategic camera pans.
            float height = SampleOriginalHeight(heightData, position);
            vertices.Add(new Vector3(position.x, height, position.y));
            uvs.Add(uv);
        }

        private static void EvaluateCatmullRom(Vector2[] points, float t, out Vector2 position, out Vector2 tangent)
        {
            int segment = t < 0.5f ? 0 : 1;
            float u = segment == 0 ? t * 2.0f : (t - 0.5f) * 2.0f;
            Vector2 p0 = points[segment];
            Vector2 p1 = points[segment + 1];
            Vector2 p2 = points[segment + 2];
            Vector2 p3 = points[segment + 3];
            float u2 = u * u;
            float u3 = u2 * u;
            float w0 = 0.5f * (-u3 + 2.0f * u2 - u);
            float w1 = 0.5f * (3.0f * u3 - 5.0f * u2 + 2.0f);
            float w2 = 0.5f * (-3.0f * u3 + 4.0f * u2 + u);
            float w3 = 0.5f * (u3 - u2);
            position = p0 * w0 + p1 * w1 + p2 * w2 + p3 * w3;
            float d0 = 0.5f * (-3.0f * u2 + 4.0f * u - 1.0f);
            float d1 = 0.5f * (9.0f * u2 - 10.0f * u);
            float d2 = 0.5f * (-9.0f * u2 + 8.0f * u + 1.0f);
            float d3 = 0.5f * (3.0f * u2 - 2.0f * u);
            tangent = p0 * d0 + p1 * d1 + p2 * d2 + p3 * d3;
        }

        private static float SampleOriginalHeight(CK3TerrainHeightData data, Vector2 world)
        {
            Texture2D lookup = data.IndirectionTexture;
            Texture2D packed = data.PackedHeightTexture;
            if (lookup == null || packed == null || !lookup.isReadable || !packed.isReadable)
                return data.HeightScale;
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

        private static int[] BuildProvinceRaster(CK3PoliticalDataBuilder.RgbPng image, CK3ProvinceDatabase database)
        {
            int[] byColor = new int[1 << 24];
            Array.Fill(byColor, -1);
            foreach (CK3ProvinceDefinition definition in database.Definitions)
            {
                Color32 color = definition.Color;
                byColor[color.r | color.g << 8 | color.b << 16] = definition.Id;
            }
            int[] result = new int[image.Width * image.Height];
            byte[] pixels = image.UnityRgbPixels;
            for (int i = 0, source = 0; i < result.Length; i++, source += 3)
                result[i] = byColor[pixels[source] | pixels[source + 1] << 8 | pixels[source + 2] << 16];
            return result;
        }

        private static Texture2D BuildRedChannelFontTexture(TMP_FontAsset font)
        {
            Texture2D source = font.atlasTexture;
            if (source == null || !source.isReadable)
                throw new InvalidOperationException("MapFont SDF 图集不可读，请先重新构建名称源与字体图集。");
            Color32[] sourcePixels = source.GetPixels32();
            bool alphaContainsDistance = sourcePixels.Any(pixel => pixel.a != 255);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FontTexturePath);
            if (texture == null || texture.width != source.width || texture.height != source.height || texture.format != TextureFormat.R8)
            {
                if (texture != null) AssetDatabase.DeleteAsset(FontTexturePath);
                texture = new Texture2D(source.width, source.height, TextureFormat.R8, false, true) { name = "CK3 原版 MapFont R 通道 SDF" };
                AssetDatabase.CreateAsset(texture, FontTexturePath);
            }
            byte[] red = new byte[sourcePixels.Length];
            for (int i = 0; i < red.Length; i++) red[i] = alphaContainsDistance ? sourcePixels[i].a : sourcePixels[i].r;
            texture.SetPixelData(red, 0);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Mesh LoadOrCreateMesh(string path, string meshName)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = meshName };
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material LoadOrCreateMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CK3 原版地图名称材质" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;
            return material;
        }

        private static void BindScene(Mesh mesh, Material material, string sceneObjectName, bool actualRealm)
        {
            GameObject root = GameObject.Find(CK3TerrainWorldBuilder.RootName);
            if (root == null) throw new InvalidOperationException("场景中缺少 CK3 原版完整世界地形根对象。");
            Transform existing = root.transform.Find(sceneObjectName);
            GameObject target = existing != null ? existing.gameObject : new GameObject(sceneObjectName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(target, "创建 CK3 原版曲线地图名称");
                target.transform.SetParent(root.transform, false);
            }
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null)
                filter = target.AddComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = target.AddComponent<MeshRenderer>();
            if (filter == null || renderer == null)
                throw new InvalidOperationException($"无法给场景对象 {sceneObjectName} 创建 MeshFilter/MeshRenderer。");
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            target.SetActive(true);
            CK3MapModeController controller = root.GetComponent<CK3MapModeController>();
            if (controller != null)
            {
                if (actualRealm) controller.ConfigureActualRealmMapNames(target);
                else controller.ConfigureMapNames(target);
            }
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static string ToAbsolute(string assetPath)
        {
            string root = System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("无法确定 Unity 项目根目录。");
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(root, assetPath));
        }
    }
}
