using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CK3Map.Editor
{
    public readonly struct CK3WaterSurfaceBuildResult
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly Texture2D WaterColorTexture;

        public CK3WaterSurfaceBuildResult(Mesh mesh, Material material, Texture2D texture)
        {
            Mesh = mesh;
            Material = material;
            WaterColorTexture = texture;
        }
    }

    public static class CK3WaterSurfaceBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Water";
        public const string MeshPath = OutputRoot + "/CK3原版完整世界海面网格.asset";
        public const string MaterialPath = OutputRoot + "/CK3原版低规格水面.mat";
        public const string WaterColorPath = OutputRoot + "/CK3原版水色与高光_BC7.asset";
        public const string SceneObjectName = "CK3原版完整世界海面";

        private const string WaterColorSourcePath =
            "Assets/CK3Map/Data/Source/game/gfx/map/water/watercolor_rgb_waterspec_a.dds";
        private const string HeightDataPath =
            "Assets/CK3Map/Data/Generated/Terrain/CK3原版高度页表.asset";
        private const string ShaderName = "CK3Map/Water Surface Low Spec";
        private const uint DdsMagic = 0x20534444;
        private const uint DdsHeaderSize = 124;
        private const uint Dx10FourCc = 0x30315844;
        private const uint DxgiBc7Srgb = 98;
        private const int ExpectedWidth = 4608;
        private const int ExpectedHeight = 2304;
        private const int ExpectedMipCount = 13;
        private const float WorldExtentX = 9215.0f;
        private const float WorldExtentZ = 4607.0f;
        private const float OriginalWaterLevel = 3.0f;

        public static CK3WaterSurfaceBuildResult BuildAndBind()
        {
            EnsureAssetFolder(OutputRoot);
            CK3TerrainHeightData heightData =
                AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(HeightDataPath);
            if (heightData == null)
            {
                throw new InvalidOperationException("缺少原版高度页表，请先完成阶段 3。");
            }

            Texture2D waterColor = BuildWaterColorTexture();
            Mesh mesh = BuildOceanMesh();
            Material material = BuildMaterial(waterColor, heightData);
            BindScene(mesh, material);
            AssetDatabase.SaveAssets();
            return new CK3WaterSurfaceBuildResult(mesh, material, waterColor);
        }

        private static Texture2D BuildWaterColorTexture()
        {
            string absolutePath = ToAbsolutePath(WaterColorSourcePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("缺少 CK3 原版水色纹理。", WaterColorSourcePath);
            }

            using FileStream stream = File.OpenRead(absolutePath);
            using BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadUInt32() != DdsMagic || reader.ReadUInt32() != DdsHeaderSize)
            {
                throw new InvalidDataException("原版水色纹理不是预期 DDS 文件。");
            }

            reader.ReadUInt32();
            int height = reader.ReadInt32();
            int width = reader.ReadInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            int mipCount = reader.ReadInt32();
            stream.Position = 84;
            uint fourCc = reader.ReadUInt32();
            stream.Position = 128;
            uint dxgiFormat = reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            if (width != ExpectedWidth || height != ExpectedHeight ||
                mipCount != ExpectedMipCount || fourCc != Dx10FourCc || dxgiFormat != DxgiBc7Srgb)
            {
                throw new InvalidDataException(
                    $"原版水色 DDS 不匹配：{width}×{height}，Mip {mipCount}，" +
                    $"FourCC 0x{fourCc:X8}，DXGI {dxgiFormat}。");
            }

            DeleteAssetIfPresent(WaterColorPath);
            Texture2D texture = new Texture2D(width, height, TextureFormat.BC7, true, false)
            {
                name = "CK3 原版水色与高光（BC7 sRGB）",
                filterMode = FilterMode.Trilinear,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Repeat,
                anisoLevel = 1
            };

            try
            {
                for (int mip = 0, mipWidth = width, mipHeight = height;
                     mip < mipCount;
                     mip++, mipWidth = Math.Max(1, mipWidth / 2), mipHeight = Math.Max(1, mipHeight / 2))
                {
                    int blockWidth = Math.Max(1, (mipWidth + 3) / 4);
                    int blockHeight = Math.Max(1, (mipHeight + 3) / 4);
                    int byteCount = checked(blockWidth * blockHeight * 16);
                    byte[] bytes = reader.ReadBytes(byteCount);
                    if (bytes.Length != byteCount)
                    {
                        throw new EndOfStreamException($"水色 DDS 的 Mip {mip} 数据不完整。");
                    }

                    texture.SetPixelData(bytes, mip, 0);
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("原版水色 DDS 存在未登记的尾部数据。");
                }

                texture.Apply(false, true);
                AssetDatabase.CreateAsset(texture, WaterColorPath);
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static Mesh BuildOceanMesh()
        {
            DeleteAssetIfPresent(MeshPath);
            Mesh mesh = new Mesh
            {
                name = "CK3 原版完整世界四角海面"
            };
            mesh.vertices = new[]
            {
                new Vector3(0.0f, OriginalWaterLevel, 0.0f),
                new Vector3(0.0f, OriginalWaterLevel, WorldExtentZ),
                new Vector3(WorldExtentX, OriginalWaterLevel, 0.0f),
                new Vector3(WorldExtentX, OriginalWaterLevel, WorldExtentZ)
            };
            // Original order is a four-vertex triangle strip. Unity MeshTopology has no strip,
            // so the identical corners are expanded into the strip's two triangles.
            mesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            // The shader fixes Y to WATERLEVEL=3. Keep a conservative non-zero Unity culling
            // thickness around that real position so the enormous horizontal plane is not
            // discarded at grazing camera angles because of a zero-thickness AABB.
            mesh.bounds = new Bounds(
                new Vector3(WorldExtentX * 0.5f, OriginalWaterLevel, WorldExtentZ * 0.5f),
                new Vector3(WorldExtentX, 2.0f, WorldExtentZ));
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private static Material BuildMaterial(Texture2D waterColor, CK3TerrainHeightData heightData)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"找不到水面 Shader：{ShaderName}。");
            }

            DeleteAssetIfPresent(MaterialPath);
            Material material = new Material(shader)
            {
                name = "CK3 原版低规格水面",
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetTexture("_WaterColorTexture", waterColor);
            material.SetTexture("_CK3HeightLookupTexture", heightData.IndirectionTexture);
            material.SetTexture("_CK3PackedHeightTexture", heightData.PackedHeightTexture);
            material.SetFloat("_WaterHeight", OriginalWaterLevel);
            material.SetFloat("_FlatMapLerp", 0.0f);
            material.SetFloat("_FlatMapHeight", 3.92f);
            material.SetFloat("_UnityFlatMapWaterOffset", 0.02f);
            material.SetFloat("_WaterFadeShoreMaskDepth", 0.5f);
            material.SetFloat("_WaterFadeShoreMaskSharpness", 5.0f);
            material.SetVector("_CK3WorldSpaceToLookup", heightData.WorldSpaceToLookup);
            material.SetVector(
                "_CK3OriginalHeightmapToWorldSpace",
                heightData.OriginalHeightmapToWorldSpace);
            material.SetVector(
                "_CK3IndirectionSize",
                new Vector4(heightData.IndirectionSize.x, heightData.IndirectionSize.y, 0.0f, 0.0f));
            material.SetFloat("_CK3BaseTileSize", heightData.BaseTileSize);
            material.SetFloat("_CK3HeightScale", heightData.HeightScale);
            material.SetVector("_CK3WorldExtents", heightData.WorldExtents);
            Vector4[] constants = heightData.TileToHeightmapScaleAndOffset;
            for (int index = 0; index < constants.Length; index++)
            {
                material.SetVector($"_CK3TileToHeightMap{index}", constants[index]);
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void BindScene(Mesh mesh, Material material)
        {
            GameObject previous = GameObject.Find(SceneObjectName);
            if (previous != null)
            {
                Undo.DestroyObjectImmediate(previous);
            }

            GameObject water = new GameObject(SceneObjectName);
            Undo.RegisterCreatedObjectUndo(water, "构建 CK3 原版完整世界海面");
            water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            water.transform.localScale = Vector3.one;
            MeshFilter filter = Undo.AddComponent<MeshFilter>(water);
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(water);
            CK3WaterSurfaceBinding binding = Undo.AddComponent<CK3WaterSurfaceBinding>(water);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            binding.Configure(material);
            CK3MapModeController controller = UnityEngine.Object.FindObjectOfType<CK3MapModeController>();
            if (controller != null)
            {
                controller.ConfigureWater(material);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
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
                                 throw new InvalidOperationException("无法定位 Unity 项目目录。");
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
