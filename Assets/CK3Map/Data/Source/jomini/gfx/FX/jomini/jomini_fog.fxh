Includes = {
	"cw/camera.fxh"
	"cw/random.fxh"
	"jomini/jomini.fxh"
}

Code
[[
	float CalculateDistanceFogFactor( float3 WorldSpacePos )
	{
		float3 Diff = CameraPosition - WorldSpacePos;
		float vFogFactor = 1.0 - abs( normalize( Diff ).y ); // abs b/c of reflections
		float vSqDistance = dot( Diff, Diff );

		float vMin = min( ( vSqDistance - FogBegin2 ) / ( FogEnd2 - FogBegin2 ), FogMax );
		return saturate( vMin * vFogFactor );
	}

	float3 ApplyDistanceFog( float3 Color, float vFogFactor )
	{
		return lerp( Color, FogColor, vFogFactor );
	}

	float3 ApplyDistanceFog( float3 Color, float3 WorldSpacePos )
	{
		float vFogFactor = CalculateDistanceFogFactor( WorldSpacePos );

		// Calculate a blend factor based on the position relative to the camera
		float BlendFactor = smoothstep( CameraPosition.x + RelativeFogBegin, CameraPosition.x - RelativeFogEnd, WorldSpacePos.x );

		// Interpolate between the original fog color and the relative fog color
		float3 BlendedFogColor = FogColor + BlendFactor * RelativeFogColor;

		// Ensure the resulting color channels do not exceed 1
		BlendedFogColor = min( BlendedFogColor, float3( 1.0f, 1.0f, 1.0f ) );

		// Calculate a height factor to reduce fog effect higher up
		float HeightFactor = smoothstep( RelativeFogHeightBegin, RelativeFogHeightEnd, WorldSpacePos.y );
		float NoiseValue = CalcNoise( WorldSpacePos.xz * 0.02f );

		vFogFactor *= ( 1.0f + 0.5f * ( NoiseValue ) );

		vFogFactor *= ( 1.0f - HeightFactor );

		return lerp( Color, BlendedFogColor, vFogFactor );
	}
]]
