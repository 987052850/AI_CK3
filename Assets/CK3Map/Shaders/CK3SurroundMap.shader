Shader "CK3Map/Surround Map"
{
    Properties
    {
        [NoScaleOffset] _SurroundMask("原版世界外侧遮罩", 2D) = "white" {}
        [NoScaleOffset] _SurroundFade("原版世界外侧淡出", 2D) = "white" {}
        _FlatMapLerp("原版平面地图过渡", Range(0,1)) = 0
        _FlatMapHeight("原版平面地图高度", Float) = 3.92
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" "RenderType"="Transparent" }
        Pass
        {
            Name "CK3SurroundMapFlat"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SurroundMask); SAMPLER(sampler_SurroundMask);
            TEXTURE2D(_SurroundFade); SAMPLER(sampler_SurroundFade);
            CBUFFER_START(UnityPerMaterial)
                float _FlatMapLerp;
                float _FlatMapHeight;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                positionWS.y = _FlatMapHeight;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Direct port of PS_surroundmap_flat. The old woodgrain tile is intentionally zero.
                float mask = SAMPLE_TEXTURE2D(_SurroundMask, sampler_SurroundMask, input.uv).b;
                float2 blackUV = input.uv * 0.75 + float2(0.125, 0.125);
                float black = SAMPLE_TEXTURE2D(_SurroundFade, sampler_SurroundFade, blackUV).r;
                return half4(0.0, 0.0, 0.0, saturate(mask * _FlatMapLerp * black));
            }
            ENDHLSL
        }
    }
}
