Includes = {
	"cw/camera.fxh"
	"cw/utility.fxh"
}

# Please note that this file is for the spline system
# ( thus it has nothing to do with "border splines" )

VertexStruct VS_INPUT
{
    float3  Position   		: POSITION;
	float	MaxU	 		: TEXCOORD0;
	float2  UV				: TEXCOORD1;
	float3	Tangent 		: TEXCOORD2;
	float3	Normal			: TEXCOORD3;
};

VertexStruct VS_OUTPUT
{
    float4 Position	    	: PDX_POSITION;
	float2 UV			    : TEXCOORD0;
	float3 Tangent			: TEXCOORD1;
	float3 Normal			: TEXCOORD2;
	float3 WorldSpacePos	: TEXCOORD3;
	float  MaxU				: TEXCOORD4;
};

ConstantBuffer( 3 )
{
	float GlobalOpacity;
	float UVScale;
	float EndFadeoutFactor;
};

VertexShader =
{
	MainCode VertexShader
	{
		Input = "VS_INPUT"
		Output = "VS_OUTPUT"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT Out;
			
				Out.UV 				= Input.UV;
				Out.Tangent 		= Input.Tangent;
				Out.Normal			= Input.Normal;
				Out.WorldSpacePos 	= Input.Position;
				Out.MaxU 			= Input.MaxU;
				
				Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Input.Position, 1.0f ) );
			
				return Out;
			}		
		]]
	}
}

PixelShader =
{
	Code
	[[
		float3 JominiFlatSplineSampleNormal( in PdxTextureSampler2D NormalTexture, float3 Normal, float3 Tangent, float2 UV, float2 dx, float2 dy )
		{
			float4 NormalSample = PdxTex2DGrad( NormalTexture, UV, dx, dy );

			float3 UnpackedNormalSample = UnpackRRxGNormal( NormalSample );

			Normal = normalize( Normal );
			Tangent = normalize( Tangent );
			float3 Bitangent = normalize( cross( Normal, Tangent ) );
			float3x3 TBN = Create3x3( Tangent, Bitangent, Normal );

			return normalize( mul( UnpackedNormalSample, TBN ) );
		}

		// The mask texture contains 2 horizontal "lanes". The first is for splines' mid-section,
		// the second for splines' start- and end-section.
		float2 JominiFlatSplineSampleMask( in PdxTextureSampler2D MaskTexture, VS_OUTPUT Input )
		{
			float2 Mask = float2( 1,1 );
			float2 MaskUV = Input.UV;
			// each lane occupies a vertical space of height 0.5, this remaps the v-coordinate to that range
			MaskUV.y *= 0.5f;
			float2 dx = ddx( MaskUV );
			float2 dy = ddy( MaskUV );
			
			if ( MaskUV.x < 0.5f )
			{
				// close to the start of the spline, we sample from the 2nd lane of the mask texture
				float2 HeadUV = float2( MaskUV.x, MaskUV.y + 0.5f );
				// note that the transition from 2nd to 1st lane will autmatically be smooth, since the mid-sections of lane 2 and 1 are identical
				Mask = PdxTex2DGrad( MaskTexture, HeadUV, dx, dy ).rg;
			}
			else
			{
				Mask = PdxTex2DGrad( MaskTexture, MaskUV, dx, dy ).rg;
			}

			float DistanceToEnd = Input.MaxU - MaskUV.x;
			if ( DistanceToEnd < 0.5f )
			{
				// close to the end of the spline, we transition to sampling from the 2nd lane of the mask texture
				// (note: for very short splines, the 1st lane was never used, but smooth transition is still required)
				float BlendStart = max( Input.MaxU * 0.33f, Input.MaxU - 0.5f );
				float BlendStop = max( Input.MaxU * 0.66f, Input.MaxU - 0.25f );
				float BlendValue = RemapClamped( MaskUV.x, BlendStart, BlendStop, 0.0f, 1.0f );
				float2 TailUV = float2( 1.0f - DistanceToEnd, MaskUV.y + 0.5f );
				float2 EndSectionMask = PdxTex2DGrad( MaskTexture, TailUV, dx, dy ).rg;
				Mask = lerp( Mask, EndSectionMask, BlendValue );
			}
			
			return Mask;
		}

		float JominiFlatSplineEdgeOpacity( float t, float MaxT, float offset )
		{
			float DistanceToEnd = min( t, MaxT - t ) + 0.0001f;	// Extra bias to make it possible to turn off fadeout completely			
			return RemapClamped( DistanceToEnd * EndFadeoutFactor, 0, offset, 0, 1 );

		}

		// Input Parameters:
		// Input: The vertex input to pixel shader
		// StackedTextureCount: The number of textures stacked in vertical direction in shader		
		void JominiFlatSplineStackedUV(
			VS_OUTPUT Input,
			int StackedTextureCount,
			out float2 UV,
			out float2 dx,
			out float2 dy
		)
		{
			UV = Input.UV;
			uint Variant = uint( CalcRandom( floor( UV.x ) ) * StackedTextureCount );
			UV.y = ( UV.y + float( Variant ) ) / StackedTextureCount;

			dx = ddx( UV );
			dy = ddy( UV );
		}
	]]

}
