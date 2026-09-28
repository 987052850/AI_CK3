Includes = {
	"cw/fullscreen_vertexshader.fxh"
	"jomini/posteffect_base.fxh"
}

ConstantBuffer( JominiLensFlareConstants )
{
	float _LensToScreenScale;
	float _Scale;
	float _Bias;
	float _GhostDispersal;
	float _DistortionFactor;
	float _HaloPow;
	float _HaloWidth;
	float _HaloRadius;
	float _DistortionFactorHalo;
};

PixelShader = 
{
	Code
	[[
		float3 ChromaticSample( in PdxTextureSampler2D Texture, float2 UV, float2 Direction, float3 Distortion )
		{
			float3 Ret = float3(PdxTex2DLod0( Texture, UV + Direction * Distortion.r ).r,
											 PdxTex2DLod0( Texture, UV + Direction * Distortion.g ).g,
											 PdxTex2DLod0( Texture, UV + Direction * Distortion.b ).b);
			//Lastly we threshold the input with the help of the bias and scale parameters to avoid too many lens flares
			Ret = max( vec3( 0.0f ), ( Ret + _Bias ) * _Scale );
			return Ret;
		}

		float WindowCubic( float x, float c, float r )
		{
			x = min( abs( x - c ) / r, 1.0f );
			return 1.0f - x * x * ( 3.0f - 2.0f * x );
		}
	]]
}