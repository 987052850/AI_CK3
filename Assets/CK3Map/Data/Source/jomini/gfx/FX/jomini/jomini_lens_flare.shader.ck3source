Includes = {
	"jomini/jomini_lens_flare.fxh"
}

PixelShader =
{
	TextureSampler MainScene
	{
		Ref = PdxTexture0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler LensColor
	{
		Ref = PdxTexture1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Point"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}

	MainCode PixelShaderLensFlare
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				static const int NumGhosts = 8;

				float2 UV = vec2( 1.0f ) - Input.uv; //We flip the downsampled image to make it less apparent where we sampled
				float2 GhostVec = ( vec2( 0.5f ) - UV ) * _GhostDispersal;

				float3 ChromaticDistortion = float3( -InvDownSampleSize.x, 0.0f, InvDownSampleSize.x ) * _DistortionFactor;
				float2 ChromaticDirection = normalize( GhostVec );

				float4 vColor = vec4( 0.0f );

				for ( int i = 0; i < NumGhosts; ++i )
				{
					float2 Offset = frac( UV + GhostVec * float( i ) );
					float D = distance( Offset, vec2( 0.5f ) );
					float Weight = 1.0f - smoothstep( 0.0f, 0.25f, D ); //Fades out ghosts the further from the center that the sample is
					vColor.rgb += ChromaticSample( MainScene, Offset, ChromaticDirection, ChromaticDistortion ) * Weight;
				}

				// Apply lens color
				float2 LensColorUV = float2( length( vec2( 0.5f ) - UV ) / length( vec2( 0.5f ) ), 0.5 );
				vColor.rgb *= PdxTex2DLod0( LensColor, LensColorUV ).rgb;

				//// Apply lens halo
				float2 HaloVec = vec2( 0.5f ) - UV;
				HaloVec.x /= _HaloWidth;
				HaloVec = normalize( HaloVec );
				HaloVec.x *= _HaloWidth;
				float2 wuv = ( UV - float2( 0.5f, 0.0f ) ) / float2( _HaloWidth, 1.0f ) + float2( 0.5f, 0.0f );
				float d = distance( wuv, vec2( 0.5f ) );
				float HaloWeight = WindowCubic( d, _HaloRadius, _HaloPow );
				HaloVec *= _HaloRadius;

				vColor.rgb += ChromaticSample( MainScene, ( ( UV + HaloVec ) * HaloWeight ), ChromaticDirection, ChromaticDistortion * _DistortionFactorHalo ) * smoothstep( 0.0f, 0.95f, HaloWeight );
				return vColor;
			}
		]]
	}

	MainCode PixelShaderAnamorphicLensFlare
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR" 
		Code
		[[
			PDX_MAIN
			{
				float2 UV = Input.uv * float2( _LensToScreenScale, 1.0f );
				float2 DistortionVec = ( UV * normalize( float2( 0.0f, 1.0f ) ) );

				float3 ChromaticDistortion = float3( -InvDownSampleSize.x, 0.0f, InvDownSampleSize.x ) * _DistortionFactor;
				float2 ChromaticDirection = normalize( DistortionVec );

				float4 vColor = vec4( 0.0f );

				vColor.rgb += ChromaticSample( MainScene, UV, ChromaticDirection, ChromaticDistortion * _DistortionFactor );

				return vColor;
			}
		]]
	}
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}

RasterizerState RasterizerState
{
}

Effect LensFlare
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderLensFlare"
}

Effect AnamorphicLensFlare
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShaderAnamorphicLensFlare"
}
