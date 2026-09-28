Code
[[
	int SampleProvinceId( float2 Coord, PdxTextureSampler2D IndirectionMap )
	{
		const float2 Normalized = PdxTex2D( IndirectionMap, Coord ).rg;
		const int2 Absolute = int2( Normalized * 255.0f + 0.5f );
		return int( Absolute.x + Absolute.y * 256.0f );
	}

	float4 ColorSample( float2 Coord, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap )
	{
		float2 ColorIndex = PdxTex2D( IndirectionMap, Coord ).rg;
		return PdxTex2DLoad0( ColorMap, int2( ColorIndex * 255.0f + vec2( 0.5f ) ) );
	}

	float4 ColorSampleAtOffset( float2 Coord, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap, float2 Offset )
	{
		float2 ColorIndex = PdxTex2D( IndirectionMap, Coord ).rg;
		return PdxTex2DLoad0( ColorMap, int2( ColorIndex * 255.0f + vec2( 0.5f ) + ( Offset ) ) );
	}

	float4 BilinearColorSample( float2 Coord, float2 TextureSize, float2 InvTextureSize, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap )
	{
		float2 Pixel = Coord * TextureSize + 0.5f;
		
		float2 FracCoord = frac( Pixel );
		Pixel = floor( Pixel ) / TextureSize - InvTextureSize / 2.0f;
	
		float4 C11 = ColorSample( Pixel, IndirectionMap, ColorMap );
		float4 C21 = ColorSample( Pixel + float2( InvTextureSize.x, 0.0f ), IndirectionMap, ColorMap );
		float4 C12 = ColorSample( Pixel + float2( 0.0, InvTextureSize.y ), IndirectionMap, ColorMap );
		float4 C22 = ColorSample( Pixel + InvTextureSize, IndirectionMap, ColorMap );
	
		float4 X1 = lerp( C11, C21, FracCoord.x );
		float4 X2 = lerp( C12, C22, FracCoord.x );
		return lerp( X1, X2, FracCoord.y );
	}

	float4 BilinearColorSampleAtOffset( float2 Coord, float2 TextureSize, float2 InvTextureSize, PdxTextureSampler2D IndirectionMap, PdxTextureSampler2D ColorMap, float2 TextureOffset )
	{
		float2 Pixel = ( Coord * TextureSize + 0.5f );
		
		float2 FracCoord = frac( Pixel );
		Pixel = floor( Pixel ) / TextureSize - InvTextureSize / 2.0f;
	
		float4 C11 = ColorSampleAtOffset( Pixel, IndirectionMap, ColorMap, TextureOffset );
		float4 C21 = ColorSampleAtOffset( Pixel + float2( InvTextureSize.x, 0.0f ), IndirectionMap, ColorMap, TextureOffset );
		float4 C12 = ColorSampleAtOffset( Pixel + float2( 0.0, InvTextureSize.y ), IndirectionMap, ColorMap, TextureOffset );
		float4 C22 = ColorSampleAtOffset( Pixel + InvTextureSize, IndirectionMap, ColorMap, TextureOffset );
	
		float4 X1 = lerp( C11, C21, FracCoord.x );
		float4 X2 = lerp( C12, C22, FracCoord.x );
		return lerp( X1, X2, FracCoord.y );
	}

	float CalculateStripeMask(in float2 UV, float Offset)
	{
		// diagonal
		float AiagonalAngle = 3.14159f / 8.0f;
		float StripeFreq = 12000.0f; // larger value gives smaller width

		float StripePattern = UV.x * cos( AiagonalAngle ) * StripeFreq + UV.y * sin( AiagonalAngle ) * StripeFreq;
		float StripeWidthScale = 0.3f;
		float StripeMask = cos( StripePattern + Offset ) - StripeWidthScale;
		float Width = fwidth( StripePattern );
		Width = max( Width, 0.0001f );

		StripeMask = smoothstep( -Width, Width, StripeMask);

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