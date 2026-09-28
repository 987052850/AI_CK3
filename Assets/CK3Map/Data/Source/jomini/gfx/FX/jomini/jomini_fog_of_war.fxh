Includes = {
	#"cw/utility.fxh"
}

ConstantBuffer( JominiFogOfWar )
{
	float2	FogOfWarAlphaMapSize;
	float2	InverseWorldSize;
	float2	FogOfWarPatternSpeed;
	float	FogOfWarPatternStrength;
	float	FogOfWarPatternTiling;
	float	FogOfWarTime;
	float	FogOfWarAlphaMin;
	float	FogOfWarContrast;
	float	FogOfWarBrightness
}

PixelShader = 
{
	Code
	[[
		#ifndef FOG_OF_WAR_BLEND_FUNCTION
			#define FOG_OF_WAR_BLEND_FUNCTION loc_BlendFogOfWar
			float4 loc_BlendFogOfWar( float Alpha )
			{
				return float4( vec3(0.0), 1.0 - Alpha );
			}
		#endif
		
		void loc_ApplyFogOfWarPattern( inout float Alpha, in float3 Coordinate, PdxTextureSampler2D FogOfWarAlphaMask )
		{
			if( FogOfWarPatternStrength > 0.0f )
			{
				float2 UV = Coordinate.xz * InverseWorldSize * FogOfWarPatternTiling;
				UV += FogOfWarPatternSpeed * FogOfWarTime;
				float Noise1 = 1.0f - PdxTex2D( FogOfWarAlphaMask, UV ).g;
				float Noise2 = 1.0f - PdxTex2D( FogOfWarAlphaMask, UV * -0.13 ).g;
				float Detail = 0.5f;
				
				float Noise = saturate( Noise2 * (1.0f-Detail) + Detail * 0.5f + (Noise1-0.5f) * Detail );
				
				Noise *= 1.0f - Alpha;
				Alpha = smoothstep( 0.0, 1.0, Alpha + Noise * FogOfWarPatternStrength );
			}
		}
		float GetFogOfWarAlpha( in float3 Coordinate, PdxTextureSampler2D FogOfWarAlphaMask )
		{		
			float Alpha = PdxTex2D( FogOfWarAlphaMask, Coordinate.xz * InverseWorldSize ).r;
			
			loc_ApplyFogOfWarPattern( Alpha, Coordinate, FogOfWarAlphaMask );
			
			return FogOfWarAlphaMin + Alpha * (1.0f - FogOfWarAlphaMin);
		}
		float GetFogOfWarAlphaMultiSampled( in float3 Coordinate, PdxTextureSampler2D FogOfWarAlphaMask )
		{
			float Width = 5.0f;
			float Alpha = 0.0f; 
			Alpha += PdxTex2D( FogOfWarAlphaMask, ( Coordinate.xz + float2( 0,-1) * Width ) * InverseWorldSize ).r;
			Alpha += PdxTex2D( FogOfWarAlphaMask, ( Coordinate.xz + float2(-1, 0) * Width ) * InverseWorldSize ).r;
			Alpha += PdxTex2D( FogOfWarAlphaMask, ( Coordinate.xz + float2( 1, 0) * Width ) * InverseWorldSize ).r;
			Alpha += PdxTex2D( FogOfWarAlphaMask, ( Coordinate.xz + float2( 0, 1) * Width ) * InverseWorldSize ).r;
			Alpha /= 4.0f;
			
			loc_ApplyFogOfWarPattern( Alpha, Coordinate, FogOfWarAlphaMask );
			
			return FogOfWarAlphaMin + Alpha * (1.0f - FogOfWarAlphaMin);
		}

		float3 FogOfWarColorAdjustment( in float3 Color, in float Alpha )
		{
			// Convert contrast from range -1 to 1 to 0 to 2
			const float ContrastFactor = FogOfWarContrast + 1.0f;
			const float Average = dot( Color.rgb, vec3( 0.3333f ) );
			const float3 ColorAdjustment = ( Color.rgb - Average ) * ContrastFactor + Average + FogOfWarBrightness;
			return saturate( lerp( Color, ColorAdjustment, Alpha ) );
		}

		float3 FogOfWarBlend( float3 Color, float Alpha )
		{		
			float4 ColorAndAlpha = FOG_OF_WAR_BLEND_FUNCTION( Alpha );
			float3 ColorAdjustment = FogOfWarColorAdjustment( Color, ColorAndAlpha.a );
			return lerp( ColorAdjustment, ColorAndAlpha.rgb, ColorAndAlpha.a );
		}

		// Immediate mode
		float3 JominiApplyFogOfWar( in float3 Color, in float3 Coordinate, PdxTextureSampler2D FogOfWarAlphaMask )
		{
		#ifdef JOMINI_DISABLE_FOG_OF_WAR
			return Color;
		#else
			float Alpha = GetFogOfWarAlpha( Coordinate, FogOfWarAlphaMask );
			return FogOfWarBlend( Color, Alpha );
		#endif
		}
		float3 JominiApplyFogOfWarMultiSampled( in float3 Color, in float3 Coordinate, PdxTextureSampler2D FogOfWarAlphaMask )
		{
		#ifdef JOMINI_DISABLE_FOG_OF_WAR
			return Color;
		#else
			float Alpha = GetFogOfWarAlphaMultiSampled( Coordinate, FogOfWarAlphaMask );
			return FogOfWarBlend( Color, Alpha );
		#endif
		}
		
		// Post process
		float4 JominiApplyFogOfWar( in float3 WorldSpacePos, PdxTextureSampler2D FogOfWarAlphaMask )
		{
		#ifdef JOMINI_DISABLE_FOG_OF_WAR
			return float4( vec3(0.0), 1.0 );
		#else
			return FOG_OF_WAR_BLEND_FUNCTION( GetFogOfWarAlpha( WorldSpacePos, FogOfWarAlphaMask ) );
		#endif
		}
		
		#ifndef ApplyFogOfWar		
		#define ApplyFogOfWar JominiApplyFogOfWar
		#endif
		#ifndef ApplyFogOfWarMultiSampled		
		#define ApplyFogOfWarMultiSampled JominiApplyFogOfWarMultiSampled
		#endif
	]]
}
