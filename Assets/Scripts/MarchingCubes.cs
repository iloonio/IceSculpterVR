using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds1 = new WaitForSeconds(1);
    [SerializeField] private float width = 16;
    [SerializeField] private float height = 16;

    [SerializeField] private float isolevel = 0.5f;

    [SerializeField] private float noiseResolution = 0.1f;

    [SerializeField] private bool visualizeNoise;

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    private MeshFilter meshFilter;

    
    private float[,,] heights; //3d Array of heights for each point in the grid
    // TODO: Why is it called heights? rename it to something better. 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        StartCoroutine(UpdateAll());
    }

    private IEnumerator UpdateAll()
    {
        while (true)
        {
            SetHeights();
            MarchCubes();
            SetMesh(); 
            yield return _waitForSeconds1;  
        }
        
    }

    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    float[] cubeVertices = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int vertex = new Vector3Int(x, y, z) + MarchingTable.Vertices[i];
                        cubeVertices[i] = heights[vertex.x, vertex.y, vertex.z];
                    }

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

            for (int i = 0; i < 3; i++)
            {
                int triTableValue = MarchingTable.Triangles[configurationIndex, edgeIndex];
                if (triTableValue == -1)
                {
                    return; // No more triangles for this configuration
                }

                Vector3 edgeStart = pos + MarchingTable.Edges[triTableValue, 0];
                Vector3 edgeEnd = pos + MarchingTable.Edges[triTableValue, 1];

                Vector3 vertex = (edgeStart + edgeEnd) / 2; // midpoint for vertex insertion

                vertices.Add(vertex);
                triangles.Add(vertices.Count - 1); // Add the index of the new vertex to the triangle list

                edgeIndex++;
            }
        }
    }

    private int GetConfigIndex(float[] cubeVertices)
    {
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
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    // Sets height of grid points using Perlin noise to generate terrain. 
    private void SetHeights()
    {
        heights = new float[(int)width + 1, (int)height + 1, (int)width + 1];

        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z <= width; z++)
                {
                    float currentHeight = Mathf.PerlinNoise(x * noiseResolution, z * noiseResolution) * height;
                    float newHeight;


                    if(y > currentHeight)
                    {
                        newHeight = y - currentHeight;
                    } 
                    else
                    {
                        newHeight = currentHeight - y;
                    }
                    
                    heights[x, y, z] = newHeight;
                }
            }
        }   
    }

    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying) return;

        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z <= width; z++)
                {
                    Gizmos.color = new Color(heights[x,y,z], heights[x,y,z], heights[x,y,z], 1);
                    Gizmos.DrawSphere(new Vector3(x, y, z), 0.2f);
                }
            }
        }
    }
}
