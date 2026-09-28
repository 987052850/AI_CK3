Includes = {
	"jomini/posteffect_base.fxh"
}


PixelShader =
{
	TextureSampler MainScene
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	
	ConstantBuffer( 2 )
	{
		float2 UVScale; // For mapping portion of a texture to whole viewport
	};
	
	MainCode PixelShaderCombine
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float4 Color = PdxTex2DLod0( MainScene, Input.uv * UVScale );
				return Color;
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = yes
	SourceBlend = "ONE"
	DestBlend = "ONE"
	WriteMask = "RED|GREEN|BLUE|ALPHA"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}


Effect combine
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderCombine"
}
