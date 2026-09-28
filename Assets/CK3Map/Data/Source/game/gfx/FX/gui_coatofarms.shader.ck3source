Includes = {
	"cw/pdxgui.fxh"
	"cw/pdxgui_sprite_base.fxh"
	"cw/pdxgui_sprite_textures.fxh"
}

ConstantBuffer( 2 )
{
	float3 HighlightColor;
	float4 CoALeftTop_WidthHeight;
	float4 CoAOffset_Scale;
};

VertexStruct VS_OUTPUT_COA
{
	float4 Position		: PDX_POSITION;
	float2 UV			: TEXCOORD0;
	float2 RelativePos	: TEXCOORD1;
	float4 Color		: COLOR;
	float2 TexelSize	: TEXCOORD2;
};

VertexShader =
{
	MainCode VertexShader
	{
		Input = "VS_INPUT_PDX_GUI"
		Output = "VS_OUTPUT_COA"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_COA Out;
				float2 PixelPos = WidgetLeftTop + Input.LeftTop_WidthHeight.xy + Input.Position * Input.LeftTop_WidthHeight.zw;
				Out.Position = PixelToScreenSpace( PixelPos );
				Out.UV.xy = Input.Position;
				Out.TexelSize = 1.0 / Input.LeftTop_WidthHeight.zw;
				Out.Color = Input.Color;
				Out.RelativePos = Input.Position;
				
				return Out;
			}
		]]
	}
}


PixelShader =
{
	TextureSampler Mask
	{
		Ref = PdxTexture0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	TextureSampler CoA
	{
		Ref = PdxTexture1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	MainCode PixelShader
	{
		Input = "VS_OUTPUT_COA"
		Output = "PDX_COLOR"
		Code
		[[
			float2 fade( float2 t )
			{ 
				return t * t * ( 3.0f - 2.0f * t );
			}

			float valueNoise( float2 p )
			{
				float2 i = floor( p );
				float2 f = frac( p );
				float a = frac( sin( dot( i + float2( 0, 0 ), float2( 127.1f, 311.7f ) ) ) * 43758.5453f );
				float b = frac( sin( dot( i + float2( 1, 0 ), float2( 127.1f, 311.7f ) ) ) * 43758.5453f );
				float c = frac( sin( dot( i + float2( 0, 1 ), float2( 127.1f, 311.7f ) ) ) * 43758.5453f );
				float d = frac( sin( dot( i + float2( 1, 1 ), float2( 127.1f, 311.7f ) ) ) * 43758.5453f );

				float2 u = fade( f );
				return lerp( lerp( a, b, u.x ), lerp( c, d, u.x ), u.y );
			}

			float fbm( float2 p )
			{
				float f = 0.0f;
				float amp = 0.5f;
				for ( int i = 0; i < 4; i++ )
				{
					f += valueNoise( p ) * amp;
					p *= 2.0f;
					amp *= 0.5f;
				}
				return f;
			}

			float roundedBoxSDF( float2 p, float2 b, float r )
			{
				float2 q = abs( p ) - b + r;
				return length( max( q, 0.0f ) ) - r;
			}
			
			float circleSDF( float2 p, float Radius ) {
				return length( p ) - Radius;
			}
			PDX_MAIN
			{	
				float2 UV = ((Input.UV.xy - 0.5) / CoAOffset_Scale.zw + 0.5) + CoAOffset_Scale.xy;

				// Clamping to make sure UV outside of 0.0-1.0 repeat at CoA edges, and not sample the next CoA in the Atlas
				UV = clamp(UV, Input.TexelSize / 2, float2(1.0, 1.0) - Input.TexelSize / 2);

				UV = CoALeftTop_WidthHeight.xy + UV.xy * CoALeftTop_WidthHeight.zw;

				float4 OutColor = PdxTex2D( CoA, UV.xy );
				#if defined( SEAL )
					float4 background = PdxTex2D( Mask, Input.RelativePos );
					float3 SealPattern = OutColor.rgb;
					const float SealPatternOffset = 0.0004;	// The larger the number, the thicker the outlines. Also depends on the size of CoA used
					const float SealPatternStrength = 2.7;	// The larger the number, the more contrast there is in the seal pattern
					SealPattern += PdxTex2D( CoA, UV.xy - float2( SealPatternOffset, SealPatternOffset ) ).xyz * SealPatternStrength;
					SealPattern -= PdxTex2D( CoA, UV.xy + float2( SealPatternOffset, SealPatternOffset ) ).xyz * SealPatternStrength;
					SealPattern = .7 + SealPattern * .7;	// The pattern is pretty dark at this point, this offsets it towards white and squishes the contrast a bit. A good choice of these values depends on the seal background texture used.
					SealPattern = clamp( DisableColor( SealPattern ), .0, 1.7 );	// This clamps the seal pattern values. It doesn't clamp it to 1.0 because it's ok if it makes the seal background brighter in some parts.  A good choice of these values depends on the seal background texture used.

					OutColor = float4( SealPattern, Input.Color.a );

					OutColor *= background;
				#elif defined( RED_INK_STAMP )
					// Generate the base stamp pattern
					float Luma = dot( OutColor.rgb, float3( 0.299f, 0.787f, 0.114f ) );
					float MaskBright = smoothstep( 0.304f, 0.781f, Luma );
					float MaskDark = smoothstep( 0.722f, 0.963f, 1.0f - Luma );
					float StampPattern = max( MaskBright, MaskDark );

					// Apply color variation for a more natural look
					float3 BaseRed = float3( 0.78f, 0.15f, 0.14f );
					float RedNoise = fbm( Input.UV * 20.0f );
					float3 RedColor = BaseRed * lerp( 0.7f, 1.3f, RedNoise );
					float3 BackgroundColor = vec3( 0.0f );
					float3 Result = lerp( BackgroundColor, RedColor, StampPattern );
					
					// Add a border around the stamp
					float BorderSize = 0.05f;
					float CornerRadius = 0.074f;
					float2 CenteredUV = Input.UV - 0.5f;
					float2 BoxHalfSize = 0.5f - BorderSize;
					float BorderWidth = 0.055f;
					#ifdef RED_INK_CIRCLE_STAMP
						// Apply a fadeout mask for smooth edges
						float FadeoutMaskDist = circleSDF( CenteredUV, 1.349f );
						float FadeoutMask = smoothstep( 0.03f, 0.064, -abs( FadeoutMaskDist ) + 0.95f );
						Result = lerp( Result, BackgroundColor, FadeoutMask );

						float Dist = circleSDF( CenteredUV, 0.48 );
						float IsBorder = smoothstep( 0.03f, 0.055f, -abs( Dist ) + BorderWidth );
						Result = lerp( Result, RedColor, IsBorder );
					#else
						// Apply a fadeout mask for smooth edges
						float FadeoutMaskDist = roundedBoxSDF( CenteredUV, 1.385f, 1.0f);
						float FadeoutMask = smoothstep( 0.03f, 0.064, -abs( FadeoutMaskDist ) + 1.011f );
						Result = lerp( Result, BackgroundColor, FadeoutMask );

						float Dist = roundedBoxSDF(CenteredUV, BoxHalfSize, CornerRadius);
						float IsBorder = smoothstep( 0.03f, 0.055f, -abs( Dist ) + BorderWidth );
						Result = lerp( Result, RedColor, IsBorder );
					#endif
					// Introduce surface imperfections for realism
					float EdgeDist = min( min( Input.UV.x, 1.0f - Input.UV.x ), min( Input.UV.y, 1.0f - Input.UV.y ) );
					float EdgeFactor = smoothstep( 0.0f, BorderSize * 30.0f, EdgeDist );
					float Imperfection = smoothstep( 0.185f, 0.511f, RedNoise + EdgeFactor * 0.309f );
					Result = lerp( BackgroundColor, Result, Imperfection );

					float4 Background = PdxTex2D( Mask, Input.RelativePos );
					OutColor = float4( Result, Input.Color.a );
					OutColor *= Background.a;
					OutColor.a = OutColor.r;
				#else
					float mask = PdxTex2D( Mask, Input.RelativePos ).a;
					OutColor *= mask;
					OutColor *= Input.Color;
				#endif
				
 				ApplyModifyTextures( OutColor, Input.UV.xy );

				#ifdef DISABLED
					OutColor.rgb = DisableColor( OutColor.rgb );
				#endif

				return OutColor;
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
}

Effect PdxGuiDefault
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
}

Effect PdxGuiDefaultDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "DISABLED" }
}

Effect PdxGuiPreMultipliedAlpha
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	BlendState = PreMultipliedAlpha
}

Effect PdxGuiPreMultipliedAlphaDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	BlendState = PreMultipliedAlpha
	
	Defines = { "DISABLED" }
}

Effect PdxGuiSeal
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "SEAL" }
}

Effect PdxGuiSealDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "DISABLED" "SEAL" }
}

Effect PdxGuiRedInkSquareStamp
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "RED_INK_STAMP" }
}

Effect PdxGuiRedInkSquareStampDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "DISABLED" "RED_INK_STAMP" }
}

Effect PdxGuiRedInkCircleStamp
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "RED_INK_STAMP" "RED_INK_CIRCLE_STAMP" }
}

Effect PdxGuiRedInkCircleStampDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShader"
	
	Defines = { "DISABLED" "RED_INK_STAMP" "RED_INK_CIRCLE_STAMP" }
}