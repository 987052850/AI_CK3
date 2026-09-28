Includes = {
	"cw/fullscreen_vertexshader.fxh"
}

PixelShader =
{
	MainCode PixelShaderSSAOApply
	{
		TextureSampler SSAOTexture
		{
			Ref = SSAO
			MagFilter = "Point"
			MinFilter = "Point"
			MipFilter = "Point"
			SampleModeU = "Clamp"
			SampleModeV = "Clamp"
		}

		TextureSampler SSAOColor
		{
			Ref = JominiSSAOColor
			MagFilter = "Linear"
			MinFilter = "Linear"
			MipFilter = "Linear"
			SampleModeU = "Wrap"
			SampleModeV = "Wrap"
		}

		TextureSampler SSAOColorMultisampled
		{
			Ref = JominiSSAOColorMultisampled
			MagFilter = "Linear"
			MinFilter = "Linear"
			MipFilter = "Linear"
			SampleModeU = "Wrap"
			SampleModeV = "Wrap"
			MultiSampled = yes
		}
	
		ConstantBuffer( PdxConstantBuffer0 )
		{
			float 	SSAOBlendFactor;
			int 	MultisampledColorSampleCount;
			float2	Resolution;
		};

		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[		
			PDX_MAIN
			{	
			float SSAO = PdxTex2DLod0( SSAOTexture, Input.uv ).r;
			
			#ifdef DEBUG
				float v = lerp( 1.0, SSAO, SSAOBlendFactor );
				return float4( vec3( v ), 1.0 );
			#else
				float SSAOSample = saturate( ( 1.0 - SSAO ) * SSAOBlendFactor );

				#if defined( COLOR )
					float4 ColorSample = PdxTex2D( SSAOColor, Input.uv );

					float SSAOFactor = SSAOSample * ColorSample.a;
					return float4( ( ColorSample.rgb * SSAOFactor ) + ( vec3( 1.0f ) - vec3( SSAOFactor ) ), 1.0f );
				#elif defined( MULTISAMPLED_COLOR )
					int2 PixelIndex = int2( Input.uv * Resolution );
					float4 ColorSample = vec4( 0.0f );
					for( int i = 0; i < MultisampledColorSampleCount; ++i )
					{
						ColorSample += PdxTex2DMultiSampled( SSAOColorMultisampled, PixelIndex, i );
					}
					ColorSample /= MultisampledColorSampleCount;

					float SSAOFactor = SSAOSample * ColorSample.a;
					return float4( ( ColorSample.rgb * SSAOFactor ) + ( vec3( 1.0f ) - vec3( SSAOFactor ) ), 1.0f );
				#else
					return float4( vec3( 0.0f ), SSAOSample );
				#endif
			#endif
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

BlendState BlendStateColor
{
	BlendEnable = yes
	SourceBlend = "dest_color"
	DestBlend = "zero"
	WriteMask = "RED|GREEN|BLUE"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}

Effect SSAOApply
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderSSAOApply"
}

Effect SSAOApplyColor
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderSSAOApply"
	Defines = { "COLOR" }
	BlendState = BlendStateColor
}

Effect SSAOApplyColorMultisampled
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderSSAOApply"
	Defines = { "MULTISAMPLED_COLOR" }
	BlendState = BlendStateColor
}

Effect SSAOApplyDebug
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderSSAOApply"
	Defines = { "DEBUG" }
}
