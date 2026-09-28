Shader "CK3Map/River Surface"
{
    Properties
    {
        [NoScaleOffset] _WaterColorTexture("原版水色与高光图", 2D) = "black" {}
        [NoScaleOffset] _CK3HeightLookupTexture("原版高度间接寻址图", 2D) = "black" {}
        [NoScaleOffset] _CK3PackedHeightTexture("原版压缩高度图", 2D) = "black" {}
        _RiverColor("河面颜色倍率", Color) = (0.72,0.88,0.92,1)
        _RiverOpacity("河面透明度", Range(0,1)) = 0.82
        _RiverHeightOffset("河面离地高度", Range(0,0.5)) = 0.04
        _TextureUvScale("原版纵向纹理缩放", Float) = 0.8
        _FlowNormalUvScale("原版流动法线缩放", Float) = 0.4
        _FlowNormalSpeed("原版流动速度", Float) = 0.075
        _OceanFadeRate("原版入海淡出率", Float) = 0.8
        _FlatMapLerp("原版平面地图过渡", Range(0,1)) = 0
        _FlatMapHeight("原版平面地图高度", Float) = 3.92
        _CK3WorldSpaceToLookup("世界坐标到页表坐标", Vector) = (0.0001085069,0.0002170139,0,0)
        _CK3OriginalHeightmapToWorldSpace("原始高度像素到世界坐标", Vector) = (0.5,0.5,0,0)
        _CK3IndirectionSize("原版间接寻址图尺寸", Vector) = (288,144,0,0)
        _CK3BaseTileSize("原版高度瓦片尺寸", Float) = 65
        _CK3HeightScale("原版高度缩放", Float) = 50
        _CK3WorldExtents("原版世界范围", Vector) = (9215,4607,0,0)
        _CK3TileToHeightMap0("高度页常量 0", Vector) = (0,0,0,0)
        _CK3TileToHeightMap1("高度页常量 1", Vector) = (0,0,0,0)
        _CK3TileToHeightMap2("高度页常量 2", Vector) = (0,0,0,0)
        _CK3TileToHeightMap3("高度页常量 3", Vector) = (0,0,0,0)
        _CK3TileToHeightMap4("高度页常量 4", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Name "CK3RiverSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1,-1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertexMain
            #pragma fragment FragmentMain
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WaterColorTexture); SAMPLER(sampler_WaterColorTexture);
            TEXTURE2D(_CK3HeightLookupTexture); SAMPLER(sampler_CK3HeightLookupTexture);
            TEXTURE2D(_CK3PackedHeightTexture); SAMPLER(sampler_CK3PackedHeightTexture);

            CBUFFER_START(UnityPerMaterial)
            half4 _RiverColor;
            float _RiverOpacity, _RiverHeightOffset, _TextureUvScale, _FlowNormalUvScale;
            float _FlowNormalSpeed, _OceanFadeRate, _FlatMapLerp, _FlatMapHeight;
            float4 _CK3WorldSpaceToLookup, _CK3OriginalHeightmapToWorldSpace, _CK3IndirectionSize;
            float _CK3BaseTileSize, _CK3HeightScale;
            float4 _CK3WorldExtents;
            float4 _CK3TileToHeightMap0, _CK3TileToHeightMap1, _CK3TileToHeightMap2;
            float4 _CK3TileToHeightMap3, _CK3TileToHeightMap4;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float2 data:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float width:TEXCOORD2; };

            float4 TileConstant(int level)
            {
                if (level==0) return _CK3TileToHeightMap0;
                if (level==1) return _CK3TileToHeightMap1;
                if (level==2) return _CK3TileToHeightMap2;
                if (level==3) return _CK3TileToHeightMap3;
                return _CK3TileToHeightMap4;
            }

            float Height01(float2 xz)
            {
                float2 lookup=clamp(xz*_CK3WorldSpaceToLookup.xy,0.0,0.999999);
                float2 uv=(floor(lookup*_CK3IndirectionSize.xy)+0.5)/_CK3IndirectionSize.xy;
                float4 ind=SAMPLE_TEXTURE2D_LOD(_CK3HeightLookupTexture,sampler_CK3HeightLookupTexture,uv,0)*255.0;
                float tileSize=(_CK3BaseTileSize-1.0)/ind.z+1.0;
                float2 tileUV=ind.rg+0.5/tileSize+frac(lookup*_CK3IndirectionSize.xy)*(tileSize-1.0)/tileSize;
                float4 c=TileConstant((int)round(ind.a));
                return SAMPLE_TEXTURE2D_LOD(_CK3PackedHeightTexture,sampler_CK3PackedHeightTexture,tileUV*c.xy+c.zw,0).r;
            }

            float TerrainHeight(float2 xz)
            {
                // The terrain surface samples this exact height page at the vertex world XZ.
                // The previous nine-sample blur lowered rivers below steep terrain and caused
                // visible broken segments. Keep the Unity carrier coincident with terrain;
                // depth separation is controlled only by _RiverHeightOffset.
                return Height01(xz)*_CK3HeightScale;
            }

            Varyings VertexMain(Attributes input)
            {
                Varyings o;
                float3 p=input.positionOS;
                p.y=lerp(TerrainHeight(p.xz)+_RiverHeightOffset,_FlatMapHeight+_RiverHeightOffset,_FlatMapLerp);
                o.positionWS=p;
                o.positionCS=TransformWorldToHClip(p);
                o.uv=float2(input.uv.x*_TextureUvScale,input.uv.y);
                o.width=input.data.x;
                return o;
            }

            half4 FragmentMain(Varyings input):SV_Target
            {
                float2 mapUV=float2(input.positionWS.x/_CK3WorldExtents.x,1.0-input.positionWS.z/_CK3WorldExtents.y);
                half3 water=SAMPLE_TEXTURE2D(_WaterColorTexture,sampler_WaterColorTexture,mapUV).rgb;
                float bank=saturate(sin(input.uv.y*PI));
                float alpha=_RiverOpacity*smoothstep(0.0,0.12,bank);
                return half4(water*_RiverColor.rgb,alpha*_RiverColor.a);
            }
            ENDHLSL
        }
    }
}
