Includes = {
	"cw/pdxterrain.fxh"
	"cw/heightmap.fxh"
	"cw/shadow.fxh"
	"cw/utility.fxh"
	"cw/lighting_util.fxh"
	"jomini/jomini_lighting.fxh"
	"jomini/mapeditor/mapeditor_constants.fxh"
	"jomini/mapeditor/mapeditor_utils.fxh"
	"jomini/mapeditor/mapeditor_gruvbox.fxh"
}


VertexStruct VS_OUTPUT_PDX_TERRAIN
{
	float4 Position			: PDX_POSITION;
	float3 WorldSpacePos	: TEXCOORD0;
	float4 ShadowProj		: TEXCOORD2;
};


VertexShader =
{
	Code
	[[
		VS_OUTPUT_PDX_TERRAIN TerrainVertex( float2 WithinNodePos, float2 NodeOffset, float NodeScale, float2 LodDirection, float LodLerpFactor )
		{
			STerrainVertex Vertex = CalcTerrainVertex( WithinNodePos, NodeOffset, NodeScale, LodDirection, LodLerpFactor );
			
			VS_OUTPUT_PDX_TERRAIN Out;
			Out.WorldSpacePos = Vertex.WorldSpacePos;
			
			Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );
			Out.ShadowProj = mul( ShadowMapTextureMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );
			
			return Out;
		}
	]]
	
	MainCode VertexShader
	{
		Input = "VS_INPUT_PDX_TERRAIN"
		Output = "VS_OUTPUT_PDX_TERRAIN"
		Code
		[[			
			PDX_MAIN
			{
				return TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );
			}
		]]
	}
	
	MainCode VertexShaderSkirt
	{
		Input = "VS_INPUT_PDX_TERRAIN_SKIRT"
		Output = "VS_OUTPUT_PDX_TERRAIN"
		Code
		[[			
			PDX_MAIN
			{
				VS_OUTPUT_PDX_TERRAIN Out = TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );
				
				float3 Position = FixPositionForSkirt( Out.WorldSpacePos, Input.VertexID );
				Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Position, 1.0 ) );

				return Out;
			}
		]]
	}
}


PixelShader =
{
	TextureSampler MaskTexture
	{
		Index = 7
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	TextureSampler MaskPaletteTexture
	{
		Index = 8
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	MainCode NoPixelShader
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				discard;
				return float4(0, 0, 0, 0);
			}
		]]
	}

	MainCode HeightmapResolution
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				const float FADE_DIST = 1000;
				const float BORDER_SIZE_MAX = 0.5;
				const float BORDER_SIZE_MIN = 0.02;

				float CameraDistance = distance( CameraPosition, Input.WorldSpacePos.xyz );
   				float CameraDistFactor = ( FADE_DIST - clamp( CameraDistance, 0.0, FADE_DIST )) / FADE_DIST;
   				//CameraDistFactor = smoothstep( 0, 1, CameraDistFactor );

   				// NOTE(TS): Do not clamp to [0..1]
				float2 LookupCoordinates = Input.WorldSpacePos.xz * WorldSpaceToLookup;

				// Do not render if outside lookup map
				if( LookupCoordinates.x >= 1.0 || LookupCoordinates.x < 0
					|| LookupCoordinates.y > 1.0 || LookupCoordinates.y < 0.0  )
				{
					return float4(0, 0, 0, 0);
				}

				float4 IndirectionSample = SampleLookupTexture( LookupCoordinates );
				float BorderSize = lerp( BORDER_SIZE_MAX, BORDER_SIZE_MIN, CameraDistFactor );
				BorderSize = BorderSize / ( IndirectionSample.w + 1 );
				float2 HeightMapCoord = GetHeightMapCoordinates( Input.WorldSpacePos.xz );
				float HeightmapBorder = GetTextureBorder( HeightMapCoord, PackedHeightMapSize, BorderSize );

				// Highlight selection
				float4 SelectionMask = PdxTex2DLod0( MaskTexture, LookupCoordinates );
				float SelectionAlpha = 0.0;
				if( SelectionMask.r > 0.0 )
				{
					SelectionAlpha = 1.0;
				}

				float3 BorderColor = MapEditorGetCompressionLevelColor( int(IndirectionSample.w) );
				float4 SolidGridColor = ( 1.0 - CameraDistFactor ) * float4( BorderColor, lerp( 0.3, 0.8, SelectionAlpha ) );
				float4 GridColor = CameraDistFactor * HeightmapBorder * float4( BorderColor, lerp( 0.4, 0.8, SelectionAlpha ) * HeightmapBorder );
				float4 FinalColor = SolidGridColor + GridColor;

				// Sample lookup border
				float LookupBorder = GetTextureBorder( LookupCoordinates, IndirectionSize, lerp( 0.1, 0.006, CameraDistFactor ) );
				if( LookupBorder > 0 )
				{
					FinalColor = float4( BorderColor, lerp( 0.4, 1.0, SelectionAlpha ) );
				}

				return FinalColor;
			}
		]]
	}
	
	MainCode Select
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float4 FinalColor = float4(0,0,0,0);
				
				if( Input.WorldSpacePos.x >= SelectMin.x &&
					Input.WorldSpacePos.x <= SelectMax.x &&
					Input.WorldSpacePos.z >= SelectMin.y &&
					Input.WorldSpacePos.z <= SelectMax.y)
				{
					FinalColor = float4(GRUVBOX_DARK_GREEN, 0.4);
				}
				
				return FinalColor;
			}
		]]
	}

	MainCode BrushOutline
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float4 FinalColor = float4( 0, 0, 0, 0 );
				TerrainEditorBrushOutline( FinalColor, CursorPos, Input.WorldSpacePos.xz, CameraPosition.y );
				return FinalColor;
			}
		]]
	}

	TextureSampler DetailErrorsTexture
	{
		Ref = TerrainDetailErrorTexture
		MagFilter = "Point"
		MinFilter = "Point"
		MipFilter = "Point"
		SampleModeU = "Clamp"
		SampleModeV = "Clamp"
	}

	MainCode DetailErrors
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float CameraDistance = distance( CameraPosition, Input.WorldSpacePos.xyz );
	    		float CameraDistFactor = (CHECKERS_FADE_DIST - clamp( CameraDistance, 0.0, CHECKERS_FADE_DIST )) / CHECKERS_FADE_DIST;

				float3 Checkers = TerrainEditorCheckers( CHECKERS_COLOR_ERROR, Input.WorldSpacePos.xz, CameraDistFactor ).rgb;
				float2 DetailErrorsCoordinates = Input.WorldSpacePos.xz * WorldSpaceToDetail + DetailTexelSize * 0.5;
				float4 DetailError = PdxTex2DLod0( DetailErrorsTexture, DetailErrorsCoordinates );
				float4 FinalColor = float4( Checkers.rgb, DetailError.r );

				FinalColor.a *= ( 1.0 - CameraDistFactor * 0.7 );

				return FinalColor;

			}
		]]
	}

	MainCode HighlightMaterials
	{
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				if( !MapEditorIsHighlighted( Input.WorldSpacePos.xz ) )
				{
					discard;
				}
				float CameraDistance = distance( CameraPosition, Input.WorldSpacePos.xyz );
				float CameraDistFactor = (CHECKERS_FADE_DIST - clamp( CameraDistance, 0.0, CHECKERS_FADE_DIST )) / CHECKERS_FADE_DIST;
				return float4( 1.0, 1.0, 1.0, 1.0 - CameraDistFactor * 0.7 );
			}
		]]
	}
	MainCode MaskOverlay
	{
		ConstantBuffer( PdxConstantBuffer0 )
		{
			float4 MaskOverlayRGBA;
			float2 MaskOverlayScale;
		}		
		Input = "VS_OUTPUT_PDX_TERRAIN"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				float2 UV = ( Input.WorldSpacePos.xz * WorldSpaceToDetail + DetailTexelSize * 0.5 ) / MaskOverlayScale;

			#ifdef MASK_OVERLAY_RGB

				return PdxTex2D( MaskTexture, UV ) * MaskOverlayRGBA;

			#elif defined( MASK_OVERLAY_RGB_PALETTE )
				
				#ifdef PALETTE_32

					float4 IndexToReconstruct = PdxTex2D( MaskTexture, UV ).rgba;

					int4 ColorIndices = int4( int( IndexToReconstruct.r * 255.0 ),
						int( IndexToReconstruct.g * 255.0 ),
						int( IndexToReconstruct.b * 255.0 ),
						int( IndexToReconstruct.a * 255.0 ) );

					int ColorIndex = ( ColorIndices.r & 0x000000ff )
						| ( ( ColorIndices.g << 8 ) & 0x0000ff00 )
						| ( ( ColorIndices.b << 16 ) & 0x00ff0000 ) 
						| ( ( ColorIndices.a << 24 ) & 0xff000000 );

				#elif defined ( PALETTE_16 )

					int ColorIndex = int( PdxTex2D( MaskTexture, UV ).r * 65535.0 );

				#elif defined ( PALETTE_8 )

					int ColorIndex = int( PdxTex2D( MaskTexture, UV ).r * 255.0 );

				#endif

				float2 PaletteSize;
				PdxTex2DSize( MaskPaletteTexture, PaletteSize );
				int PaletteWidth = int( PaletteSize.x );
				int Column = ColorIndex % PaletteWidth;
				int Row = ColorIndex / PaletteWidth;
				return PdxTex2DLoad0( MaskPaletteTexture, int2( Column, Row ) ).bgra * MaskOverlayRGBA;	

			#else

				float Opacity = PdxTex2D( MaskTexture, UV ).r;
				return float4( MaskOverlayRGBA.rgb, MaskOverlayRGBA.a * Opacity );

			#endif
			}
		]]
	}
	MainCode PartialTerrainOverlay
    {
        ConstantBuffer( PdxConstantBuffer0 )
        {
            float2 OverlayWorldStart;
            float2 OverlayWorldEnd;
            float4 OverlayTexelSize;
            float Opacity;
            int FlipY;
        }
        TextureSampler OverlayTexture
        {
            Ref = EditorTerrainOverlayTexture
            MagFilter = "Linear"
            MinFilter = "Linear"
            MipFilter = "Linear"
            SampleModeU = "Clamp"
            SampleModeV = "Clamp"
        }
        Input = "VS_OUTPUT_PDX_TERRAIN"
        Output = "PDX_COLOR"
        Code
        [[
            PDX_MAIN
            {
                if (Input.WorldSpacePos.x < OverlayWorldStart.x || Input.WorldSpacePos.z < OverlayWorldStart.y || Input.WorldSpacePos.x > OverlayWorldEnd.x || Input.WorldSpacePos.z > OverlayWorldEnd.y)
                {
                    discard;
                }

                float2 UV = ( (Input.WorldSpacePos.xz - OverlayWorldStart) / (OverlayWorldEnd - OverlayWorldStart) );
                if ( FlipY == 1 )
                {
                    UV.y = 1 - UV.y;
                }
                float4 OverlayRGBA = PdxTex2D( OverlayTexture, UV );
                return float4( OverlayRGBA.rgb, OverlayRGBA.a * Opacity );
            }
        ]]
    }
    MainCode PartialTerrainOverlayNoInterpolation
    {
        ConstantBuffer( PdxConstantBuffer0 )
        {
            float2 OverlayWorldStart;
            float2 OverlayWorldEnd;
            float4 OverlayTexelSize;
            float Opacity;
            int FlipY;
        }
        TextureSampler OverlayTexture
        {
            Ref = EditorTerrainOverlayTexture
            MagFilter = "Point"
            MinFilter = "Point"
            MipFilter = "Point"
            SampleModeU = "Clamp"
            SampleModeV = "Clamp"
        }
        Input = "VS_OUTPUT_PDX_TERRAIN"
        Output = "PDX_COLOR"
        Code
        [[
            PDX_MAIN
            {
                if (Input.WorldSpacePos.x < OverlayWorldStart.x || Input.WorldSpacePos.z < OverlayWorldStart.y || Input.WorldSpacePos.x > OverlayWorldEnd.x || Input.WorldSpacePos.z > OverlayWorldEnd.y)
                {
                    discard;
                }

                float2 UV = ( (Input.WorldSpacePos.xz - OverlayWorldStart) / (OverlayWorldEnd - OverlayWorldStart) );
                if ( FlipY == 1 )
                {
                    UV.y = 1 - UV.y;
                }
                float4 OverlayRGBA = PdxTex2D( OverlayTexture, UV );
                return float4( OverlayRGBA.rgb, OverlayRGBA.a * Opacity );
            }
        ]]
    }
}

###

BlendState BlendStateOverlay
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
}

BlendState BlendStateAdd
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "ONE"
	WriteMask = "RED|GREEN|BLUE|ALPHA"
}

DepthStencilState DepthStencilStateOverlay
{
	DepthEnable = no
}

###

Effect PdxTerrainSelect
{
	VertexShader = "VertexShader"
	PixelShader = "Select"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}

Effect PdxTerrainSelectSkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxTerrainWireframe
{
	VertexShader = "VertexShader"
	PixelShader = "HeightmapResolution"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}

Effect PdxTerrainWireframeSkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxTerrainBrushOutline
{
	VertexShader = "VertexShader"
	PixelShader = "BrushOutline"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}

Effect PdxTerrainBrushOutlineSkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxTerrainDetailErrors
{
	VertexShader = "VertexShader"
	PixelShader = "DetailErrors"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}

Effect PdxTerrainDetailErrorsSkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxTerrainHighlightMaterials
{
	VertexShader = "VertexShader"
	PixelShader = "HighlightMaterials"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}

Effect PdxTerrainHighlightMaterialsSkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxTerrainMaskOverlay
{
	VertexShader = "VertexShader"
	PixelShader = "MaskOverlay"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}
Effect PdxTerrainMaskOverlaySkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxPartialTerrainOverlay
{
	VertexShader = "VertexShader"
	PixelShader = "PartialTerrainOverlay"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}
Effect PdxPartialTerrainOverlaySkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}

###

Effect PdxPartialTerrainOverlayNoInterpolation
{
	VertexShader = "VertexShader"
	PixelShader = "PartialTerrainOverlayNoInterpolation"
	BlendState = BlendStateOverlay
	DepthStencilState = DepthStencilStateOverlay
}
Effect PdxPartialTerrainOverlaySkirt
{
	VertexShader = "VertexShaderSkirt"
	PixelShader = "NoPixelShader"
}