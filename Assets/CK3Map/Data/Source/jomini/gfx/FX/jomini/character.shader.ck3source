Includes = {
	"cw/pdxmesh_blendshapes.fxh"
	"cw/pdxmesh.fxh"
	"cw/utility.fxh"
	"cw/shadow.fxh"
	"cw/camera.fxh"
	"cw/alpha_to_coverage.fxh"
	"jomini/jomini_lighting.fxh"
	"jomini/jomini_fog.fxh"
	"jomini/texture_decals.fxh"
	"constants.fxh"
}

PixelShader =
{
	TextureSampler DiffuseMap
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler PropertiesMap
	{
		Index = 1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler NormalMap
	{
		Index = 2
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler SSAOColorMap
	{
		Index = 3
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

	VertexStruct PS_COLOR_SSAO
	{
		float4 Color		: PDX_COLOR0;
		float4 SSAOColor	: PDX_COLOR1;
	};
}

VertexStruct VS_OUTPUT_PDXMESHCHARACTER
{
    float4 	Position		: PDX_POSITION;
	float3 	Normal			: TEXCOORD0;
	float3 	Tangent			: TEXCOORD1;
	float3 	Bitangent		: TEXCOORD2;
	float2 	UV0				: TEXCOORD3;
	float3 	WorldSpacePos	: TEXCOORD4;

	# [ObjectDataIndex][DecalDataIndex]
	uint2	DataIndices		: TEXCOORD5; 
	uint	DecalCount		: TEXCOORD6;
};

struct SCharacterConstants
{
	float4		_HairPropertyMult;
	float3 		_PaletteColorSkin;
	float3 		_PaletteColorHair;
	float3		_PaletteColorEyes;
};

Code
[[
	SCharacterConstants GetCharacterConstants( uint ObjectDataIndex ) 
	{
		uint BeginIndex = ObjectDataIndex + PDXMESH_USER_DATA_OFFSET;

		SCharacterConstants Result;
		Result._HairPropertyMult = Data[ BeginIndex ];
		Result._PaletteColorSkin = Data[ BeginIndex + 1].xyz;
		Result._PaletteColorHair = float3( Data[ BeginIndex + 1].w, Data[ BeginIndex + 2].xy );
		Result._PaletteColorEyes = float3( Data[ BeginIndex + 2 ].zw, Data[ BeginIndex + 3 ].x );

		return Result;
	}
]]

VertexShader = {
	Code
	[[
		VS_OUTPUT_PDXMESHCHARACTER ConvertOutput( VS_OUTPUT_PDXMESH In )
		{
			VS_OUTPUT_PDXMESHCHARACTER Out;
			
			Out.Position = In.Position;
			Out.Normal = In.Normal;
			Out.Tangent = In.Tangent;
			Out.Bitangent = In.Bitangent;
			Out.UV0 = In.UV0;
			Out.WorldSpacePos = In.WorldSpacePos;
			return Out;
		}
	]]
	
	MainCode VS_standard
	{
		Input = "VS_INPUT_PDXMESHSTANDARD"
		Output = "VS_OUTPUT_PDXMESHCHARACTER"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_PDXMESHCHARACTER Out = ConvertOutput( PdxMeshVertexShaderStandard( Input ) );
				Out.DataIndices = uint2( Input.InstanceIndices.y, Input.InstanceIndices.w );
				Out.DecalCount = GetActiveDecals( Input.InstanceIndices.y );
				return Out;
			}
		]]
	}
}

PixelShader =
{
	Code
	[[
		void DebugReturn( inout float3 Out, SMaterialProperties MaterialProps, SLightingProperties LightingProps, PdxTextureSamplerCube EnvironmentMap, float3 SssColor, float SssMask )
		{
			#if defined(PDX_DEBUG_CHARACTER_SSS_MASK)
			Out = SssMask;
			#elif defined(PDX_DEBUG_CHARACTER_SSS_COLOR)
			Out = SssColor;
			#else
			DebugReturn( Out, MaterialProps, LightingProps, EnvironmentMap );
			#endif
		}

		float3 CommonPixelShader( float4 Diffuse, float4 Properties, float3 NormalSample, in VS_OUTPUT_PDXMESHCHARACTER Input )
		{
			float3x3 TBN = Create3x3( normalize( Input.Tangent ), normalize( Input.Bitangent ), normalize( Input.Normal ) );
			float3 Normal = normalize( mul( NormalSample, TBN ) );
			
			SMaterialProperties MaterialProps = GetMaterialProperties( Diffuse.rgb, Normal, saturate( Properties.a ), Properties.g, Properties.b );
			SLightingProperties LightingProps = GetSunLightingProperties( Input.WorldSpacePos, ShadowTexture );
				
			float3 Color = CalculateSunLighting( MaterialProps, LightingProps, EnvironmentMap );
			
			float3 SssColor = vec3(0.0f);
			float SssMask = Properties.r;
			#ifdef FAKE_SSS_EMISSIVE
				float3 SkinColor = RGBtoHSV( Diffuse.rgb );
				SkinColor.z = 1.0f;
				SssColor = HSVtoRGB(SkinColor) * SssMask * 0.5f * MaterialProps._DiffuseColor;
				Color += SssColor;
			#endif
			
			Color = ApplyDistanceFog( Color, Input.WorldSpacePos );
			
			DebugReturn( Color, MaterialProps, LightingProps, EnvironmentMap, SssColor, SssMask );			
			return Color;
		}

		float3 GetColorMaskBlend( float3 DiffuseColor, float3 PaletteColor, float ColorMaskStrength )
		{
			return DiffuseColor.rgb + ( DiffuseColor.rgb * PaletteColor ) * ColorMaskStrength;
		}
	]]

	MainCode PS_skin
	{
		Input = "VS_OUTPUT_PDXMESHCHARACTER"
		Output = "PS_COLOR_SSAO"
		Code
		[[
			PDX_MAIN
			{			
				PS_COLOR_SSAO Out;

				float2 UV0 = Input.UV0;
				float4 Diffuse;
				float4 Properties;
				float3 NormalSample;
				
				Diffuse = PdxTex2D( DiffuseMap, UV0 );
				Properties = PdxTex2D( PropertiesMap, UV0 );
				NormalSample = UnpackRRxGNormal( PdxTex2D( NormalMap, UV0 ) );

				SCharacterConstants Constants = GetCharacterConstants( Input.DataIndices.x );
				
				float ColorMaskStrength = Diffuse.a;
				Diffuse.rgb = GetColorMaskBlend( Diffuse.rgb, Constants._PaletteColorSkin.rgb, ColorMaskStrength );

				ApplyDecals( Diffuse.rgb, NormalSample, Properties, UV0, Input.DataIndices.y, Input.DecalCount );
				
				float3 Color = CommonPixelShader( Diffuse, Properties, NormalSample, Input );
				Out.Color = float4( Color, 1.0f );

				Out.SSAOColor = PdxTex2D( SSAOColorMap, UV0 );
				Out.SSAOColor.rgb *= Constants._PaletteColorSkin.rgb;
				return Out;
			}
			
		]]
	}
	
	MainCode PS_eye
	{
		Input = "VS_OUTPUT_PDXMESHCHARACTER"
		Output = "PS_COLOR_SSAO"
		Code
		[[
			PDX_MAIN
			{
				PS_COLOR_SSAO Out;

				float2 UV0 = Input.UV0;
				float4 Diffuse = PdxTex2D( DiffuseMap, UV0 );								
				float4 Properties = PdxTex2D( PropertiesMap, UV0 );
				float3 NormalSample = UnpackRRxGNormal( PdxTex2D( NormalMap, UV0 ) );

				SCharacterConstants Constants = GetCharacterConstants( Input.DataIndices.x );
				
				float ColorMaskStrength = Diffuse.a;
				Diffuse.rgb = GetColorMaskBlend( Diffuse.rgb, Constants._PaletteColorEyes, ColorMaskStrength );
				
				float3 Color = CommonPixelShader( Diffuse, Properties, NormalSample, Input );
				Out.Color = float4( Color, 1.0f );
				
				Out.SSAOColor = PdxTex2D( SSAOColorMap, UV0 );
				Out.SSAOColor.rgb *= Constants._PaletteColorEyes;
	
				return Out;
			}
		]]
	}

	MainCode PS_attachment
	{		
		Input = "VS_OUTPUT_PDXMESHCHARACTER"
		Output = "PS_COLOR_SSAO"
		Code
		[[
			PDX_MAIN
			{
				PS_COLOR_SSAO Out;

				float2 UV0 = Input.UV0;
				float4 Diffuse = PdxTex2D( DiffuseMap, UV0 );								
				float4 Properties = PdxTex2D( PropertiesMap, UV0 );
				float3 NormalSample = UnpackRRxGNormal( PdxTex2D( NormalMap, UV0 ) );		
				Properties.r = 1.0; // wipe this clean now, ready to be modified later
								
				float3 Color = CommonPixelShader( Diffuse, Properties, NormalSample, Input );

				Out.Color = float4( Color, Diffuse.a );
				Out.SSAOColor = float4( vec3( 0.0f ), 1.0f );

				return Out;
			}
		]]
	}
	MainCode PS_character_hair_backface
	{
		Input = "VS_OUTPUT_PDXMESHCHARACTER"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{			
				return float4( vec3( 0.0f ), 1.0f );
			}
		]]
	}
	MainCode PS_hair
	{
		Input = "VS_OUTPUT_PDXMESHCHARACTER"
		Output = "PS_COLOR_SSAO"
		Code
		[[
			PDX_MAIN
			{
				PS_COLOR_SSAO Out;

				SCharacterConstants Constants = GetCharacterConstants( Input.DataIndices.x );

				float2 UV0 = Input.UV0;
				float4 Diffuse = PdxTex2D( DiffuseMap, UV0 );								
				float4 Properties = PdxTex2D( PropertiesMap, UV0 );
				Properties *= Constants._HairPropertyMult;
				float4 NormalSampleRaw = PdxTex2D( NormalMap, UV0 );
				float3 NormalSample = UnpackRRxGNormal( NormalSampleRaw ) * ( PDX_IsFrontFace ? 1 : -1 );

				float ColorMaskStrength = NormalSampleRaw.b;
				Diffuse.rgb = GetColorMaskBlend( Diffuse.rgb, Constants._PaletteColorHair, ColorMaskStrength );
				
				float3 Color = CommonPixelShader( Diffuse, Properties, NormalSample, Input );

				#ifdef ALPHA_TO_COVERAGE
					Diffuse.a = RescaleAlphaByMipLevel( Diffuse.a, UV0, DiffuseMap );

					const float CUTOFF = 0.5f;
					Diffuse.a = SharpenAlpha( Diffuse.a, CUTOFF );
				#endif

				#ifdef WRITE_ALPHA_ONE
					Out.Color = float4( Color, 1.0f );
				#else
					#ifdef HAIR_TRANSPARENCY_HACK
						// TODO [HL]: Hack to stop clothing fragments from being discarded by transparent hair,
						// proper fix is to ensure that hair is drawn after clothes
						// https://beta.paradoxplaza.com/browse/PSGE-3103
						clip( Diffuse.a - 0.5f );
					#endif

					Out.Color = float4( Color, Diffuse.a );
				#endif

				Out.SSAOColor = PdxTex2D( SSAOColorMap, UV0 );
				Out.SSAOColor.rgb *= Constants._PaletteColorHair;

				return Out;
			}
		]]
	}
}

BlendState hair_alpha_blend
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
	SourceAlpha = "ONE"
	DestAlpha = "INV_SRC_ALPHA"
	WriteMask = "RED|GREEN|BLUE|ALPHA"
}

DepthStencilState hair_alpha_blend
{
	DepthWriteEnable = no
}

BlendState alpha_to_coverage
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
	WriteMask = "RED|GREEN|BLUE|ALPHA"
	SourceAlpha = "ONE"
	DestAlpha = "INV_SRC_ALPHA"
	AlphaToCoverage = yes
}

RasterizerState rasterizer_no_culling
{
	CullMode = "none"
}

RasterizerState rasterizer_backfaces
{
	FrontCCW = yes
}
RasterizerState shadow_rasterizer_state
{
	# Taken from pdxmesh.shader	
	DepthBias = 100
	SlopeScaleDepthBias = 2
}

Effect character_skin
{
	VertexShader = "VS_standard"
	PixelShader = "PS_skin"
	Defines = { "FAKE_SSS_EMISSIVE" "PDX_MESH_BLENDSHAPES" }
}

# The suffix for shadow effects is "Shadow" not "_shadow", hence the weird case convention.

Effect character_skinShadow
{
	VertexShader = "VertexPdxMeshStandardShadow"
	PixelShader = "PixelPdxMeshStandardShadow"
	RasterizerState = "shadow_rasterizer_state"
	Defines = { "PDXMESH_DISABLE_DITHERED_OPACITY" "PDX_MESH_BLENDSHAPES" }
}

Effect character_eye
{
	VertexShader = "VS_standard"
	PixelShader = "PS_eye"
}

Effect character_eyeShadow
{
	VertexShader = "VertexPdxMeshStandardShadow"
	PixelShader = "PixelPdxMeshStandardShadow"
	RasterizerState = "shadow_rasterizer_state"
	Defines = { "PDXMESH_DISABLE_DITHERED_OPACITY" "PDX_MESH_BLENDSHAPES" }
}

Effect character_attachment
{
	VertexShader = "VS_standard"
	PixelShader = "PS_attachment"
	Defines = { "PDX_MESH_BLENDSHAPES" }
}

Effect character_attachmentShadow
{
	VertexShader = "VertexPdxMeshStandardShadow"
	PixelShader = "PixelPdxMeshStandardShadow"
	RasterizerState = "shadow_rasterizer_state"
	Defines = { "PDXMESH_DISABLE_DITHERED_OPACITY" "PDX_MESH_BLENDSHAPES" }
}


Effect character_attachment_pattern_alpha_to_coverageShadow
{
	VertexShader = "VertexPdxMeshStandardShadow"
	PixelShader = "PixelPdxMeshStandardShadow"
	RasterizerState = "shadow_rasterizer_state"
	Defines = { "PDXMESH_DISABLE_DITHERED_OPACITY" "PDX_MESH_BLENDSHAPES" }
}

Effect character_attachment_alpha_to_coverage
{
	VertexShader = "VS_standard"
	PixelShader = "PS_attachment"
	BlendState = "alpha_to_coverage"
	Defines = { "PDX_MESH_BLENDSHAPES" }
}

Effect character_hair
{
	VertexShader = "VS_standard"
	PixelShader = "PS_hair"
	BlendState = "alpha_to_coverage"
	RasterizerState = "rasterizer_no_culling"
	Defines = { "ALPHA_TO_COVERAGE" "PDX_MESH_BLENDSHAPES" }
}

Effect character_hairShadow
{
	VertexShader = "VertexPdxMeshStandardShadow"
	PixelShader = "PixelPdxMeshStandardShadow"
	RasterizerState = "shadow_rasterizer_state"
	Defines = { "PDXMESH_DISABLE_DITHERED_OPACITY" "PDX_MESH_BLENDSHAPES" }
}

Effect character_hair_alpha
{
	VertexShader = "VS_standard"
	PixelShader = "PS_hair"
	BlendState = "hair_alpha_blend"
	DepthStencilState = "hair_alpha_blend"
	Defines = { "PDX_MESH_BLENDSHAPES" }
}

Effect character_hair_opaque
{
	VertexShader = "VS_standard"
	PixelShader = "PS_hair"
	
	Defines = { "WRITE_ALPHA_ONE" "PDX_MESH_BLENDSHAPES" }
}

Effect character_hair_backside
{
	VertexShader = "VS_standard"
	PixelShader = "PS_character_hair_backface"
	RasterizerState = "rasterizer_backfaces"
}
