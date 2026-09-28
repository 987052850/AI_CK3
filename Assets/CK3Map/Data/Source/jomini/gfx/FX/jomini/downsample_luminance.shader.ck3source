Includes = {
	"jomini/posteffect_base.fxh"
}


VertexShader =
{
	ConstantBuffer( 2 )
	{
		float2 UVScale;
	};

	MainCode VertexShader
	{
		Input = "VS_INPUT_FULLSCREEN"
		Output = "VS_OUTPUT_FULLSCREEN"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_FULLSCREEN VertexOut = FullscreenVertexShader( Input );
				
			#ifdef UV_SCALE
				VertexOut.uv *= UVScale;
			#endif
			
				return VertexOut;
			}
		]]
	}
}

PixelShader =
{
	TextureSampler Scene
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler LastLuminance
	{
		Index = 1
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	
	ConstantBuffer( 3 )
	{
		float2 GatherSize;
		float2 PixelSize;
		float TauDeltaTime;
		float MinHdr
		float MaxHdr;
	};

	MainCode PixelShaderDownsample
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
			#ifdef LUMINANCE_SAMPLE_ALPHA
				float vLogLuminance = PdxTex2DLod0( Scene, Input.uv ).a;
			#else
				float vLogLuminance = PdxTex2DLod0( Scene, Input.uv ).r;
			#endif

				return float4( vLogLuminance, 0.0, 0.0, 1.0 );
			}
		]]
	}

	MainCode PixelShaderGather
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			float CalculateAdaptedLuminance(float vCurrentLuminance)
			{
				float vLastLuminance = PdxTex2DLod0(LastLuminance, vec2(0.5)).r;
				float vAdaptedLum = vLastLuminance + (vCurrentLuminance - vLastLuminance) * (1.0 - exp(-TauDeltaTime));

				return vAdaptedLum;
			}
		
			PDX_MAIN
			{
				float2 baseOffset = PixelSize * 0.5;

				float vSum = 0.0;
				float v = baseOffset.y;
				for (int y = 0; y < GatherSize.y; ++y)
				{
					float u = baseOffset.x;
					for (int x = 0; x < GatherSize.x; ++x)
					{
						vSum += PdxTex2DLod0( Scene, float2(u, v) ).r;

						u += PixelSize.x;
					}

					v += PixelSize.y;
				}

				vSum /= GatherSize.x * GatherSize.y;

				float vCurrentLuminance = clamp(exp(vSum), MinHdr, MaxHdr);
				//float vCurrentLuminance = exp(vSum);
				return float4( CalculateAdaptedLuminance(vCurrentLuminance), 0.0, 0.0, 1.0 );
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


Effect LuminanceDownsample
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderDownsample"
	
	Defines = { "UV_SCALE" }
}

Effect LuminanceGather
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderGather"
}
