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
    
    // 点云显示范围控制
    public bool enableFiltering = true;
    public float minX = -1.0f;
    public float maxX = 1.0f;
    public float minY = 0.0f;
    public float maxY = 2.0f;
    public float minZ = -1.0f;
    public float maxZ = 0.0f;
    
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
            // 确保点云对象的transform为identity，这样局部坐标就等于世界坐标
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            Debug.Log("=== PointCloudRenderer: Set transform to identity ===");
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
        
        // 过滤点云，只保留x(-1,-1), y(0,2), z(-1,0)范围内的点
        FilterPointCloud();
    }
    
    // 过滤点云，只保留指定范围内的点
    void FilterPointCloud()
    {
        if (!enableFiltering)
        {
            Debug.Log("=== PointCloudRenderer: Filtering disabled ===");
            return;
        }
        
        List<Vector3> filteredVertices = new List<Vector3>();
        List<Color> filteredColors = new List<Color>();
        
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 point = vertices[i];
            // 检查点是否在指定范围内
            if (point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY && point.z >= minZ && point.z <= maxZ)
            {
                filteredVertices.Add(point);
                filteredColors.Add(colors[i]);
            }
        }
        
        // 替换为过滤后的点
        vertices = filteredVertices;
        colors = filteredColors;
        
        Debug.Log($"=== PointCloudRenderer: Filtered point cloud - {filteredVertices.Count} points remaining ===");
        Debug.Log($"=== Filter range: x[{minX},{maxX}], y[{minY},{maxY}], z[{minZ},{maxZ}] ===");
    }
    
    // 重置过滤，显示所有点云
    public void ResetFilter()
    {
        enableFiltering = false;
        LoadPointCloud();
        CreateMesh();
        Debug.Log("=== PointCloudRenderer: Filter reset - all points displayed ===");
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
        
        // 在原点创建黄色球形测试对象
        CreateSphereTestObject(new Vector3(0, 0, 0), Color.yellow, "TestObject_Origin_Yellow");  // 原点
        // 生成测试对象，确保x轴红色，y轴绿色，z轴蓝色
        CreateTestObject(new Vector3(1, 0, 0), Color.red, "TestObject_X_Red");     // x轴
        CreateTestObject(new Vector3(0, 1, 0), Color.green, "TestObject_Y_Green");   // y轴
        CreateTestObject(new Vector3(0, 0, 1), Color.blue, "TestObject_Z_Blue");    // z轴
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
        // 设置为PointCloudRenderer的子物体
        obj.transform.parent = transform;
        obj.transform.localPosition = position;
        
        // 添加Cube作为正方形
        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateCubeMesh(0.05f);
        
        // 添加MeshRenderer和Material
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        // 使用Unlit shader
        Material material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        renderer.material = material;
        
        // 添加BoxCollider
        obj.AddComponent<BoxCollider>();
        
        Debug.Log($"Created test object {name} at {position} with color {color}");
    }
    
    // 创建球形测试对象
    void CreateSphereTestObject(Vector3 position, Color color, string name)
    {
        // 创建GameObject
        GameObject obj = new GameObject(name);
        // 设置为PointCloudRenderer的子物体
        obj.transform.parent = transform;
        obj.transform.localPosition = position;
        
        // 添加Sphere作为球形
        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateSphereMesh(0.05f);
        
        // 添加MeshRenderer和Material
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        // 使用Unlit shader
        Material material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        renderer.material = material;
        
        // 添加SphereCollider
        obj.AddComponent<SphereCollider>();
        
        Debug.Log($"Created sphere test object {name} at {position} with color {color}");
    }
    
    // 创建球形网格
    Mesh CreateSphereMesh(float radius)
    {
        Mesh mesh = new Mesh();
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        
        int segments = 8;
        float angleStep = 2 * Mathf.PI / segments;
        
        // 顶部顶点
        vertices.Add(new Vector3(0, radius, 0));
        normals.Add(Vector3.up);
        
        // 中间层
        for (int i = 1; i < segments; i++)
        {
            float y = radius * Mathf.Cos(i * angleStep / 2);
            float r = radius * Mathf.Sin(i * angleStep / 2);
            
            for (int j = 0; j < segments; j++)
            {
                float x = r * Mathf.Cos(j * angleStep);
                float z = r * Mathf.Sin(j * angleStep);
                vertices.Add(new Vector3(x, y, z));
                normals.Add(new Vector3(x, y, z).normalized);
            }
        }
        
        // 底部顶点
        vertices.Add(new Vector3(0, -radius, 0));
        normals.Add(Vector3.down);
        
        // 顶部三角
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add((i + 1) % segments + 1);
        }
        
        // 中间三角
        for (int i = 0; i < segments - 2; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int baseIndex = i * segments + 1;
                triangles.Add(baseIndex + j);
                triangles.Add(baseIndex + (j + 1) % segments);
                triangles.Add(baseIndex + j + segments);
                
                triangles.Add(baseIndex + (j + 1) % segments);
                triangles.Add(baseIndex + (j + 1) % segments + segments);
                triangles.Add(baseIndex + j + segments);
            }
        }
        
        // 底部三角
        int bottomIndex = vertices.Count - 1;
        int lastLayerStart = (segments - 2) * segments + 1;
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(bottomIndex);
            triangles.Add(lastLayerStart + i);
            triangles.Add(lastLayerStart + (i + 1) % segments);
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        
        return mesh;
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
        
        // 点云过滤设置
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Point Cloud Filtering", EditorStyles.boldLabel);
        renderer.enableFiltering = EditorGUILayout.Toggle("Enable Filtering", renderer.enableFiltering);
        
        if (renderer.enableFiltering)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("X Range");
            renderer.minX = EditorGUILayout.FloatField(renderer.minX);
            renderer.maxX = EditorGUILayout.FloatField(renderer.maxX);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Y Range");
            renderer.minY = EditorGUILayout.FloatField(renderer.minY);
            renderer.maxY = EditorGUILayout.FloatField(renderer.maxY);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Z Range");
            renderer.minZ = EditorGUILayout.FloatField(renderer.minZ);
            renderer.maxZ = EditorGUILayout.FloatField(renderer.maxZ);
            EditorGUILayout.EndHorizontal();
            
            // 应用过滤按钮
            if (GUILayout.Button("Apply Filter"))
            {
                renderer.LoadPointCloud();
                renderer.CreateMesh();
                EditorUtility.SetDirty(renderer);
            }
        }
        
        // 重置过滤按钮
        if (GUILayout.Button("Reset Filter"))
        {
            renderer.ResetFilter();
            EditorUtility.SetDirty(renderer);
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
    

}