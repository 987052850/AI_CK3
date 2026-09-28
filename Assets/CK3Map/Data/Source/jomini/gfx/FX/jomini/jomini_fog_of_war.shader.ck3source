Includes = {
	#"jomini/jomini_fog_of_war.fxh"
	"cw/fullscreen_vertexshader.fxh"
	"cw/random.fxh"
}

ConstantBuffer( PdxConstantBuffer0 )
{	
	float2		KernelSize;
	float2		NoiseUvScale;
	int			NumSamples;
	float3 dummy;	
	float4		DiscSamples[8];
}

PixelShader =
{
	TextureSampler IndirectionMap
	{
		Ref = JominiProvinceColorIndirection
		MinFilter = "Point"
		MagFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}
	TextureSampler ProvinceAlpha
	{
		Ref = PdxTexture0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	TextureSampler NoiseTexture
	{
		Ref = PdxTexture1
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}

	MainCode PS_create_alpha_map
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			
			float2 RotateDisc( float2 Disc, float2 Rotate )
			{
				return float2( Disc.x * Rotate.x - Disc.y * Rotate.y, Disc.x * Rotate.y + Disc.y * Rotate.x );
			}
			PDX_MAIN
			{
				float Alpha = 0;
				float RandomAngle = CalcRandom( Input.uv ) * 3.14159 * 2.0;
				float2 Rotate = float2( cos( RandomAngle ), sin( RandomAngle ) );
				int Samples = (NumSamples+1) / 2;
				for( int i = 0; i < Samples; ++i )
				{
					float2 ColorIndex = PdxTex2DLod0( IndirectionMap, Input.uv + RotateDisc(DiscSamples[i].xy,Rotate) * KernelSize ).rg;
					Alpha += PdxTex2DLoad0( ProvinceAlpha, int2( ColorIndex * 255.0 ) ).r;
					ColorIndex = PdxTex2DLod0( IndirectionMap, Input.uv + RotateDisc(DiscSamples[i].zw,Rotate) * KernelSize ).rg;
					Alpha += PdxTex2DLoad0( ProvinceAlpha, int2( ColorIndex * 255.0 ) ).r;
				}
				Alpha /= Samples*2;
				float Noise = PdxTex2D( NoiseTexture, Input.uv * NoiseUvScale ).r;
				return float4( Alpha, Noise, 0, 1);
			}
		]]
	}
}

BlendState BlendState
{
	BlendEnable = no
}
Effect CreateFogOfWarAlphaMap
{
	VertexShader = VertexShaderFullscreen
	PixelShader = PS_create_alpha_map
}