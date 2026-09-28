# Adapted from code in portrait_decals.fxh. This contains the per-pixel apply function for decals

includes = {
	"jomini/texture_decals_base.fxh"
}

# Instance buffer containing the packed decal information
ConstantBuffer( PdxMeshDecalsInstanceData )
{
	uint4 DecalInstanceData[2];
};

PixelShader =
{
	# Source texture ( most likely an atlas )
	TextureSampler DecalsTexture
	{
		Ref = PdxMeshDecalsTexture
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	Code
	[[		
		// Per-decal information. Read from the instance buffer..
		struct STextureDecalData
		{
			float2 _SourceDiffuseUV;
			float2 _SourceNormalUV;
			float2 _SourcePropertiesUV;

			float2 _SourceFrameSize;

			float2 _DstUV;
			float2 _DstFrameSize;

			float _Strength;

			uint _DiffuseBlendMode;
			uint _NormalBlendMode;
			uint _PropertiesBlendMode;
		};

		// Match in pdx_mesh_types.h
		static const uint PackedDecalDataVec2Size = 2;

		// Must specify a constant, as the maximum unroll-length of the loop must be known at compile time. This is because we sample a texture inside the loop
		static const uint MaxAppliedDecals = 8;

		static const uint Uint8Max = 255;
		static const uint Uint16Max = 65535;
		static const uint InvalidBlendMode = Uint8Max;

		float2 Unpackfloat2( uint From )
		{
			uint X = From & Uint16Max;
			uint Y = ( From >> 16 ) & Uint16Max;

			return float2( X, Y ) / float( Uint16Max );
		}

		uint UnpackDiffuseBlendMode( uint From )
		{
			return From & Uint8Max;
		}

		uint UnpackNormalBlendMode( uint From )
		{
			return ( From >> 8 ) & Uint8Max;
		}

		uint UnpackPropertiesBlendMode( uint From )
		{
			return ( From >> 16 ) & Uint8Max;
		}

		// Read the information of one decal from the input data index. The input data index refers to one uint4 in the DecalInstanceData
		STextureDecalData GetDecalData( uint DecalDataIndex )
		{
			// Floats are packed into uint16 values between 0 and uint16 max
			// Blend modes packed into uint8 values

			STextureDecalData Data;

			Data._SourceDiffuseUV = Unpackfloat2( DecalInstanceData[ DecalDataIndex ].x );
			Data._SourceNormalUV = Unpackfloat2( DecalInstanceData[ DecalDataIndex ].y );
			Data._SourcePropertiesUV = Unpackfloat2( DecalInstanceData[ DecalDataIndex ].z );

			Data._SourceFrameSize = Unpackfloat2( DecalInstanceData[ DecalDataIndex ].w );

			Data._DstUV = Unpackfloat2( DecalInstanceData[ DecalDataIndex + 1 ].x );
			Data._DstFrameSize = Unpackfloat2( DecalInstanceData[ DecalDataIndex +1 ].y );

			Data._Strength = Unpackfloat2( DecalInstanceData[ DecalDataIndex + 1 ].z ).x;

			Data._DiffuseBlendMode = UnpackDiffuseBlendMode( DecalInstanceData[ DecalDataIndex + 1 ].w );
			Data._NormalBlendMode = UnpackNormalBlendMode( DecalInstanceData[ DecalDataIndex + 1 ].w );
			Data._PropertiesBlendMode = UnpackPropertiesBlendMode( DecalInstanceData[ DecalDataIndex + 1 ].w );

			Data._NormalBlendMode = ( Data._NormalBlendMode * uint( Data._NormalBlendMode != BLEND_MODE_OVERLAY ) ) + ( BLEND_MODE_OVERLAY_NORMAL * uint( Data._NormalBlendMode == BLEND_MODE_OVERLAY ) );

			return Data;
		}

		// Remap our source UV so that our target texture sub-area, so that UV values inside that area maps between 0.f - 1.f.( Source UVs outside that area will be below or above )
		// DstUV will be at the center of our frame, which is to say, a value of 0.5, 0.5 means it should map to the center of the target texture
		// DstFrameSize is a fraction of the target texture. A value of 1.f, 1.f means it will simply cover the entire target
		float2 RemapToDstUV( float2 BaseUV, float2 DstUV, float2 DstFrameSize )
		{
			float2 DstUVOffset = DstUV - float2( 0.5f, 0.5f );

			float2 OffsetFromCenter = BaseUV - float2( 0.5f, 0.5f );
			float2 ScaledOffset = OffsetFromCenter / DstFrameSize;
			float2 ScaledDstOffset = DstUVOffset / DstFrameSize;
			float2 UV = ( float2( 0.5f, 0.5f ) + ScaledOffset ) + ScaledDstOffset; 

			return UV;
		}

		// 1.f/0.f Mask multiplier for UV values outside 0.f - 1.f so that we only overlay our decal texture in the desired area
		float CalcOutsideMultiplier( float2 UV )
		{
			return float( UV.x <= 1.f ) * float( UV.y <= 1.f ) * float( UV.x >= 0.f ) * float( UV.y >= 0.f );
		}

		// Remap the source UV ( between 0.f-1.f ) to our atlas frame sub-area
		float2 RemapToAtlasFrameUV( float2 BaseUV, float2 DstUV, float2 DstFrameSize )
		{
			float2 ScaledBase = BaseUV * DstFrameSize;
			return ScaledBase + DstUV;
		}

		// Apply decal data for one pixel
		void ApplyDecals( inout float3 Diffuse, inout float3 Normals, inout float4 Properties, float2 UV, uint InstanceIndex, uint DecalCount )
		{
#ifndef PDX_OPENGL
			[ unroll( MaxAppliedDecals ) ]
#endif
			for ( uint DecalIndex = 0; DecalIndex < DecalCount; ++DecalIndex )
			{
				uint DecalDataIndex = InstanceIndex + ( PackedDecalDataVec2Size * DecalIndex );

				STextureDecalData Data = GetDecalData( DecalDataIndex );

				float2 DstUV = RemapToDstUV( UV, Data._DstUV, Data._DstFrameSize );
				
				float OutsideMultiplier = CalcOutsideMultiplier( DstUV );

				float Weight = Data._Strength * OutsideMultiplier;

				if ( Data._DiffuseBlendMode != InvalidBlendMode )
				{
					float2 DiffuseUV = RemapToAtlasFrameUV( DstUV.xy, Data._SourceDiffuseUV, Data._SourceFrameSize );
					float4 DiffuseSample = PdxTex2D( DecalsTexture, DiffuseUV );
					Weight = DiffuseSample.a * Weight;
					Diffuse = BlendDecal( Data._DiffuseBlendMode, float4( Diffuse, 0.0f ), DiffuseSample, Weight ).rgb;
				}

				if ( Data._NormalBlendMode != InvalidBlendMode )
				{
					float2 NormalUV = RemapToAtlasFrameUV( DstUV.xy, Data._SourceNormalUV, Data._SourceFrameSize );
					float3 NormalSample = UnpackDecalNormal( PdxTex2D( DecalsTexture, NormalUV ), Weight );
					Normals = BlendDecal( Data._NormalBlendMode, float4( Normals, 0.0f ), float4( NormalSample, 0.0f ), Weight ).xyz;
				}

				if ( Data._PropertiesBlendMode != InvalidBlendMode ) 
				{
					float2 PropertiesUV = RemapToAtlasFrameUV( DstUV.xy, Data._SourcePropertiesUV, Data._SourceFrameSize );
					float4 PropertiesSample = PdxTex2D( DecalsTexture, PropertiesUV );
					Properties = BlendDecal( Data._PropertiesBlendMode, Properties, PropertiesSample, Weight );
				}
			}

			Normals = normalize( Normals );
		}
	]]
}
