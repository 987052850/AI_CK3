Includes = {
	"coat_of_arms/coat_of_arms_pattern.fxh"
}

PixelShader =
{	
	MainCode PS_Pattern
	{
		Input = "VS_OUTPUT_COA_ATLAS"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				return Pattern( Input );
			}
		]]
	}
}

Effect coa_create_colored_pattern
{
	VertexShader = VertexShaderCOAPattern
	PixelShader = PS_Pattern
}

Effect coa_create_colored_pattern_blend
{
	VertexShader = VertexShaderCOAPattern
	PixelShader = PS_Pattern
	BlendState = BlendStateNoAlpha
}
