Includes = {
	"cw/fullscreen_vertexshader.fxh"
	"cw/camera.fxh"
	"jomini/jomini.fxh"
}

PixelShader =
{
	MainCode PixelShader
	{
		TextureSampler EnvironmentMap
		{
			Ref = JominiEnvironmentMap
			MagFilter = "Linear"
			MinFilter = "Linear"
			MipFilter = "Linear"
			SampleModeU = "Wrap"
			SampleModeV = "Wrap"
			Type = "Cube"
		}
	
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[		
			PDX_MAIN
			{
				float3 WorldSpacePos = WorldSpacePositionFromDepth( 1.0, Input.uv );
				float3 Direction = normalize( WorldSpacePos - CameraPosition );
				float3 RotatedDirection = mul( CastTo3x3( CubemapYRotation ), Direction );
				
				float3 CubemapSample = PdxTexCube( EnvironmentMap, RotatedDirection ).rgb * CubemapIntensity;
				
				return float4( CubemapSample, 1.0 );
			}
		]]
	}
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}


Effect Cubemap
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"
}