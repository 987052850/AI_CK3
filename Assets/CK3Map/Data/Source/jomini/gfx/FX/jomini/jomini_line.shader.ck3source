Includes = {
	"cw/lines.fxh"
	"cw/utility.fxh"
	"cw/camera.fxh"
	"jomini/jomini_lighting.fxh"
}


PixelShader =
{
	TextureSampler DiffuseTexture
	{
		Ref = PdxTexture0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler NormalTexture
	{
		Ref = PdxTexture1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler PropertiesTexture
	{
		Ref = PdxTexture2
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler MaskTexture
	{
		Ref = PdxTexture3
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	
	MainCode PS_lighting
	{
		TextureSampler EnvironmentMap
		{
			Ref = JominiEnvironmentMap
			MagFilter = "Linear"
			MinFilter = "Linear"
			MipFilter = "Linear"
			SampleModeU = "Wrap"
			SampleModeV = "Wrap"
			Type = "Cube"
		}
	
		Input = "VS_OUTPUT_PDXLINES"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float ProgressFactor = Progress - Input.UV0To1.x;
				clip( ProgressFactor );
				
				float4 Diffuse = PdxTex2D( DiffuseTexture, Input.UV );
				float3 NormalSample = UnpackRRxGNormal( PdxTex2D( NormalTexture, Input.UV ) );
				float4 Properties = PdxTex2D( PropertiesTexture, Input.UV );
				
				float4 Mask = SampleMask( Input.MaskUV, MaskTexture );
				
				float3x3 TBN = Create3x3( normalize( Input.Tangent ), normalize( Input.Bitangent ), normalize( Input.Normal ) );
				float3 Normal = normalize( mul( NormalSample, TBN ) );
				
				SMaterialProperties MaterialProps = GetMaterialProperties( Diffuse.rgb, Normal, Properties.a, Properties.g, Properties.b );
				SLightingProperties LightingProps = GetSunLightingProperties( Input.WorldSpacePos, 1.0 );
				float3 Color = CalculateSunLighting( MaterialProps, LightingProps, EnvironmentMap );
				
				DebugReturn( Color, MaterialProps, LightingProps, EnvironmentMap );
				
				return float4( Color, Mask.a );
			}
		]]
	}
}


BlendState BlendState
{
	BlendEnable = no
	WriteMask = "RED|GREEN|BLUE"
}
BlendState BlendStateAlphaBlend
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
	WriteMask = "RED|GREEN|BLUE"
}
BlendState BlendStateAdditiveBlend
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "ONE"
	WriteMask = "RED|GREEN|BLUE"
}


RasterizerState RasterizerState
{
	CullMode = "none"
	#FillMode = "wireframe"
}
RasterizerState RasterizerStateShadow
{
	DepthBias = 40000
	SlopeScaleDepthBias = 2
	DepthClip = no
}


DepthStencilState DepthStencilState
{
	DepthEnable = no
}
DepthStencilState DepthStencilStateEnabled
{
	DepthEnable = yes
	DepthWriteEnable = no
}
DepthStencilState DepthStencilStateShadow
{
	DepthEnable = yes
}


Effect standard_alpha_blend
{
	VertexShader = "VS_standard"
	PixelShader = "PS_standard"
	
	BlendState = BlendStateAlphaBlend
	
	Defines = { "DIFFUSE_TEXTURE DiffuseTexture" "MASK_TEXTURE MaskTexture" }
}
Effect standard_alpha_blend_depth
{
	VertexShader = "VS_standard"
	PixelShader = "PS_standard"
	
	BlendState = BlendStateAlphaBlend
	DepthStencilState = DepthStencilStateEnabled
	
	Defines = { "DIFFUSE_TEXTURE DiffuseTexture" "MASK_TEXTURE MaskTexture" }
}

Effect standard_additive
{
	VertexShader = "VS_standard"
	PixelShader = "PS_standard"
	
	BlendState = BlendStateAdditiveBlend
	
	Defines = { "DIFFUSE_TEXTURE DiffuseTexture" "MASK_TEXTURE MaskTexture" }
}
Effect standard_additive_depth
{
	VertexShader = "VS_standard"
	PixelShader = "PS_standard"
	
	BlendState = BlendStateAdditiveBlend
	DepthStencilState = DepthStencilStateEnabled
	
	Defines = { "DIFFUSE_TEXTURE DiffuseTexture" "MASK_TEXTURE MaskTexture" }
}

Effect standard_shadow
{
	VertexShader = "VS_standard"
	PixelShader = "PS_shadow"
	
	RasterizerState = RasterizerStateShadow
	DepthStencilState = DepthStencilStateShadow
	
	Defines = { "DIFFUSE_TEXTURE DiffuseTexture" "MASK_TEXTURE MaskTexture" }
}

Effect lighting
{
	VertexShader = "VS_standard"
	PixelShader = "PS_lighting"
	
	BlendState = BlendStateAlphaBlend
}
Effect lighting_depth
{
	VertexShader = "VS_standard"
	PixelShader = "PS_lighting"
	
	BlendState = BlendStateAlphaBlend
	DepthStencilState = DepthStencilStateEnabled
}