using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO: divvy up the code so everything isn't on the same file, e.g. seperate mesh rendering from marching cubes logic. 
//TODO: Remove pointless code, such as inverting surfaces. 
//TODO: add a function that reduces or increase value at a set vertex. 
//TODO: add resolution to the grid, so we can increase and decrease the number of vertices while maintaining the same world size.

[ExecuteAlways]
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

    // When true, reverse triangle winding so the mesh faces inward.
    [SerializeField] private bool invertSurface = false;

    // When true, generate both the normal and inverted-side triangles so the
    // surface is visible from either side.
    [SerializeField] private bool doubleSided = false;

    private MeshFilter meshFilter;
    private Mesh proceduralMesh;

    // 3D scalar field / density grid used by marching cubes. Each cell corner stores a
    // scalar value (here derived from Perlin noise and vertical distance). 
    private float[,,] densityGrid; // 3D scalar field for marching cubes

    

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        EnsureMeshInitialized();
        StartCoroutine(UpdateAll());
    }

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        EnsureMeshInitialized();
        if (!Application.isPlaying)
        {
            GenerateMesh();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            meshFilter = GetComponent<MeshFilter>();
            EnsureMeshInitialized();
            GenerateMesh();
        }
    }

    private IEnumerator UpdateAll()
    {
        while (true)
        {
            GenerateMesh();
            yield return oneSecondWait;  
        }
        
    }

    // Public entrypoint to (re)generate the procedural mesh.
    public void GenerateMesh()
    {
        SetDensityRandomized();
        MarchCubes();
        SetMesh();
    }

    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        // Quick sanity: densityGrid should be populated before marching.
        // If density population hasn't run yet this may be null.
        if (densityGrid == null) return;

        // iterate only over the non-padded region
        for (int x = 0; x <= gridSizeXZ; x++)
        {
            for (int y = 0; y <= gridSizeY; y++)
            {
                for (int z = 0; z <= gridSizeXZ; z++)
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
                    // subtract one to undo padding so mesh coordinates start at origin
                    // map padded indices directly so that coordinate 0 remains empty
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
        EnsureMeshInitialized();

        proceduralMesh.Clear();
        proceduralMesh.SetVertices(vertices);

        // build vertex/triangle lists; may duplicate for double sided
        List<Vector3> meshVerts = vertices;
        List<int> triList = new List<int>(triangles);

        if (invertSurface)
        {
            // flip winding on the original set
            triList = new List<int>(triangles.Count);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                triList.Add(triangles[i]);
                triList.Add(triangles[i + 2]);
                triList.Add(triangles[i + 1]);
            }
        }

        if (doubleSided)
        {
            // duplicate vertices for the inverted side so normals can differ
            meshVerts = new List<Vector3>(vertices);
            int origVertCount = vertices.Count;
            int triCount = triList.Count;
            for (int i = 0; i < triCount; i += 3)
            {
                // add reversed triangle with new vertex indices
                meshVerts.Add(meshVerts[triList[i]]);
                meshVerts.Add(meshVerts[triList[i + 2]]);
                meshVerts.Add(meshVerts[triList[i + 1]]);
                triList.Add(origVertCount++);
                triList.Add(origVertCount++);
                triList.Add(origVertCount++);
            }
        }

        proceduralMesh.SetVertices(meshVerts);
        proceduralMesh.SetTriangles(triList, 0);

        proceduralMesh.RecalculateNormals();
        if (invertSurface || doubleSided)
        {
            var ns = proceduralMesh.normals;
            if (doubleSided)
            {
                // invert normals of the duplicated (second half) vertices
                int start = meshVerts.Count - (triList.Count/3)*3;
                for (int i = start; i < ns.Length; i++) ns[i] = -ns[i];
            }
            else
            {
                for (int i = 0; i < ns.Length; i++) ns[i] = -ns[i];
            }
            proceduralMesh.normals = ns;
        }

        proceduralMesh.RecalculateNormals();
        if (invertSurface)
        {
            // normals have been calculated outward; flip them to point inward
            var ns = proceduralMesh.normals;
            for (int i = 0; i < ns.Length; i++) ns[i] = -ns[i];
            proceduralMesh.normals = ns;
        }
        proceduralMesh.RecalculateBounds();

        if (Application.isPlaying)
            meshFilter.mesh = proceduralMesh;
        else
            meshFilter.sharedMesh = proceduralMesh;
    }

    private void EnsureMeshInitialized()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (proceduralMesh == null)
        {
            proceduralMesh = meshFilter.sharedMesh != null ? meshFilter.sharedMesh : new Mesh();
            proceduralMesh.name = "ProceduralMarchingCubes";
            proceduralMesh.MarkDynamic();
            if (Application.isPlaying)
                meshFilter.mesh = proceduralMesh;
            else
                meshFilter.sharedMesh = proceduralMesh;
        }
    }

    // Populate the density grid with random values per grid point.
    // This replaces the previous Perlin-based population and is useful
    // for testing noisy, stochastic scalar fields.
    private void SetDensityRandomized()
    {
        // Add a one-cell padding on each side in every dimension; the
        // operational domain will occupy indices [1..size] and 0/size+1 are
        // unused boundary layers.
        int sizeX = (int)gridSizeXZ;
        int sizeY = (int)gridSizeY;
        int sizeZ = (int)gridSizeXZ;

        densityGrid = new float[sizeX + 2, sizeY + 2, sizeZ + 2];

        for (int x = 1; x <= sizeX; x++)
        {
            for (int y = 1; y <= sizeY; y++)
            {
                for (int z = 1; z <= sizeZ; z++)
                {
                    densityGrid[x, y, z] = Random.value;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying) return;

        // visualize full padded grid (including boundaries)
        int sizeX = (int)gridSizeXZ + 2;
        int sizeY = (int)gridSizeY + 2;
        int sizeZ = (int)gridSizeXZ + 2;
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {                    // Guard against null/uninitialized densityGrid when not
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
