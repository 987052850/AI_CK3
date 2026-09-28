using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CK3Map.Editor
{
    public readonly struct CK3RiverSurfaceBuildResult
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly int RiverCount;
        public readonly int CurvePointCount;

        public CK3RiverSurfaceBuildResult(Mesh mesh, Material material, int rivers, int points)
        { Mesh = mesh; Material = material; RiverCount = rivers; CurvePointCount = points; }
    }

    public static class CK3RiverSurfaceBuilder
    {
        public const string OutputRoot = "Assets/CK3Map/Data/Generated/Rivers";
        public const string MeshPath = OutputRoot + "/CK3原版河面网格.asset";
        public const string MaterialPath = OutputRoot + "/CK3原版河面.mat";
        public const string SceneObjectName = "CK3原版河流";
        private const string HeightDataPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版高度页表.asset";
        private const string WaterColorPath = "Assets/CK3Map/Data/Generated/Water/CK3原版水色与高光_BC7.asset";
        private const string ShaderName = "CK3Map/River Surface";
        private const float MaxAngle = 0.15f;
        private const float MinParameterDistance = 0.01f;
        private const int SmoothIterations = 2;
        private const int SmoothKernelRadius = 1;
        private const int SmoothFadeDistance = 5;

        public static CK3RiverSurfaceBuildResult BuildAndBind()
        {
            CK3RiverNetworkData network = AssetDatabase.LoadAssetAtPath<CK3RiverNetworkData>(CK3RiverNetworkBuilder.RiverDataPath);
            if (network == null) throw new InvalidOperationException("请先构建阶段 10 的原版位图河网数据。");
            CK3TerrainHeightData height = AssetDatabase.LoadAssetAtPath<CK3TerrainHeightData>(HeightDataPath);
            if (height == null) throw new InvalidOperationException("缺少阶段 3 的原版高度页表。");

            var vertices = new List<Vector3>(network.Points.Length * 3);
            var uv0 = new List<Vector2>(vertices.Capacity);
            var uv1 = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>(network.Points.Length * 6);
            int curvePoints = 0;
            for (int riverIndex = 0; riverIndex < network.Rivers.Length; riverIndex++)
            {
                CK3RiverRecord river = network.Rivers[riverIndex];
                if (river.PointCount < 2) continue;
                List<Vector3> control = BuildControlPath(network, river);
                Smooth(control);
                List<CurvePoint> curve = Tessellate(control);
                if (curve.Count < 2) continue;
                AppendRibbon(curve, vertices, uv0, uv1, indices);
                curvePoints += curve.Count;
                if ((riverIndex & 31) == 0)
                    EditorUtility.DisplayProgressBar("CK3 地图构建器", $"生成原版河流 {riverIndex + 1}/{network.Rivers.Length}", riverIndex/(float)network.Rivers.Length);
            }

            try
            {
                DeleteAsset(MeshPath);
                Mesh mesh = new Mesh { name = "CK3 原版全世界河面网格", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1);
                mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
                mesh.bounds = new Bounds(new Vector3(4607.5f,25,2303.5f),new Vector3(9216,60,4608));
                AssetDatabase.CreateAsset(mesh, MeshPath);
                Material material = BuildMaterial(height);
                BindScene(mesh, material);
                AssetDatabase.SaveAssets();
                return new CK3RiverSurfaceBuildResult(mesh, material, network.Rivers.Length, curvePoints);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static List<Vector3> BuildControlPath(CK3RiverNetworkData data, CK3RiverRecord river)
        {
            var result = new List<Vector3>(river.PointCount);
            bool reverse = river.Type == 1;
            for (int i=0;i<river.PointCount;i++)
            {
                int local = reverse ? river.PointCount-1-i : i;
                CK3RiverPixelPoint p=data.Points[river.PointStart+local];
                result.Add(new Vector3(p.X, data.BitmapSize.y-1-p.Y, p.Width));
            }
            return result;
        }

        private static void Smooth(List<Vector3> path)
        {
            if (path.Count<3) return;
            Vector3[] original=path.ToArray(); Vector3[] current=path.ToArray();
            for(int iteration=0;iteration<SmoothIterations;iteration++)
            {
                Vector3[] next=(Vector3[])current.Clone();
                for(int i=1;i<current.Length-1;i++)
                {
                    Vector3 average=Vector3.zero;
                    for(int k=-SmoothKernelRadius;k<=SmoothKernelRadius;k++) average+=SampleExtended(current,i+k);
                    average/=SmoothKernelRadius*2+1;
                    float edge=Mathf.Min(i,current.Length-1-i);
                    float fade=Mathf.Min(1.0f,edge/Mathf.Max(1.0f,SmoothFadeDistance));
                    next[i]=Vector3.Lerp(original[i],average,fade);
                }
                current=next;
            }
            for(int i=0;i<current.Length;i++) path[i]=current[i];
        }

        private static Vector3 SampleExtended(Vector3[] p,int index)
        {
            if(index<0) return p[0]+index*(p[1]-p[0]);
            if(index>=p.Length) return p[p.Length-1]+(index-(p.Length-1))*(p[p.Length-1]-p[p.Length-2]);
            return p[index];
        }

        private static Vector3 SampleExtended(List<Vector3> p,int index)
        {
            if(index<0) return p[0]+index*(p[1]-p[0]);
            if(index>=p.Count) return p[p.Count-1]+(index-(p.Count-1))*(p[p.Count-1]-p[p.Count-2]);
            return p[index];
        }

        private static List<CurvePoint> Tessellate(List<Vector3> p)
        {
            var output=new List<CurvePoint>(p.Count*2);
            for(int segment=0;segment<p.Count-1;segment++)
            {
                Vector3 p0=SampleExtended(p,segment-1),p1=SampleExtended(p,segment),p2=SampleExtended(p,segment+1),p3=SampleExtended(p,segment+2);
                CurvePoint start=Evaluate(p0,p1,p2,p3,0), end=Evaluate(p0,p1,p2,p3,1);
                if(output.Count==0) output.Add(start);
                var stack=new Stack<Interval>(); stack.Push(new Interval(0,1,start,end));
                while(stack.Count>0)
                {
                    Interval item=stack.Pop();
                    float delta=item.EndT-item.StartT;
                    float dot=Vector2.Dot(item.Start.Tangent,item.End.Tangent);
                    if(delta<=MinParameterDistance || dot>=Mathf.Cos(MaxAngle)) { AddUnique(output,item.End); continue; }
                    float middle=(item.StartT+item.EndT)*0.5f;
                    CurvePoint mid=Evaluate(p0,p1,p2,p3,middle);
                    stack.Push(new Interval(middle,item.EndT,mid,item.End));
                    stack.Push(new Interval(item.StartT,middle,item.Start,mid));
                }
            }
            return output;
        }

        private static CurvePoint Evaluate(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {
            float t2=t*t,t3=t2*t;
            Vector3 position=0.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3);
            Vector3 derivative=0.5f*((-a+c)+2*(2*a-5*b+4*c-d)*t+3*(-a+3*b-3*c+d)*t2);
            Vector2 tangent=new Vector2(derivative.x,derivative.y).normalized;
            return new CurvePoint(position,tangent);
        }

        private static void AddUnique(List<CurvePoint> list,CurvePoint p)
        {
            if(list.Count==0 || (list[list.Count-1].Position-p.Position).sqrMagnitude>1e-8f) list.Add(p);
        }

        private static void AppendRibbon(List<CurvePoint> curve,List<Vector3> vertices,List<Vector2> uv0,List<Vector2> uv1,List<int> indices)
        {
            int start=vertices.Count; float distance=0;
            for(int i=0;i<curve.Count;i++)
            {
                if(i>0) distance+=Vector2.Distance(curve[i-1].Position,curve[i].Position);
                Vector2 n=new Vector2(-curve[i].Tangent.y,curve[i].Tangent.x);
                float half=curve[i].Position.z*0.5f;
                Vector2 center=new Vector2(curve[i].Position.x,curve[i].Position.y);
                Vector2 left=center-n*half,right=center+n*half;
                vertices.Add(new Vector3(left.x,0,left.y)); vertices.Add(new Vector3(right.x,0,right.y));
                uv0.Add(new Vector2(distance,0)); uv0.Add(new Vector2(distance,1));
                uv1.Add(new Vector2(curve[i].Position.z,0)); uv1.Add(new Vector2(curve[i].Position.z,0));
                if(i==0) continue;
                int a=start+(i-1)*2,b=a+1,c=start+i*2,d=c+1;
                indices.Add(a);indices.Add(c);indices.Add(b); indices.Add(b);indices.Add(c);indices.Add(d);
            }
        }

        private static Material BuildMaterial(CK3TerrainHeightData height)
        {
            Shader shader=Shader.Find(ShaderName); if(shader==null) throw new InvalidOperationException("找不到 CK3 河面 Shader。");
            DeleteAsset(MaterialPath); Material m=new Material(shader){name="CK3 原版河面",renderQueue=(int)RenderQueue.Transparent+10};
            m.SetTexture("_WaterColorTexture",AssetDatabase.LoadAssetAtPath<Texture2D>(WaterColorPath));
            m.SetTexture("_CK3HeightLookupTexture",height.IndirectionTexture); m.SetTexture("_CK3PackedHeightTexture",height.PackedHeightTexture);
            m.SetVector("_CK3WorldSpaceToLookup",height.WorldSpaceToLookup); m.SetVector("_CK3OriginalHeightmapToWorldSpace",height.OriginalHeightmapToWorldSpace);
            m.SetVector("_CK3IndirectionSize",new Vector4(height.IndirectionSize.x,height.IndirectionSize.y,0,0)); m.SetFloat("_CK3BaseTileSize",height.BaseTileSize);
            m.SetFloat("_CK3HeightScale",height.HeightScale); m.SetVector("_CK3WorldExtents",height.WorldExtents);
            for(int i=0;i<height.TileToHeightmapScaleAndOffset.Length;i++) m.SetVector($"_CK3TileToHeightMap{i}",height.TileToHeightmapScaleAndOffset[i]);
            AssetDatabase.CreateAsset(m,MaterialPath); return m;
        }

        private static void BindScene(Mesh mesh,Material material)
        {
            GameObject old=GameObject.Find(SceneObjectName); if(old!=null) Undo.DestroyObjectImmediate(old);
            GameObject go=new GameObject(SceneObjectName); Undo.RegisterCreatedObjectUndo(go,"构建 CK3 原版河流");
            MeshFilter f=Undo.AddComponent<MeshFilter>(go); MeshRenderer r=Undo.AddComponent<MeshRenderer>(go);
            f.sharedMesh=mesh;r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.allowOcclusionWhenDynamic=false;
            CK3RiverSurfaceBinding binding=Undo.AddComponent<CK3RiverSurfaceBinding>(go);
            binding.Configure(material);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void DeleteAsset(string path) { if(AssetDatabase.LoadMainAssetAtPath(path)!=null) AssetDatabase.DeleteAsset(path); }

        private readonly struct CurvePoint { public readonly Vector3 Position; public readonly Vector2 Tangent; public CurvePoint(Vector3 p,Vector2 t){Position=p;Tangent=t;} }
        private readonly struct Interval { public readonly float StartT,EndT; public readonly CurvePoint Start,End; public Interval(float a,float b,CurvePoint s,CurvePoint e){StartT=a;EndT=b;Start=s;End=e;} }
    }
}
