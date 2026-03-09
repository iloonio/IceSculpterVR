using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

[ExecuteAlways]
public class MarchingChunk : MonoBehaviour
{
    public MarchingCubes parent;
    private Vector3Int startIdx;
    private Vector3Int endIdx;

    private Mesh proceduralMesh;
    private MeshFilter meshFilter;

    private List<Vector3> vertices = new();
    private List<int> triangles = new();



    public void Initialize(MarchingCubes parent, Vector3Int start, Vector3Int end)
    {
        this.parent = parent;
        startIdx = start;
        endIdx = end;

        meshFilter = GetComponent<MeshFilter>();

        proceduralMesh = new Mesh {name = $"Chunk_{start.x}_{start.y}_{start.z}"};
        proceduralMesh.MarkDynamic(); // Hint to Unity that this mesh will be updated frequently

        GenerateMesh();
    }

    public void GenerateMesh()
    {
        vertices.Clear();
        triangles.Clear();

        // densityGrid should be populated before marching.
        // If density population hasn't run yet this may be null.
        if (parent.densityGrid == null)
        {
            Debug.LogWarning("Density grid not initialized. Cannot generate mesh.");
            return;
        }

        for (int x = startIdx.x; x <= endIdx.x; x++)
        {
            for (int y = startIdx.y; y <= endIdx.y; y++)
            {
                for (int z = startIdx.z; z <= endIdx.z; z++)
                {
                    float[] cubeVertices = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int v = new Vector3Int(x,y,z) + MarchingTable.Vertices[i];
                        cubeVertices[i] = parent.densityGrid[v.x, v.y, v.z];
                    }

                    Vector3 cellPos = new Vector3(
                        (x-1) * parent.stepSize.x,
                        (y-1) * parent.stepSize.y,
                        (z-1) * parent.stepSize.z
                    );

                    MarchCube(cellPos, GetConfigIndex(cubeVertices));
                }
            }
        }

        SetMesh();
    }

    private void MarchCube(Vector3 pos, int configIndex)
    {
        if (configIndex == 0 || configIndex == 255)
        {
            return; // No triangles to generate
        }

        int edgeIndex = 0;
        for (int t = 0; t < 5; t++)
        {
            // The triangle table lists up to 5 triangles per configuration,
            // each triangle consisting of 3 edges. We iterate over those
            // edge indices and create a vertex for each referenced edge.
            for (int i = 0; i < 3; i++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];
                if (triTableValue == -1)
                {
                    // Sentinel value indicating no further triangles for this config
                    return;
                }

                Vector3 offsetStart = Vector3.Scale(MarchingTable.Edges[triTableValue, 0], parent.stepSize);
                Vector3 offsetEnd = Vector3.Scale(MarchingTable.Edges[triTableValue, 1], parent.stepSize);

                // Each `triTableValue` encodes an edge by indexing into
                // `MarchingTable.Edges` which stores the two corner offsets for
                // that edge. `pos` is the grid cell origin; adding the corner
                // offsets yields world-space positions for the two edge endpoints.
                Vector3 edgeStart = pos + offsetStart;
                Vector3 edgeEnd = pos + offsetEnd;

                // This implementation places the new vertex at the midpoint
                // between the two edge endpoints. A more accurate approach is
                // to linearly interpolate along the edge using the scalar
                // values at the two corners to find the exact isosurface
                // crossing point. Midpoint is simpler and cheaper.
                Vector3 vertex = (edgeStart + edgeEnd) / 2;

                vertices.Add(vertex);
                triangles.Add(vertices.Count - 1);

                edgeIndex++;
            }
        }
    }

    private int GetConfigIndex(float[] cubeVertices)
    {
        // Construct an 8-bit configuration index (bitmask) where bit i
        // corresponds to whether corner i is inside (> isolevel) the
        // isosurface. This configuration index selects the triangle pattern
        // from the marching cubes triangle table.
        int configIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cubeVertices[i] > parent.isolevel) 
            {
                configIndex |= (1 << i);
            }
        }
        return configIndex;
    }

    private void SetMesh()
    {
        // 1. Flip the winding order of the triangles so the mesh faces outward
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int temp = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = temp;
        }

        // 2. build collision mesh
        proceduralMesh.Clear();
        proceduralMesh.SetVertices(vertices);
        proceduralMesh.SetTriangles(triangles, 0);
        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();

        // 3. Assign to renderer
        meshFilter.sharedMesh = proceduralMesh;

        // 4. Assign to collider
        if (TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider.sharedMesh = proceduralMesh;
            meshCollider.enabled = false; // toggle to force update
            meshCollider.enabled = true;
        }
    }


    // Determines if an edit bounding box touches this chunk's territory
    public bool Overlaps(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
    {
        // Standard AABB (Axis-Aligned Bounding Box) intersection check
        return (minX <= endIdx.x && maxX >= startIdx.x) &&
               (minY <= endIdx.y && maxY >= startIdx.y) &&
               (minZ <= endIdx.z && maxZ >= startIdx.z);
    }
}

