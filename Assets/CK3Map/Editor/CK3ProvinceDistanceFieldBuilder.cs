using System;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    internal static class CK3ProvinceDistanceFieldBuilder
    {
        // Recovered from CK3's jomini_gradient_borders state block. The original
        // increasing-width path runs ceil(log2(127 / 4)) passes: 16, 8, 4, 2, 1.
        private const float MaximumSearchDistance = 127.0f;
        private static readonly int[] SampleWidths = { 16, 8, 4, 2, 1 };

        public static Texture2D Build(
            Texture2D provinceIndirection,
            Texture2D provincePalette,
            string assetPath,
            string assetName,
            Color32[] wildCardColors = null)
        {
            if (provinceIndirection == null || provincePalette == null)
            {
                throw new ArgumentNullException(nameof(provinceIndirection));
            }

            Shader shader = Shader.Find("CK3Map/Province Distance Field");
            if (shader == null)
            {
                throw new InvalidOperationException("找不到 CK3Map/Province Distance Field Shader。");
            }

            int width = provinceIndirection.width / 4;
            int height = provinceIndirection.height / 4;
            Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture first = CreateDeltaTexture(width, height, "CK3 距离向量 A");
            RenderTexture second = CreateDeltaTexture(width, height, "CK3 距离向量 B");
            RenderTexture final = CreateDistanceTexture(width, height);
            RenderTexture wildCardMask = null;
            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;

            try
            {
                material.SetTexture("_ProvinceColorIndirectionTexture", provinceIndirection);
                material.SetTexture("_ProvinceColorTexture", provincePalette);
                material.SetVector("_GradientTextureSize", new Vector4(width, height, 0.0f, 0.0f));
                material.SetVector("_IndirectionMapSize", new Vector4(
                    provinceIndirection.width,
                    provinceIndirection.height,
                    0.0f,
                    0.0f));
                material.SetFloat("_MaxSearchDist", MaximumSearchDistance);

                GL.sRGBWrite = false;
                if (wildCardColors != null && wildCardColors.Length > 0)
                {
                    int count = Math.Min(4, wildCardColors.Length);
                    Vector4[] colors = new Vector4[count];
                    for (int i = 0; i < count; i++)
                        colors[i] = (Color)wildCardColors[i];
                    material.SetInt("_WildCardColorsCount", count);
                    material.SetInt("_WildCardSampleCount", 4);
                    material.SetFloat("_WildCardSampleWidth", 10.0f);
                    material.SetVectorArray("_WildCardColors", colors);
                    wildCardMask = CreateWildCardMask(width, height);
                    Graphics.Blit(null, wildCardMask, material, 0); // Original WildCardMask effect.
                    material.SetTexture("_DeltaVectors", wildCardMask);
                    Graphics.Blit(null, first, material, 2); // Original InitWithWildcards effect.
                }
                else
                {
                    Graphics.Blit(null, first, material, 1); // Original Init effect.
                }
                RenderTexture source = first;
                RenderTexture destination = second;
                foreach (int sampleWidth in SampleWidths)
                {
                    material.SetTexture("_DeltaVectors", source);
                    material.SetVector("_SampleOffset", new Vector4(
                        -sampleWidth,
                        0.0f,
                        sampleWidth,
                        0.0f));
                    Graphics.Blit(null, destination, material, 3); // Original Fill effect.
                    RenderTexture swap = source;
                    source = destination;
                    destination = swap;
                }

                material.SetTexture("_DeltaVectors", source);
                Graphics.Blit(null, final, material, 4); // Original Finalize effect.

                RenderTexture.active = final;
                Texture2D texture = new Texture2D(width, height, TextureFormat.R8, false, true)
                {
                    name = assetName,
                    filterMode = FilterMode.Bilinear,
                    wrapModeU = TextureWrapMode.Clamp,
                    wrapModeV = TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                texture.Apply(false, true);

                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                AssetDatabase.CreateAsset(texture, assetPath);
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
                first.Release();
                second.Release();
                final.Release();
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(final);
                if (wildCardMask != null)
                {
                    wildCardMask.Release();
                    UnityEngine.Object.DestroyImmediate(wildCardMask);
                }
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static RenderTexture CreateDeltaTexture(int width, int height, string name)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.RG16,
                RenderTextureReadWrite.Linear)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture CreateDistanceTexture(int width, int height)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.R8,
                RenderTextureReadWrite.Linear)
            {
                name = "CK3 边界距离场",
                filterMode = FilterMode.Bilinear,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture CreateWildCardMask(int width, int height)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.R8,
                RenderTextureReadWrite.Linear)
            {
                name = "CK3 水岸 Wildcard Mask",
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }
    }
}
