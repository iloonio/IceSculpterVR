using System.Collections;
using UnityEngine;

public class MarchingCubes : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds1 = new WaitForSeconds(1);
    [SerializeField] private float width = 16;
    [SerializeField] private float height = 16;

    [SerializeField] private float noiseResolution = 0.1f;

    [SerializeField] private bool visualizeNoise;

    
    private float[,,] heights; //3d Array of heights for each point in the grid
    // TODO: Why is it called heights? rename it to something better. 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(UpdateAll());
    }

    private IEnumerator UpdateAll()
    {
        while (true)
        {
            SetHeights(); 
            yield return _waitForSeconds1;  
        }
        
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
