using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class MeshFileManager : MonoBehaviour
{
    [SerializeField] private MarchingCubes m_MarchingCubes;
    [SerializeField] private Button m_SaveButton;
    [SerializeField] private Button m_LoadButton;

    [SerializeField] private bool m_Save = false;
    [SerializeField] private bool m_Load = false;

    private string fileName = "saved_mesh.txt";
    private string filePath;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!m_MarchingCubes) FindAnyObjectByType<MarchingCubes>();

        filePath = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"File path is: {filePath}");
    }

    void OnValidate()
    {
        if (m_Save)
        {
            m_Save = false;
            SaveMesh();
        }

        if (m_Load)
        {
            m_Load = false;
            LoadMesh();
        }
    }

    public void SaveMesh()
    {
        if (File.Exists(filePath)) 
            File.Delete(filePath);

        int gridResolution = m_MarchingCubes.GetGridResolution();
        StringBuilder s = new StringBuilder((int) Math.Pow(gridResolution, 3) * 2 + 3);
        s.Append($"{gridResolution}\n");

        for (int z = 0; z < gridResolution; z++)
            for (int y = 0; y < gridResolution; y++)
                for (int x = 0; x < gridResolution; x++)
                    s.Append($"{(int) Math.Round(m_MarchingCubes.GetDensityAt(x, y, z))} ");

        File.AppendAllText(filePath, s.ToString());
        Debug.Log("Saved mesh to file sucessfully!");
    }

    public void LoadMesh()
    {
        string[] lines = File.ReadAllLines(filePath);
        int gridResolution = int.Parse(lines[0]);
        string[] densityStrings = lines[1].Split(" ");
        var densityFloats = from density in densityStrings 
                            select float.Parse(density);
        
        for (int z = 0; z < gridResolution; z++)
            for (int y = 0; y < gridResolution; y++)
                for (int x = 0; x < gridResolution; x++)
                    m_MarchingCubes.SetDensityAt(x, y, z, densityFloats.ElementAt(x + gridResolution * (y + gridResolution * z)));

        m_MarchingCubes.Refresh();
        Debug.Log("Loaded mesh from file sucessfully!");
    }
}
