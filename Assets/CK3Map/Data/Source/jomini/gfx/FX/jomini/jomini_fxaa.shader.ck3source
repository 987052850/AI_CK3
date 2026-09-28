
Includes = {
	"cw/utility.fxh"
	"cw/camera.fxh"
	"jomini/posteffect_base.fxh"
}

ConstantBuffer( 2 )
{
	float2 InverseScreenResolution;				
	# Only used on FXAA Quality.
	# The minimum amount of local contrast required to apply algorithm.
	#   0.333 - too little (faster)
	#   0.250 - low quality
	#   0.166 - default
	#   0.125 - high quality 
	#   0.063 - overkill (slower)
	float QualityEdgeThreshold;// = 0.166;
	#
	# Only used on FXAA Quality.
	# Trims the algorithm from processing darks.
	#   0.0833 - upper limit (default, the start of visible unfiltered edges)
	#   0.0625 - high quality (faster)
	#   0.0312 - visible limit (slower)
	# Special notes when using FXAA_GREEN_AS_LUMA,
	#   Likely want to set this to zero.
	#   As colors that are mostly not-green
	#   will appear very dark in the green channel!
	#   Tune by looking at mostly non-green content,
	#   then start at zero and increase until aliasing is a problem.
	float QualityEdgeThresholdMin;// = 0.0625;
	
	# Only used on FXAA Quality.
	# Choose the amount of sub-pixel aliasing removal.
	# This can effect sharpness.
	#   1.00 - upper limit (softer)
	#   0.75 - default amount of filtering
	#   0.50 - lower limit (sharper, less sub-pixel aliasing removal)
	#   0.25 - almost off
	#   0.00 - completely off
	float QualitySubpix;// = 0.75;
};

PixelShader =
{
	TextureSampler MainScene
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	MainCode PixelShader
	{
		Input = "VS_OUTPUT_FULLSCREEN"
		Output = "PDX_COLOR"
		Code
		[[
			// Depending on ERestoreEffect we either already have luma in aplha channel from restorescene.shader,
			// or we need to calculate it
			float CalculateLuma( float4 rgba )
			{
				#if defined( CALC_LUMA )
					return dot( LUMINANCE_VECTOR, rgba.rgb );
				#else
					return rgba.a;
				#endif
			}

			PDX_MAIN
			{
				//NVIDIA FXAA 3.11 by TIMOTHY LOTTES
				//Described in heavy detail: https://gist.github.com/kosua20/0c506b81b3812ac900048059d2383126
								
				/////////////////////
				// static stuff 
				/////////////////////
				
				#define FXAA_QUALITY_P0 1.0
				#define FXAA_QUALITY_P1 1.5
				#define FXAA_QUALITY_P2 2.0
				#define FXAA_QUALITY_P3 4.0
				#define FXAA_QUALITY_P4 12.0
				
				//////////////////////
				//Actual shader
				//////////////////////
								 
				float4 color = PdxTex2DLod0( MainScene, Input.uv );
			
				////////////
				// Finding luma in neighbour		
				////////////		
				float lumaS = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(0, 1) ) );
				float lumaE = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(1, 0) ) );
				float lumaN = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(0, -1) ) );
				float lumaW = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(-1, 0) ) );
				float lumaM = CalculateLuma( color );				
				
				float maxSM = max(lumaS, lumaM);
				float minSM = min(lumaS, lumaM);
				float maxESM = max(lumaE, maxSM);
				float minESM = min(lumaE, minSM);
				float maxWN = max(lumaN, lumaW);
				float minWN = min(lumaN, lumaW);
				float rangeMax = max(maxWN, maxESM);
				float rangeMin = min(minWN, minESM);
				float rangeMaxScaled = rangeMax * QualityEdgeThreshold;
				float range = rangeMax - rangeMin;
				float rangeMaxClamped = max(QualityEdgeThresholdMin, rangeMaxScaled);
				bool earlyExit = range < rangeMaxClamped;
				
				if(earlyExit)
				{
					return color;
				}
				
				float lumaNW = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(-1, -1) ) );
				float lumaSE = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(1, 1) ) );
				float lumaNE = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(1, -1) ) );
				float lumaSW = CalculateLuma( PdxTex2DLod0Offset( MainScene, Input.uv, int2(-1, 1) ) );
				
				float lumaNS = lumaN + lumaS;
				float lumaWE = lumaW + lumaE;
				float subpixRcpRange = 1.0/range;
				float subpixNSWE = lumaNS + lumaWE;
				float edgeHorz1 = (-2.0 * lumaM) + lumaNS;
				float edgeVert1 = (-2.0 * lumaM) + lumaWE;
				
				float lumaNESE = lumaNE + lumaSE;
				float lumaNWNE = lumaNW + lumaNE;
				float edgeHorz2 = (-2.0 * lumaE) + lumaNESE;
				float edgeVert2 = (-2.0 * lumaN) + lumaNWNE;
				
				float lumaNWSW = lumaNW + lumaSW;
				float lumaSWSE = lumaSW + lumaSE;
				float edgeHorz4 = (abs(edgeHorz1) * 2.0) + abs(edgeHorz2);
				float edgeVert4 = (abs(edgeVert1) * 2.0) + abs(edgeVert2);
				float edgeHorz3 = (-2.0 * lumaW) + lumaNWSW;
				float edgeVert3 = (-2.0 * lumaS) + lumaSWSE;
				float edgeHorz = abs(edgeHorz3) + edgeHorz4;
				float edgeVert = abs(edgeVert3) + edgeVert4;
				
				float subpixNWSWNESE = lumaNWSW + lumaNESE;
				float lengthSign = InverseScreenResolution.x;
				bool horzSpan = edgeHorz >= edgeVert;
				float subpixA = subpixNSWE * 2.0 + subpixNWSWNESE;
				
				if(!horzSpan)
				{
					lumaN = lumaW;
					lumaS = lumaE;
				}
				else 
				{
					lengthSign = InverseScreenResolution.y;
				}				
				float subpixB = (subpixA * (1.0/12.0)) - lumaM;
				
				float gradientN = lumaN - lumaM;
				float gradientS = lumaS - lumaM;
				float lumaNN = lumaN + lumaM;
				float lumaSS = lumaS + lumaM;
				bool pairN = abs(gradientN) >= abs(gradientS);
				float gradient = max(abs(gradientN), abs(gradientS));
				if(pairN) lengthSign = -lengthSign;
				float subpixC = saturate(abs(subpixB) * subpixRcpRange);
				
				float2 posB = Input.uv;
				float2 offNP;
				offNP.x = (!horzSpan) ? 0.0 : InverseScreenResolution.x;
				offNP.y = ( horzSpan) ? 0.0 : InverseScreenResolution.y;
				if(!horzSpan) posB.x += lengthSign * 0.5;
				if( horzSpan) posB.y += lengthSign * 0.5;
				
				float2 posN;
				posN.x = posB.x - offNP.x * FXAA_QUALITY_P0;
				posN.y = posB.y - offNP.y * FXAA_QUALITY_P0;
				float2 posP;
				posP.x = posB.x + offNP.x * FXAA_QUALITY_P0;
				posP.y = posB.y + offNP.y * FXAA_QUALITY_P0;
				float subpixD = ((-2.0)*subpixC) + 3.0;
				float lumaEndN = CalculateLuma(PdxTex2DLod0(MainScene, posN));
				float subpixE = subpixC * subpixC;
				float lumaEndP = CalculateLuma(PdxTex2DLod0(MainScene, posP));
				
				if(!pairN) lumaNN = lumaSS;
				float gradientScaled = gradient * 1.0/4.0;
				float lumaMM = lumaM - lumaNN * 0.5;
				float subpixF = subpixD * subpixE;
				bool lumaMLTZero = lumaMM < 0.0;
			/*--------------------------------------------------------------------------*/
				lumaEndN -= lumaNN * 0.5;
				lumaEndP -= lumaNN * 0.5;
				bool doneN = abs(lumaEndN) >= gradientScaled;
				bool doneP = abs(lumaEndP) >= gradientScaled;
				if(!doneN) posN.x -= offNP.x * FXAA_QUALITY_P1;
				if(!doneN) posN.y -= offNP.y * FXAA_QUALITY_P1;
				bool doneNP = (!doneN) || (!doneP);
				if(!doneP) posP.x += offNP.x * FXAA_QUALITY_P1;
				if(!doneP) posP.y += offNP.y * FXAA_QUALITY_P1;
				
				//Various passes to improve the end result
				if(doneNP) 
				{
					if(!doneN) lumaEndN = CalculateLuma(PdxTex2DLod0(MainScene, posN.xy));
					if(!doneP) lumaEndP = CalculateLuma(PdxTex2DLod0(MainScene, posP.xy));
					if(!doneN) lumaEndN = lumaEndN - lumaNN * 0.5;
					if(!doneP) lumaEndP = lumaEndP - lumaNN * 0.5;
					doneN = abs(lumaEndN) >= gradientScaled;
					doneP = abs(lumaEndP) >= gradientScaled;
					if(!doneN) posN.x -= offNP.x * FXAA_QUALITY_P2;
					if(!doneN) posN.y -= offNP.y * FXAA_QUALITY_P2;
					doneNP = (!doneN) || (!doneP);
					if(!doneP) posP.x += offNP.x * FXAA_QUALITY_P2;
					if(!doneP) posP.y += offNP.y * FXAA_QUALITY_P2;
					
					if(doneNP) 
					{
						if(!doneN) lumaEndN = CalculateLuma(PdxTex2DLod0(MainScene, posN.xy));
						if(!doneP) lumaEndP = CalculateLuma(PdxTex2DLod0(MainScene, posP.xy));
						if(!doneN) lumaEndN = lumaEndN - lumaNN * 0.5;
						if(!doneP) lumaEndP = lumaEndP - lumaNN * 0.5;
						doneN = abs(lumaEndN) >= gradientScaled;
						doneP = abs(lumaEndP) >= gradientScaled;
						if(!doneN) posN.x -= offNP.x * FXAA_QUALITY_P3;
						if(!doneN) posN.y -= offNP.y * FXAA_QUALITY_P3;
						doneNP = (!doneN) || (!doneP);
						if(!doneP) posP.x += offNP.x * FXAA_QUALITY_P3;
						if(!doneP) posP.y += offNP.y * FXAA_QUALITY_P3;
						
						if(doneNP) 
						{
							if(!doneN) lumaEndN = CalculateLuma(PdxTex2DLod0(MainScene, posN.xy));
							if(!doneP) lumaEndP = CalculateLuma(PdxTex2DLod0(MainScene, posP.xy));
							if(!doneN) lumaEndN = lumaEndN - lumaNN * 0.5;
							if(!doneP) lumaEndP = lumaEndP - lumaNN * 0.5;
							doneN = abs(lumaEndN) >= gradientScaled;
							doneP = abs(lumaEndP) >= gradientScaled;
							if(!doneN) posN.x -= offNP.x * FXAA_QUALITY_P4;
							if(!doneN) posN.y -= offNP.y * FXAA_QUALITY_P4;
							if(!doneP) posP.x += offNP.x * FXAA_QUALITY_P4;
							if(!doneP) posP.y += offNP.y * FXAA_QUALITY_P4;
						}
					}
				}
				
				float dstN = Input.uv.x - posN.x;
				float dstP = posP.x - Input.uv.x;
				if(!horzSpan) dstN = Input.uv.y - posN.y;
				if(!horzSpan) dstP = posP.y - Input.uv.y;
			/*--------------------------------------------------------------------------*/
				bool goodSpanN = (lumaEndN < 0.0) != lumaMLTZero;
				float spanLength = (dstP + dstN);
				bool goodSpanP = (lumaEndP < 0.0) != lumaMLTZero;
				float spanLengthRcp = 1.0/spanLength;
			/*--------------------------------------------------------------------------*/
				bool directionN = dstN < dstP;
				float dst = min(dstN, dstP);
				bool goodSpan = directionN ? goodSpanN : goodSpanP;
				float subpixG = subpixF * subpixF;
				float pixelOffset = (dst * (-spanLengthRcp)) + 0.5;
				float subpixH = subpixG * QualitySubpix;
			/*--------------------------------------------------------------------------*/
				float pixelOffsetGood = goodSpan ? pixelOffset : 0.0;
				float pixelOffsetSubpix = max(pixelOffsetGood, subpixH);
				if(!horzSpan) Input.uv.x += pixelOffsetSubpix * lengthSign;
				if( horzSpan) Input.uv.y += pixelOffsetSubpix * lengthSign;

				return float4( PdxTex2DLod0( MainScene, Input.uv ).xyz, color.a );
			}
		]]
	}
}


Effect Fxaa
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"
}

Effect FxaaCalcLuma
{
	VertexShader = "VertexShaderFullscreen"
	PixelShader = "PixelShader"
	Defines = { "CALC_LUMA" }
}
