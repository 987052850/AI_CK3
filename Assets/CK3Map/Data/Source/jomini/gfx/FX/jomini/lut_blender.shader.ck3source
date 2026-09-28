Includes = {
	"jomini/posteffect_base.fxh"
}


PixelShader =
{
	TextureSampler BaseLUT1
	{
		Index = 0
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler BaseLUT2
	{
		Index = 1
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler BlendLUT1
	{
		Index = 2
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler BlendLUT2
	{
		Index = 3
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	
	ConstantBuffer( 0 )
	{
		float vBlendFactor1;
		float vBlendFactor2;
	};

	MainCode PixelShader
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float3 base = PdxTex2DLod0( BaseLUT1, Input.uv ).rgb;
				float3 blend = PdxTex2DLod0( BlendLUT1, Input.uv ).rgb;

			#ifdef DOUBLE_BLEND
				base = lerp(base, PdxTex2DLod0( BaseLUT2, Input.uv ).rgb, vBlendFactor1);
				blend = lerp(blend, PdxTex2DLod0( BlendLUT2, Input.uv ).rgb, vBlendFactor1);
			#endif

				//return float4( 1, 0, 0, 1 );
				return float4( lerp(base, blend, vBlendFactor2), 1 );
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = no
	WriteMask = "RED|GREEN|BLUE"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}

Effect LutBlend1
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"
}

Effect LutBlend2
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"

	Defines = { "DOUBLE_BLEND" }
}
