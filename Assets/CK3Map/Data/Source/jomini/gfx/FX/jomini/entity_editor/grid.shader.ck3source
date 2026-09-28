Includes = {
	"cw/camera.fxh"
	"cw/pdxmesh.fxh"
	"cw/pdxmesh_helper.fxh"
}

PixelShader =
{
	MainCode PS_grid
	{
		ConstantBuffer( EntityEditorGridConstants )
		{
			float MinorGridOpacity;
			float MajorGridOpacity;
		};

		# Roughly based on http://asliceofrendering.com/scene%20helper/2020/01/05/InfiniteGrid/
		Input = "VS_OUTPUT"
		Output = "PDX_COLOR"
		Code
		[[
			float4 Grid( float3 WorldSpacePos, float InvScale, float AxisLineWidth )
			{
				float2 ScaledWorldPos = WorldSpacePos.xz * InvScale;
				float2 Derivative = fwidth( ScaledWorldPos );
				float2 DistToLine = abs( frac( ScaledWorldPos - 0.5 ) - 0.5 ) / Derivative;
				float Dist = min( DistToLine.x, DistToLine.y );
				float4 Color = float4( 1.0, 1.0, 1.0, 1.0 - min( Dist, 1.0 ) );

				if ( WorldSpacePos.x > -AxisLineWidth && WorldSpacePos.x < AxisLineWidth )
				{
					Color.rgb = float3( 0.0, 0.0, 1.0 );
				}

				if ( WorldSpacePos.z > -AxisLineWidth && WorldSpacePos.z < AxisLineWidth )
				{
					Color.rgb = float3( 1.0, 0.0, 0.0 );
				}

				return Color;
			}

			PDX_MAIN
			{
				float CameraDistFromPlane = abs( CameraPosition.y );
				int GridScaleIndex = min( int( floor( log10( CameraDistFromPlane + 1.0 ) ) ) - 1, 1);
				float GridSubdivisionMultiplier = 10.0;
				float Scale = pow( GridSubdivisionMultiplier, GridScaleIndex );

				float InvScale = 1.0 / Scale;
				float LineSizeWSFactor = 500.0;
				float AxisLineWidth = CameraDistFromPlane / LineSizeWSFactor;

				return Grid( Input.WorldSpacePos, InvScale, AxisLineWidth ) * MinorGridOpacity +
					   Grid( Input.WorldSpacePos, InvScale * (1.0 / GridSubdivisionMultiplier), AxisLineWidth ) * MajorGridOpacity;
			}
		]]
	}
}

RasterizerState DisableCulling
{
	CullMode = "none"
}

BlendState AlphaBlend
{
	BlendEnable = yes
	SourceBlend = "SRC_ALPHA"
	DestBlend = "INV_SRC_ALPHA"
}

Effect Grid
{
	VertexShader = "VS_standard"
	PixelShader = "PS_grid"
	BlendState = "AlphaBlend"
	RasterizerState = "DisableCulling"
}
