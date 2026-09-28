Shader "CK3Map/Tree Surface"
{
    Properties
    {
        _BaseMap("Diffuse", 2D) = "white" {}
        _NormalMap("Normal And Tint Mask", 2D) = "bump" {}
        _TintMap("Tree Tint", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.4
        _GlobalOpacity("Global Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_TintMap); SAMPLER(sampler_TintMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
                float _GlobalOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float tintSeed : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float CalcRandom(float2 seed)
            {
                return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
            }

            float3 Overlay(float3 baseColor, float3 blendColor)
            {
                float3 low = 2.0 * baseColor * blendColor;
                float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
                return lerp(low, high, step(0.5, baseColor));
            }

            float DitherThreshold(float2 screenPosition)
            {
                // CK3 tree.shader 中已确认的 4x4 Bayer 矩阵。
                uint2 pixel = uint2(screenPosition) & 3u;
                uint index = pixel.y * 4u + pixel.x;
                if (index == 0u) return 0.0;
                if (index == 1u) return 0.5;
                if (index == 2u) return 0.125;
                if (index == 3u) return 0.625;
                if (index == 4u) return 0.75;
                if (index == 5u) return 0.25;
                if (index == 6u) return 0.875;
                if (index == 7u) return 0.375;
                if (index == 8u) return 0.1875;
                if (index == 9u) return 0.6875;
                if (index == 10u) return 0.0625;
                if (index == 11u) return 0.5625;
                if (index == 12u) return 0.9375;
                if (index == 13u) return 0.4375;
                if (index == 14u) return 0.8125;
                return 0.3125;
            }

            void ApplyInstanceOpacity(float opacity, float2 screenPosition)
            {
                // 对应 PdxMeshApplyDitheredOpacity 的默认分支：以屏幕坐标随机裁剪完整实例。
                if (opacity < 1.0)
                    clip(opacity - CalcRandom(screenPosition));
            }

            void ApplyTreeAlpha(float alpha, float2 screenPosition)
            {
                // 对应 tree.shader/DitheredAlpha。
                float threshold = _Cutoff + (DitherThreshold(screenPosition) - 0.5) * 0.6;
                clip(alpha - threshold);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.tintSeed = CalcRandom(float2(unity_ObjectToWorld._m02, unity_ObjectToWorld._m22));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 diffuse = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                ApplyInstanceOpacity(_GlobalOpacity, input.positionCS.xy);
                ApplyTreeAlpha(diffuse.a, input.positionCS.xy);

                half tintMask = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).b;
                half3 tint = SAMPLE_TEXTURE2D_LOD(_TintMap, sampler_TintMap, float2(input.tintSeed, 0.5), 0).rgb;
                tint = Overlay(diffuse.rgb, tint);
                diffuse.rgb = lerp(diffuse.rgb, tint, tintMask);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half direct = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lighting = ambient + mainLight.color * direct * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                return half4(diffuse.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
