Includes = {
	"cw/heightmap.fxh"
	"cw/pdxterrain.fxh"
	"jomini/mapeditor/mapeditor_gruvbox.fxh"
}

PixelShader
{
	Code
	[[
		bool MapEditorIsMaterialHighlighted( uint Index )
		{
			if (Index >= TOTAL_TERRAIN_MATERIAL_COUNT )
			{
				return false;
			}
			uint Row = Index / uint(4);
			uint Col = Index - Row * 4;
			return MaterialsVisibility[Row][Col] > 0.0;
		}

		bool MapEditorIsHighlighted( float2 WorldSpacePosXZ )
		{
			float2 DetailIndexCoordinates = WorldSpacePosXZ * WorldSpaceToDetail + DetailTexelSize * 0.5;
			
			float4 DetailIndex = PdxTex2D( DetailIndexTexture, DetailIndexCoordinates ) * 255.0;

			for( int i = 0; i < 4; ++i )
			{
				if( MapEditorIsMaterialHighlighted( int( DetailIndex[i] ) ) )
				{
					return true;
				}
			}

			return false;
		}

		float3 MapEditorGetCompressionLevelColor( int IndirectionSample )
		{
			// FIXME(TS): prettier way to do this lookup
			#ifdef PDX_HLSL
			const float3 COMPRESS_LEVEL_COLORS[] = {
				GRUVBOX_DARK_RED,
				GRUVBOX_DARK_ORANGE,
				GRUVBOX_DARK_YELLOW,
				GRUVBOX_DARK_GREEN,
				GRUVBOX_DARK_AQUA,
				GRUVBOX_DARK_BLUE,
				GRUVBOX_DARK_PURPLE
			};
			#else
			const float3 COMPRESS_LEVEL_COLORS[] = float3[](
				GRUVBOX_DARK_RED,
				GRUVBOX_DARK_ORANGE,
				GRUVBOX_DARK_YELLOW,
				GRUVBOX_DARK_GREEN,
				GRUVBOX_DARK_AQUA,
				GRUVBOX_DARK_BLUE,
				GRUVBOX_DARK_PURPLE
			);
			#endif

			return COMPRESS_LEVEL_COLORS[IndirectionSample];
		}
	]]
}

Code
[[
	float3 TerrainEditorCheckers( float3 CheckersBaseColor, float2 Position, float Amount )
	{
		float3 CheckersColor = lerp( CheckersBaseColor, CHECKERS_COLOR_TOP, 0.75 );
		float2 CheckersPos = round( Position * CHECKERS_COUNT );
		float3 CheckersTexture = (int(CheckersPos.x + CheckersPos.y) % 2) == 0 ? CheckersBaseColor : CheckersColor;
		CheckersTexture = lerp( CheckersBaseColor, CheckersTexture, Amount );
		return CheckersTexture;
	}

	bool TerrainEditorBrushOutlineInternal( inout float4 ColorOut, in float2 CursorPos, in float2 FragmentPos, in float OutlineWidth )
	{
		float Distance = distance( CursorPos, FragmentPos );

		// Inner radius, potentially affected by brush hardness.
		if( Distance > BrushInnerRadius && Distance < (BrushInnerRadius + OutlineWidth) )
		{
			ColorOut.rgb = GRUVBOX_LIGHT_BLUE;
			ColorOut.a = 1;
			return true;
		}

		// Outer radius, including border pixels.
		if( Distance > BrushOuterRadius && Distance < (BrushOuterRadius + OutlineWidth) )
		{
			ColorOut.rgb = GRUVBOX_LIGHT_RED;
			ColorOut.a = 1;
			return true;
		}
		return false;
	}

	void TerrainEditorBrushOutline( inout float4 ColorOut, in float2 CursorPos, in float2 FragmentPos, in float CameraHeight )
	{
		float CameraHeightFactor = (clamp( CameraHeight, BRUSH_OUTLINE_DIST_MIN, BRUSH_OUTLINE_DIST_MAX ) - BRUSH_OUTLINE_DIST_MIN) / ( BRUSH_OUTLINE_DIST_MAX - BRUSH_OUTLINE_DIST_MIN );
		float OutlineWidth = lerp( BRUSH_OUTLINE_WIDTH_MIN, BRUSH_OUTLINE_WIDTH_MAX, CameraHeightFactor );

		if( TerrainEditorBrushOutlineInternal( ColorOut, CursorPos, FragmentPos, OutlineWidth ) )
			return;

	#ifdef TERRAIN_WRAP_X
		float WorldWidth = 1.0f / WorldSpaceToTerrain0To1.x;
		if( TerrainEditorBrushOutlineInternal( ColorOut, float2( CursorPos.x - WorldWidth, CursorPos.y ), FragmentPos, OutlineWidth ) )
			return;
		if( TerrainEditorBrushOutlineInternal( ColorOut, float2( CursorPos.x + WorldWidth, CursorPos.y ), FragmentPos, OutlineWidth ) )
			return;
	#endif
	}
]]