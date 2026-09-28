Includes = {
	"cw/pdxgui.fxh"
	"cw/utility.fxh"
}

ConstantBuffer( PdxConstantBuffer2 )
{
	float4 ColorIn;
	float4 CachedColor; // When dragging the "area" this won't change until mouse up
	int nActiveColor;
};

VertexStruct VS_OUTPUT_PDX_GUI2
{
	float4 Position		: PDX_POSITION;
	float2 UV0			: TEXCOORD0;
	float2 Pos			: TEXCOORD1;
	float2 WidthHeight	: TEXCOORD2;
	float4 Color		: COLOR;
};

VertexShader =
{
	MainCode VertexShader
	{
		Input = "VS_INPUT_PDX_GUI"
		Output = "VS_OUTPUT_PDX_GUI2"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_PDX_GUI copy = PdxGuiDefaultVertexShader( Input );
				
				VS_OUTPUT_PDX_GUI2 Out;
				Out.Position = copy.Position;
				Out.UV0 = copy.UV0;
				Out.Color = copy.Color;
				Out.Pos = Input.Position;
				Out.WidthHeight = Input.LeftTop_WidthHeight.zw;
	
				return Out;
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
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	
	MainCode PixelShaderArea
	{
		Input = "VS_OUTPUT_PDX_GUI2"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 scale = Input.WidthHeight / (Input.WidthHeight - vec2( 1.0 ));
				float2 offset = Input.Pos * scale - float2(0.5, 0.5) / Input.WidthHeight;
				
				float4 ColorBackground = PdxTex2D( Texture, Input.Pos * (Input.WidthHeight / vec2(16.0)) );
				float4 ColorOut = CachedColor;
				ColorOut.a = 1.0f;

				if( nActiveColor == 0 ) // Red
				{
					ColorOut.r = CachedColor.r;
					ColorOut.g = 1.0 - offset.y;
					ColorOut.b = offset.x;
				}
				else if( nActiveColor == 1 ) // Green
				{
					ColorOut.r = 1.0 - offset.y;
					ColorOut.g = CachedColor.g;
					ColorOut.b = offset.x;
				}
				else if( nActiveColor == 2 ) // Blue
				{
					ColorOut.r = offset.x;
					ColorOut.g = 1.0 - offset.y;
					ColorOut.b = CachedColor.b;
				}
				else if( nActiveColor == 3 ) // Alpha
				{
				}
				else if( nActiveColor == 4 ) // Hue
				{
					float3 HSV = RGBtoHSV( CachedColor.rgb );
					HSV.g = offset.x;
					HSV.b = 1.0 - offset.y;
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				else if( nActiveColor == 5 ) // Saturation
				{
					float3 HSV = RGBtoHSV( CachedColor.rgb );
					HSV.r = offset.x;
					HSV.b = 1.0 - offset.y;
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				else if( nActiveColor == 6 ) // Value
				{
					float3 HSV = RGBtoHSV( CachedColor.rgb );
					HSV.r = offset.x;
					HSV.g = 1.0 - offset.y;
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				
			    return DisableColorReturn( ColorOut * ColorOut.a + ColorBackground * (1.0 - ColorOut.a) );
			}
		]]
	}
	
	MainCode PixelShaderSlider
	{
		Input = "VS_OUTPUT_PDX_GUI2"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 scale = Input.WidthHeight / (Input.WidthHeight - vec2( 1.0 ));
				float2 offset = Input.Pos * scale - float2(0.5, 0.5) / Input.WidthHeight;
				
				float4 ColorBackground = PdxTex2D( Texture, Input.Pos * (Input.WidthHeight / float2(16.0, 16.0)) );
				float4 ColorOut = CachedColor;
				
				if( nActiveColor == 0 )
				{
					ColorOut.r = 1.0 - offset.y;
					ColorOut.g = CachedColor.g;
					ColorOut.b = CachedColor.b;
				}
				else if( nActiveColor == 1 )
				{
					ColorOut.r = CachedColor.r;
					ColorOut.g = 1.0 - offset.y;
					ColorOut.b = CachedColor.b;
				}
				else if( nActiveColor == 2 )
				{
					ColorOut.r = CachedColor.r;
					ColorOut.g = CachedColor.g;
					ColorOut.b = 1.0 - offset.y;
				}
				else if( nActiveColor == 3 )
				{
					ColorOut.r = CachedColor.r;
					ColorOut.g = CachedColor.g;
					ColorOut.b = CachedColor.b;
					ColorOut.a = 1.0 - offset.y;
					
					return ColorOut * ColorOut.a + ColorBackground * (1.0 - ColorOut.a);
				}
				else if( nActiveColor == 4 )
				{
					float3 HSV = float3(  1.0, 1.0, 1.0 );
					HSV.r = ( 1.0 - offset.y );
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				else if( nActiveColor == 5 )
				{
					float3 HSV = RGBtoHSV( CachedColor.rgb );
					HSV.g = 1.0 - offset.y;
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				else if( nActiveColor == 6 )
				{
					float3 HSV = RGBtoHSV( CachedColor.rgb );
					HSV.b = 1.0 - offset.y;
					ColorOut.rgb = HSVtoRGB( HSV );
				}
				
			    return DisableColorReturn( ColorOut * CachedColor.a + ColorBackground * (1.0 - CachedColor.a) );
			}
		]]		
	}
	
	MainCode PixelShaderButton
	{
		Input = "VS_OUTPUT_PDX_GUI2"
		Output = "PDX_COLOR"
		
		Code
		[[
			PDX_MAIN
			{
				float4 ColorBackground = PdxTex2D( Texture, Input.Pos * (Input.WidthHeight / float2(16.0, 16.0)) );
				float4 ColorOut = ColorIn;
				return DisableColorReturn( ColorOut * ColorIn.a + ColorBackground * (1.0 - ColorIn.a) );
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


Effect PdxGuiColorArea
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderArea"
}

Effect PdxGuiColorAreaDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderArea"
	
	Defines = { "DISABLED" }
}


Effect PdxGuiColorSlider
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderSlider"
}

Effect PdxGuiColorSliderDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderSlider"
	
	Defines = { "DISABLED" }
}


Effect PdxGuiColorButton
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderButton"
}

Effect PdxGuiColorButtonDisabled
{
	VertexShader = "VertexShader"
	PixelShader = "PixelShaderButton"
	
	Defines = { "DISABLED" }
}
