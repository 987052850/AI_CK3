Shader "CK3Map/Province Distance Field"
{
    Properties
    {
        [HideInInspector] _DeltaVectors ("Delta Vectors", 2D) = "white" {}
        [HideInInspector] _ProvinceColorIndirectionTexture ("Province Indirection", 2D) = "black" {}
        [HideInInspector] _ProvinceColorTexture ("Province Colors", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_DeltaVectors);
        SAMPLER(sampler_DeltaVectors);
        TEXTURE2D(_ProvinceColorIndirectionTexture);
        SAMPLER(sampler_ProvinceColorIndirectionTexture);
        TEXTURE2D(_ProvinceColorTexture);
        SAMPLER(sampler_ProvinceColorTexture);

        float2 _GradientTextureSize;
        float2 _IndirectionMapSize;
        float3 _SampleOffset;
        float _MaxSearchDist;
        int _WildCardSampleCount;
        float _WildCardSampleWidth;
        int _WildCardColorsCount;
        float4 _WildCardColors[4];

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        int2 ClampTexel(float2 texel, int2 size)
        {
            // CK3 game/map_data/heightmap.heightmap: should_wrap_x=no.
            // No Y wrapping is enabled by the current map either.
            return int2(floor(clamp(texel, float2(0.0, 0.0), float2(size - 1))));
        }

        float2 GetDeltaVector(float2 coordinate)
        {
            int2 size = int2(_GradientTextureSize + 0.5);
            return _DeltaVectors.Load(int3(ClampTexel(coordinate, size), 0)).rg * _MaxSearchDist;
        }

        float4 GetProvinceColor(float2 position)
        {
            int2 sourceSize = int2(_IndirectionMapSize + 0.5);
            int2 sourceTexel = ClampTexel(position, sourceSize);
            float2 colorIndex = _ProvinceColorIndirectionTexture.Load(int3(sourceTexel, 0)).rg;
            int2 paletteTexel = int2(colorIndex * 255.0 + 0.5);
            return _ProvinceColorTexture.Load(int3(clamp(paletteTexel, int2(0, 0), int2(255, 255)), 0));
        }

        float SameColor(float4 left, float4 right)
        {
            float4 difference = left - right;
            return step(dot(difference, difference), 0.0001);
        }

        float DifferentColor(float4 left, float4 right)
        {
            float4 difference = left - right;
            return step(0.0001, dot(difference, difference));
        }

        float IsWildCard(float4 color)
        {
            float result = 0.0;
            [loop]
            for (int index = 0; index < _WildCardColorsCount; ++index)
            {
                result += SameColor(color, _WildCardColors[index]);
            }
            return result;
        }

        float4 CalcMainColor(float4 a, float4 b, float4 c, float4 d)
        {
            float cd = SameColor(c, d);
            float bd = SameColor(b, d);
            float bc = SameColor(b, c);
            float4 color = a;
            color = lerp(color, b, bd);
            color = lerp(color, b, bc);
            color = lerp(color, c, cd);
            return color;
        }

        float3 UpdateDeltaVector(float2 coordinate, float2 offset, float3 currentBest)
        {
            float3 sampleValue;
            sampleValue.xy = GetDeltaVector(coordinate + offset) + abs(float2(offset) * 4.0);
            sampleValue.z = dot(sampleValue.xy, sampleValue.xy);
            return sampleValue.z < currentBest.z ? sampleValue : currentBest;
        }

        float4 CalculateInitialDelta(Varyings input, bool useWildcards)
        {
            if (useWildcards && GetDeltaVector(input.positionCS.xy).x > 0.5)
            {
                return 1.0;
            }

            float2 texelAreaCenter = input.positionCS.xy * 4.0;
            float2 offset = float2(-3.5, -3.5);
            float4 samples[64];
            [unroll]
            for (int y = 0; y < 8; ++y)
            {
                [unroll]
                for (int x = 0; x < 8; ++x)
                {
                    samples[x + y * 8] = GetProvinceColor(texelAreaCenter + offset + float2(x, y));
                }
            }

            float4 mainColor = CalcMainColor(samples[27], samples[28], samples[35], samples[36]);
            float currentMinimumDistanceSquared = 129.0;
            float2 closestBorderPoint = -1.0;
            [unroll]
            for (int y = 0; y < 8; ++y)
            {
                [unroll]
                for (int x = 0; x < 8; ++x)
                {
                    float2 samplePoint = abs(float2(x, y) + offset);
                    float sampleDistanceSquared = dot(samplePoint, samplePoint);
                    float2 sourceTexel = texelAreaCenter + offset + float2(x, y);
                    float isMasked = useWildcards
                        ? GetDeltaVector(sourceTexel / 4.0).x
                        : 0.0;
                    float isBorder = (1.0 - step(0.5, isMasked))
                        * DifferentColor(mainColor, samples[x + y * 8]);
                    float isCloser = isBorder * step(sampleDistanceSquared, currentMinimumDistanceSquared);
                    currentMinimumDistanceSquared = lerp(
                        currentMinimumDistanceSquared,
                        sampleDistanceSquared,
                        isCloser);
                    closestBorderPoint = lerp(closestBorderPoint, samplePoint, isCloser);
                }
            }

            bool notFound = all(closestBorderPoint == float2(-1.0, -1.0));
            float2 normalizedDelta = notFound
                ? float2(1.0, 1.0)
                : closestBorderPoint / _MaxSearchDist;
            return float4(normalizedDelta, 0.0, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "WildCardMask"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragmentWildcard

            float4 FragmentWildcard(Varyings input) : SV_Target
            {
                float2 texelAreaCenter = input.positionCS.xy * 4.0;
                float containsWildcard = 0.0;
                [loop]
                for (int y = 0; y < 4 && containsWildcard < 0.5; ++y)
                {
                    [loop]
                    for (int x = 0; x < 4 && containsWildcard < 0.5; ++x)
                    {
                        containsWildcard += IsWildCard(GetProvinceColor(
                            texelAreaCenter + float2(-1.5, -1.5) + float2(x, y)));
                    }
                }
                if (containsWildcard < 0.5)
                {
                    return 0.0;
                }

                // The original shader explicitly limits this table to sixteen samples.
                float2 samples[16];
                float angleStep = 3.1416 / max(1, _WildCardSampleCount);
                [loop]
                for (int index = 0; index < _WildCardSampleCount; ++index)
                {
                    float angle = index * angleStep;
                    samples[index] = float2(cos(angle), sin(angle));
                }
                [loop]
                for (int index = 0; index < _WildCardSampleCount; ++index)
                {
                    float2 offset = samples[index] * _WildCardSampleWidth;
                    float4 color1 = GetProvinceColor(texelAreaCenter + offset);
                    float4 color2 = GetProvinceColor(texelAreaCenter - offset);
                    if (IsWildCard(color1) + IsWildCard(color2) < 0.5 &&
                        SameColor(color1, color2) < 0.5)
                    {
                        return 0.0;
                    }
                }
                return 1.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Init"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragmentInit

            float4 FragmentInit(Varyings input) : SV_Target
            {
                return CalculateInitialDelta(input, false);
            }
            ENDHLSL
        }

        Pass
        {
            Name "InitWithWildcards"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragmentInitWithWildcards

            float4 FragmentInitWithWildcards(Varyings input) : SV_Target
            {
                return CalculateInitialDelta(input, true);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Fill"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragmentFill

            float4 FragmentFill(Varyings input) : SV_Target
            {
                float2 coordinate = input.positionCS.xy;
                float3 offsets = _SampleOffset;
                float3 best;
                best.xy = GetDeltaVector(coordinate);
                best.z = dot(best.xy, best.xy);
                best = UpdateDeltaVector(coordinate, offsets.xx, best);
                best = UpdateDeltaVector(coordinate, offsets.xy, best);
                best = UpdateDeltaVector(coordinate, offsets.xz, best);
                best = UpdateDeltaVector(coordinate, offsets.yx, best);
                best = UpdateDeltaVector(coordinate, offsets.yz, best);
                best = UpdateDeltaVector(coordinate, offsets.zx, best);
                best = UpdateDeltaVector(coordinate, offsets.zy, best);
                best = UpdateDeltaVector(coordinate, offsets.zz, best);
                return float4(best.xy / _MaxSearchDist, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Finalize"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragmentFinalize

            float4 FragmentFinalize(Varyings input) : SV_Target
            {
                float2 vectorValue = GetDeltaVector(input.positionCS.xy);
                float squaredLength = dot(vectorValue, vectorValue);
                float distanceValue = squaredLength <= 0.0
                    ? 1.0
                    : sqrt(squaredLength) / _MaxSearchDist;
                return float4(distanceValue, distanceValue, distanceValue, distanceValue);
            }
            ENDHLSL
        }
    }
}
