/*
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 点云导入器 - 支持PLY格式导入、Delaunay三角剖分表面重构
/// 生成可用于碰撞检测的Mesh
/// </summary>
public class PointCloudImporter : MonoBehaviour
{
    [Header("Point Cloud File")]
    [Tooltip("默认点云文件路径")]
    public string filePath = "Assets/Data/HD2K_SN36245620_15-18-34/HD2K_SN36245620_15-18-34_original_processed.ply";
    
    [Header("Import Settings")]
    [Tooltip("坐标缩放因子，1.0表示1:1导入")]
    public float scaleFactor = 1.0f;
    
    [Tooltip("是否进行Delaunay三角剖分")]
    public bool enableTriangulation = true;
    
    [Tooltip("三角剖分邻居点数")]
    public int neighborCount = 8;
    
    [Tooltip("最大三角形边长（过滤过大三角形）")]
    public float maxTriangleEdgeLength = 0.5f;
    
    [Header("Rendering")]
    [Tooltip("渲染材质")]
    public Material meshMaterial;
    
    [Tooltip("是否使用Billboard作为备选")]
    public bool useBillboardFallback = false;
    
    [Tooltip("Billboard点大小")]
    public float billboardPointSize = 0.01f;
    
    // 存储导入的数据
    private List<Vector3> vertices = new List<Vector3>();
    private List<Color> vertexColors = new List<Color>();
    private List<int> triangles = new List<int>();
    private Mesh generatedMesh;
    
    // 点云边界
    private Bounds pointCloudBounds;
    
    void Start()
    {
        ImportPointCloud();
    }
    
    /// <summary>
    /// 导入点云主方法
    /// </summary>
    public void ImportPointCloud()
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogError($"[PointCloudImporter] File not found: {filePath}");
            return;
        }
        
        Debug.Log($"[PointCloudImporter] Starting import from: {filePath}");
        
        // 清理旧数据
        ClearExistingData();
        
        // 读取PLY文件
        if (!LoadPLYFile(filePath))
        {
            Debug.LogError("[PointCloudImporter] Failed to load PLY file");
            return;
        }
        
        // 计算边界
        CalculateBounds();
        
        // 生成Mesh
        if (enableTriangulation && vertices.Count > 0)
        {
            GenerateTriangulatedMesh();
        }
        else
        {
            GeneratePointMesh();
        }
        
        // 添加碰撞器
        AddMeshCollider();
        
        Debug.Log($"[PointCloudImporter] Import complete. Vertices: {vertices.Count}, Triangles: {triangles.Count / 3}");
    }
    
    /// <summary>
    /// 清理现有数据
    /// </summary>
    void ClearExistingData()
    {
        vertices.Clear();
        vertexColors.Clear();
        triangles.Clear();
        
        // 销毁旧Mesh
        if (generatedMesh != null)
        {
            DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
        
        // 移除MeshFilter和MeshRenderer
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null) DestroyImmediate(filter);
        
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) DestroyImmediate(renderer);
        
        // 移除碰撞器
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            DestroyImmediate(col);
        }
        
        Debug.Log("[PointCloudImporter] Cleared existing data");
    }
    
    /// <summary>
    /// 加载PLY文件
    /// </summary>
    bool LoadPLYFile(string path)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(path);
            int headerEndIndex = FindHeaderEnd(fileData);
            
            if (headerEndIndex < 0)
            {
                Debug.LogError("[PointCloudImporter] Could not find end_header in PLY file");
                return false;
            }
            
            // 解析Header
            string header = System.Text.Encoding.ASCII.GetString(fileData, 0, headerEndIndex);
            PLYHeaderInfo headerInfo = ParseHeader(header);
            
            Debug.Log($"[PointCloudImporter] PLY Format: {(headerInfo.isBinary ? "Binary" : "ASCII")}, Vertices: {headerInfo.vertexCount}");
            
            // 读取顶点数据
            if (headerInfo.isBinary)
            {
                ReadBinaryData(fileData, headerEndIndex, headerInfo);
            }
            else
            {
                string asciiData = System.Text.Encoding.ASCII.GetString(fileData, headerEndIndex, fileData.Length - headerEndIndex);
                ReadASCIIData(asciiData, headerInfo);
            }
            
            // 应用缩放
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] *= scaleFactor;
            }
            
            Debug.Log($"[PointCloudImporter] Loaded {vertices.Count} vertices");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PointCloudImporter] Error loading PLY: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 查找Header结束位置
    /// </summary>
    int FindHeaderEnd(byte[] data)
    {
        string endMarker = "end_header\n";
        byte[] markerBytes = System.Text.Encoding.ASCII.GetBytes(endMarker);
        
        for (int i = 0; i <= data.Length - markerBytes.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < markerBytes.Length; j++)
            {
                if (data[i + j] != markerBytes[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i + markerBytes.Length;
        }
        return -1;
    }
    
    /// <summary>
    /// PLY文件头信息
    /// </summary>
    struct PLYHeaderInfo
    {
        public int vertexCount;
        public bool isBinary;
        public bool isLittleEndian;
        public List<string> properties;
        public int vertexSize; // 每个顶点的字节数
    }
    
    /// <summary>
    /// 解析PLY文件头
    /// </summary>
    PLYHeaderInfo ParseHeader(string header)
    {
        PLYHeaderInfo info = new PLYHeaderInfo
        {
            properties = new List<string>(),
            isBinary = false,
            isLittleEndian = true,
            vertexSize = 0
        };
        
        string[] lines = header.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("format"))
            {
                info.isBinary = trimmed.Contains("binary");
                info.isLittleEndian = trimmed.Contains("little_endian");
            }
            else if (trimmed.StartsWith("element vertex"))
            {
                string[] parts = trimmed.Split(' ');
                if (parts.Length >= 3) int.TryParse(parts[2], out info.vertexCount);
            }
            else if (trimmed.StartsWith("property"))
            {
                info.properties.Add(trimmed);
                // 计算属性大小
                if (trimmed.Contains("float") || trimmed.Contains("int"))
                    info.vertexSize += 4;
                else if (trimmed.Contains("double"))
                    info.vertexSize += 8;
                else if (trimmed.Contains("uchar"))
                    info.vertexSize += 1;
            }
        }
        
        return info;
    }
    
    /// <summary>
    /// 读取二进制数据
    /// </summary>
    void ReadBinaryData(byte[] data, int startIndex, PLYHeaderInfo info)
    {
        int index = startIndex;
        
        for (int i = 0; i < info.vertexCount && index < data.Length; i++)
        {
            // 假设标准格式: x,y,z (double), r,g,b (uchar)
            if (index + 27 <= data.Length)
            {
                double x = BitConverter.ToDouble(data, index);
                double y = BitConverter.ToDouble(data, index + 8);
                double z = BitConverter.ToDouble(data, index + 16);
                
                if (!info.isLittleEndian)
                {
                    x = ReverseDouble(x);
                    y = ReverseDouble(y);
                    z = ReverseDouble(z);
                }
                
                vertices.Add(new Vector3((float)x, (float)y, (float)z));
                
                byte r = data[index + 24];
                byte g = data[index + 25];
                byte b = data[index + 26];
                vertexColors.Add(new Color(r / 255f, g / 255f, b / 255f));
                
                index += 27;
            }
        }
    }
    
    /// <summary>
    /// 读取ASCII数据
    /// </summary>
    void ReadASCIIData(string data, PLYHeaderInfo info)
    {
        string[] lines = data.Split('\n');
        int count = 0;
        
        foreach (string line in lines)
        {
            if (count >= info.vertexCount) break;
            
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            string[] parts = trimmed.Split(' ');
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
                    color = new Color(r / 255f, g / 255f, b / 255f);
                }
                vertexColors.Add(color);
                count++;
            }
        }
    }
    
    double ReverseDouble(double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }
    
    /// <summary>
    /// 计算点云边界
    /// </summary>
    void CalculateBounds()
    {
        if (vertices.Count == 0) return;
        
        Vector3 min = vertices[0];
        Vector3 max = vertices[0];
        
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        
        pointCloudBounds = new Bounds((min + max) / 2f, max - min);
        Debug.Log($"[PointCloudImporter] Bounds: Center={pointCloudBounds.center}, Size={pointCloudBounds.size}");
    }
    
    /// <summary>
    /// 生成三角剖分Mesh（Delaunay-like）
    /// </summary>
    void GenerateTriangulatedMesh()
    {
        Debug.Log("[PointCloudImporter] Starting surface reconstruction...");
        
        // 使用基于距离的表面重构算法
        triangles = SurfaceReconstruction.Reconstruct(vertices, neighborCount, maxTriangleEdgeLength);
        
        if (triangles.Count == 0)
        {
            Debug.LogWarning("[PointCloudImporter] Triangulation produced no triangles, falling back to point rendering");
            GeneratePointMesh();
            return;
        }
        
        // 创建Mesh
        generatedMesh = new Mesh();
        generatedMesh.vertices = vertices.ToArray();
        generatedMesh.colors = vertexColors.ToArray();
        generatedMesh.triangles = triangles.ToArray();
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        
        // 设置组件
        SetupMeshComponents();
        
        Debug.Log($"[PointCloudImporter] Generated mesh with {triangles.Count / 3} triangles");
    }
    
    /// <summary>
    /// 生成点云Mesh（无三角剖分）
    /// </summary>
    void GeneratePointMesh()
    {
        generatedMesh = new Mesh();
        generatedMesh.vertices = vertices.ToArray();
        generatedMesh.colors = vertexColors.ToArray();
        
        // 使用点拓扑
        int[] indices = new int[vertices.Count];
        for (int i = 0; i < vertices.Count; i++) indices[i] = i;
        generatedMesh.SetIndices(indices, MeshTopology.Points, 0);
        
        generatedMesh.RecalculateBounds();
        
        SetupMeshComponents();
        
        Debug.Log($"[PointCloudImporter] Generated point mesh with {vertices.Count} points");
    }
    
    /// <summary>
    /// 设置Mesh组件
    /// </summary>
    void SetupMeshComponents()
    {
        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        filter.mesh = generatedMesh;
        
        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        if (meshMaterial == null)
        {
            meshMaterial = new Material(Shader.Find("Standard"));
            meshMaterial.SetFloat("_Glossiness", 0.0f);
            meshMaterial.SetFloat("_Metallic", 0.0f);
            meshMaterial.EnableKeyword("_VERTEXCOLORS");
        }
        renderer.material = meshMaterial;
        
        // 设置Tag用于射线检测
        gameObject.tag = "PointCloud";
    }
    
    /// <summary>
    /// 添加Mesh碰撞器
    /// </summary>
    void AddMeshCollider()
    {
        if (generatedMesh == null || triangles.Count == 0)
        {
            // 如果没有三角形，使用BoxCollider作为备选
            BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.center = pointCloudBounds.center - transform.position;
            boxCol.size = pointCloudBounds.size;
            Debug.Log("[PointCloudImporter] Added BoxCollider (no mesh triangles)");
        }
        else
        {
            MeshCollider meshCol = gameObject.AddComponent<MeshCollider>();
            meshCol.sharedMesh = generatedMesh;
            meshCol.convex = false;
            Debug.Log("[PointCloudImporter] Added MeshCollider");
        }
    }
    
    /// <summary>
    /// 获取生成的Mesh
    /// </summary>
    public Mesh GetMesh()
    {
        return generatedMesh;
    }
    
    /// <summary>
    /// 获取点云边界
    /// </summary>
    public Bounds GetBounds()
    {
        return pointCloudBounds;
    }
    
    /// <summary>
    /// 重新加载点云（供外部调用）
    /// </summary>
    public void Reload()
    {
        ImportPointCloud();
    }
}

/// <summary>
/// 表面重构算法 - 基于局部邻域的三角剖分
/// </summary>
public static class SurfaceReconstruction
{
    /// <summary>
    /// 重建表面 - 使用基于距离的方法
    /// </summary>
    public static List<int> Reconstruct(List<Vector3> vertices, int neighborCount, float maxEdgeLength)
    {
        List<int> triangles = new List<int>();
        
        if (vertices.Count < 3) return triangles;
        
        // 构建KD树或空间哈希用于快速邻域查询
        SpatialHash spatialHash = new SpatialHash(vertices, maxEdgeLength * 2f);
        
        // 对每个点，找到其邻域并创建局部三角形
        HashSet<(int, int, int)> triangleSet = new HashSet<(int, int, int)>();
        
        for (int i = 0; i < vertices.Count; i++)
        {
            // 找到最近的邻居
            List<int> neighbors = spatialHash.FindNearestNeighbors(i, neighborCount, maxEdgeLength);
            
            // 创建局部三角形
            List<int> localTriangles = CreateLocalTriangles(i, neighbors, vertices, maxEdgeLength);
            
            // 添加到集合（自动去重）
            for (int t = 0; t < localTriangles.Count; t += 3)
            {
                int a = localTriangles[t];
                int b = localTriangles[t + 1];
                int c = localTriangles[t + 2];
                
                // 排序确保唯一性
                int[] sorted = new int[] { a, b, c };
                System.Array.Sort(sorted);
                
                var key = (sorted[0], sorted[1], sorted[2]);
                if (!triangleSet.Contains(key))
                {
                    triangleSet.Add(key);
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
            }
        }
        
        return triangles;
    }
    
    /// <summary>
    /// 为单个顶点创建局部三角形
    /// </summary>
    static List<int> CreateLocalTriangles(int centerIdx, List<int> neighbors, List<Vector3> vertices, float maxEdgeLength)
    {
        List<int> triangles = new List<int>();
        
        if (neighbors.Count < 2) return triangles;
        
        Vector3 center = vertices[centerIdx];
        
        // 按角度排序邻居
        List<(int idx, float angle)> sortedNeighbors = new List<(int, float)>();
        Vector3 reference = Vector3.right;
        
        foreach (int neighborIdx in neighbors)
        {
            Vector3 dir = vertices[neighborIdx] - center;
            float angle = Mathf.Atan2(dir.y, dir.x);
            sortedNeighbors.Add((neighborIdx, angle));
        }
        
        sortedNeighbors.Sort((a, b) => a.angle.CompareTo(b.angle));
        
        // 创建扇形三角形
        for (int i = 0; i < sortedNeighbors.Count; i++)
        {
            int nextIdx = (i + 1) % sortedNeighbors.Count;
            
            int a = centerIdx;
            int b = sortedNeighbors[i].idx;
            int c = sortedNeighbors[nextIdx].idx;
            
            // 检查边长
            float ab = Vector3.Distance(vertices[a], vertices[b]);
            float ac = Vector3.Distance(vertices[a], vertices[c]);
            float bc = Vector3.Distance(vertices[b], vertices[c]);
            
            if (ab <= maxEdgeLength && ac <= maxEdgeLength && bc <= maxEdgeLength)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }
        
        return triangles;
    }
}

/// <summary>
/// 空间哈希 - 用于快速邻域查询
/// </summary>
public class SpatialHash
{
    private Dictionary<Vector3Int, List<int>> grid;
    private List<Vector3> vertices;
    private float cellSize;
    private Vector3 minBounds;
    
    public SpatialHash(List<Vector3> verts, float cellSize)
    {
        this.vertices = verts;
        this.cellSize = cellSize;
        this.grid = new Dictionary<Vector3Int, List<int>>();
        
        // 计算边界
        minBounds = verts[0];
        foreach (var v in verts)
        {
            minBounds = Vector3.Min(minBounds, v);
        }
        
        // 构建网格
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3Int cell = GetCell(verts[i]);
            if (!grid.ContainsKey(cell))
            {
                grid[cell] = new List<int>();
            }
            grid[cell].Add(i);
        }
    }
    
    Vector3Int GetCell(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt((pos.x - minBounds.x) / cellSize),
            Mathf.FloorToInt((pos.y - minBounds.y) / cellSize),
            Mathf.FloorToInt((pos.z - minBounds.z) / cellSize)
        );
    }
    
    /// <summary>
    /// 查找最近邻居
    /// </summary>
    public List<int> FindNearestNeighbors(int vertexIdx, int count, float maxDistance)
    {
        List<int> neighbors = new List<int>();
        Vector3 pos = vertices[vertexIdx];
        Vector3Int centerCell = GetCell(pos);
        
        // 搜索相邻的格子
        int searchRadius = Mathf.CeilToInt(maxDistance / cellSize);
        
        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                for (int z = -searchRadius; z <= searchRadius; z++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(x, y, z);
                    if (grid.ContainsKey(cell))
                    {
                        foreach (int idx in grid[cell])
                        {
                            if (idx != vertexIdx)
                            {
                                float dist = Vector3.Distance(pos, vertices[idx]);
                                if (dist <= maxDistance)
                                {
                                    neighbors.Add(idx);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        // 按距离排序并取前count个
        neighbors.Sort((a, b) => 
            Vector3.Distance(pos, vertices[a]).CompareTo(Vector3.Distance(pos, vertices[b])));
        
        if (neighbors.Count > count)
        {
            neighbors = neighbors.GetRange(0, count);
        }
        
        return neighbors;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PointCloudImporter))]
public class PointCloudImporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PointCloudImporter importer = (PointCloudImporter)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Point Cloud Import Settings", EditorStyles.boldLabel);
        
        // 文件选择
        EditorGUILayout.BeginHorizontal();
        importer.filePath = EditorGUILayout.TextField("File Path", importer.filePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select PLY File", "Assets/Data", "ply");
            if (!string.IsNullOrEmpty(path))
            {
                importer.filePath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 导入设置
        importer.scaleFactor = EditorGUILayout.FloatField("Scale Factor", importer.scaleFactor);
        importer.enableTriangulation = EditorGUILayout.Toggle("Enable Triangulation", importer.enableTriangulation);
        
        if (importer.enableTriangulation)
        {
            importer.neighborCount = EditorGUILayout.IntSlider("Neighbor Count", importer.neighborCount, 3, 20);
            importer.maxTriangleEdgeLength = EditorGUILayout.FloatField("Max Edge Length", importer.maxTriangleEdgeLength);
        }
        
        // 渲染设置
        EditorGUILayout.Space();
        importer.meshMaterial = (Material)EditorGUILayout.ObjectField("Material", importer.meshMaterial, typeof(Material), false);
        
        EditorGUILayout.Space();
        
        // 导入按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Import Point Cloud", GUILayout.Height(30)))
        {
            importer.ImportPointCloud();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space();
        
        // 显示信息
        Mesh mesh = importer.GetMesh();
        if (mesh != null)
        {
            EditorGUILayout.LabelField("Mesh Info:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Vertices: {mesh.vertexCount}");
            EditorGUILayout.LabelField($"Triangles: {mesh.triangles.Length / 3}");
            EditorGUILayout.LabelField($"Bounds: {mesh.bounds}");
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(importer);
        }
    }
}
#endif
*/
