Includes = {
	"cw/camera.fxh"
	"cw/fullscreen_vertexshader.fxh"
	"jomini/jomini_fog_of_war.fxh"
}

PixelShader =
{
	MainCode PixelShaderFoWApply
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
		TextureSampler FogOfWarAlpha
		{
			Ref = JominiFogOfWar
			MagFilter = "Linear"
			MinFilter = "Linear"
			MipFilter = "Linear"
			SampleModeU = "Wrap"
			SampleModeV = "Wrap"
		}

		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float3 WorldSpacePos = WorldSpacePositionFromDepth( PdxTex2DLod0( DepthBuffer, Input.uv ).r, Input.uv );
				return ApplyFogOfWar( WorldSpacePos, FogOfWarAlpha );
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
