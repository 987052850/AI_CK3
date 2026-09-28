Includes = {
	"jomini/posteffect_base.fxh"
}


VertexStruct VS_OUTPUT_BLOOM
{
    float4 position			: PDX_POSITION;
	float2 uvBloom			: TEXCOORD0;
	float4 uvBloom2_0		: TEXCOORD1;
	float4 uvBloom2_1		: TEXCOORD2;
	float4 uvBloom2_2		: TEXCOORD3;
	float4 uvBloom2_3		: TEXCOORD4;
};


ConstantBuffer( 2 )
{
	float2 InvBloomSize;
	float2 UVScale;
	float4 Weights;
	float4 Offsets;
	float Weight0;
	float Axis;
};


VertexShader =
{
	MainCode VertexShader
	{
		Input = "VS_INPUT_FULLSCREEN"
		Output = "VS_OUTPUT_BLOOM"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_BLOOM VertexOut;
				VertexOut.position = float4( Input.position, 0.0, 1.0 );

				VertexOut.uvBloom = ( Input.position + 1.0 ) * 0.5;
				VertexOut.uvBloom.y = 1.0 - VertexOut.uvBloom.y;
				VertexOut.uvBloom *= UVScale;

				float2 vInvSize = InvBloomSize;
				float2 vAxisOffset = vInvSize * float2( Axis, 1.0 - Axis );

				VertexOut.uvBloom2_0 = float4(
						VertexOut.uvBloom + vAxisOffset * Offsets[0],
						VertexOut.uvBloom - vAxisOffset * Offsets[0] );
				VertexOut.uvBloom2_1 = float4(
						VertexOut.uvBloom + vAxisOffset * Offsets[1],
						VertexOut.uvBloom - vAxisOffset * Offsets[1] );
				VertexOut.uvBloom2_2 = float4(
						VertexOut.uvBloom + vAxisOffset * Offsets[2],
						VertexOut.uvBloom - vAxisOffset * Offsets[2] );
				VertexOut.uvBloom2_3 = float4(
						VertexOut.uvBloom + vAxisOffset * Offsets[3],
						VertexOut.uvBloom - vAxisOffset * Offsets[3] );

				return VertexOut;
			}
		]]
	}
}

PixelShader =
{
	TextureSampler BloomSource
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	MainCode PixelShader
	{
		Input = "VS_OUTPUT_BLOOM"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float3 color = PdxTex2DLod0( BloomSource, Input.uvBloom ).rgb * Weight0;

				color += Weights[0] * ( PdxTex2DLod0( BloomSource, Input.uvBloom2_0.xy ).rgb + PdxTex2DLod0( BloomSource, Input.uvBloom2_0.zw ).rgb );
				color += Weights[1] * ( PdxTex2DLod0( BloomSource, Input.uvBloom2_1.xy ).rgb + PdxTex2DLod0( BloomSource, Input.uvBloom2_1.zw ).rgb );
				color += Weights[2] * ( PdxTex2DLod0( BloomSource, Input.uvBloom2_2.xy ).rgb + PdxTex2DLod0( BloomSource, Input.uvBloom2_2.zw ).rgb );
				color += Weights[3] * ( PdxTex2DLod0( BloomSource, Input.uvBloom2_3.xy ).rgb + PdxTex2DLod0( BloomSource, Input.uvBloom2_3.zw ).rgb );

				return float4(color, 1.0);
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = no
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}


Effect bloom
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
}
