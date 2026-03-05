using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using UnityEditor;

[ExecuteInEditMode]

public class PointCloudRenderer : MonoBehaviour
{
    public string filePath = "Assets/Data/point_cloud_gen.ply";
    public float pointSize = 0.05f;
    public float pointWidth = 1.0f;
    public float pointHeight = 1.0f;
    public Material pointMaterial;
    
    private Mesh mesh;
    public List<Vector3> vertices = new List<Vector3>();
    public List<Color> colors = new List<Color>();
    
    void Start()
    {
        // 每次进入游戏运行模式时重新初始化
        Initialize();
    }
    
    void OnEnable()
    {
        // 每次启用时重新初始化
        Initialize();
    }
    
    void OnLevelWasLoaded(int level)
    {
        // 场景加载时重新初始化
        Initialize();
    }
    
    void Initialize()
    {
        // 无论是否在编辑器模式还是运行模式，都重新载入点云
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            Debug.Log($"=== PointCloudRenderer: Initializing - Loading point cloud from {filePath} ===");
            LoadPointCloud();
            CreateMesh();
            Debug.Log("=== PointCloudRenderer: Initialization complete ===");
        }
        else
        {
            Debug.Log($"=== PointCloudRenderer: File not found or path empty - {filePath} ===");
        }
    }
    
    public void LoadPointCloud()
    {
        // 清空之前的数据
        vertices.Clear();
        colors.Clear();
        
        if (filePath.EndsWith(".ply"))
        {
            LoadPLYFile(filePath);
        }
        else if (filePath.EndsWith(".csv"))
        {
            LoadCSVFile(filePath);
        }
        else
        {
            Debug.LogError("Unsupported file format. Please use .ply or .csv");
        }
    }
    
    void LoadPLYFile(string path)
    {
        using (StreamReader reader = new StreamReader(path))
        {
            string line;
            bool isHeader = true;
            int vertexCount = 0;
            
            while ((line = reader.ReadLine()) != null)
            {
                if (isHeader)
                {
                    if (line.StartsWith("element vertex"))
                    {
                        string[] parts = line.Split(' ');
                        vertexCount = int.Parse(parts[2]);
                    }
                    else if (line == "end_header")
                    {
                        isHeader = false;
                    }
                }
                else
                {
                    string[] parts = line.Split(' ');
                    if (parts.Length >= 3)
                    {
                        float x = float.Parse(parts[0]);
                        float y = float.Parse(parts[1]);
                        float z = float.Parse(parts[2]);
                        vertices.Add(new Vector3(x, y, z));
                        
                        Color color = Color.white;
                        if (parts.Length >= 9)
                        {
                            byte r = byte.Parse(parts[6]);
                            byte g = byte.Parse(parts[7]);
                            byte b = byte.Parse(parts[8]);
                            color = new Color(r / 255f, g / 255f, b / 255f);
                        }
                        colors.Add(color);
                    }
                }
            }
        }
    }
    
    void LoadCSVFile(string path)
    {
        using (StreamReader reader = new StreamReader(path))
        {
            string line;
            bool isHeader = true;
            
            while ((line = reader.ReadLine()) != null)
            {
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }
                
                string[] parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    float x = float.Parse(parts[0]);
                    float y = float.Parse(parts[1]);
                    float z = float.Parse(parts[2]);
                    vertices.Add(new Vector3(x, y, z));
                    
                    Color color = Color.white;
                    if (parts.Length >= 6)
                    {
                        float r = float.Parse(parts[3]);
                        float g = float.Parse(parts[4]);
                        float b = float.Parse(parts[5]);
                        color = new Color(r, g, b);
                    }
                    colors.Add(color);
                }
            }
        }
    }
    
    public void CreateMesh()
    {
        // 清除现有的MeshFilter和MeshRenderer组件
        MeshFilter existingFilter = GetComponent<MeshFilter>();
        if (existingFilter != null)
        {
            DestroyImmediate(existingFilter);
        }
        
        MeshRenderer existingRenderer = GetComponent<MeshRenderer>();
        if (existingRenderer != null)
        {
            DestroyImmediate(existingRenderer);
        }
        
        mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.colors = colors.ToArray();
        mesh.SetIndices(Enumerable.Range(0, vertices.Count).ToArray(), MeshTopology.Points, 0);
        
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (pointMaterial == null)
        {
            pointMaterial = new Material(Shader.Find("Custom/PointCloudShader"));
        }
        meshRenderer.material = pointMaterial;
        UpdateMaterialProperties();
    }
    
    void Update()
    {
        // 确保在编辑器模式和运行时参数的更新
        if (pointMaterial != null)
        {
            UpdateMaterialProperties();
        }
    }
    
    void UpdateMaterialProperties()
    {
        pointMaterial.SetFloat("_PointSize", pointSize);
        pointMaterial.SetFloat("_PointWidth", pointWidth);
        pointMaterial.SetFloat("_PointHeight", pointHeight);
    }
    
    // 生成测试对象
    [ContextMenu("Generate Test Objects")]
    public void GenerateTestObjects()
    {
        // 清除现有的测试对象
        ClearTestObjects();
        
        // 生成测试对象
        CreateTestObject(new Vector3(0, 0, 0), Color.red, "TestObject_Red");
        CreateTestObject(new Vector3(0, 1, 0), Color.green, "TestObject_Green");
        CreateTestObject(new Vector3(1, 0, 0), Color.blue, "TestObject_Blue");
        CreateTestObject(new Vector3(0, 0, 1), Color.yellow, "TestObject_Yellow");
    }
    
    // 清除测试对象
    [ContextMenu("Clear Test Objects")]
    public void ClearTestObjects()
    {
        // 查找并删除所有测试对象
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("TestObject_"))
            {
                DestroyImmediate(obj);
            }
        }
    }
    
    // 创建测试对象
    void CreateTestObject(Vector3 position, Color color, string name)
    {
        // 创建GameObject
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        
        // 添加Cube作为正方形
        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateCubeMesh(0.05f);
        
        // 添加MeshRenderer和Material
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        renderer.material = material;
        
        // 添加BoxCollider
        obj.AddComponent<BoxCollider>();
        
        Debug.Log($"Created test object {name} at {position} with color {color}");
    }
    
    // 创建立方体网格
    Mesh CreateCubeMesh(float size)
    {
        Mesh mesh = new Mesh();
        
        // 顶点
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-size/2, -size/2, -size/2),
            new Vector3(size/2, -size/2, -size/2),
            new Vector3(size/2, size/2, -size/2),
            new Vector3(-size/2, size/2, -size/2),
            new Vector3(-size/2, -size/2, size/2),
            new Vector3(size/2, -size/2, size/2),
            new Vector3(size/2, size/2, size/2),
            new Vector3(-size/2, size/2, size/2)
        };
        
        // 三角形
        int[] triangles = new int[]
        {
            0, 1, 2, 0, 2, 3, // 前面
            1, 5, 6, 1, 6, 2, // 右面
            5, 4, 7, 5, 7, 6, // 后面
            4, 0, 3, 4, 3, 7, // 左面
            3, 2, 6, 3, 6, 7, // 上面
            4, 5, 1, 4, 1, 0  // 下面
        };
        
        // 法线
        Vector3[] normals = new Vector3[]
        {
            new Vector3(0, 0, -1),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        
        return mesh;
    }
}

[CustomEditor(typeof(PointCloudRenderer))]
public class PointCloudRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            EditorGUILayout.HelpBox("Target is null", MessageType.Error);
            return;
        }
        
        PointCloudRenderer renderer = (PointCloudRenderer)target;
        
        // 基本参数
        renderer.filePath = EditorGUILayout.TextField("File Path", renderer.filePath);
        renderer.pointSize = EditorGUILayout.Slider("Point Size", renderer.pointSize, 0.001f, 5.0f);
        renderer.pointWidth = EditorGUILayout.Slider("Point Width", renderer.pointWidth, 0.01f, 10.0f);
        renderer.pointHeight = EditorGUILayout.Slider("Point Height", renderer.pointHeight, 0.01f, 10.0f);
        renderer.pointMaterial = (Material)EditorGUILayout.ObjectField("Point Material", renderer.pointMaterial, typeof(Material), false);
        
        // 编辑器按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Import PLY File"))
        {
            string path = EditorUtility.OpenFilePanel("Select PLY File", Application.dataPath, "ply");
            if (!string.IsNullOrEmpty(path))
            {
                renderer.filePath = path;
                renderer.LoadPointCloud();
                renderer.CreateMesh();
                EditorUtility.SetDirty(renderer);
            }
        }
        
        if (GUILayout.Button("Save PLY File"))
        {
            string path = EditorUtility.SaveFilePanel("Save PLY File", Application.dataPath, "point_cloud", "ply");
            if (!string.IsNullOrEmpty(path))
            {
                SavePLYFile(renderer, path);
            }
        }
        
        // 测试对象按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Test Objects"))
        {
            renderer.GenerateTestObjects();
        }
        
        if (GUILayout.Button("Clear Test Objects"))
        {
            renderer.ClearTestObjects();
        }
        
        // 应用更改
        if (GUI.changed)
        {
            EditorUtility.SetDirty(renderer);
        }
    }
    
    void SavePLYFile(PointCloudRenderer renderer, string path)
    {
        if (renderer == null || renderer.vertices == null || renderer.colors == null)
        {
            Debug.LogError("Cannot save PLY file: renderer or data is null");
            return;
        }
        
        try
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                // 写入PLY文件头
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine("comment Generated by PointCloudRenderer");
                writer.WriteLine($"element vertex {renderer.vertices.Count}");
                writer.WriteLine("property float32 x");
                writer.WriteLine("property float32 y");
                writer.WriteLine("property float32 z");
                writer.WriteLine("property uchar red");
                writer.WriteLine("property uchar green");
                writer.WriteLine("property uchar blue");
                writer.WriteLine("end_header");
                
                // 写入顶点数据
                for (int i = 0; i < renderer.vertices.Count; i++)
                {
                    if (i < renderer.colors.Count)
                    {
                        Vector3 vertex = renderer.vertices[i];
                        Color color = renderer.colors[i];
                        int r = Mathf.RoundToInt(color.r * 255);
                        int g = Mathf.RoundToInt(color.g * 255);
                        int b = Mathf.RoundToInt(color.b * 255);
                        writer.WriteLine($"{vertex.x} {vertex.y} {vertex.z} {r} {g} {b}");
                    }
                }
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"PLY file saved to: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving PLY file: {e.Message}");
        }
    }
}