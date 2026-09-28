Code
[[
	int SampleProvinceId( float2 Coord, PdxTextureSampler2D IndirectionMap )
	{
		const float2 Normalized = PdxTex2D( IndirectionMap, Coord ).rg;
		const int2 Absolute = int2( Normalized * 255.0 + 0.5 );
		return int( Absolute.x + Absolute.y * 256.0 );
	}

	float4 ColorSample( float2 Coord, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap )
	{
		float2 ColorIndex = PdxTex2D( IndirectionMap, Coord ).rg;
		return PdxTex2DLoad0( ColorMap, int2( ColorIndex * 255.0 + vec2(0.5f) ) );
	}

	float4 ColorSampleAtOffset( float2 Coord, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap, float2 Offset )
	{
		float2 ColorIndex = PdxTex2D( IndirectionMap, Coord ).rg;
		return PdxTex2DLoad0( ColorMap, int2( ColorIndex * 255.0 + vec2(0.5) + ( Offset ) ) );
	}
    
	float4 BilinearColorSample( float2 Coord, float2 TextureSize, float2 InvTextureSize, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap )
	{
		float2 Pixel = Coord * TextureSize + 0.5;
		
		float2 FracCoord = frac(Pixel);
		Pixel = floor(Pixel) / TextureSize - InvTextureSize / 2.0;
	
		float4 C11 = ColorSample( Pixel, IndirectionMap, ColorMap );
		float4 C21 = ColorSample( Pixel + float2( InvTextureSize.x, 0.0), IndirectionMap, ColorMap );
		float4 C12 = ColorSample( Pixel + float2( 0.0, InvTextureSize.y), IndirectionMap, ColorMap );
		float4 C22 = ColorSample( Pixel + InvTextureSize, IndirectionMap, ColorMap );
	
		float4 x1 = lerp(C11, C21, FracCoord.x);
		float4 x2 = lerp(C12, C22, FracCoord.x);
		return lerp(x1, x2, FracCoord.y);
	}

	float4 BilinearColorSampleAtOffset( float2 Coord, float2 TextureSize, float2 InvTextureSize, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap, float2 TextureOffset )
	{
		float2 Pixel = ( Coord * TextureSize + 0.5 );
		
		float2 FracCoord = frac(Pixel);
		Pixel = floor(Pixel) / TextureSize - InvTextureSize / 2.0;
	
		float4 C11 = ColorSampleAtOffset( Pixel, IndirectionMap, ColorMap, TextureOffset );
		float4 C21 = ColorSampleAtOffset( Pixel + float2( InvTextureSize.x, 0.0), IndirectionMap, ColorMap, TextureOffset );
		float4 C12 = ColorSampleAtOffset( Pixel + float2( 0.0, InvTextureSize.y), IndirectionMap, ColorMap, TextureOffset );
		float4 C22 = ColorSampleAtOffset( Pixel + InvTextureSize, IndirectionMap, ColorMap, TextureOffset );
	
		float4 x1 = lerp(C11, C21, FracCoord.x);
		float4 x2 = lerp(C12, C22, FracCoord.x);
		return lerp(x1, x2, FracCoord.y);
	}
	

	float CalculateStripeMask( in float2 UV, float Offset )
	{
		// diagonal
		float t = 3.14159 / 8.0;
		float w = 12000;			  // larger value gives smaller width
		
		float StripeMask = cos( ( UV.x * cos( t ) * w ) + ( UV.y * sin( t ) * w ) + Offset ); 
		StripeMask = smoothstep(0.0, 1.0, StripeMask * 2.2f );
		return StripeMask;
	}	
	
	void ApplyDiagonalStripes( inout float3 BaseColor, float3 StripeColor, float StripeAlpha, float2 WorldSpacePosXZ )
	{
		float Mask = CalculateStripeMask( WorldSpacePosXZ, 0.f );
		float OffsetMask = CalculateStripeMask( WorldSpacePosXZ, -0.5f );
		float Shadow = 1 - saturate( Mask - OffsetMask ) ;
		Mask *= StripeAlpha;
		BaseColor = lerp( BaseColor, BaseColor * Shadow, StripeAlpha );
		BaseColor = lerp( BaseColor, StripeColor.rgb, Mask );
	}
	
	void ApplyDiagonalStripes( inout float4 BaseColor, float4 StripeColor, float ShadowAmount, float2 WorldSpacePosXZ )
	{
		float Mask = CalculateStripeMask( WorldSpacePosXZ, 0.0f );
		float OffsetMask = CalculateStripeMask( WorldSpacePosXZ, -0.5f );
		float Shadow = 1.0f - saturate( Mask - OffsetMask );
		Mask *= StripeColor.a;
		BaseColor.rgb = lerp( BaseColor.rgb, BaseColor.rgb * Shadow, Mask * ShadowAmount );
		BaseColor = lerp( BaseColor, StripeColor, Mask );
	}
]]