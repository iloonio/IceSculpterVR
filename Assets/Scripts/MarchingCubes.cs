using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
    // Wait helper reused in the coroutine to avoid allocating every frame.
    private static WaitForSeconds oneSecondWait = new WaitForSeconds(1);

    // Grid dimensions.
    [SerializeField] private float gridSizeXZ = 16;
    [SerializeField] private float gridSizeY = 16;

    // The isosurface threshold for the marching cubes algorithm.
    // Points with scalar value > `isolevel` are considered "inside" the surface.
    [SerializeField] private float isolevel = 0.5f;

    // Controls the frequency/scale of the Perlin noise used to populate the scalar field.
    [SerializeField] private float noiseResolution = 0.1f;

    [SerializeField] private bool visualizeNoise;

    // Mesh data produced by the algorithm.
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    private MeshFilter meshFilter;

    // 3D scalar field / density grid used by marching cubes. Each cell corner stores a
    // scalar value (here derived from Perlin noise and vertical distance). 
    private float[,,] densityGrid; // 3D scalar field for marching cubes

    

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        StartCoroutine(UpdateAll());
    }

    private IEnumerator UpdateAll()
    {
        while (true)
        {
            SetDensityWithPerlin();
            MarchCubes();
            SetMesh(); 
            yield return oneSecondWait;  
        }
        
    }

    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        // Quick sanity: densityGrid should be populated before marching.
        // If `SetDensityWithPerlin` hasn't run yet this may be null.
        if (densityGrid == null) return;

        for (int x = 0; x < gridSizeXZ; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int z = 0; z < gridSizeXZ; z++)
                {
                    // Collect scalar values at the 8 corners of the current cube.
                    // The ordering must match the `MarchingTable.Vertices` ordering
                    // so the bitmask and triangle table indices line up correctly.
                    float[] cubeVertices = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int vertex = new Vector3Int(x, y, z) + MarchingTable.Vertices[i];
                        cubeVertices[i] = densityGrid[vertex.x, vertex.y, vertex.z];
                    }

                    // Determine the cube configuration and emit triangles.
                    MarchCube(new Vector3(x, y, z), GetConfigIndex(cubeVertices));
                }
            }
        }
    }

    private void MarchCube(Vector3 pos, int configurationIndex)
    {
        if (configurationIndex == 0 || configurationIndex == 255)
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
                int triTableValue = MarchingTable.Triangles[configurationIndex, edgeIndex];
                if (triTableValue == -1)
                {
                    // Sentinel value indicating no further triangles for this config
                    return;
                }

                // Each `triTableValue` encodes an edge by indexing into
                // `MarchingTable.Edges` which stores the two corner offsets for
                // that edge. `pos` is the grid cell origin; adding the corner
                // offsets yields world-space positions for the two edge endpoints.
                Vector3 edgeStart = pos + MarchingTable.Edges[triTableValue, 0];
                Vector3 edgeEnd = pos + MarchingTable.Edges[triTableValue, 1];

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
        int configurationIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cubeVertices[i] > isolevel)
            {
                configurationIndex |= (1 << i);
            }
        }
        return configurationIndex;
    }

    private void SetMesh()
    {
        // Currently a new Mesh is created each update which allocates memory.
        // For frequently-updating procedural meshes consider reusing a single
        // Mesh instance (e.g. `meshFilter.mesh` or a cached Mesh) and calling
        // `mesh.Clear()` before assigning `vertices`/`triangles`. Also use
        // `mesh.MarkDynamic()` for better performance on dynamic meshes.
        Mesh mesh = new()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    // Sets height of grid points using Perlin noise to generate terrain. 
    private void SetDensityWithPerlin()
    {
        // Allocate scalar field with +1 to include grid boundaries.
        densityGrid = new float[(int)gridSizeXZ + 1, (int)gridSizeY + 1, (int)gridSizeXZ + 1];

        /* 
        Populate the scalar field. For each grid column (x,z) we compute a
        Perlin-based terrain height; then each cell's scalar value is set to
        the absolute vertical distance from that terrain surface. This makes
        `heights` behave like a distance field where the surface is at low
        values. The marching cubes algorithm compares these values to `isolevel` 
        to locate the surface.
        */ 
        for (int x = 0; x <= gridSizeXZ; x++)
        {
            for (int z = 0; z <= gridSizeXZ; z++)
            {
                // `currentHeight` depends only on (x,z) so computing it once
                // outside the `y` loop avoids redundant PerlinNoise calls.
                float currentHeight = Mathf.PerlinNoise(x * noiseResolution, z * noiseResolution) * gridSizeY;

                for (int y = 0; y <= gridSizeY; y++)
                {
                    float newHeight;

                    if (y > currentHeight)
                    {
                        newHeight = y - currentHeight;
                    }
                    else
                    {
                        newHeight = currentHeight - y;
                    }

                    densityGrid[x, y, z] = newHeight;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying) return;

        for (int x = 0; x <= gridSizeXZ; x++)
        {
            for (int y = 0; y <= gridSizeY; y++)
            {
                for (int z = 0; z <= gridSizeXZ; z++)
                {
                    // Guard against null/uninitialized densityGrid when not
                    // populated yet (e.g. if this runs before Start()).
                    if (densityGrid == null) continue;

                    // Clamp or remap the scalar for color display — values in
                    // `densityGrid` may exceed 1.0 since they represent distances.
                    float v = Mathf.Clamp01(densityGrid[x, y, z]);
                    Gizmos.color = new Color(v, v, v, 1);
                    Gizmos.DrawSphere(new Vector3(x, y, z), 0.2f);
                }
            }
        }
    }
}
