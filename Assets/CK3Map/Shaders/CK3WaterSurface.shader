Shader "CK3Map/Water Surface Low Spec"
{
    Properties
    {
        [NoScaleOffset] _WaterColorTexture("原版水色与高光图", 2D) = "black" {}
        [NoScaleOffset] _FlatMapTexture("原版手绘平面地图", 2D) = "gray" {}
        [NoScaleOffset] _PaperTearMask("原版撕纸过渡遮罩", 2D) = "white" {}
        [NoScaleOffset] _CK3HeightLookupTexture("原版高度间接寻址图", 2D) = "black" {}
        [NoScaleOffset] _CK3PackedHeightTexture("原版压缩高度图", 2D) = "black" {}
        _WaterHeight("海平面高度", Float) = 3
        _FlatMapLerp("原版平面地图过渡", Range(0, 1)) = 0
        _FlatMapHeight("原版平面地图高度", Float) = 3.92
        _UnityFlatMapWaterOffset("Unity 平面水面抬升", Range(0, 0.2)) = 0.02
        _WaterFadeShoreMaskDepth("岸边淡出深度", Float) = 0.5
        _WaterFadeShoreMaskSharpness("岸边淡出锐度", Float) = 5
        _CK3WorldSpaceToLookup("世界坐标到页表坐标", Vector) = (0.0001085069, 0.0002170139, 0, 0)
        _CK3OriginalHeightmapToWorldSpace("原始高度像素到世界坐标", Vector) = (0.5, 0.5, 0, 0)
        _CK3IndirectionSize("原版间接寻址图尺寸", Vector) = (288, 144, 0, 0)
        _CK3BaseTileSize("原版高度瓦片尺寸", Float) = 65
        _CK3HeightScale("原版高度缩放", Float) = 50
        _CK3WorldExtents("原版世界范围", Vector) = (9215, 4607, 0, 0)
        _CK3TileToHeightMap0("原版高度页常量 0", Vector) = (0,0,0,0)
        _CK3TileToHeightMap1("原版高度页常量 1", Vector) = (0,0,0,0)
        _CK3TileToHeightMap2("原版高度页常量 2", Vector) = (0,0,0,0)
        _CK3TileToHeightMap3("原版高度页常量 3", Vector) = (0,0,0,0)
        _CK3TileToHeightMap4("原版高度页常量 4", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "CK3WaterLowSpec"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            // CK3 stores DepthBias=-100 in its native raster state. ShaderLab units are not
            // numerically interchangeable with that value. Unity 2022.3 documents that the
            // factor term is required for polygons whose Z slope changes with the camera;
            // Offset -1,-1 is Unity's documented coplanar-surface form.
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertexMain
            #pragma fragment FragmentMain

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WaterColorTexture);
            SAMPLER(sampler_WaterColorTexture);
            TEXTURE2D(_FlatMapTexture);
            SAMPLER(sampler_FlatMapTexture);
            TEXTURE2D(_PaperTearMask);
            SAMPLER(sampler_PaperTearMask);
            TEXTURE2D(_CK3HeightLookupTexture);
            SAMPLER(sampler_CK3HeightLookupTexture);
            TEXTURE2D(_CK3PackedHeightTexture);
            SAMPLER(sampler_CK3PackedHeightTexture);

            CBUFFER_START(UnityPerMaterial)
                float _WaterHeight;
                float _FlatMapLerp;
                float _FlatMapHeight;
                float _UnityFlatMapWaterOffset;
                float _WaterFadeShoreMaskDepth;
                float _WaterFadeShoreMaskSharpness;
                float4 _CK3WorldSpaceToLookup;
                float4 _CK3OriginalHeightmapToWorldSpace;
                float4 _CK3IndirectionSize;
                float _CK3BaseTileSize;
                float _CK3HeightScale;
                float4 _CK3WorldExtents;
                float4 _CK3TileToHeightMap0;
                float4 _CK3TileToHeightMap1;
                float4 _CK3TileToHeightMap2;
                float4 _CK3TileToHeightMap3;
                float4 _CK3TileToHeightMap4;
            CBUFFER_END

            struct VertexInput
            {
                float3 positionOS : POSITION;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv01 : TEXCOORD1;
            };

            float4 CK3GetTileConstant(int level)
            {
                if (level == 0) return _CK3TileToHeightMap0;
                if (level == 1) return _CK3TileToHeightMap1;
                if (level == 2) return _CK3TileToHeightMap2;
                if (level == 3) return _CK3TileToHeightMap3;
                return _CK3TileToHeightMap4;
            }

            float CK3GetHeight01(float2 worldSpacePositionXZ)
            {
                float2 lookupCoordinates = clamp(
                    worldSpacePositionXZ * _CK3WorldSpaceToLookup.xy,
                    float2(0.0, 0.0),
                    float2(0.999999, 0.999999));
                float2 lookupUV = (floor(lookupCoordinates * _CK3IndirectionSize.xy) + 0.5)
                    / _CK3IndirectionSize.xy;
                float4 indirection = SAMPLE_TEXTURE2D_LOD(
                    _CK3HeightLookupTexture,
                    sampler_CK3HeightLookupTexture,
                    lookupUV,
                    0.0) * 255.0;

                float currentTileSize = (_CK3BaseTileSize - 1.0) / indirection.z + 1.0;
                float currentTileOffset = 0.5 / currentTileSize;
                float currentTileScale = (currentTileSize - 1.0) / currentTileSize;
                float2 withinTile = frac(lookupCoordinates * _CK3IndirectionSize.xy);
                float2 tileUV = indirection.rg + currentTileOffset + withinTile * currentTileScale;
                float4 tileConstant = CK3GetTileConstant((int)round(indirection.a));
                float2 packedUV = tileUV * tileConstant.xy + tileConstant.zw;
                return SAMPLE_TEXTURE2D_LOD(
                    _CK3PackedHeightTexture,
                    sampler_CK3PackedHeightTexture,
                    packedUV,
                    0.0).r;
            }

            float CK3GetHeightMultisample(float2 worldSpacePositionXZ, float filterSize)
            {
                float2 offset = filterSize * _CK3OriginalHeightmapToWorldSpace.xy;
                float height = CK3GetHeight01(worldSpacePositionXZ);
                height += CK3GetHeight01(worldSpacePositionXZ + float2(-offset.x, 0.0));
                height += CK3GetHeight01(worldSpacePositionXZ + float2( offset.x, 0.0));
                height += CK3GetHeight01(worldSpacePositionXZ + float2(0.0, -offset.y));
                height += CK3GetHeight01(worldSpacePositionXZ + float2(0.0,  offset.y));
                height += CK3GetHeight01(worldSpacePositionXZ + float2(-offset.x, -offset.y));
                height += CK3GetHeight01(worldSpacePositionXZ + float2( offset.x, -offset.y));
                height += CK3GetHeight01(worldSpacePositionXZ + float2( offset.x,  offset.y));
                height += CK3GetHeight01(worldSpacePositionXZ + float2(-offset.x,  offset.y));
                return height * (_CK3HeightScale / 9.0);
            }

            VertexOutput VertexMain(VertexInput input)
            {
                VertexOutput output;
                // CK3 keeps the native water plane at WATERLEVEL=3 and pulls it forward with
                // RasterizerState DepthBias=-100. ShaderLab Offset has no evidence-backed
                // numerical conversion for that native integer bias. During CK3's flat-map
                // transition, keep the Unity carrier immediately above the identically flattened
                // terrain instead of letting camera angle/depth precision decide visibility.
                float flatWaterHeight = _FlatMapHeight + _UnityFlatMapWaterOffset;
                float renderedWaterHeight = lerp(_WaterHeight, flatWaterHeight, _FlatMapLerp);
                output.positionWS = float3(input.positionOS.x, renderedWaterHeight, input.positionOS.z);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv01 = float2(
                    output.positionWS.x / _CK3WorldExtents.x,
                    1.0 - output.positionWS.z / _CK3WorldExtents.y);
                return output;
            }

            half4 FragmentMain(VertexOutput input) : SV_Target
            {
                float terrainHeight = CK3GetHeightMultisample(input.positionWS.xz, 0.65);
                // Depth remains CK3's physical WATERLEVEL minus the unflattened terrain.
                // The rendered Y compensation above exists only to carry native DepthBias
                // behavior into Unity's flat-map depth ordering.
                float depth = _WaterHeight - terrainHeight;
                float waterFade = 1.0 - saturate(
                    (_WaterFadeShoreMaskDepth - depth) * _WaterFadeShoreMaskSharpness);
                half3 waterColor = SAMPLE_TEXTURE2D(
                    _WaterColorTexture,
                    sampler_WaterColorTexture,
                    input.uv01).rgb;
                float paperBlend;
                if (_FlatMapLerp >= 1.0) paperBlend = 1.0;
                else if (_FlatMapLerp <= 0.001) paperBlend = 0.0;
                else
                {
                    float mainMask = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, input.uv01 * float2(12.0, 6.0)).r;
                    float variation = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, input.uv01 * float2(16.0, 8.0)).g;
                    float largeScale = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, input.uv01).b;
                    float maskValue = mainMask * 0.5 + largeScale * 0.5;
                    float threshold = 1.2 - _FlatMapLerp * 1.4 + (largeScale - 0.5) * 0.3;
                    float blendBase = threshold + variation;
                    paperBlend = smoothstep(blendBase - 0.35, blendBase + 0.35, maskValue);
                }
                half3 flatMapColor = SAMPLE_TEXTURE2D(
                    _FlatMapTexture, sampler_FlatMapTexture, input.uv01).rgb;
                waterColor = lerp(waterColor, flatMapColor, paperBlend);
                return half4(waterColor, waterFade);
            }
            ENDHLSL
        }
    }
}
