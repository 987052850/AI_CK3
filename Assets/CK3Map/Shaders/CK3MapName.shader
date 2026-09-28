Shader "CK3Map/Map Name"
{
    Properties
    {
        [NoScaleOffset] _FontAtlas("原版字体距离场（R 通道）", 2D) = "white" {}
        [NoScaleOffset] _MapNameOverlayTexture("原版粗糙覆盖纹理", 2D) = "gray" {}
        _Transparency("整体透明度", Range(0, 1)) = 1
        _LodFactor("原版远距模糊系数", Float) = 0.05
        _ThicknessBias("原版字重偏移", Float) = 0.015
        _FlatMapLerp("原版平面地图过渡", Range(0, 1)) = 1
        _FlatMapHeight("原版平面地图高度", Float) = 3.92
        [HideInInspector] _CK3HeightLookupTexture("原版高度间接寻址图", 2D) = "black" {}
        [HideInInspector] _CK3PackedHeightTexture("原版压缩高度图", 2D) = "black" {}
        [HideInInspector] _CK3WorldSpaceToLookup("世界坐标到页表", Vector) = (0,0,0,0)
        [HideInInspector] _CK3IndirectionSize("高度页表尺寸", Vector) = (0,0,0,0)
        [HideInInspector] _CK3BaseTileSize("高度基础瓦片尺寸", Float) = 65
        [HideInInspector] _CK3HeightScale("地形高度缩放", Float) = 50
        [HideInInspector] _CK3TerrainVerticalOffset("地形整体高度偏移", Float) = 0
        [HideInInspector] _CK3TileToHeightMap0("高度页常量0", Vector) = (0,0,0,0)
        [HideInInspector] _CK3TileToHeightMap1("高度页常量1", Vector) = (0,0,0,0)
        [HideInInspector] _CK3TileToHeightMap2("高度页常量2", Vector) = (0,0,0,0)
        [HideInInspector] _CK3TileToHeightMap3("高度页常量3", Vector) = (0,0,0,0)
        [HideInInspector] _CK3TileToHeightMap4("高度页常量4", Vector) = (0,0,0,0)
        [HideInInspector] _TextureSize("字体图集尺寸", Vector) = (1024, 1024, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CK3MapNameFlatMap"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex VertexMain
            #pragma fragment FragmentMain
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FontAtlas);
            SAMPLER(sampler_FontAtlas);
            TEXTURE2D(_MapNameOverlayTexture);
            SAMPLER(sampler_MapNameOverlayTexture);
            TEXTURE2D(_CK3HeightLookupTexture);
            SAMPLER(sampler_CK3HeightLookupTexture);
            TEXTURE2D(_CK3PackedHeightTexture);
            SAMPLER(sampler_CK3PackedHeightTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _TextureSize;
                float _Transparency;
                float _LodFactor;
                float _ThicknessBias;
                float _FlatMapLerp;
                float _FlatMapHeight;
                float4 _CK3WorldSpaceToLookup;
                float4 _CK3IndirectionSize;
                float _CK3BaseTileSize;
                float _CK3HeightScale;
                float _CK3TerrainVerticalOffset;
                float4 _CK3TileToHeightMap0;
                float4 _CK3TileToHeightMap1;
                float4 _CK3TileToHeightMap2;
                float4 _CK3TileToHeightMap3;
                float4 _CK3TileToHeightMap4;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 CK3GetTileConstant(int level)
            {
                if (level == 0) return _CK3TileToHeightMap0;
                if (level == 1) return _CK3TileToHeightMap1;
                if (level == 2) return _CK3TileToHeightMap2;
                if (level == 3) return _CK3TileToHeightMap3;
                return _CK3TileToHeightMap4;
            }

            float CK3GetHeight(float2 worldSpacePositionXZ)
            {
                // Exact same height-page lookup used by CK3TerrainSurface.shader.
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
                float2 tileUV = indirection.rg
                    + currentTileOffset
                    + withinTile * currentTileScale;
                float4 tileConstant = CK3GetTileConstant((int)round(indirection.a));
                float2 packedUV = tileUV * tileConstant.xy + tileConstant.zw;
                return SAMPLE_TEXTURE2D_LOD(
                    _CK3PackedHeightTexture,
                    sampler_CK3PackedHeightTexture,
                    packedUV,
                    0.0).r * _CK3HeightScale;
            }

            Varyings VertexMain(Attributes input)
            {
                Varyings output;
                // Direct port of MapNameVertexShader in countrynames.fxh.
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float terrainHeight = CK3GetHeight(positionWS.xz) + _CK3TerrainVerticalOffset;
                positionWS.y = lerp(terrainHeight, _FlatMapHeight, _FlatMapLerp);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float OverlayChannel(float baseValue, float blendValue)
            {
                return baseValue < 0.5
                    ? 2.0 * baseValue * blendValue
                    : 1.0 - 2.0 * (1.0 - baseValue) * (1.0 - blendValue);
            }

            float3 OverlayColor(float3 baseColor, float3 blendColor)
            {
                return float3(
                    OverlayChannel(baseColor.r, blendColor.r),
                    OverlayChannel(baseColor.g, blendColor.g),
                    OverlayChannel(baseColor.b, blendColor.b));
            }

            half4 FragmentMain(Varyings input) : SV_Target
            {
                const float3 textColorFlatMap = float3(0.006, 0.005, 0.005);
                const float3 outlineColorFlatMap = float3(0.2, 0.18, 0.18);
                const float interiorMid = 0.5;
                const float interiorSmoothing = 0.05;
                const float outlineWidth = 0.4;
                const float outlineSoftEdgeScale = 2.5;
                const float outlineAlphaScale = 0.2;

                float sampleValue = SAMPLE_TEXTURE2D(_FontAtlas, sampler_FontAtlas, input.uv).r;
                float2 textureCoordinate = input.uv * _TextureSize.xy;
                float2 dxValue = ddx(textureCoordinate);
                float2 dyValue = ddy(textureCoordinate);
                float ratio = sqrt(max(dot(dxValue, dxValue), dot(dyValue, dyValue)));
                float interiorFactor = smoothstep(interiorMid - interiorSmoothing, interiorMid, sampleValue);

                float4 interiorOverlay = SAMPLE_TEXTURE2D(
                    _MapNameOverlayTexture,
                    sampler_MapNameOverlayTexture,
                    input.uv * float2(20.0, 20.0));
                float4 outlineOverlay = SAMPLE_TEXTURE2D(
                    _MapNameOverlayTexture,
                    sampler_MapNameOverlayTexture,
                    input.uv * float2(50.0, 30.0));
                float noiseVariation = lerp(0.2, 0.8, outlineOverlay.a);
                float outlineSmoothing = outlineWidth + ratio * _LodFactor * 0.4;
                float outlineFactor = pow(smoothstep(
                    interiorMid - outlineSmoothing * noiseVariation,
                    interiorMid - interiorSmoothing,
                    sampleValue), outlineSoftEdgeScale) * outlineAlphaScale;

                float3 color = lerp(outlineColorFlatMap, textColorFlatMap, interiorFactor);
                color = lerp(color, OverlayColor(color, interiorOverlay.rgb), 0.5);
                float alpha = max(outlineFactor, interiorFactor) * _Transparency;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
