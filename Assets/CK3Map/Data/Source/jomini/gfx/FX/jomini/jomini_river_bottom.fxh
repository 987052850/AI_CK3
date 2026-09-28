Includes = {
	"cw/pdxterrain.fxh"
	"cw/upscale_utils.fxh"
	"jomini/jomini_river.fxh"
}

PixelShader =
{
	TextureSampler BottomDiffuse
	{
		Ref = JominiRiver0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler BottomNormal
	{
		Ref = JominiRiver1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler BottomProperties
	{
		Ref = JominiRiver2
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}

	TextureSampler EnvironmentMap
	{
		Ref = JominiEnvironmentMap
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
		Type = "Cube"
	}

	TextureSampler ShadowTexture
	{
		Ref = PdxShadowmap
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
		CompareFunction = less_equal
		SamplerType = "Compare"
	}

	VertexStruct PS_RIVER_BOTTOM_OUT
	{
		float4 Color		: PDX_COLOR0;
		float4 Blend		: PDX_COLOR0_SRC1;
	};

	Code
	[[
		// Version of CalculateShadow (originally in Clausewitz's shadow.fxh) that specifically excludes shadow casters that are
		// at or below the water surface, and thus stops the water surface itself from shadowing the bottom.
		float CalculateRiverBottomShadow( float4 ShadowProj, float4 WaterSurfaceProj, PdxTextureSampler2DCmp ShadowMap )
		{
			ShadowProj.xyz = ShadowProj.xyz / ShadowProj.w;
			WaterSurfaceProj.xyz = WaterSurfaceProj.xyz / WaterSurfaceProj.w;

			float RandomAngle = CalcRandom( round( ShadowScreenSpaceScale * ShadowProj.xy ) ) * 3.14159 * 2.0;
			float2 Rotate = float2( cos( RandomAngle ), sin( RandomAngle ) );

			// Exclude the water surface from casting shadows
			float ShadowCmpDepth = min( ShadowProj.z, WaterSurfaceProj.z ) - Bias;

			// Sample each of them checking whether the pixel under test is shadowed or not
			float ShadowTerm = 0.0;
			for( int i = 0; i < NumSamples; i++ )
			{
				float4 Samples = DiscSamples[i] * KernelScale;
				ShadowTerm += PdxTex2DCmpLod0( ShadowMap, ShadowProj.xy + RotateDisc( Samples.xy, Rotate ), ShadowCmpDepth );
				ShadowTerm += PdxTex2DCmpLod0( ShadowMap, ShadowProj.xy + RotateDisc( Samples.zw, Rotate ), ShadowCmpDepth );
			}
			// Get the average
			ShadowTerm = ShadowTerm / float(2 * NumSamples);

			const float FadeStrength = 32.0; // 32 found empirically, so it looks ok
			float3 FadeFactor = saturate( float3( 1.0 - abs( 0.5 - ShadowProj.xy ) * 2.0, 1.0 - ShadowProj.z ) * FadeStrength );
			ShadowTerm = lerp( 1.0, ShadowTerm, min( min( FadeFactor.x, FadeFactor.y ), FadeFactor.z ) );
			return ShadowTerm;
		}

		// Version of GetSunLightingProperties (originally in jomini_lighting.fxh) that takes a water depth and also calculates the
		// intersection of the sun ray with the water surface.
		SLightingProperties GetRiverBottomSunLightingProperties( float3 WorldSpacePos, float WaterDepth, PdxTextureSampler2DCmp ShadowMap )
		{
			// Find where ray to the sun interesects the water surface
			float SunRaySurfaceIntersection = WaterDepth / ToSunDir.y;
			float3 WaterSurfacePos = WorldSpacePos + SunRaySurfaceIntersection * ToSunDir;
			float4 WaterSurfaceProj = mul( ShadowMapTextureMatrix, float4( WaterSurfacePos, 1.0 ) );
			float4 ShadowProj = mul( ShadowMapTextureMatrix, float4( WorldSpacePos, 1.0 ) );
			float ShadowTerm = CalculateRiverBottomShadow( ShadowProj, WaterSurfaceProj, ShadowMap );

			return GetSunLightingProperties( WorldSpacePos, ShadowTerm );
		}

		void CalculateParallaxOffsetSteep( float3 TangentSpaceToCameraDir, float3 WorldSpaceToCameraDir, float2 UV, out float2 TangentSpaceOffset, out float2 WorldSpaceOffset )
		{
			static const float MinNumLayers = 2;
			static const float MaxNumLayers = 10;

			float NumLayers = lerp( MaxNumLayers, MinNumLayers, WorldSpaceToCameraDir.y );
			float LayerDepth = _Depth / NumLayers;
			float CurrentDepth = 0.0;

			float4 Step;
			Step.xy =  ( ( -TangentSpaceToCameraDir.xy * _Depth ) / TangentSpaceToCameraDir.z ) / NumLayers;
			Step.zw =  ( ( -WorldSpaceToCameraDir.xz * _Depth ) / WorldSpaceToCameraDir.y ) / NumLayers;

			float4 Offset = vec4( 0.0f );

			float Depth = CalcDepth( UV );

			while( Depth > CurrentDepth )
			{
				CurrentDepth += LayerDepth;
				Offset += Step;

				Depth = CalcDepth( UV + Offset.xy );
			}

			float PrevDepth = CalcDepth( UV + Offset.xy - Step.xy ) - CurrentDepth + LayerDepth;

			float NextDepth = Depth - CurrentDepth;

			float Weight = NextDepth / (NextDepth - PrevDepth);
			Offset -= Step * Weight;

			TangentSpaceOffset = Offset.xy;
			WorldSpaceOffset = Offset.zw;
		}

		// Depth from texture sample version
		void CalculateParallaxOffsetSteep( float3 TangentSpaceToCameraDir, float3 WorldSpaceToCameraDir, float2 UV, out float2 TangentSpaceOffset, out float2 WorldSpaceOffset, PdxTextureSampler2D BottomNormal )
		{
			int MinNumLayers = 2;
			int MaxNumLayers = _ParallaxIterations;

			float NumLayers = lerp( float( MaxNumLayers ), float( MinNumLayers ), WorldSpaceToCameraDir.y );
			float LayerDepth = _Depth / NumLayers;
			float CurrentDepth = 0.0f;

			float4 Step = vec4( 0.0f );
			float4 Offset = vec4( 0.0f );
			float Depth = CalcDepth( UV, BottomNormal );

			Step.xy = ( ( -TangentSpaceToCameraDir.xy * _Depth ) / TangentSpaceToCameraDir.z) / NumLayers;
			Step.zw = ( ( -WorldSpaceToCameraDir.xz * _Depth ) / WorldSpaceToCameraDir.y ) / NumLayers;
			Step.xz *= _TextureUvScale;

			for ( int i = 0; i < MaxNumLayers; i++ )
			{
				if ( Depth > CurrentDepth )
				{
					CurrentDepth += LayerDepth;
					Offset += Step;

					float NewDepth = CalcDepth( UV + Offset.xy, BottomNormal );
					Depth = NewDepth;
				}
			}

			float PrevDepth = CalcDepth( UV + Offset.xy - Step.xy, BottomNormal ) - CurrentDepth + LayerDepth;
			float NextDepth = Depth - CurrentDepth;

			float Weight = NextDepth / (NextDepth - PrevDepth);
			Offset -= Step * Weight;

			TangentSpaceOffset = Offset.xy;
			WorldSpaceOffset = Offset.zw;
		}

		void CalcParallaxedUvs( in VS_OUTPUT_RIVER Input, in float3x3 TBN, out float2 WorldUV, out float2 TangentUV )
		{
			float3 ToCameraDir = normalize( CameraPosition - Input.WorldSpacePos );

			float3x3 InvTBN = transpose( TBN );
			float3 TangentSpaceToCameraDir = mul( ToCameraDir, InvTBN );

			float ParallaxScale = Input.Width;

			float2 TangentSpaceParallax;
			float2 WorldSpaceParallax;
			CalculateParallaxOffsetSteep( TangentSpaceToCameraDir, ToCameraDir, Input.UV, TangentSpaceParallax, WorldSpaceParallax );

			WorldUV = Input.WorldSpacePos.xz + WorldSpaceParallax * ParallaxScale;
			TangentUV = Input.UV + TangentSpaceParallax;
		}

		// Depth from texture sample version
		void CalcParallaxedUvs( in VS_OUTPUT_RIVER Input, in float3x3 TBN, out float2 WorldUV, out float2 TangentUV, PdxTextureSampler2D BottomNormal )
		{
			float3 ToCameraDir = normalize( CameraPosition - Input.WorldSpacePos );

			float3x3 InvTBN = transpose( TBN );
			float3 TangentSpaceToCameraDir = mul( ToCameraDir, InvTBN );

			float ParallaxScale = Input.Width;

			float2 TangentSpaceParallax;
			float2 WorldSpaceParallax;
			CalculateParallaxOffsetSteep( TangentSpaceToCameraDir, ToCameraDir, Input.UV, TangentSpaceParallax, WorldSpaceParallax, BottomNormal );

			WorldUV = Input.WorldSpacePos.xz + WorldSpaceParallax * ParallaxScale;
			TangentUV = Input.UV + TangentSpaceParallax;
		}

		PS_RIVER_BOTTOM_OUT CalcRiverBottom( in VS_OUTPUT_RIVER Input )
		{
			float3 Normal = normalize(Input.Normal);
			float3 Tangent = normalize(Input.Tangent);
			float3 Bitangent = normalize( cross( Normal, Tangent ) );
			float3x3 TBN = Create3x3( Tangent, Bitangent, Normal );

			// Parallax
			float2 WorldUV;
			float2 TangentUV;
			CalcParallaxedUvs( Input, TBN, WorldUV, TangentUV );

			// Fake some depth
			float UnderOceanFade = 1.0f - saturate( ( _WaterHeight - Input.WorldSpacePos.y ) * _OceanFadeRate );
			float FadeOut = min( UnderOceanFade, Input.Transparency );

			float Depth = CalcDepth( TangentUV );
			float WorldSpaceDepth = Depth * Input.Width * FadeOut;
			float3 WorldSpacePos;
			WorldSpacePos.xz = WorldUV;
			WorldSpacePos.y = Input.WorldSpacePos.y - WorldSpaceDepth;

			// Sampling
			float4 Diffuse = PdxTex2DUpscale( BottomDiffuse, WorldUV );
			float4 Properties = PdxTex2DUpscale( BottomProperties, WorldUV );
			float3 NormalSample = UnpackRRxGNormal( PdxTex2DUpscale( BottomNormal, WorldUV ) );

			// normals
			float SampleWidth = 0.1f;
			float2 DepthSampleOffset = float2( 0.0f, SampleWidth * 0.5f );
			float DepthDelta = ( CalcDepth( TangentUV - DepthSampleOffset ) - CalcDepth( TangentUV + DepthSampleOffset ) ) * UnderOceanFade;
			float Slope = DepthDelta / SampleWidth;
			float Angle = atan( Slope );
			float3 ParallaxNormal = float3( 0, -sin( Angle ), cos( Angle ) );
			ParallaxNormal.xy += NormalSample.xy;
			Normal = normalize( mul( ParallaxNormal, TBN ) );

			// lighting
			SMaterialProperties MaterialProps = GetMaterialProperties( Diffuse.rgb, Normal, Properties.a, Properties.g, Properties.b );
			SLightingProperties LightingProps = GetRiverBottomSunLightingProperties( WorldSpacePos, WorldSpaceDepth, ShadowTexture );

			float3 Color = CalculateSunLighting( MaterialProps, LightingProps, EnvironmentMap );

			float FadeToConnection = saturate( ( Input.DistanceToMain - 0.6f * abs(Input.UV.y-0.5f) ) * 5.0f );
			float EdgeFade = saturate( Depth * 13.0f );
			float Alpha = FadeOut * FadeToConnection * EdgeFade;

			DebugReturn( Color, MaterialProps, LightingProps, EnvironmentMap );

			WorldSpacePos.y -= pow( Depth / _Depth, 2 ) * _DepthFakeFactor * FadeOut;

			PS_RIVER_BOTTOM_OUT Out;
			Out.Color.rgb = Color;
			Out.Color.a = CompressWorldSpace( WorldSpacePos );
			Out.Blend = vec4(Alpha);
			return Out;
		}

		// New updated version
		PS_RIVER_BOTTOM_OUT CalcRiverBottomAdvanced( in VS_OUTPUT_RIVER Input )
		{
			PS_RIVER_BOTTOM_OUT Out;

			float3 Normal = normalize( Input.Normal );
			float3 Tangent = normalize( Input.Tangent );
			float3 Bitangent = normalize( cross( Normal, Tangent ) );
			float3x3 TBN = Create3x3( Tangent, Bitangent, Normal );

			Input.UV = float2( Input.UV.x * _TextureUvScale, Input.UV.y );

			// Parallax
			float2 WorldUV;
			float2 TangentUV;
			CalcParallaxedUvs( Input, TBN, WorldUV, TangentUV, BottomNormal );

			// Fake some depth
			float UnderOceanFade = 1.0f - saturate( ( _WaterHeight - Input.WorldSpacePos.y ) * _OceanFadeRate );
			float FadeOut = min( UnderOceanFade, Input.Transparency );

			float Depth = CalcDepth( TangentUV, BottomNormal );
			float WorldSpaceDepth = Depth * Input.Width * FadeOut;
			float3 WorldSpacePos;
			WorldSpacePos.xz = WorldUV;
			WorldSpacePos.y = Input.WorldSpacePos.y - WorldSpaceDepth;

			// Sampling
			float4 Diffuse = PdxTex2DUpscale( BottomDiffuse, TangentUV );
			float4 Properties = PdxTex2DUpscale( BottomProperties, TangentUV );
			float3 NormalSample = UnpackRRxGNormal( PdxTex2DUpscale( BottomNormal, TangentUV ) );

			// Normals
			float SampleWidth = 0.1f;
			float2 DepthSampleOffset = float2( 0.0f, SampleWidth * 0.5f );
			float DepthDelta = ( CalcDepth( TangentUV - DepthSampleOffset, BottomNormal ) - CalcDepth( TangentUV + DepthSampleOffset, BottomNormal ) ) * UnderOceanFade;
			float Slope = DepthDelta / SampleWidth;
			float Angle = atan( Slope );
			float3 ParallaxNormal = float3( 0, -sin( Angle ), cos( Angle ) );
			ParallaxNormal.xy += NormalSample.xy;
			Normal = normalize( mul( ParallaxNormal, TBN ) );
			Normal = SimpleRotateNormalToTerrain( Normal, Input.WorldSpacePos.xz );

			// Lighting
			SMaterialProperties MaterialProps = GetMaterialProperties( Diffuse.rgb, Normal, Properties.a, Properties.g, Properties.b );
			SLightingProperties LightingProps = GetRiverBottomSunLightingProperties( WorldSpacePos, WorldSpaceDepth, ShadowTexture );
			float3 Color = CalculateSunLighting( MaterialProps, LightingProps, EnvironmentMap );

			float FadeToConnection = saturate( ( Input.DistanceToMain - 0.6f * abs( Input.UV.y - 0.5f ) ) * 5.0f );

			// Edge fade
			float EdgeFade1 = smoothstep( 0.0f, _BankFade, Input.UV.y );
			float EdgeFade2 = smoothstep( 0.0f, _BankFade, 1.0f - Input.UV.y );
			float Alpha = Diffuse.a * FadeOut * FadeToConnection * EdgeFade1 * EdgeFade2;

			DebugReturn( Color, MaterialProps, LightingProps, EnvironmentMap );

			WorldSpacePos.y -= pow( Depth / _Depth, 2 ) * _DepthFakeFactor * FadeOut;

			// Output
			Out.Color.rgb = Color;
			Out.Color.a = CompressWorldSpace( WorldSpacePos );
			Out.Blend = vec4( Alpha );
			return Out;
		}
	]]
}

BlendState BlendState
{
	BlendEnable = yes
	SourceBlend = "src1_alpha"
	DestBlend = "inv_src1_alpha"
	SourceAlpha = "src1_alpha"
	DestAlpha = "inv_src1_alpha"
	WriteMask = "RED|GREEN|BLUE|ALPHA"
}

RasterizerState RasterizerState
{
	DepthBias = -50000
	#fillmode = wireframe
	#CullMode = none
}

DepthStencilState DepthStencilState
{
	DepthWriteEnable = no
}