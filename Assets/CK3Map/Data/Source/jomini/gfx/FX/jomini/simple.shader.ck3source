Includes = {
}

PixelShader =
{
	TextureSampler SimpleTexture
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
}


VertexStruct VS_INPUT
{
    float4 vPosition  : POSITION;
    float2 vTexCoord  : TEXCOORD0;
	float4 vColor	  : COLOR;
};

VertexStruct VS_OUTPUT
{
    float4  vPosition : PDX_POSITION;
    float2  vTexCoord : TEXCOORD0;
	float4  vColor	  : TEXCOORD1;
};


ConstantBuffer( 0 )
{
	float4x4 Mat;
};


VertexShader =
{
	MainCode VertexShaderSimple3D
	{
		Input = "VS_INPUT"
		Output = "VS_OUTPUT"
		Code
		[[
			PDX_MAIN
			{
			    VS_OUTPUT Out;

			    Out.vPosition  	= mul( Mat, Input.vPosition );
			    Out.vTexCoord  	= Input.vTexCoord;
				Out.vColor		= Input.vColor;
			
			    return Out;
			}
		]]
	}

	MainCode VertexShaderSimple
	{
		Input = "VS_INPUT"
		Output = "VS_OUTPUT"
		Code
		[[
			PDX_MAIN
			{
			    VS_OUTPUT Out;

			    Out.vPosition  	= mul( Mat, Input.vPosition );
			    Out.vTexCoord  	= Input.vTexCoord;			
				Out.vColor		= Input.vColor;
			
			    return Out;
			}
		]]
	}
}

PixelShader =
{
	MainCode PixelShaderSimple
	{
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
			    float4 OutColor = PdxTex2D( SimpleTexture, Input.vTexCoord );
				OutColor = OutColor * Input.vColor;
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


Effect Simple
{
	VertexShader = "VertexShaderSimple"
	PixelShader = "PixelShaderSimple"
}

Effect Simple3D
{
	VertexShader = "VertexShaderSimple3D"
	PixelShader = "PixelShaderSimple"
}

