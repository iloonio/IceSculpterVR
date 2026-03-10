using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
public class MarchingCubes : MonoBehaviour
{
    // =========================================================
    // --- SETTINGS ---
    // =========================================================
    [Header("Grid Settings")]
    [SerializeField] private Vector3 worldSize = new(8.0f, 8.0f, 8.0f);
    [SerializeField] private int chunkSize = 3;
    [SerializeField] private bool randomizeDensity = false;
    [SerializeField] private MarchingChunk chunkPrefab;
    
    [Header("Generation Mode")]
    // The isosurface threshold for the marching cubes algorithm.
    // Points with density value > `isolevel` are considered "inside" the surface.
    [SerializeField] internal float isolevel = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool visualizeVertices = false;


    // =========================================================
    // INTERNAL DATA 
    // =========================================================
    internal float[,,] densityGrid; // 3D scalar field for marching cubes

    private MarchingChunk[] chunks; // Array of chunk references 
    internal Vector3 stepSize;

    private int gridResolution;
    
    private static WaitForSeconds oneSecondWait = new(1); // I honestly don't know why we do this. 


    // =========================================================
    // UNITY MESSAGES AND LIFECYCLE METHODS
    // =========================================================
    private void OnEnable() => InitialSetup();
    
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
        gridResolution = chunkSize*2; 

        for(int i = transform.childCount - 1; i >= 0; i--) //kill all the old kids
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        SpawnChunks(); //birth new children with fresh meshes
    }

    public void SpawnChunks()
    {
        stepSize = new Vector3(worldSize.x / gridResolution, 
                               worldSize.y / gridResolution, 
                               worldSize.z / gridResolution);
        
        if (randomizeDensity) InitializeDensityRandomized();
        else InitializeDensity();

        chunks = new MarchingChunk[8];

        int cellsPerChunk = chunkSize; // e.g. chunksize=3 means 4 cells per chunks, because we need a shared boundary while also including the margins. 
        int chunkIndex = 0;

        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3Int startIndex = new Vector3Int(
                        (x*cellsPerChunk),
                        (y*cellsPerChunk),
                        (z*cellsPerChunk)
                    );

                    Vector3Int endIndex = startIndex + new Vector3Int(
                        (cellsPerChunk),
                        (cellsPerChunk),
                        (cellsPerChunk)
                    );

                    // create chunk child object 
                    GameObject chunkObj = Instantiate(chunkPrefab.gameObject, transform);

                    chunkObj.name = $"Chunk_{x}_{y}_{z}";

                    chunkObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                    MarchingChunk chunkScript = chunkObj.AddComponent<MarchingChunk>();
                    chunkScript.Initialize(this, startIndex, endIndex); // pass reference to parent and assigned grid bounds
                    chunks[chunkIndex] = chunkScript;
                    chunkIndex++;
                }
    }

    // =========================================================
    // API & UTILITIES
    // =========================================================

    // helper function to convert world position to grid index, 
    // accounting for the transform and grid scaling
    public Vector3Int WorldToGridIndex(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);

        int x = Mathf.RoundToInt(localPos.x / stepSize.x) + 1; // +1 for padding (why?)
        int y = Mathf.RoundToInt(localPos.y / stepSize.y) + 1;
        int z = Math.Abs(Mathf.RoundToInt(localPos.z / stepSize.z) + 1);

        Debug.Log($"World pos {worldPos} maps to local pos {localPos} and grid index ({x}, {y}, {z})");

        return new Vector3Int(x, y, z);
    }


    public void SetDensityAtPos(Vector3 worldPos, float density, float brushRadius)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Vector3Int gridIndex = WorldToGridIndex(worldPos);
        bool gridUpdated = false; //update flag to rebuild the mesh
        int radiusInCells = Mathf.CeilToInt(brushRadius/stepSize.x);
        radiusInCells = Mathf.Max(1, radiusInCells); //ensure we always affect atleast one cell

        int maxX = Math.Min(gridResolution+1, gridIndex.x+radiusInCells);
        int maxY = Math.Min(gridResolution+1, gridIndex.y+radiusInCells);
        int maxZ = Math.Min(gridResolution+1, gridIndex.z+radiusInCells);

        int minX = Math.Max(1, gridIndex.x-radiusInCells);
        int minY = Math.Max(1, gridIndex.y-radiusInCells);      
        int minZ = Math.Max(1, gridIndex.z-radiusInCells);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3 point = new Vector3((x-1)*stepSize.x, (y-1)*stepSize.y, (z-1)*stepSize.z); 

                    if (Vector3.Distance(localPos, point) <= brushRadius && densityGrid[x, y, z] != density)
                    {
                        densityGrid[x, y, z] = density;
                        gridUpdated = true;
                        Debug.Log($"Set density at grid index ({x}, {y}, {z}) to {density}");
                    }
                }   
        if (gridUpdated)
        {
            foreach (var chunk in chunks)
            {
                if(chunk == null) continue; // Guard against uninitialized chunk references
                if (chunk.Overlaps(minX, maxX, minY, maxY, minZ, maxZ))
                {
                    chunk.GenerateMesh();
                }
            }
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
        foreach (var chunk in chunks)
        {
            if(chunk == null) continue; // Guard against uninitialized chunk references
            chunk.GenerateMesh();
        }
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
                    densityGrid[x, y, z] = UnityEngine.Random.value;
    }
    
    // Coroutine that updates the mesh every second with random values. 
    private IEnumerator RandomizeGridRoutine()
    {
        while (randomizeDensity)
        {
            InitializeDensityRandomized();
            foreach (var chunk in chunks)
            {
                if(chunk != null) chunk.GenerateMesh(); // Guard against uninitialized chunk references
            }
            yield return oneSecondWait;  
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

