Includes = {
	"cw/pdxmesh.fxh"
	"cw/pdxmesh_helper.fxh"
	"jomini/jomini_mapobject.fxh"
	"jomini/jomini_water.fxh"
	"jomini/jomini_fog.fxh"
}

PixelShader =
{
	Code
	[[
		float GetOpacity( uint InstanceIndex )
		{
			#ifdef JOMINI_MAP_OBJECT
				return UnpackAndGetMapObjectOpacity( InstanceIndex );
			#else
				return PdxMeshGetOpacity( InstanceIndex );
			#endif
		}

		void ApplyDither( uint InstanceIndex, float2 Position )
		{
			float Opacity = GetOpacity( InstanceIndex );
			PdxMeshApplyDitheredOpacity( Opacity, Position );
		}

		float3 TransformNormal( float3 NormalSample, float3 Normal, float3 Tangent, float3 Bitangent )
		{
			float3 InNormal = normalize( Normal );
			float3x3 TBN = Create3x3( normalize( Tangent ), normalize( Bitangent ), InNormal );
			return normalize( mul( NormalSample, TBN ) );
		}

		float HandleUnderwater( float Alpha, float3 WorldSpacePos )
		{
			#ifdef UNDERWATER
				clip( _WaterHeight - WorldSpacePos.y + 0.1 ); // +0.1 to avoid gap between water and mesh
				return CompressWorldSpace( WorldSpacePos );
			#else
				return Alpha;
			#endif
		}

		float3 HandleDistanceFog( float3 Color, float3 WorldSpacePos )
		{
			#ifndef UNDERWATER
				Color = ApplyDistanceFog( Color, WorldSpacePos );
			#endif

			return Color;
		}
	]]

	#MainCode PS_example_shadow_alphablend
	#{
	#	Input = "VS_OUTPUT_MAPOBJECT_SHADOW"
	#	Output = "void"
	#	Code
	#	[[
	#		PDX_MAIN
	#		{
	#			ApplyDither( Input.InstanceIndex24_Opacity8, Input.Position.xy );
	#
	#			float Alpha = PdxTex2D( PDXMESH_AlphaBlendShadowMap, Input.UV ).a;
	#			clip( Alpha - 0.5 );
	#		}
	#	]]
	#}

	#MainCode PS_example_shadow_alphablend
	#{
	#	Input = "VS_OUTPUT_PDXMESHSHADOWSTANDARD"
	#	Output = "void"
	#	Code
	#	[[
	#		PDX_MAIN
	#		{
	#			ApplyDither( uint( Input.UV_InstanceIndex.z ), Input.Position.xy );
	#
	#			float Alpha = PdxTex2D( PDXMESH_AlphaBlendShadowMap, Input.UV_InstanceIndex.xy ).a;
	#			clip( Alpha - 0.5 );
	#		}
	#	]]
	#}
}

VertexShader =
{
	MainCode VS_mapobject
	{
		Input = "VS_INPUT_PDXMESH_MAPOBJECT"
		Output = "VS_OUTPUT"
		Code
		[[
			PDX_MAIN
			{
				float4x4 WorldMatrix = UnpackAndGetMapObjectWorldMatrix( Input.InstanceIndex24_Opacity8 );
				VS_OUTPUT Out = ConvertOutput( PdxMeshVertexShader( PdxMeshConvertInput( Input ), 0/*bone offset not supported*/, WorldMatrix ) );
				Out.InstanceIndex = Input.InstanceIndex24_Opacity8;
				return Out;
			}
		]]
	}
}


RasterizerState ShadowRasterizerState
{
	DepthBias = 40000
	SlopeScaleDepthBias = 2
}
