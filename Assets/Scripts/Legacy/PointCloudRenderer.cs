using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

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
        // 注册场景加载完成事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDisable()
    {
        // 注销场景加载完成事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载时重新初始化
        Initialize();
    }
    
    public void Initialize()
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
        // 清理旧的点云对象和碰撞箱
        ClearPointCloud();
        
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
    
    // 清理点云对象和碰撞箱
    void ClearPointCloud()
    {
        // 删除所有点云Mesh子对象
        ClearMeshChildren();
        
        // 删除点云碰撞箱
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            DestroyImmediate(collider);
        }
        
        Debug.Log("=== PointCloudRenderer: Cleared old point cloud and colliders ===");
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
        // 首先读取整个文件到内存
        byte[] fileData = File.ReadAllBytes(path);
        
        // 找到header的结束位置
        int headerEndIndex = FindHeaderEndIndex(fileData);
        if (headerEndIndex < 0)
        {
            Debug.LogError("Could not find end_header in PLY file");
            return;
        }
        
        // 解析header
        string headerText = System.Text.Encoding.ASCII.GetString(fileData, 0, headerEndIndex);
        string[] headerLines = headerText.Split('\n');
        
        int vertexCount = 0;
        bool isBinary = false;
        bool isLittleEndian = true;
        List<string> propertyNames = new List<string>();
        
        foreach (string line in headerLines)
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("format"))
            {
                if (trimmedLine.Contains("binary"))
                {
                    isBinary = true;
                    isLittleEndian = trimmedLine.Contains("little_endian");
                }
            }
            else if (trimmedLine.StartsWith("element vertex"))
            {
                string[] parts = trimmedLine.Split(' ');
                vertexCount = int.Parse(parts[2]);
            }
            else if (trimmedLine.StartsWith("property"))
            {
                string[] parts = trimmedLine.Split(' ');
                if (parts.Length >= 3)
                {
                    propertyNames.Add(parts[parts.Length - 1]);
                }
            }
        }
        
        // 数据从header结束后的下一个字节开始
        int dataStartIndex = headerEndIndex;
        
        // 读取数据
        if (isBinary)
        {
            LoadBinaryPLYData(fileData, dataStartIndex, vertexCount, isLittleEndian, propertyNames);
        }
        else
        {
            string asciiData = System.Text.Encoding.ASCII.GetString(fileData, dataStartIndex, fileData.Length - dataStartIndex);
            LoadASCIIPLYData(asciiData, vertexCount);
        }
    }
    
    int FindHeaderEndIndex(byte[] data)
    {
        string searchString = "end_header\n";
        byte[] searchBytes = System.Text.Encoding.ASCII.GetBytes(searchString);
        
        for (int i = 0; i <= data.Length - searchBytes.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (data[i + j] != searchBytes[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
            {
                return i + searchBytes.Length;
            }
        }
        
        return -1;
    }
    
    void LoadBinaryPLYData(byte[] data, int startIndex, int vertexCount, bool isLittleEndian, List<string> propertyNames)
    {
        int index = startIndex;
        
        for (int i = 0; i < vertexCount && index + 27 <= data.Length; i++)
        {
            // 读取x, y, z (double类型，每个8字节)
            double x = BitConverter.ToDouble(data, index);
            double y = BitConverter.ToDouble(data, index + 8);
            double z = BitConverter.ToDouble(data, index + 16);
            
            // 如果不是小端，需要反转字节
            if (!isLittleEndian)
            {
                x = ReverseBytes(x);
                y = ReverseBytes(y);
                z = ReverseBytes(z);
            }
            
            vertices.Add(new Vector3((float)x, (float)y, (float)z));
            
            // 读取颜色 (uchar类型，每个1字节)
            byte r = data[index + 24];
            byte g = data[index + 25];
            byte b = data[index + 26];
            
            Color color = new Color(r / 255f, g / 255f, b / 255f);
            colors.Add(color);
            
            // 每个顶点占27字节 (3个double + 3个byte)
            index += 27;
        }
        
        Debug.Log($"=== PointCloudRenderer: Loaded {vertices.Count} vertices from binary PLY ===");
    }
    
    double ReverseBytes(double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }
    
    void LoadASCIIPLYData(string asciiData, int vertexCount)
    {
        string[] lines = asciiData.Split('\n');
        int count = 0;
        
        foreach (string line in lines)
        {
            if (count >= vertexCount) break;
            
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;
            
            string[] parts = trimmedLine.Split(' ');
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
                count++;
            }
        }
        
        Debug.Log($"=== PointCloudRenderer: Loaded {vertices.Count} vertices from ASCII PLY ===");
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
        // 清除现有的子对象（用于分批渲染）
        ClearMeshChildren();
        
        // 清除主对象的MeshFilter和MeshRenderer
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
        
        // 使用空间分桶算法，将空间上接近的点放入同一个Mesh
        CreateSpatialMeshBatches();
    }
    
    void CreateSpatialMeshBatches()
    {
        const int maxVerticesPerMesh = 65000;
        int totalVertices = vertices.Count;
        
        // 计算点云的边界框
        Vector3 minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        
        foreach (Vector3 vertex in vertices)
        {
            minBounds = Vector3.Min(minBounds, vertex);
            maxBounds = Vector3.Max(maxBounds, vertex);
        }
        
        Vector3 boundsSize = maxBounds - minBounds;
        Debug.Log($"=== PointCloudRenderer: Bounds size: {boundsSize}, Min: {minBounds}, Max: {maxBounds} ===");
        
        // 计算需要的网格数量（每个网格最多65000个顶点）
        int targetBatchCount = Mathf.CeilToInt((float)totalVertices / maxVerticesPerMesh);
        
        // 计算每个维度的分割数，使总网格数接近targetBatchCount
        int divisionsPerAxis = Mathf.CeilToInt(Mathf.Pow(targetBatchCount, 1f / 3f));
        divisionsPerAxis = Mathf.Max(1, divisionsPerAxis);
        
        Debug.Log($"=== PointCloudRenderer: Creating spatial grid with {divisionsPerAxis}x{divisionsPerAxis}x{divisionsPerAxis} divisions ===");
        
        // 创建空间网格字典：key是网格坐标，value是该网格内的顶点索引列表
        Dictionary<Vector3Int, List<int>> spatialGrid = new Dictionary<Vector3Int, List<int>>();
        
        // 将每个顶点分配到对应的空间网格
        for (int i = 0; i < totalVertices; i++)
        {
            Vector3 vertex = vertices[i];
            Vector3 normalizedPos = new Vector3(
                (vertex.x - minBounds.x) / boundsSize.x,
                (vertex.y - minBounds.y) / boundsSize.y,
                (vertex.z - minBounds.z) / boundsSize.z
            );
            
            Vector3Int gridCoord = new Vector3Int(
                Mathf.FloorToInt(normalizedPos.x * divisionsPerAxis),
                Mathf.FloorToInt(normalizedPos.y * divisionsPerAxis),
                Mathf.FloorToInt(normalizedPos.z * divisionsPerAxis)
            );
            
            // 确保坐标在有效范围内
            gridCoord.x = Mathf.Clamp(gridCoord.x, 0, divisionsPerAxis - 1);
            gridCoord.y = Mathf.Clamp(gridCoord.y, 0, divisionsPerAxis - 1);
            gridCoord.z = Mathf.Clamp(gridCoord.z, 0, divisionsPerAxis - 1);
            
            if (!spatialGrid.ContainsKey(gridCoord))
            {
                spatialGrid[gridCoord] = new List<int>();
            }
            spatialGrid[gridCoord].Add(i);
        }
        
        Debug.Log($"=== PointCloudRenderer: Created {spatialGrid.Count} spatial cells ===");
        
        // 将空间网格合并成Mesh批次（每个Mesh最多65000个顶点）
        List<List<int>> meshBatches = new List<List<int>>();
        List<int> currentBatch = new List<int>();
        
        foreach (var kvp in spatialGrid)
        {
            List<int> cellVertices = kvp.Value;
            
            // 如果当前批次加上这个网格的顶点数超过限制，就创建新批次
            if (currentBatch.Count + cellVertices.Count > maxVerticesPerMesh && currentBatch.Count > 0)
            {
                meshBatches.Add(new List<int>(currentBatch));
                currentBatch.Clear();
            }
            
            // 如果单个网格就超过限制，需要分割
            if (cellVertices.Count > maxVerticesPerMesh)
            {
                // 先保存当前批次
                if (currentBatch.Count > 0)
                {
                    meshBatches.Add(new List<int>(currentBatch));
                    currentBatch.Clear();
                }
                
                // 分割大网格
                for (int i = 0; i < cellVertices.Count; i += maxVerticesPerMesh)
                {
                    List<int> splitBatch = cellVertices.GetRange(i, Mathf.Min(maxVerticesPerMesh, cellVertices.Count - i));
                    meshBatches.Add(splitBatch);
                }
            }
            else
            {
                currentBatch.AddRange(cellVertices);
            }
        }
        
        // 添加最后一个批次
        if (currentBatch.Count > 0)
        {
            meshBatches.Add(currentBatch);
        }
        
        Debug.Log($"=== PointCloudRenderer: Merged into {meshBatches.Count} mesh batches ===");
        
        // 创建Mesh
        for (int i = 0; i < meshBatches.Count; i++)
        {
            CreateMeshBatchFromIndices(i, meshBatches[i]);
        }
        
        Debug.Log($"=== PointCloudRenderer: Created {meshBatches.Count} spatial mesh batches ===");
        
        // 为点云添加包围盒碰撞器
        AddPointCloudCollider(minBounds, maxBounds);
    }
    
    // 为点云添加包围盒碰撞器
    void AddPointCloudCollider(Vector3 minBounds, Vector3 maxBounds)
    {
        // 计算包围盒中心点和大小
        Vector3 center = (minBounds + maxBounds) / 2f;
        Vector3 size = maxBounds - minBounds;
        
        // 添加BoxCollider
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = center - transform.position; // 转换为局部坐标
        collider.size = size;
        
        // 设置tag为"Ply"用于射线检测
        gameObject.tag = "Ply";
        
        Debug.Log($"=== PointCloudRenderer: Added BoxCollider - Center: {center}, Size: {size} ===");
    }
    
    void ClearMeshChildren()
    {
        // 删除所有用于分批渲染的子对象
        List<Transform> childrenToDestroy = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("PointCloudMesh_"))
            {
                childrenToDestroy.Add(child);
            }
        }
        
        foreach (Transform child in childrenToDestroy)
        {
            DestroyImmediate(child.gameObject);
        }
    }
    
    void CreateMeshBatchFromIndices(int batchIndex, List<int> vertexIndices)
    {
        // 创建子对象
        GameObject meshObj = new GameObject($"PointCloudMesh_{batchIndex}");
        meshObj.transform.parent = transform;
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localRotation = Quaternion.identity;
        meshObj.transform.localScale = Vector3.one;
        
        // 设置tag为"Ply"用于射线检测
        meshObj.tag = "Ply";
        
        int vertexCount = vertexIndices.Count;
        
        // 提取这批顶点数据
        Vector3[] batchVertices = new Vector3[vertexCount];
        Color[] batchColors = new Color[vertexCount];
        int[] indices = new int[vertexCount];
        
        for (int i = 0; i < vertexCount; i++)
        {
            int sourceIndex = vertexIndices[i];
            batchVertices[i] = vertices[sourceIndex];
            batchColors[i] = colors[sourceIndex];
            indices[i] = i;
        }
        
        // 创建Mesh
        Mesh mesh = new Mesh();
        mesh.vertices = batchVertices;
        mesh.colors = batchColors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mesh.UploadMeshData(true); // 优化：上传到GPU后释放CPU内存
        
        // 添加组件
        MeshFilter meshFilter = meshObj.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = meshObj.AddComponent<MeshRenderer>();
        if (pointMaterial == null)
        {
            pointMaterial = new Material(Shader.Find("Custom/PointCloudShader"));
        }
        meshRenderer.material = pointMaterial;
        
        Debug.Log($"=== PointCloudRenderer: Spatial batch {batchIndex} created with {vertexCount} vertices ===");
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
        
        // 基本参数 - 文件路径选择
        EditorGUILayout.LabelField("Point Cloud File", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        renderer.filePath = EditorGUILayout.TextField(renderer.filePath);
        if (GUILayout.Button("Browse...", GUILayout.Width(80)))
        {
            string selectedPath = EditorUtility.OpenFilePanel(
                "Select Point Cloud File",
                "Assets/Data",
                "ply,csv");
            
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 转换为相对路径
                string projectPath = System.IO.Path.GetFullPath(".");
                if (selectedPath.StartsWith(projectPath))
                {
                    renderer.filePath = selectedPath.Substring(projectPath.Length + 1).Replace('\\', '/');
                }
                else
                {
                    renderer.filePath = selectedPath;
                }
                EditorUtility.SetDirty(renderer);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 加载点云按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Point Cloud", GUILayout.Height(25)))
        {
            renderer.Initialize();
            EditorUtility.SetDirty(renderer);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
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