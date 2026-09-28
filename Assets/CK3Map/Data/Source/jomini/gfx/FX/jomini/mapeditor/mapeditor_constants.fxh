ConstantBuffer( PdxConstantBuffer3 )
{
	float4		MaterialsVisibility[16];
}

ConstantBuffer( EditorSelectConstants )
{
	float2		SelectMin;
	float2		SelectMax;
}

ConstantBuffer( PdxConstantBuffer4 )
{
	float2		CursorPos;
	float 		BrushInnerRadius;
	float 		BrushOuterRadius;
}

Code
[[
	static const float3 CHECKERS_COLOR_ERROR		= float3( 1, 0, 1 );
	static const float3 CHECKERS_COLOR_TRANSPARENT	= float3( 1, 1, 1 );
	static const float3 CHECKERS_COLOR_TOP 			= float3( 0, 0, 0 );
	static const float  CHECKERS_COUNT				= 1;
	static const float  CHECKERS_FADE_DIST			= 700;

	//static const float  BRUSH_OUTLINE_WIDTH_MIN		= 0.05;
	static const float  BRUSH_OUTLINE_WIDTH_MIN		= 0.20;
	//static const float  BRUSH_OUTLINE_WIDTH_MAX		= 1.3;
	static const float  BRUSH_OUTLINE_WIDTH_MAX		= 2.5;
	static const float  BRUSH_OUTLINE_DIST_MIN		= 50;
	static const float  BRUSH_OUTLINE_DIST_MAX		= 2000;
	
	static const int TOTAL_TERRAIN_MATERIAL_COUNT  = 64;
]]
