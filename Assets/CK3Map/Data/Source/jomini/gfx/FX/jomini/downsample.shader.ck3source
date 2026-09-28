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
	
	MainCode PixelShaderDownSampleBrightPass
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float4 vColor = PdxTex2DLod0( MainScene, Input.uv );
				float vMax = max(0, max( max( vColor.r, vColor.g ), vColor.b ) - BrightThreshold );
				vMax /= (0.5 + vMax);

				float logLuminance = log(max(0.0, dot(vColor.rgb, LUMINANCE_VECTOR)) + 0.0001f);

				return float4( vColor.rgb * vMax, logLuminance );
			}
		]]
	}
	
	MainCode PixelShaderDownsample
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				// TODO, pretty sure this is not working correctly when w/h % 2 =! 0
				float4 Color = PdxTex2DLod0( MainScene, Input.uv * UVScale );
				//return float4(0.2, 0, 0, 1);
				return Color;
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


Effect downsamplebrightpass
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderDownSampleBrightPass"
}

Effect downsample
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderDownsample"
}