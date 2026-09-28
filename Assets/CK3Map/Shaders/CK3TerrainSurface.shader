Shader "CK3Map/Terrain Surface Pre-Lighting"
{
    Properties
    {
        [NoScaleOffset] _DetailTextures("原版漫反射与高度数组", 2DArray) = "" {}
        [NoScaleOffset] _NormalTextures("原版 RRxG 法线数组", 2DArray) = "" {}
        [NoScaleOffset] _MaterialTextures("原版材质属性数组", 2DArray) = "" {}
        [NoScaleOffset] _DetailIndexTexture("原版四层材质索引图", 2D) = "black" {}
        [NoScaleOffset] _DetailMaskTexture("原版四层材质权重图", 2D) = "black" {}
        [NoScaleOffset] _ColorTexture("原版宏观颜色图", 2D) = "gray" {}
        [NoScaleOffset] _FlatMapTexture("原版手绘平面地图", 2D) = "gray" {}
        [NoScaleOffset] _PaperTearMask("原版撕纸过渡遮罩", 2D) = "white" {}
        [NoScaleOffset] _CK3HeightLookupTexture("原版高度间接寻址图", 2D) = "black" {}
        [NoScaleOffset] _CK3PackedHeightTexture("原版压缩高度图", 2D) = "black" {}

        _DetailBlendRange("原版高度混合范围", Float) = 0.25
        _DetailTileFactor("原版默认二维平铺系数", Vector) = (0.03662507, -0.03662507, 0, 0)
        _DetailTileOffset("原版平铺起点偏移", Vector) = (0, -512, 0, 0)
        _WorldSpaceToDetail("世界坐标到控制图坐标", Vector) = (0.0001085069, 0.0002170139, 0, 0)
        _DetailTexelSize("控制图单像素尺寸", Vector) = (0.0001085069, 0.0002170139, 0, 0)
        _DetailTextureSize("控制图像素尺寸", Vector) = (9216, 4608, 0, 0)
        _WorldSpaceToTerrain01("世界坐标到地图端点坐标", Vector) = (0.0001085187, 0.0002170610, 0, 0)
        _CK3WorldSpaceToLookup("原版世界坐标到页表坐标", Vector) = (0.0001085069, 0.0002170139, 0, 0)
        _CK3IndirectionSize("原版间接寻址图尺寸", Vector) = (288, 144, 0, 0)
        _CK3PackedHeightMapSize("原版压缩高度图尺寸", Vector) = (3185, 4061, 0, 0)
        _CK3WorldExtents("原版世界范围", Vector) = (9215, 4607, 0, 0)
        _CK3BaseTileSize("原版高度瓦片尺寸", Float) = 65
        _CK3HeightScale("原版高度缩放", Float) = 50
        _CK3TerrainVerticalOffset("地形整体高度偏移", Float) = 0
        _CK3NormQuadtreeToWorld("原版四叉树到世界缩放", Float) = 16384
        _CK3SkirtSize("原版裙边高度偏移", Float) = -5
        _CK3TileToHeightMap0("原版高度页常量 0", Vector) = (0,0,0,0)
        _CK3TileToHeightMap1("原版高度页常量 1", Vector) = (0,0,0,0)
        _CK3TileToHeightMap2("原版高度页常量 2", Vector) = (0,0,0,0)
        _CK3TileToHeightMap3("原版高度页常量 3", Vector) = (0,0,0,0)
        _CK3TileToHeightMap4("原版高度页常量 4", Vector) = (0,0,0,0)
        [NoScaleOffset] _ProvinceColorIndirectionTexture("CK3 省份编号间接寻址图", 2D) = "black" {}
        [NoScaleOffset] _ProvinceColorTexture("CK3 地图模式颜色表", 2D) = "black" {}
        [NoScaleOffset] _BorderDistanceFieldTexture("CK3 原版边界距离场", 2D) = "white" {}
        _CK3ProvinceOverlayEnabled("显示 CK3 政治填色", Float) = 0
        _CK3ProvinceOverlayBlend("CK3 政治填色整体倍率", Range(0, 1)) = 1
        _CK3DistanceSampleOffset("距离场五点采样偏移", Range(0, 2)) = 0.75
        _GB_GradientAlphaInside("边界渐变内部透明度", Float) = 0.4
        _GB_GradientAlphaOutside("边界渐变外部透明度", Float) = 0.8
        _GB_GradientWidth("边界渐变宽度", Float) = 0.4
        _GB_GradientColorMul("边界渐变颜色倍率", Float) = 1
        _GB_EdgeWidth("深色边缘宽度", Float) = 0.04
        _GB_EdgeSmoothness("深色边缘锐度", Float) = 0.015
        _GB_EdgeAlpha("深色边缘透明度", Float) = 1
        _GB_EdgeColorMul("深色边缘颜色倍率", Float) = 0.65
        _GB_PreLightingBlend("光照前填色混合", Float) = 0.5
        _GB_PostLightingBlend("光照后填色混合", Float) = 0.95
        _CK3MacroColorStrength("宏观颜色影响强度", Range(0, 2)) = 1
        _CK3NormalHeightScale("原版地形法线高度倍率", Float) = 0.8
        _CK3NormalStepSize("原版地形法线采样步长", Float) = 1.6
        _CK3WaterZoomedInZoomedOutFactor("原版远近景水面因子", Range(0, 1)) = 1
        [Toggle] _CK3MapLightingEnabled("启用 CK3 地图光照", Float) = 1
        [NoScaleOffset] _CK3TerrainSunnyEnvironmentMap("原版晴天地形环境立方体", Cube) = "black" {}
        _CK3TerrainSunnySunColor("原版晴天地形太阳颜色", Color) = (1, 0.9, 0.8, 1)
        _CK3TerrainSunnySunIntensity("原版晴天地形太阳强度", Float) = 8
        _CK3TerrainSunnyIblScale("原版晴天地形环境光倍率", Float) = 0.25
        _CK3TerrainSunnySpecularFactor("原版晴天地形高光倍率", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "CK3TerrainSurfacePreLighting"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertexMain
            #pragma fragment FragmentMain
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_DetailTextures);
            SAMPLER(sampler_DetailTextures);
            TEXTURE2D_ARRAY(_NormalTextures);
            SAMPLER(sampler_NormalTextures);
            TEXTURE2D_ARRAY(_MaterialTextures);
            SAMPLER(sampler_MaterialTextures);
            TEXTURE2D(_DetailIndexTexture);
            SAMPLER(sampler_DetailIndexTexture);
            TEXTURE2D(_DetailMaskTexture);
            SAMPLER(sampler_DetailMaskTexture);
            TEXTURE2D(_ColorTexture);
            SAMPLER(sampler_ColorTexture);
            TEXTURE2D(_FlatMapTexture);
            SAMPLER(sampler_FlatMapTexture);
            TEXTURE2D(_PaperTearMask);
            SAMPLER(sampler_PaperTearMask);
            TEXTURE2D(_CK3HeightLookupTexture);
            SAMPLER(sampler_CK3HeightLookupTexture);
            TEXTURE2D(_CK3PackedHeightTexture);
            SAMPLER(sampler_CK3PackedHeightTexture);
            TEXTURE2D(_ProvinceColorIndirectionTexture);
            SAMPLER(sampler_ProvinceColorIndirectionTexture);
            TEXTURE2D(_ProvinceColorTexture);
            TEXTURE2D(_BorderDistanceFieldTexture);
            SAMPLER(sampler_BorderDistanceFieldTexture);
            TEXTURECUBE(_CK3TerrainSunnyEnvironmentMap);
            SAMPLER(sampler_CK3TerrainSunnyEnvironmentMap);

            CBUFFER_START(UnityPerMaterial)
                float _DetailBlendRange;
                float4 _DetailTileFactor;
                float4 _DetailTileOffset;
                float4 _WorldSpaceToDetail;
                float4 _DetailTexelSize;
                float4 _DetailTextureSize;
                float4 _WorldSpaceToTerrain01;
                float4 _CK3WorldSpaceToLookup;
                float4 _CK3IndirectionSize;
                float4 _CK3PackedHeightMapSize;
                float4 _CK3WorldExtents;
                float _CK3BaseTileSize;
                float _CK3HeightScale;
                float _CK3TerrainVerticalOffset;
                float _FlatMapHeight;
                float _FlatMapLerp;
                float _CK3NormQuadtreeToWorld;
                float _CK3SkirtSize;
                float4 _CK3TileToHeightMap0;
                float4 _CK3TileToHeightMap1;
                float4 _CK3TileToHeightMap2;
                float4 _CK3TileToHeightMap3;
                float4 _CK3TileToHeightMap4;
                float _CK3ProvinceOverlayEnabled;
                float _CK3ProvinceOverlayBlend;
                float _CK3DistanceSampleOffset;
                float _GB_GradientAlphaInside;
                float _GB_GradientAlphaOutside;
                float _GB_GradientWidth;
                float _GB_GradientColorMul;
                float _GB_EdgeWidth;
                float _GB_EdgeSmoothness;
                float _GB_EdgeAlpha;
                float _GB_EdgeColorMul;
                float _GB_PreLightingBlend;
                float _GB_PostLightingBlend;
                float _CK3MacroColorStrength;
                float _CK3NormalHeightScale;
                float _CK3NormalStepSize;
                float _CK3WaterZoomedInZoomedOutFactor;
                float _CK3MapLightingEnabled;
                float4 _CK3TerrainSunnySunColor;
                float _CK3TerrainSunnySunIntensity;
                float _CK3TerrainSunnyIblScale;
                float _CK3TerrainSunnySpecularFactor;
            CBUFFER_END

            float4 _CK3NodeOffsetScaleLerp;
            float _CK3IsSkirt;

            // CK3 fills PdxTerrainConstants.PackedDetailTileFactors on the CPU. Unity materials
            // do not serialize array properties, so the persistent CK3 library supplies this
            // equivalent constant array through CK3TerrainSurfaceBinding.
            float4 _CK3PackedDetailTileFactors[105];

            struct VertexInput
            {
                float3 positionOS : POSITION;
                float2 withinNodePosition : TEXCOORD0;
                float2 lodDirection : TEXCOORD1;
                uint vertexId : SV_VertexID;
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

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            VertexOutput VertexMain(VertexInput input)
            {
                VertexOutput output;
                float nodeScale = 1.0 / _CK3NodeOffsetScaleLerp.z;
                float2 nodeOffset = _CK3NodeOffsetScaleLerp.xy * nodeScale;
                float2 quadtreePosition = input.withinNodePosition * nodeScale + nodeOffset;
                float2 worldXZ = clamp(
                    quadtreePosition * _CK3NormQuadtreeToWorld,
                    float2(0.0, 0.0),
                    _CK3WorldExtents.xy);
                float height = CK3GetHeight(worldXZ) + _CK3TerrainVerticalOffset;
                if (_CK3IsSkirt > 0.5)
                {
                    // Direct port of FixPositionForSkirt. The configured SkirtSize
                    // is applied after the original height lookup.
                    height += _CK3SkirtSize * ((input.vertexId + 1u) % 2u);
                }
                // Direct port of pdxterrain.shader TERRAIN_FLAT_MAP_LERP.
                height = lerp(height, _FlatMapHeight, _FlatMapLerp);
                output.positionWS = float3(worldXZ.x, height, worldXZ.y);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float4 CK3CalcHeightBlendFactors(
                float4 materialHeights,
                float4 materialFactors,
                float blendRange)
            {
                float4 material = materialHeights + materialFactors;
                float blendStart = max(max(material.x, material.y), max(material.z, material.w)) - blendRange;
                float4 materialBlend = max(
                    material - float4(blendStart, blendStart, blendStart, blendStart),
                    float4(0.0, 0.0, 0.0, 0.0));
                const float epsilon = 0.00001;
                return materialBlend / (dot(materialBlend, float4(1.0, 1.0, 1.0, 1.0)) + epsilon);
            }

            float3 CK3UnpackRRxGNormal(float4 sampleValue)
            {
                float x = sampleValue.g * 2.0 - 1.0;
                float y = -(sampleValue.a * 2.0 - 1.0);
                float z = sqrt(saturate(1.0 - x * x - y * y));
                return float3(x, y, z);
            }

            float CK3SoftLight(float baseValue, float blendValue)
            {
                return (1.0 - 2.0 * blendValue) * baseValue * baseValue
                    + 2.0 * baseValue * blendValue;
            }

            float3 CK3SoftLight(float3 baseValue, float3 blendValue, float opacity)
            {
                float3 blended = float3(
                    CK3SoftLight(baseValue.r, blendValue.r),
                    CK3SoftLight(baseValue.g, blendValue.g),
                    CK3SoftLight(baseValue.b, blendValue.b));
                return lerp(baseValue, blended, opacity);
            }

            float4 CK3ProvinceColorSample(float2 coordinates)
            {
                // CK3 sampler state is U=Wrap and V=Border(0,0,0,0).
                if (coordinates.y < 0.0 || coordinates.y > 1.0)
                {
                    return _ProvinceColorTexture.Load(int3(0, 0, 0));
                }
                coordinates.x = frac(coordinates.x);
                float2 colorIndex = SAMPLE_TEXTURE2D_LOD(
                    _ProvinceColorIndirectionTexture,
                    sampler_ProvinceColorIndirectionTexture,
                    coordinates,
                    0.0).rg;
                int2 palettePixel = int2(colorIndex * 255.0 + float2(0.5, 0.5));
                return _ProvinceColorTexture.Load(int3(palettePixel, 0));
            }

            // Direct port of Jomini's BilinearColorSample. The indirection texture stays
            // point-filtered and four palette colors are interpolated explicitly.
            float4 CK3BilinearProvinceColorSample(float2 coordinates)
            {
                const float2 textureSize = float2(9216.0, 4608.0);
                const float2 inverseTextureSize = 1.0 / textureSize;
                float2 pixel = coordinates * textureSize + 0.5;
                float2 fraction = frac(pixel);
                pixel = floor(pixel) / textureSize - inverseTextureSize * 0.5;
                float4 c11 = CK3ProvinceColorSample(pixel);
                float4 c21 = CK3ProvinceColorSample(pixel + float2(inverseTextureSize.x, 0.0));
                float4 c12 = CK3ProvinceColorSample(pixel + float2(0.0, inverseTextureSize.y));
                float4 c22 = CK3ProvinceColorSample(pixel + inverseTextureSize);
                return lerp(lerp(c11, c21, fraction.x), lerp(c12, c22, fraction.x), fraction.y);
            }

            float CK3CalcDistanceFieldValue(float2 coordinates)
            {
                const float2 inverseGradientTextureSize = 1.0 / float2(2304.0, 1152.0);
                float2 offset = _CK3DistanceSampleOffset * inverseGradientTextureSize;
                float distanceValue = SAMPLE_TEXTURE2D(
                    _BorderDistanceFieldTexture,
                    sampler_BorderDistanceFieldTexture,
                    coordinates).r;
                distanceValue += SAMPLE_TEXTURE2D(_BorderDistanceFieldTexture, sampler_BorderDistanceFieldTexture, coordinates + offset * float2(-1.0, -1.0)).r;
                distanceValue += SAMPLE_TEXTURE2D(_BorderDistanceFieldTexture, sampler_BorderDistanceFieldTexture, coordinates + offset * float2( 1.0, -1.0)).r;
                distanceValue += SAMPLE_TEXTURE2D(_BorderDistanceFieldTexture, sampler_BorderDistanceFieldTexture, coordinates + offset * float2(-1.0,  1.0)).r;
                distanceValue += SAMPLE_TEXTURE2D(_BorderDistanceFieldTexture, sampler_BorderDistanceFieldTexture, coordinates + offset * float2( 1.0,  1.0)).r;
                return distanceValue / 5.0;
            }

            float4 CK3CalcPrimaryProvinceOverlay(float2 coordinates, float distanceValue)
            {
                float4 primaryColor = CK3BilinearProvinceColorSample(coordinates);
                float gradientDenominator = max(0.000001, _GB_GradientWidth);
                float gradientFactor = saturate(
                    (_GB_EdgeWidth + _GB_GradientWidth - distanceValue) / gradientDenominator);
                float gradientAlpha = lerp(
                    _GB_GradientAlphaInside,
                    _GB_GradientAlphaOutside,
                    gradientFactor);
                float edge = 1.0 - smoothstep(
                    _GB_EdgeWidth,
                    _GB_EdgeWidth + max(0.0001, _GB_EdgeSmoothness),
                    distanceValue);
                float edgeGapAdjustment = lerp(
                    8.0,
                    2.0,
                    smoothstep(0.0, 0.05, _GB_EdgeSmoothness));

                float4 color;
                color.rgb = lerp(
                    primaryColor.rgb * _GB_GradientColorMul,
                    primaryColor.rgb * _GB_EdgeColorMul,
                    edge);
                color.a = primaryColor.a * max(
                    gradientAlpha * (1.0 - pow(edge, edgeGapAdjustment)),
                    _GB_EdgeAlpha * edge);
                return color;
            }

            float3 CK3CalculateTerrainNormal(float2 worldSpacePositionXZ)
            {
                float stepSize = max(0.0001, _CK3NormalStepSize);
                float heightMinX = CK3GetHeight(worldSpacePositionXZ + float2(-stepSize, 0.0));
                float heightMaxX = CK3GetHeight(worldSpacePositionXZ + float2( stepSize, 0.0));
                float heightMinZ = CK3GetHeight(worldSpacePositionXZ + float2(0.0, -stepSize));
                float heightMaxZ = CK3GetHeight(worldSpacePositionXZ + float2(0.0,  stepSize));
                float3 normal = float3(
                    (heightMinX - heightMaxX) * _CK3NormalHeightScale,
                    2.0,
                    (heightMinZ - heightMaxZ) * _CK3NormalHeightScale);
                return normalize(normal);
            }

            float3 CK3FresnelSchlick(float3 f0, float3 f90, float cosAngle)
            {
                return f0 + (f90 - f0) * pow(1.0 - cosAngle, 5.0);
            }

            float CK3DGGX(float nDotH, float alpha)
            {
                float alpha2 = alpha * alpha;
                float f = (nDotH * alpha2 - nDotH) * nDotH + 1.0;
                return alpha2 / max(PI * f * f, 0.000001);
            }

            float CK3VOptimized(float lDotH, float alpha)
            {
                float k = alpha * 0.5;
                float k2 = k * k;
                return 0.25 / max(lDotH * lDotH * (1.0 - k2) + k2, 0.000001);
            }

            float3 CK3GetSpecularDominantDirection(float3 normal, float3 reflection, float roughness)
            {
                float smoothness = saturate(1.0 - roughness);
                float factor = smoothness * (sqrt(smoothness) + roughness);
                return normalize(lerp(normal, reflection, factor));
            }

            float CK3BurleyToMipSimple(float perceptualRoughness)
            {
                const float mipCount = 10.0;
                const float mipOffset = 2.0;
                float scale = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
                return scale * (mipCount - 1.0 - mipOffset);
            }

            float3 CK3CalculateMapLighting(
                float3 surfaceColor,
                float3 normal,
                float4 materialSample,
                float3 worldPosition,
                Light mainLight)
            {
                float perceptualRoughness = saturate(materialSample.a);
                float roughness = perceptualRoughness * perceptualRoughness;
                float metalness = saturate(materialSample.b);
                float sampledSpecular = 0.25 * saturate(materialSample.g);
                float3 diffuseColor = lerp(surfaceColor, float3(0.0, 0.0, 0.0), metalness);
                float3 specularColor = lerp(sampledSpecular.xxx, surfaceColor, metalness);

                float3 toCamera = normalize(_WorldSpaceCameraPos.xyz - worldPosition);
                float3 toLight = normalize(mainLight.direction);
                float3 halfDirection = normalize(toCamera + toLight);
                float nDotV = saturate(dot(normal, toCamera)) + 0.00001;
                float nDotL = saturate(dot(normal, toLight)) + 0.00001;
                float nDotH = saturate(dot(normal, halfDirection));
                float lDotH = saturate(dot(toLight, halfDirection));

                // Direct port of CK3 cw/lighting.fxh with PDX_SimpleLighting enabled.
                float3 lightIntensity = _CK3TerrainSunnySunColor.rgb
                    * _CK3TerrainSunnySunIntensity
                    * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation;
                float3 diffuseLight = (1.0 / PI) * diffuseColor * lightIntensity * nDotL;
                float3 fresnel = CK3FresnelSchlick(specularColor, float3(1.0, 1.0, 1.0), lDotH);
                float distribution = CK3DGGX(nDotH, lerp(0.03, 1.0, roughness));
                float visibility = CK3VOptimized(lDotH, roughness);
                float3 specularLight = distribution * fresnel * visibility * lightIntensity * nDotL;

                float3 diffuseRadiance = SAMPLE_TEXTURECUBE_LOD(
                    _CK3TerrainSunnyEnvironmentMap,
                    sampler_CK3TerrainSunnyEnvironmentMap,
                    normal,
                    7.0).rgb * _CK3TerrainSunnyIblScale;
                float3 diffuseIbl = diffuseRadiance * diffuseColor;

                float3 reflection = reflect(-toCamera, normal);
                float3 dominantReflection = CK3GetSpecularDominantDirection(normal, reflection, roughness);
                float nDotR = saturate(dot(normal, dominantReflection));
                float3 specularReflection = CK3FresnelSchlick(
                    specularColor,
                    float3(1.0, 1.0, 1.0),
                    nDotR);
                float specularFade = 1.0 / (roughness * roughness + 1.0);
                float3 specularRadiance = SAMPLE_TEXTURECUBE_LOD(
                    _CK3TerrainSunnyEnvironmentMap,
                    sampler_CK3TerrainSunnyEnvironmentMap,
                    dominantReflection,
                    CK3BurleyToMipSimple(perceptualRoughness)).rgb
                    * _CK3TerrainSunnyIblScale;
                float3 specularIbl = specularRadiance * specularFade * specularReflection;

                return diffuseLight + diffuseIbl
                    + (specularLight + specularIbl) * _CK3TerrainSunnySpecularFactor;
            }

            float2 CK3CalcDetailUV(float2 worldSpacePositionXZ)
            {
                return (worldSpacePositionXZ + _DetailTileOffset.xy) * _DetailTileFactor.xy;
            }

            float2 CK3CalcDetailUVForMaterial(float2 worldSpacePositionXZ, int detailIndex)
            {
                return (worldSpacePositionXZ + _DetailTileOffset.xy)
                    * _CK3PackedDetailTileFactors[detailIndex].xy;
            }

            void CK3CalculateDetails(
                float2 worldSpacePositionXZ,
                out float4 detailDiffuseHeight,
                out float3 detailNormal,
                out float4 detailMaterial)
            {
                float2 detailCoordinates = worldSpacePositionXZ * _WorldSpaceToDetail.xy;
                float2 detailCoordinatesScaled = detailCoordinates * _DetailTextureSize.xy;
                float2 detailCoordinatesScaledFloored = floor(detailCoordinatesScaled);
                float2 detailCoordinatesFraction = detailCoordinatesScaled - detailCoordinatesScaledFloored;
                detailCoordinates = detailCoordinatesScaledFloored * _DetailTexelSize.xy
                    + _DetailTexelSize.xy * 0.5;

                float4 factors = float4(
                    (1.0 - detailCoordinatesFraction.x) * (1.0 - detailCoordinatesFraction.y),
                    detailCoordinatesFraction.x * (1.0 - detailCoordinatesFraction.y),
                    (1.0 - detailCoordinatesFraction.x) * detailCoordinatesFraction.y,
                    detailCoordinatesFraction.x * detailCoordinatesFraction.y);

                float4 detailIndex = SAMPLE_TEXTURE2D_LOD(
                    _DetailIndexTexture,
                    sampler_DetailIndexTexture,
                    detailCoordinates,
                    0.0) * 255.0;
                float4 detailMask = SAMPLE_TEXTURE2D_LOD(
                    _DetailMaskTexture,
                    sampler_DetailMaskTexture,
                    detailCoordinates,
                    0.0) * factors[0];

                const float2 offsets[3] =
                {
                    float2(1.0, 0.0),
                    float2(0.0, 1.0),
                    float2(1.0, 1.0)
                };

                [unroll]
                for (int neighbor = 0; neighbor < 3; ++neighbor)
                {
                    float2 neighborCoordinates = detailCoordinates
                        + offsets[neighbor] * _DetailTexelSize.xy;
                    float4 neighborIndices = SAMPLE_TEXTURE2D_LOD(
                        _DetailIndexTexture,
                        sampler_DetailIndexTexture,
                        neighborCoordinates,
                        0.0) * 255.0;
                    float4 neighborMasks = SAMPLE_TEXTURE2D_LOD(
                        _DetailMaskTexture,
                        sampler_DetailMaskTexture,
                        neighborCoordinates,
                        0.0) * factors[neighbor + 1];

                    [unroll]
                    for (int sourceLayer = 0; sourceLayer < 4; ++sourceLayer)
                    {
                        [unroll]
                        for (int destinationLayer = 0; destinationLayer < 4; ++destinationLayer)
                        {
                            if (detailIndex[destinationLayer] == neighborIndices[sourceLayer])
                            {
                                detailMask[destinationLayer] += neighborMasks[sourceLayer];
                            }
                        }
                    }
                }

                float2 defaultDetailUV = CK3CalcDetailUV(worldSpacePositionXZ);
                float2 derivativeX = ddx(defaultDetailUV);
                float2 derivativeY = ddy(defaultDetailUV);

                float2 detailUV[4];
                [unroll]
                for (int layer = 0; layer < 4; ++layer)
                {
                    detailUV[layer] = CK3CalcDetailUVForMaterial(
                        worldSpacePositionXZ,
                        (int)detailIndex[layer]);
                }

                float4 detailTexture[4];
                [unroll]
                for (int diffuseLayer = 0; diffuseLayer < 4; ++diffuseLayer)
                {
                    detailTexture[diffuseLayer] = SAMPLE_TEXTURE2D_ARRAY_GRAD(
                        _DetailTextures,
                        sampler_DetailTextures,
                        detailUV[diffuseLayer],
                        (int)detailIndex[diffuseLayer],
                        derivativeX,
                        derivativeY)
                        * smoothstep(0.0, 0.1, detailMask[diffuseLayer]);
                }

                float4 blendFactors = CK3CalcHeightBlendFactors(
                    float4(
                        detailTexture[0].a,
                        detailTexture[1].a,
                        detailTexture[2].a,
                        detailTexture[3].a),
                    detailMask,
                    _DetailBlendRange);

                detailDiffuseHeight = detailTexture[0] * blendFactors[0]
                    + detailTexture[1] * blendFactors[1]
                    + detailTexture[2] * blendFactors[2]
                    + detailTexture[3] * blendFactors[3];

                detailMaterial = float4(0.0, 0.0, 0.0, 0.0);
                float4 detailNormalSample = float4(0.0, 0.0, 0.0, 0.0);
                [unroll]
                for (int propertyLayer = 0; propertyLayer < 4; ++propertyLayer)
                {
                    float blendFactor = blendFactors[propertyLayer];
                    if (blendFactor > 0.0)
                    {
                        int textureIndex = (int)detailIndex[propertyLayer];
                        float4 normalTexture = SAMPLE_TEXTURE2D_ARRAY_GRAD(
                            _NormalTextures,
                            sampler_NormalTextures,
                            detailUV[propertyLayer],
                            textureIndex,
                            derivativeX,
                            derivativeY);
                        float4 materialTexture = SAMPLE_TEXTURE2D_ARRAY_GRAD(
                            _MaterialTextures,
                            sampler_MaterialTextures,
                            detailUV[propertyLayer],
                            textureIndex,
                            derivativeX,
                            derivativeY);
                        detailNormalSample += normalTexture * blendFactor;
                        detailMaterial += materialTexture * blendFactor;
                    }
                }

                detailNormal = CK3UnpackRRxGNormal(detailNormalSample);
            }

            float4 FragmentMain(VertexOutput input) : SV_Target
            {
                float4 detailDiffuseHeight;
                float3 detailNormal;
                float4 detailMaterial;
                CK3CalculateDetails(
                    input.positionWS.xz,
                    detailDiffuseHeight,
                    detailNormal,
                    detailMaterial);

                float2 colorMapCoordinates = input.positionWS.xz * _WorldSpaceToTerrain01.xy;
                float4 colorMapSample = SAMPLE_TEXTURE2D(
                    _ColorTexture,
                    sampler_ColorTexture,
                    float2(colorMapCoordinates.x, 1.0 - colorMapCoordinates.y));
                colorMapSample.rgb = pow(
                    max(colorMapSample.rgb, float3(0.0, 0.0, 0.0)),
                    float3(2.2, 2.2, 2.2));

                float3 diffuse = CK3SoftLight(
                    detailDiffuseHeight.rgb,
                    colorMapSample.rgb,
                    saturate((1.0 - detailMaterial.r) * _CK3MacroColorStrength));

                float3 provinceOverlayColor = float3(0.0, 0.0, 0.0);
                float preLightingBlend = 0.0;
                float postLightingBlend = 0.0;

                if (_CK3ProvinceOverlayEnabled > 0.5)
                {
                    float distanceValue = CK3CalcDistanceFieldValue(colorMapCoordinates);
                    float4 provinceOverlay = CK3CalcPrimaryProvinceOverlay(
                        colorMapCoordinates,
                        distanceValue);
                    provinceOverlayColor = provinceOverlay.rgb;
                    preLightingBlend = _GB_PreLightingBlend
                        * provinceOverlay.a * _CK3ProvinceOverlayBlend;
                    postLightingBlend = _GB_PostLightingBlend
                        * provinceOverlay.a * _CK3ProvinceOverlayBlend;
                    diffuse = lerp(diffuse, provinceOverlayColor, saturate(preLightingBlend));
                }

                float3 terrainNormal = CK3CalculateTerrainNormal(input.positionWS.xz);
                float4 shadowCoordinates = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoordinates);
                float nDotL = saturate(dot(terrainNormal, mainLight.direction)) + 0.00001;
                float3 finalColor;
                if (_CK3MapLightingEnabled > 0.5)
                {
                    finalColor = CK3CalculateMapLighting(
                        diffuse,
                        terrainNormal,
                        detailMaterial,
                        input.positionWS,
                        mainLight);
                }
                else
                {
                    float mainLightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                    float3 indirectLight = SampleSH(terrainNormal);
                    finalColor = diffuse * (
                        indirectLight + mainLight.color * (nDotL * mainLightAttenuation));
                }

                if (_CK3ProvinceOverlayEnabled > 0.5)
                {
                    float borderLightingFloor = max(
                        _CK3WaterZoomedInZoomedOutFactor - 0.4,
                        0.4);
                    float3 postLightingBorderColor = provinceOverlayColor
                        * lerp(borderLightingFloor, 1.0, nDotL);
                    finalColor = lerp(
                        finalColor,
                        postLightingBorderColor,
                        saturate(postLightingBlend));
                }

                // Direct port of game/gfx/FX/paper_transition.fxh.
                float2 flatMapUV = float2(colorMapCoordinates.x, 1.0 - colorMapCoordinates.y);
                float paperBlend;
                if (_FlatMapLerp >= 1.0)
                {
                    paperBlend = 1.0;
                }
                else if (_FlatMapLerp <= 0.001)
                {
                    paperBlend = 0.0;
                }
                else
                {
                    float mainMask = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, flatMapUV * float2(12.0, 6.0)).r;
                    float variation = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, flatMapUV * float2(16.0, 8.0)).g;
                    float largeScale = SAMPLE_TEXTURE2D(
                        _PaperTearMask, sampler_PaperTearMask, flatMapUV).b;
                    float maskValue = mainMask * 0.5 + largeScale * 0.5;
                    float threshold = 1.2 - _FlatMapLerp * 1.4 + (largeScale - 0.5) * 0.3;
                    float blendBase = threshold + variation;
                    paperBlend = smoothstep(blendBase - 0.35, blendBase + 0.35, maskValue);
                }
                float3 flatMapColor = SAMPLE_TEXTURE2D(
                    _FlatMapTexture, sampler_FlatMapTexture, flatMapUV).rgb;
                // pdxterrain.shader colors FlatMap with the same province overlay before the
                // final paper transition; otherwise the flat-map endpoint would lose F1/F2.
                if (_CK3ProvinceOverlayEnabled > 0.5)
                {
                    flatMapColor = lerp(
                        flatMapColor,
                        provinceOverlayColor,
                        saturate(preLightingBlend + postLightingBlend));
                }
                finalColor = lerp(finalColor, flatMapColor, paperBlend);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
