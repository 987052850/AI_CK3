Includes = {
	"cw/utility.fxh"
	"cw/fullscreen_vertexshader.fxh"
	"cw/camera.fxh"
	"jomini/posteffect_base.fxh"
}


ConstantBuffer( JominiDofConstants )
{
	int _SampleCount;
	float _BaseRadius;
	float _BlurBlendMin;
	float _BlurBlendMax;

	float _BlurMin;
	float _BlurMax;
	float _BlurScale;
	float _BlurExponent;
	float _HeightMin;
	float _HeightMax;

	float2 _DownSampledResolution;
	float2 _InvDownSampledResolution;
	
	float _FocusDepth;
};

PixelShader = 
{
	Code
	[[
		// Implements https://blog.tuxedolabs.com/2018/05/04/bokeh-depth-of-field-in-single-pass.html
		// Simulates a camera with depth of field to blur unfocused areas of an image

		static const float GOLDEN_ANGLE = 2.39996323; 

		// Caluclate pixel Coc
		// FocusDepth is the depth-distance to keep in focus (usually middle of what the camera sees). 
		// BlurScale can be used to modify the size of the blurred area.
		float GetCoc( float Depth, float FocusDepth, float BlurScale )
		{
			float Coc = clamp( ( 1.0f / FocusDepth - 1.0f / Depth ) * BlurScale, -1.0f, 1.0f );
			return abs( Coc ) * _SampleCount;
		}

		// Calculate average Coc
		float GetAverageCoc( float2 UV, float2 PixelSize, float2 ScreenRes, float FocusDepth, float BlurScale )
		{
			float CenterDepth = GetViewSpaceDepth( UV, ScreenRes );
			float CenterSize = GetCoc( CenterDepth, FocusDepth, BlurScale );
			float CocAverage = CenterSize;

			float Total = 1.0f;
			float Radius = _BaseRadius;
			float Angle = 0.0f;
			for ( int Sample = 0; Sample < _SampleCount; Sample++ )
			{
				float2 SampleUV = UV + float2( cos( Angle ), sin( Angle ) ) * PixelSize * Radius;
				float SampleDepth = GetViewSpaceDepth( SampleUV, ScreenRes );
				float SampleCoc = GetCoc( SampleDepth, FocusDepth, BlurScale );

				if ( SampleDepth > CenterDepth )
				{
					SampleCoc = clamp( SampleCoc, 0.0f, CenterSize * 2.0f );
				}

				float Contribution = smoothstep( Radius - 0.5f, Radius + 0.5f, SampleCoc );
				CocAverage += lerp( CocAverage / Total, SampleCoc, Contribution );
				Total += 1.0f;   
				Radius += _BaseRadius / Radius;
				Angle += GOLDEN_ANGLE;
			}
					
			return CocAverage /= Total;
		}

		// Dof - Full calculation
		// Can be used without additional renderpasses
		float3 DepthOfField( float2 UV, float2 PixelSize, float2 ScreenRes, float FocusDepth, float BlurScale, PdxTextureSampler2D MainScene )
		{
			float CenterDepth = GetViewSpaceDepth( UV, ScreenRes );
			float CenterCoc = GetCoc( CenterDepth, FocusDepth, BlurScale );
			float3 Color = PdxTex2DLod0( MainScene, UV ).rgb;

			float Total = 1.0f;
			float Radius = _BaseRadius;
			float Angle = 0.0f;
			for ( int Sample = 0; Sample < _SampleCount; Sample++ )
			{
				float2 SampleUV = UV + float2( cos( Angle ), sin( Angle ) ) * PixelSize * Radius;
				float3 SampleColor = PdxTex2DLod0( MainScene, SampleUV ).rgb;
				float SampleDepth = GetViewSpaceDepth( SampleUV, ScreenRes );
				float SampleCoc = GetCoc( SampleDepth, FocusDepth, BlurScale );

				if ( SampleDepth > CenterDepth )
				{
					SampleCoc = clamp( SampleCoc, 0.0f, CenterCoc * 2.0f );
				}
		
				float Contribution = smoothstep( Radius - 0.5f, Radius + 0.5f, SampleCoc );
				Color += lerp( Color / Total, SampleColor, Contribution );
				Total += 1.0f;   
				Radius += _BaseRadius / Radius;
				Angle += GOLDEN_ANGLE;
			}
		
			return Color /= Total;
		}

		// Dof - Sampled Coc, prepass required with coc data in the alpha channel
		float3 DepthOfField( float2 UV, float2 PixelSize, float2 ScreenRes, PdxTextureSampler2D MainScene )
		{
			float CenterDepth = GetViewSpaceDepth( UV, ScreenRes );
			float CenterCoc = PdxTex2DLod0( MainScene, UV ).a;
			float3 Color = PdxTex2DLod0( MainScene, UV ).rgb;
		
			float Total = 1.0f;
			float Radius = _BaseRadius;
			float Angle = 0.0f;
			for ( int Sample = 0; Sample < _SampleCount; Sample++ )
			{
				float2 SampleUV = UV + float2( cos( Angle ), sin( Angle ) ) * PixelSize * Radius;
				float3 SampleColor = PdxTex2DLod0( MainScene, SampleUV ).rgb;
				float SampleCoc = PdxTex2DLod0( MainScene, SampleUV ).a;

				float SampleDepth = GetViewSpaceDepth( SampleUV, ScreenRes );
				if ( SampleDepth > CenterDepth )
				{
					SampleCoc = clamp( SampleCoc, 0.0f, CenterCoc * 2.0f );
				}
				
				float Contribution = smoothstep( Radius - 0.5f, Radius + 0.5f, SampleCoc );	
				Color += lerp( Color / Total, SampleColor, Contribution );
				Total += 1.0f;   
				Radius += _BaseRadius / Radius;
				Angle += GOLDEN_ANGLE;
			}

			return Color /= Total;
		}

	]]
}

