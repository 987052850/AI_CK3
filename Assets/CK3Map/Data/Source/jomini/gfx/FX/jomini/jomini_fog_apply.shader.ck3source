Includes = {
	"cw/camera.fxh"
	"cw/fullscreen_vertexshader.fxh"
	"jomini/jomini_fog.fxh"
}

PixelShader =
{
	MainCode PixelShaderFogApply
	{
		TextureSampler DepthBuffer
		{
			Ref = PdxTexture0
			MagFilter = "Point"
			MinFilter = "Point"
			MipFilter = "Point"
			SampleModeU = "Clamp"
			SampleModeV = "Clamp"
		}

		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[			
			PDX_MAIN
			{				
				float3 WorldSpacePos = WorldSpacePositionFromDepth( PdxTex2DLod0( DepthBuffer, Input.uv ).r, Input.uv );
				float FogFactor = CalculateDistanceFogFactor( WorldSpacePos );

				return float4( FogColor, FogFactor );
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = yes
	SourceBlend = "src_alpha"
	DestBlend = "inv_src_alpha"
	WriteMask = "RED|GREEN|BLUE"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}


Effect FogApply
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderFogApply"
}