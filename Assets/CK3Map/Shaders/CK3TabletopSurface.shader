Shader "CK3Map/Tabletop Surface"
{
    Properties
    {
        _BaseMap("原版漫反射", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap("原版法线", 2D) = "bump" {}
        [NoScaleOffset] _PropertiesMap("原版属性图", 2D) = "white" {}
        [NoScaleOffset] _CK3TabletopEnvironmentMap("原版桌面环境立方体", Cube) = "black" {}
        _BaseColor("基础颜色", Color) = (1,1,1,1)
        _CK3TabletopSunColor("原版桌面太阳颜色", Color) = (1,0.92,0.85,1)
        _CK3TabletopSunIntensity("原版桌面太阳强度", Float) = 5
        _CK3TabletopIblScale("原版桌面环境光强度", Float) = 1
        _CK3TabletopSpecularFactor("原版桌面高光倍率", Float) = 1
        _Cutoff("透明裁切", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("启用透明裁切", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("剔除模式", Float) = 2
        _GlobalOpacity("整体透明度", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "TabletopForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_PropertiesMap); SAMPLER(sampler_PropertiesMap);
            TEXTURECUBE(_CK3TabletopEnvironmentMap);
            SAMPLER(sampler_CK3TabletopEnvironmentMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _CK3TabletopSunColor;
                float _CK3TabletopSunIntensity;
                float _CK3TabletopIblScale;
                float _CK3TabletopSpecularFactor;
                float _Cutoff;
                float _AlphaClip;
                float _Cull;
                float _GlobalOpacity;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD0;
            };

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

            float3 CK3CalculateTabletopLighting(
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
                float nDotL = saturate(dot(normal, toLight)) + 0.00001;
                float nDotH = saturate(dot(normal, halfDirection));
                float lDotH = saturate(dot(toLight, halfDirection));

                float3 lightIntensity = _CK3TabletopSunColor.rgb
                    * _CK3TabletopSunIntensity
                    * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation;
                float3 diffuseLight = (1.0 / PI) * diffuseColor * lightIntensity * nDotL;
                float3 fresnel = CK3FresnelSchlick(
                    specularColor,
                    float3(1.0, 1.0, 1.0),
                    lDotH);
                float distribution = CK3DGGX(nDotH, lerp(0.03, 1.0, roughness));
                float visibility = CK3VOptimized(lDotH, roughness);
                float3 specularLight = distribution * fresnel * visibility * lightIntensity * nDotL;

                float3 diffuseRadiance = SAMPLE_TEXTURECUBE_LOD(
                    _CK3TabletopEnvironmentMap,
                    sampler_CK3TabletopEnvironmentMap,
                    normal,
                    7.0).rgb * _CK3TabletopIblScale;
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
                    _CK3TabletopEnvironmentMap,
                    sampler_CK3TabletopEnvironmentMap,
                    dominantReflection,
                    CK3BurleyToMipSimple(perceptualRoughness)).rgb * _CK3TabletopIblScale;
                float3 specularIbl = specularRadiance * specularFade * specularReflection;

                return diffuseLight + diffuseIbl
                    + (specularLight + specularIbl) * _CK3TabletopSpecularFactor;
            }
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float4 tangentWS:TEXCOORD2;
                float2 uv:TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(baseSample.a * _GlobalOpacity - _Cutoff);
                #endif
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                float3 normalWS = normalize(
                    normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);
                half4 properties = SAMPLE_TEXTURE2D(_PropertiesMap, sampler_PropertiesMap, input.uv);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color = CK3CalculateTabletopLighting(
                    baseSample.rgb,
                    normalWS,
                    properties,
                    input.positionWS,
                    light);
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
    }
}
