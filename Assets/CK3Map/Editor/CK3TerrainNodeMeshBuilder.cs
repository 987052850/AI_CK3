using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CK3Map.Editor
{
    /// <summary>
    /// Builds the persistent base terrain node mesh recovered from CK3's
    /// CPdxTerrain initialization code. This is an Editor-only asset build.
    /// </summary>
    public static class CK3TerrainNodeMeshBuilder
    {
        public const string MeshPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版地形节点网格.asset";
        public const string SkirtMeshPath = "Assets/CK3Map/Data/Generated/Terrain/CK3原版地形裙边网格.asset";

        private const int VertexSide = 33;
        private const int CellSide = 32;
        private const int VertexCount = VertexSide * VertexSide;
        private const int IndexCount = CellSide * CellSide * 6;

        public static Mesh Build()
        {
            Vector3[] positions = new Vector3[VertexCount];
            Vector2[] withinNodePositions = new Vector2[VertexCount];
            Vector2[] lodDirections = new Vector2[VertexCount];

            for (int row = 0; row < VertexSide; row++)
            {
                ushort quantizedV = QuantizeUnorm16(row, CellSide);

                for (int column = 0; column < VertexSide; column++)
                {
                    int vertex = row * VertexSide + column;
                    ushort quantizedU = QuantizeUnorm16(column, CellSide);
                    float u = quantizedU / 65535.0f;
                    float v = quantizedV / 65535.0f;

                    // CK3 stores only WithinNodePos and LodDirection. Unity needs a
                    // position stream for a persistent Mesh, so the position is the
                    // exact shader input plane before CalcTerrainVertex displaces it.
                    positions[vertex] = new Vector3(u, 0.0f, v);
                    withinNodePositions[vertex] = new Vector2(u, v);
                    lodDirections[vertex] = new Vector2(
                        (column & 1) == 0 ? 0.0f : 1.0f,
                        GetLodDirectionY(row, column));
                }
            }

            int[] indices = new int[IndexCount];
            int write = 0;

            for (int row = 0; row < CellSide; row++)
            {
                int rowStart = row * VertexSide;

                for (int column = 0; column < CellSide; column++)
                {
                    int topLeft = rowStart + column;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + VertexSide;
                    int bottomRight = bottomLeft + 1;

                    // The original alternates the diagonal by (row + column) parity.
                    if (((row + column) & 1) == 0)
                    {
                        indices[write++] = topLeft;
                        indices[write++] = bottomLeft;
                        indices[write++] = bottomRight;
                        indices[write++] = topLeft;
                        indices[write++] = bottomRight;
                        indices[write++] = topRight;
                    }
                    else
                    {
                        indices[write++] = topLeft;
                        indices[write++] = bottomLeft;
                        indices[write++] = topRight;
                        indices[write++] = topRight;
                        indices[write++] = bottomLeft;
                        indices[write++] = bottomRight;
                    }
                }
            }

            if (write != IndexCount)
            {
                throw new InvalidOperationException($"CK3 terrain index count mismatch: {write} != {IndexCount}.");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "CK3原版地形节点网格_33x33";
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                mesh.Clear();
            }

            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.uv = withinNodePositions;
            mesh.uv2 = lodDirections;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(0.5f, 0.0f, 0.5f), new Vector3(1.0f, 100.0f, 1.0f));

            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mesh;
            EditorGUIUtility.PingObject(mesh);
            BuildSkirt();
            return mesh;
        }

        private static Mesh BuildSkirt()
        {
            const int perimeterPointCount = 128;
            const int skirtVertexCount = perimeterPointCount * 2;
            const int stripIndexCount = skirtVertexCount + 2;
            Vector3[] positions = new Vector3[skirtVertexCount];
            Vector2[] withinNodePositions = new Vector2[skirtVertexCount];
            Vector2[] lodDirections = new Vector2[skirtVertexCount];
            int vertex = 0;

            // CK3 emits the perimeter clockwise. Corners belong to the horizontal
            // edges, so the two vertical loops intentionally exclude both corners.
            for (int column = 0; column <= CellSide; column++)
            {
                AppendSkirtPair(column, 0, positions, withinNodePositions, lodDirections, ref vertex);
            }

            for (int row = 1; row < CellSide; row++)
            {
                AppendSkirtPair(CellSide, row, positions, withinNodePositions, lodDirections, ref vertex);
            }

            for (int column = CellSide; column >= 0; column--)
            {
                AppendSkirtPair(column, CellSide, positions, withinNodePositions, lodDirections, ref vertex);
            }

            for (int row = CellSide - 1; row >= 1; row--)
            {
                AppendSkirtPair(0, row, positions, withinNodePositions, lodDirections, ref vertex);
            }

            if (vertex != skirtVertexCount)
            {
                throw new InvalidOperationException($"CK3 terrain skirt vertex count mismatch: {vertex} != {skirtVertexCount}.");
            }

            // CK3 submits [0..255, 0, 1] as a triangle strip. Unity Mesh has no
            // triangle-strip topology, so expand the same strip losslessly to a
            // triangle list while preserving the strip's alternating winding.
            int[] strip = new int[stripIndexCount];
            for (int i = 0; i < skirtVertexCount; i++)
            {
                strip[i] = i;
            }
            strip[skirtVertexCount] = 0;
            strip[skirtVertexCount + 1] = 1;

            int[] triangles = new int[(stripIndexCount - 2) * 3];
            int triangleWrite = 0;
            for (int i = 2; i < stripIndexCount; i++)
            {
                if ((i & 1) == 0)
                {
                    triangles[triangleWrite++] = strip[i - 2];
                    triangles[triangleWrite++] = strip[i - 1];
                }
                else
                {
                    triangles[triangleWrite++] = strip[i - 1];
                    triangles[triangleWrite++] = strip[i - 2];
                }
                triangles[triangleWrite++] = strip[i];
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkirtMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "CK3原版地形裙边网格_256顶点";
                AssetDatabase.CreateAsset(mesh, SkirtMeshPath);
            }
            else
            {
                mesh.Clear();
            }

            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.uv = withinNodePositions;
            mesh.uv2 = lodDirections;
            mesh.SetIndices(triangles, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(0.5f, 0.0f, 0.5f), new Vector3(1.0f, 100.0f, 1.0f));
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void AppendSkirtPair(
            int column,
            int row,
            Vector3[] positions,
            Vector2[] withinNodePositions,
            Vector2[] lodDirections,
            ref int vertex)
        {
            ushort quantizedU = QuantizeUnorm16(column, CellSide);
            ushort quantizedV = QuantizeUnorm16(row, CellSide);
            float u = quantizedU / 65535.0f;
            float v = quantizedV / 65535.0f;
            Vector3 position = new Vector3(u, 0.0f, v);
            Vector2 withinNodePosition = new Vector2(u, v);
            Vector2 lodDirection = new Vector2(
                (column & 1) == 0 ? 0.0f : 1.0f,
                GetLodDirectionY(row, column));

            for (int pair = 0; pair < 2; pair++)
            {
                positions[vertex] = position;
                withinNodePositions[vertex] = withinNodePosition;
                lodDirections[vertex] = lodDirection;
                vertex++;
            }
        }

        private static ushort QuantizeUnorm16(int coordinate, int denominator)
        {
            return (ushort)Mathf.Clamp(
                Mathf.RoundToInt(coordinate * 65535.0f / denominator),
                ushort.MinValue,
                ushort.MaxValue);
        }

        private static float GetLodDirectionY(int row, int column)
        {
            if ((row & 1) == 0)
            {
                return 0.0f;
            }

            return (((column >> 1) + (row >> 1)) & 1) == 0 ? 1.0f : -1.0f;
        }
    }
}
