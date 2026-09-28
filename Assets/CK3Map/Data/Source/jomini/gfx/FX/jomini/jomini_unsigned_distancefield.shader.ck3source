Includes = {
	"jomini/jomini_colormap_constants.fxh"
}

VertexStruct VS_OUTPUT
{
    float4 position			: PDX_POSITION;
};
	
VertexShader = {

	VertexStruct VS_INPUT
	{
		float2 position	: POSITION;
	};
	
	MainCode VS_standard
	{
		Input = "VS_INPUT"
		Output = "VS_OUTPUT"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT VertexOut;
				VertexOut.position = float4( Input.position, 0.0, 1.0 );

				return VertexOut;
			}
		]]
	}
}

PixelShader =
{
	TextureSampler DeltaVectors
	{
		Ref = PdxTexture0
		MagFilter = "point"
		MinFilter = "point"
		MipFilter = "point"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}

	TextureSampler ProvinceColorIndirectionTexture
	{
		Ref = JominiProvinceColorIndirection
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler ProvinceColorTexture
	{
		Ref = JominiProvinceColor
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
}

ConstantBuffer( 0 )
{
	float3 	SampleOffset;
	float 	MaxSearchDist;
	
	int		WildCardSampleCount;
	float	WildCardSampleWidth;
	int		WildCardColorsCount;
	float4 	WildCardColors[4];
};

PixelShader =
{
	Code
	[[
		// This implementation is not trivial to understand without guidance. 
		// Luckily, it's based on a paper, see the header file corresponding to this shader for more info.

		// If CJominiMap::_WrapX or CJominiMap::_WrapY is true this shader will compile with JOMINI_UNSIGNED_DISTANCE_FIELD_WRAP_X/Y respectively.
		// Idealy we want to change SampleModeU/V but since that's not possible with the current tech we'll just clamp the uv-coordinates instead.
		float2 TexelPosToUV( float2 TexelPos, float2 TextureSize )
		{
			#ifndef JOMINI_UNSIGNED_DISTANCE_FIELD_WRAP_X
				TexelPos.x = clamp( TexelPos.x, 0.0f, TextureSize.x - 1.0f );
			#endif
			#ifndef JOMINI_UNSIGNED_DISTANCE_FIELD_WRAP_Y
				TexelPos.y = clamp( TexelPos.y, 0.0f, TextureSize.y - 1.0f );
			#endif
			return TexelPos / TextureSize;
		}
		float2 GetDeltaVector( float2 Coord ) 
		{
			return PdxTex2DLod0( DeltaVectors, TexelPosToUV( Coord, GradientTextureSize ) ).rg * MaxSearchDist;
		}
		float4 GetProvinceColor( float2 Position )
		{			
			float2 ColorIndex = PdxTex2DLod0( ProvinceColorIndirectionTexture, TexelPosToUV( Position, IndirectionMapSize ) ).rg;
			return PdxTex2DLoad0( ProvinceColorTexture, int2( ( ColorIndex * 255.0f ) + vec2(0.5f) ) );
		}
		float Diff( float4 lhs, float4 rhs )
		{
			float4 d = lhs - rhs;
			return dot( d, d );
		}
		float SameColor( float4 lhs, float4 rhs )
		{
			return step( Diff( lhs, rhs ), 0.0001f );
		}
		float DifferentColor( float4 lhs, float4 rhs )
		{
			return step( 0.0001f, Diff( lhs, rhs ) );
		}
		float IsWildCard( float4 Color )
		{
			float Result = 0.0f;
			for( int i = 0; i < WildCardColorsCount; ++i )
			{
				Result += SameColor( Color, WildCardColors[i] );
			}
			return Result;
		}
	]]
	
	MainCode PS_wildcards
	{
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 TexelAreaCenter = Input.position.xy * 4;

				// First, check the 4x4 vicinity of TexelAreaCenter
				float ContainsWildcard = 0.0f;
				float2 Offset = vec2( -1.5f );
				for( int y = 0; y < 4 && ContainsWildcard < 0.5f; ++y )
				{
					for( int x = 0; x < 4 && ContainsWildcard < 0.5f; ++x )
					{
						float2 Position = TexelAreaCenter + Offset + float2(x,y);
						float4 Color = GetProvinceColor( Position );
						ContainsWildcard += IsWildCard( Color );
					}
				}
				
				if( ContainsWildcard < 0.5f )
				{
					return vec4(0.0f);
				}
				
				//TODO: This sampling should be a lot smarter. Current solution will produce bad results in many cases
				//it is also quite slow
				float2 Samples[16];
				float step = 3.1416f / WildCardSampleCount;
				for( int i = 0; i < WildCardSampleCount; ++i )
				{
					float v = i * step;
					Samples[i] = float2( cos(v), sin(v) );
				}
				
				for( int i = 0; i < WildCardSampleCount; ++i )
				{
					float4 Color1 = GetProvinceColor( TexelAreaCenter + ( Samples[i] * WildCardSampleWidth ) );
					float4 Color2 = GetProvinceColor( TexelAreaCenter - ( Samples[i] * WildCardSampleWidth ) );
					
					if( IsWildCard( Color1 ) + IsWildCard( Color2 ) < 0.5f )
					{
						float Samesies = SameColor( Color1, Color2 );
						//Samesies += SameColor( Color1, CenterColor );
						//Samesies += SameColor( Color2, CenterColor );
						
						if( Samesies < 0.5f )
							return vec4(0);
					}
				}
				return vec4(1.0);
			}
		]]
	}
	MainCode PS_init
	{
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[
			float4 CalcMainColor( float4 A, float4 B, float4 C, float4 D )
			{
				float CD = SameColor( C, D );
				float BD = SameColor( B, D );
				float BC = SameColor( B, C );
				
				float4 Color = A;
				Color = lerp( Color, B, BD );
				Color = lerp( Color, B, BC );
				Color = lerp( Color, C, CD );
				
				return Color;
			}
			
			// Adds the "seeds" (i.e., in our case, the borders) to the distance field
			PDX_MAIN
			{
				#ifdef ENABLE_WILDCARDS
				if( GetDeltaVector( Input.position.xy ).x > 0.5f )
					return vec4(1.0);
				#endif
					
				// This shader is hardcoded to downsample x4 in both axes (i.e. each entry in the distance field contains 4x4 source texels).
				// Multiplying fragment position by 4 gives us the center of the corresponding texel area
				float2 TexelAreaCenter = Input.position.xy * 4;
				// Since TexelAreaCenter is squeezed inbetween 4 texels, we need to offset by -3.5 to get to the (middle of the) texel in 
				// the bottom right of our sample area.
				float2 Offset = vec2(-3.5);
				
				// PSSL compiler does not like this array and gives "not enough registers available for the entire program."
				// Lets keep it for other platforms since it gives slightly better performance
				#ifndef PDX_PROSPERO
					// To make sure no borders are missed, we need to check a 8x8 area around our TexelAreaCenter
					float4 ColorSamples[8*8];
					for( int y = 0; y < 8; ++y )
					{
						for( int x = 0; x < 8; ++x )
						{
							ColorSamples[x + ( y * 8 )] = GetProvinceColor( TexelAreaCenter + Offset + float2(x,y) );
						}
					}
				#endif
				
				// Main color deduced from the 2x2 source texels that lie closest to TexelAreaCenter
				float4 MainColor = CalcMainColor( 
				#ifdef PDX_PROSPERO
					GetProvinceColor( TexelAreaCenter + Offset + float2(3,3) ),
					GetProvinceColor( TexelAreaCenter + Offset + float2(4,3) ),
					GetProvinceColor( TexelAreaCenter + Offset + float2(3,4) ),
					GetProvinceColor( TexelAreaCenter + Offset + float2(4,4) )
				#else
					ColorSamples[3 + ( 3 * 8 )],
					ColorSamples[4 + ( 3 * 8 )],
					ColorSamples[3 + ( 4 * 8 )],
					ColorSamples[4 + ( 4 * 8 )]
				#endif
				);
				

				float CurrentMinDistSq = ( 2*8*8 ) + 1; // with 8x8 window, max distance (squared) is 8^2 * 8^2
				float2 ClosestBorderPointDefault = vec2( -1 ); // used to detect whether a border was found at all
				float2 ClosestBorderPoint = ClosestBorderPointDefault;
				
				for( int y = 0; y < 8; ++y )
				{
					for( int x = 0; x < 8; ++x )
					{
						#ifdef ENABLE_WILDCARDS
						float2 TexelCoord = TexelAreaCenter + Offset + float2( x, y );
						float IsMasked = GetDeltaVector( TexelCoord / 4 ).x;
						if( IsMasked < 0.5f )
						#endif
						{
							// using abs since we dont care about sign
							float2 SamplePoint = abs( float2(x, y) + Offset );
							float SampleDistanceSq = dot( SamplePoint, SamplePoint );
							
							#ifdef PDX_PROSPERO
								float IsBorder = DifferentColor( MainColor, GetProvinceColor( TexelAreaCenter + Offset + float2(x,y) ) );
							#else
								float IsBorder = DifferentColor( MainColor, ColorSamples[x + ( y * 8 )] );
							#endif
							float IsBorderAndCloser = IsBorder * step( SampleDistanceSq, CurrentMinDistSq );
							CurrentMinDistSq = lerp( CurrentMinDistSq, SampleDistanceSq, IsBorderAndCloser );
							ClosestBorderPoint = lerp( ClosestBorderPoint, SamplePoint, IsBorderAndCloser );
						}
					}
				}
				
				// If we didn't find any border point, return 1 (max), otherwise, return closest point normalized by MaxSearchDist
				return float4( ( all( ClosestBorderPoint == ClosestBorderPointDefault ) ? vec2(1) : ( ClosestBorderPoint / MaxSearchDist ) ), 0.0f, 1.0f );
			}		
		]]
	}
	MainCode PS_fill
	{
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[			
			float3 UpdateDeltaVector( float2 Coordinate, float2 Offset, float3 CurrentBest )
			{
				float3 Sample;
				Sample.xy = GetDeltaVector( Coordinate + Offset ) + abs( Offset*4 );
				Sample.z = dot( Sample.xy, Sample.xy );
				return Sample.z < CurrentBest.z ? Sample : CurrentBest;
			}
			PDX_MAIN
			{
				float3 BestSample;
				BestSample.xy = GetDeltaVector( Input.position.xy ).xy;
				BestSample.z = dot(BestSample.xy,BestSample.xy);
				
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.xx, BestSample );//TL
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.xy, BestSample );//T
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.xz, BestSample );//TR
				
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.yx, BestSample );//L
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.yz, BestSample );//R
				
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.zx, BestSample );//BL
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.zy, BestSample );//B
				BestSample = UpdateDeltaVector( Input.position.xy, SampleOffset.zz, BestSample );//BR
				
				return float4( BestSample.xy / MaxSearchDist, 0.0f, 0.0f );
			}		
		]]
	}
	MainCode PS_finalize
	{
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
			    float2 Vec = GetDeltaVector( Input.position.xy );
				float SquareLength = dot(Vec,Vec);
				return vec4( SquareLength <= 0.0f ? 1.0f : sqrt( SquareLength ) / MaxSearchDist );
			}		
		]]
	}
}


BlendState BlendState
{
	BlendEnable = no
}

RasterizerState RasterizerState
{
	#CullMode = "none"
	#FillMode = "wireframe"
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
}

Effect WildCardMask
{
	VertexShader = "VS_standard"
	PixelShader = "PS_wildcards"
}

Effect Init
{
	VertexShader = "VS_standard"
	PixelShader = "PS_init"
}

Effect InitWithWildcards
{
	VertexShader = "VS_standard"
	PixelShader = "PS_init"
	Defines = { "ENABLE_WILDCARDS" }
}

Effect Fill
{
	VertexShader = "VS_standard"
	PixelShader = "PS_fill"
}

Effect Finalize
{
	VertexShader = "VS_standard"
	PixelShader = "PS_finalize"
}
