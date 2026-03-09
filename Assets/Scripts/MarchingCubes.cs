using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class MarchingCubes : MonoBehaviour
{
    // =========================================================
    // --- SETTINGS ---
    // =========================================================
    [Header("Grid Settings")]
    [SerializeField] private Vector3 worldSize = new(8.0f, 8.0f, 8.0f);
    [SerializeField] private int gridResolution = 16;
    [SerializeField] private bool randomizeDensity = false;
    
    [Header("Generation Mode")]
    // The isosurface threshold for the marching cubes algorithm.
    // Points with density value > `isolevel` are considered "inside" the surface.
    [SerializeField] private float isolevel = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool visualizeVertices = false;


    // =========================================================
    // INTERNAL DATA 
    // =========================================================
    private float[,,] densityGrid; // 3D scalar field for marching cubes
    private Vector3 stepSize;
    
    private List<Vector3> vertices = new(); // Mesh data produced by the algorithm.
    private List<int> triangles = new(); // Ditto
    private static WaitForSeconds oneSecondWait = new(1); // I honestly don't know why we do this. 
    private bool IsPointInBox(Vector3 point, Vector3 boxCenter, Vector3 halfExtents, Quaternion rotation)
    {
        // Transform the point into the box's local space
        Vector3 localPoint = Quaternion.Inverse(rotation) * (point - boxCenter);

        // Check if the local point is within the half extents of the box
        return Mathf.Abs(localPoint.x) <= halfExtents.x &&
               Mathf.Abs(localPoint.y) <= halfExtents.y &&
               Mathf.Abs(localPoint.z) <= halfExtents.z;
    }


    // -- COMPONENT REFERENCES ---
    private MeshFilter meshFilter;
    private Mesh proceduralMesh;


    // =========================================================
    // UNITY MESSAGES AND LIFECYCLE METHODS
    // =========================================================
    private void OnEnable() => InitialSetup();
    private void OnValidate() => InitialSetup();
    
    void Start()
    {
        InitialSetup();
        if (randomizeDensity && Application.isPlaying)
        {
            StartCoroutine(RandomizeGridRoutine());
        }  
    }

    private void InitialSetup()
    {
        meshFilter = GetComponent<MeshFilter>();

        EnsureMeshInitialized();
        GenerateMesh();
        
    }

    // =========================================================
    // API & UTILITIES
    // =========================================================
    public void GenerateMesh()
    {
        stepSize = new Vector3(worldSize.x / gridResolution, 
                               worldSize.y / gridResolution, 
                               worldSize.z / gridResolution);

        if (randomizeDensity) InitializeDensityRandomized();
        else InitializeDensity();

        MarchCubes();
        SetMesh();
    }

    // helper function to convert world position to grid index, 
    // accounting for the transform and grid scaling
    public Vector3Int WorldToGridIndex(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);

        int x = Mathf.RoundToInt(localPos.x / stepSize.x) + 1; // +1 for padding (why?)
        int y = Mathf.RoundToInt(localPos.y / stepSize.y) + 1;
        int z = Mathf.RoundToInt(localPos.z / stepSize.z) + 1;

        return new Vector3Int(x, y, z);
    }


    public void SetDensityAtPos(Vector3 worldPos, float density)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Vector3Int gridIndex = WorldToGridIndex(worldPos);
        bool gridUpdated = false; //update flag to rebuild the mesh

        for (int x = 1; x <= gridResolution+1; x++)
            for (int y = 1; y <= gridResolution+1; y++)
                for (int z = 1; z <= gridResolution+1; z++)
                {
                    Vector3 point = new Vector3((x-1)*stepSize.x, (y-1)*stepSize.y, (z-1)*stepSize.z); 

                    if (Vector3.Distance(localPos, point) <= 0.5f)
                    {
                        densityGrid[x, y, z] = density;
                        gridUpdated = true;
                        Debug.Log($"Set density at grid index ({x}, {y}, {z}) to {density}");
                    }
                }   
        if (gridUpdated)
        {
            MarchCubes();
            SetMesh();
        }
        
    }

    public int GetGridResolution()
    {
        return gridResolution;
    }

    public float GetDensityAt(int x, int y, int z)
    {
        int[] xyz = {x, y, z};
        foreach (int i in xyz)
        {
            if (i < 0 || i >= gridResolution)
            {
                return -1;
            }
        }

        return densityGrid[x+1, y+1, z+1];
    }

    public void SetDensityAt(int x, int y, int z, float density)
    {
        int[] xyz = {x, y, z};
        foreach (int i in xyz)
        {
            if (i < 0 || i >= gridResolution)
            {
                return;
            }
        }

        densityGrid[x+1, y+1, z+1] = density;
    }

    public void Refresh()
    {
        MarchCubes();
        SetMesh();
    }

    // =========================================================
    // DATA POPULATION
    // =========================================================
    private void InitializeDensity()
    {
        // Add a one-cell padding on each side in every dimension; the
        // operational domain will occupy indices [1..size], 0 & size+1 are
        // unused boundary layers.
        int requiredSize = gridResolution + 2;

        // Only allocate memory is density grid changes or isn't yet allocated. 
        if (densityGrid == null || densityGrid.GetLength(0) != requiredSize)
        {
            densityGrid = new float[requiredSize, requiredSize, requiredSize];
        }

        // You can make nested for-loops a lot more compact in C#
        for (int x = 1; x <= gridResolution; x++)    
            for (int y = 1; y <= gridResolution; y++)    
                for (int z = 1; z <= gridResolution; z++)
                    densityGrid[x, y, z] = 1.0f;   
    }

    private void InitializeDensityRandomized()
    {
        // Add a one-cell padding on each side in every dimension; the
        // operational domain will occupy indices [1..size] and 0/size+1 are
        // unused boundary layers.
        int requiredSize = gridResolution + 2;

        // Only allocate memory is density grid changes or isn't yet allocated. 
        if (densityGrid == null || densityGrid.GetLength(0) != requiredSize)
        {
            densityGrid = new float[requiredSize, requiredSize, requiredSize];
        }

        for (int x = 1; x <= gridResolution; x++)
            for (int y = 1; y <= gridResolution; y++)
                for (int z = 1; z <= gridResolution; z++)
                    densityGrid[x, y, z] = Random.value;
    }
    
    // Coroutine that updates the mesh every second with random values. 
    private IEnumerator RandomizeGridRoutine()
    {
        while (randomizeDensity)
        {
            GenerateMesh();
            yield return oneSecondWait;  
        }
    }


    // =========================================================
    // MARCHING CUBES
    // =========================================================
    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        // densityGrid should be populated before marching.
        // If density population hasn't run yet this may be null.
        if (densityGrid == null) return;

        int numCellsX = gridResolution;
        int numCellsY = gridResolution;
        int numCellsZ = gridResolution;

        // iterate across all cells including those at margins that use the padding
        for (int x = 0; x <= numCellsX; x++)
        {
            for (int y = 0; y <= numCellsY; y++)
            {
                for (int z = 0; z <= numCellsZ; z++)
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

                    // Test to see if this works 
                    Vector3 worldPos = new Vector3(
                        (x-1) * stepSize.x,
                        (y-1) * stepSize.y,
                        (z-1) * stepSize.z
                    );

                    // Determine the cube configuration and emit triangles.
                    // subtract one to undo padding so mesh coordinates start at origin
                    // map padded indices directly so that coordinate 0 remains empty
                    MarchCube(worldPos, GetConfigIndex(cubeVertices));
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

                Vector3 offsetStart = Vector3.Scale(MarchingTable.Edges[triTableValue, 0], stepSize);
                Vector3 offsetEnd = Vector3.Scale(MarchingTable.Edges[triTableValue, 1], stepSize);

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


    // =========================================================
    // MESH CONSTRUCTION
    // =========================================================
    private void SetMesh()
    {
        EnsureMeshInitialized();

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
        if (Application.isPlaying)
            meshFilter.mesh = proceduralMesh;
        else
            meshFilter.sharedMesh = proceduralMesh;

        // 4. Assign to collider
        if (TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider.sharedMesh = proceduralMesh;
            meshCollider.enabled = false; // toggle to force update
            meshCollider.enabled = true;
        }
    }

    private void EnsureMeshInitialized()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        
        if (proceduralMesh == null)
        {
            proceduralMesh = meshFilter.sharedMesh != null ? meshFilter.sharedMesh : new Mesh();
            proceduralMesh.name = "MarchingCubes";
            proceduralMesh.MarkDynamic();
            if (Application.isPlaying)
                meshFilter.mesh = proceduralMesh;
            else
                meshFilter.sharedMesh = proceduralMesh;
        }
    }

    
    // =========================================================
    // DEBUG VISUALIZATION
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (!visualizeVertices || !Application.isPlaying) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        // visualize full padded grid (including boundaries)
        int sizeX = gridResolution + 2;
        int sizeY = gridResolution + 2;
        int sizeZ = gridResolution + 2;
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
                    Gizmos.color = new Color(v, v, v, 0.5f);
                    Gizmos.DrawSphere(new Vector3((x-1)*stepSize.x, (y-1)*stepSize.y, (z-1)*stepSize.z), 0.02f);
                }
            }
        }
    }
}

