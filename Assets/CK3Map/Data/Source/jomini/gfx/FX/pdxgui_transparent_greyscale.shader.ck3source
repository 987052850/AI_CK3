Includes = {
	"cw/pdxgui.fxh"
	"cw/pdxgui_sprite.fxh"
	"jomini/mapeditor/mapeditor_gruvbox.fxh"
}


VertexShader =
{
	MainCode VertexShader
	{
		Input = "VS_INPUT_PDX_GUI"
		Output = "VS_OUTPUT_PDX_GUI"
		Code
		[[
			PDX_MAIN
			{
				return PdxGuiDefaultVertexShader( Input );
			}
		]]
	}
}


PixelShader =
{
	TextureSampler Texture
	{
		Ref = PdxTexture0
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	
	MainCode PixelShader
	{
		Input = "VS_OUTPUT_PDX_GUI"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 UV = Input.UV0;
				UV.y = 1.0f - UV.y;

				float4 OutColor = SampleImageSprite( Texture, UV );
				OutColor.rgb = vec3( OutColor.r );

				#ifdef BLACK_IS_TRANSPARENCY
					float Alpha = 0.0f;
					#ifdef ALPHA_VALUE
						Alpha = ALPHA_VALUE;
					#endif
					if( OutColor.r == 0 )
					{
						OutColor.a = Alpha;
					}
				#endif
				
			    return OutColor;
			}
		]]
	}

	MainCode PixelShaderMask
	{
		Input = "VS_OUTPUT_PDX_GUI"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 UV = Input.UV0;
				UV.y = 1.0f - UV.y;

				float4 OutColor = SampleImageSprite( Texture, UV );
				OutColor.rgba = vec4( OutColor.r );

				#ifdef BLACK_IS_TRANSPARENCY
					float Alpha = 0.0f;
					#ifdef ALPHA_VALUE
						Alpha = ALPHA_VALUE;
					#endif
					if( OutColor.r == 0 )
					{
						OutColor.a = Alpha;
					}
				#endif

				if( OutColor.r > 0 )
				{
					OutColor.rgb = GRUVBOX_LIGHT_BLUE;
					OutColor.a = 0.7f;
				}

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
}

Effect PdxMask
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderMask"
}

Effect PdxMaskDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderMask"
}