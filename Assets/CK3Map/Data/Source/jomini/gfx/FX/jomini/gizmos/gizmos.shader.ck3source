Includes = {
	"cw/pdxmesh.fxh"
	"cw/camera.fxh"
}

VertexStruct VS_OUTPUT_GIZMO
{
    float4 Position			: PDX_POSITION;
	float2 UV				: TEXCOORD2;
};

PixelShader =
{
	TextureSampler DiffuseMap
	{
		Index = 0
		MagFilter = "Linear"
		MinFilter = "Linear"
		MipFilter = "Linear"
		SampleModeU = "Wrap"
		SampleModeV = "Wrap"
	}
	MainCode PS_Gizmo
	{
		Input = "VS_OUTPUT_GIZMO"
		Output = "PDX_COLOR"
		Code
		[[
			PDX_MAIN
			{
				return PdxTex2D( DiffuseMap, Input.UV );
			}
		]]
	}
}


VertexShader =
{
	MainCode VS_standard
	{
		Input = "VS_INPUT_PDXMESHSTANDARD"
		Output = "VS_OUTPUT_GIZMO"
		Code
		[[
			PDX_MAIN
			{
				VS_OUTPUT_GIZMO OutGizmo;
				VS_OUTPUT_PDXMESH StdMesh = PdxMeshVertexShaderStandard( Input );
				OutGizmo.Position = StdMesh.Position;
				OutGizmo.UV = StdMesh.UV0;
				return OutGizmo;
			}
		]]
	}
}

DepthStencilState DepthStencilState
{
	DepthEnable = no
	DepthWriteEnable = no
}

Effect Gizmo
{
	VertexShader = "VS_standard"
	PixelShader = "PS_Gizmo"
}
