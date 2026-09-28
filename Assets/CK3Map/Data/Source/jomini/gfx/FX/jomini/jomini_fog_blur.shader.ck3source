
Includes = {
	"cw/utility.fxh"
	"cw/camera.fxh"
	"jomini/posteffect_base.fxh"
	"jomini/jomini_fog_blur_variable.fxh"
	"jomini/jomini.fxh"
}

PixelShader =
{
	TextureSampler MainScene
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
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 UV = Input.uv * 2.0f;
				float4 Color = PdxTex2DLod0( MainScene, UV );
				Color.a = 0.0f;
				float ZoomBlendOut =  lerp( 1.0f, 0.0f, saturate( ( CameraPosition.y - BlurBeginHeight ) / ( BlurEndHeight - BlurBeginHeight ) ) );
				if( ZoomBlendOut > 0.0f )
				{
					float Depth = GetViewSpaceDepth( UV, ScreenResolution );
					if( Depth < FogBlurSkipValue )
					{
						return Color;
					}

					float4  NormalizedDeviceCoordinates = float4( UV * 2.0f - 1.0f, 1.0f, 1.0f );
					float4 ViewRay = mul( InvProjectionMatrix, NormalizedDeviceCoordinates );
					ViewRay /= ViewRay.w;
					float3 ViewDir = normalize( ViewRay.xyz ); 
					float3 ViewPos = ViewDir * Depth;
					float3 Diff = -ViewPos;

					float VSqDistance = dot( Diff, Diff );
					float FogFactor = 1.0f - abs( normalize( Diff ).y );

					float Min = min( ( VSqDistance - FogBegin2 ) / ( FogEnd2 - FogBegin2 ), FogMax );
					float BaseFogFactor = saturate( Min * FogFactor);
					BaseFogFactor = lerp( DepthMin, DepthMax, BaseFogFactor  * DepthScale );
					BaseFogFactor = min( 1.0f, BaseFogFactor );
					if( BaseFogFactor < FogBlurSkipValue )
					{
						return Color;
					}

					float3 Scattering = vec3( 0.0f );
					for( int i = 0; i < BlurSampleCount; i++ )
					{
						float2 Offset = float2( cos( i * 0.39f ), sin( i * 0.39f ) ) * BlurSampleRadius * ( 1 + BaseFogFactor * DepthSampleOffset );
						float3 SampleColor = PdxTex2D( MainScene, UV + Offset ).rgb;
						Scattering += SampleColor ;
					}

					Scattering /= BlurSampleCount;
					Scattering *= BlurTint;

					Color.rgb = lerp( Color.rgb, Scattering, BaseFogFactor );
					Color.a = BaseFogFactor;

					#ifdef DEBUG_DEPTH_MASK
						Color.rgb = BaseFogFactor;
						Color.a = 1.0f;
					#endif

				}
				return Color;
			}
		]]
	}
}


Effect FogBlur
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"
}

#// MSAA 
Effect FogBlurMsaa
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"

	Defines = { "MULTI_SAMPLED" }
}