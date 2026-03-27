using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 点云管理器 - 高性能点云渲染
/// 使用Mesh + GPU Points渲染，避免创建大量GameObject
/// </summary>
public class PointCloudManager : MonoBehaviour
{
    [Header("File Settings")]
    [Tooltip("默认点云文件路径")]
    public string filePath = "Assets/Data/HD2K_SN36245620_15-18-34/HD2K_SN36245620_15-18-34_original_processed.ply";
    
    [Header("Rendering Settings")]
    [Tooltip("点大小")]
    public float pointSize = 0.05f;
    
    [Tooltip("坐标缩放因子")]
    public float scaleFactor = 1.0f;
    
    [Tooltip("渲染材质")]
    public Material pointMaterial;
    
    [Header("Performance Settings")]
    [Tooltip("每Mesh最大顶点数（Unity限制65535）")]
    public int maxVerticesPerMesh = 65000;
    
    [Tooltip("空间分桶网格数")]
    public int spatialDivisions = 8;
    
    /*
    [Header("Algorithm Settings")]
    [Tooltip("KD树叶子节点最大点数")]
    public int kdTreeLeafSize = 32;
    
    [Tooltip("Delaunay邻居数")]
    public int delaunayNeighbors = 8;
    
    [Tooltip("最大三角形边长")]
    public float maxEdgeLength = 0.5f;
    */
    
    // 点云数据
    private List<PointData> pointCloudData = new List<PointData>();
    private List<Mesh> pointCloudMeshes = new List<Mesh>();
    // private KDTree kdTree;
    private Mesh generatedMesh;
    
    // 空间网格数据（用于区域筛选）
    private Dictionary<Vector3Int, List<int>> spatialGrid;
    private Dictionary<Vector3Int, Bounds> gridBoundsCache;
    private Vector3 gridMinBounds;
    private Vector3 gridMaxBounds;
    private Vector3 gridCellSize;
    private Dictionary<Vector3Int, List<GameObject>> gridToGameObjects;
    
    // 精确筛选数据
    private HashSet<int> filteredPointIndices; // 存储筛选后的点索引
    private bool isPreciseFilterApplied = false; // 标记是否应用了精确筛选
    
    // 处理状态
    private bool isProcessing = false;
    private ProcessingStage currentStage = ProcessingStage.Idle;
    
    public enum ProcessingStage
    {
        Idle,
        Loading,
        BuildingMeshes,
        Complete,
        // BuildingKDTree,
        // Triangulating
    }
    
    /// <summary>
    /// 点数据结构
    /// </summary>
    public struct PointData
    {
        public Vector3 position;
        public Color color;
        public Vector3 normal;
        
        public PointData(Vector3 pos, Color col)
        {
            position = pos;
            color = col;
            normal = Vector3.up;
        }
    }
    
    void Start()
    {
        LoadPointCloud();
    }
    
    /// <summary>
    /// 加载点云（异步）
    /// </summary>
    public async void LoadPointCloud()
    {
        if (isProcessing) return;
        
        isProcessing = true;
        currentStage = ProcessingStage.Loading;
        
        Debug.Log("[PointCloudManager] Starting point cloud loading...");
        
        // 清理旧数据
        ClearData();
        
        // 异步读取文件
        bool success = await Task.Run(() => LoadPLYFile(filePath));
        
        if (!success)
        {
            Debug.LogError("[PointCloudManager] Failed to load point cloud");
            isProcessing = false;
            currentStage = ProcessingStage.Idle;
            return;
        }
        
        Debug.Log($"[PointCloudManager] Loaded {pointCloudData.Count} points");
        
        // 在主线程构建Mesh
        currentStage = ProcessingStage.BuildingMeshes;
        await Task.Yield();
        
        BuildPointCloudMeshes();
        
        isProcessing = false;
        currentStage = ProcessingStage.Complete;
        
        Debug.Log("[PointCloudManager] Point cloud rendering complete");
    }
    
    /// <summary>
    /// 构建点云Mesh（空间分桶 + 分批渲染）
    /// </summary>
    void BuildPointCloudMeshes()
    {
        if (pointCloudData.Count == 0) return;
        
        // 计算边界
        Vector3 minBounds = pointCloudData[0].position;
        Vector3 maxBounds = pointCloudData[0].position;
        
        foreach (var point in pointCloudData)
        {
            minBounds = Vector3.Min(minBounds, point.position);
            maxBounds = Vector3.Max(maxBounds, point.position);
        }
        
        Vector3 boundsSize = maxBounds - minBounds;
        Debug.Log($"[PointCloudManager] Bounds: {boundsSize}, Points: {pointCloudData.Count}");
        
        // 保存网格元数据（用于区域筛选）
        gridMinBounds = minBounds;
        gridMaxBounds = maxBounds;
        gridCellSize = boundsSize / spatialDivisions;
        gridBoundsCache = new Dictionary<Vector3Int, Bounds>();
        gridToGameObjects = new Dictionary<Vector3Int, List<GameObject>>();
        
        // 空间分桶
        spatialGrid = new Dictionary<Vector3Int, List<int>>();
        
        for (int i = 0; i < pointCloudData.Count; i++)
        {
            Vector3 normalizedPos = new Vector3(
                (pointCloudData[i].position.x - minBounds.x) / boundsSize.x,
                (pointCloudData[i].position.y - minBounds.y) / boundsSize.y,
                (pointCloudData[i].position.z - minBounds.z) / boundsSize.z
            );
            
            Vector3Int gridCoord = new Vector3Int(
                Mathf.FloorToInt(normalizedPos.x * spatialDivisions),
                Mathf.FloorToInt(normalizedPos.y * spatialDivisions),
                Mathf.FloorToInt(normalizedPos.z * spatialDivisions)
            );
            
            gridCoord.x = Mathf.Clamp(gridCoord.x, 0, spatialDivisions - 1);
            gridCoord.y = Mathf.Clamp(gridCoord.y, 0, spatialDivisions - 1);
            gridCoord.z = Mathf.Clamp(gridCoord.z, 0, spatialDivisions - 1);
            
            if (!spatialGrid.ContainsKey(gridCoord))
            {
                spatialGrid[gridCoord] = new List<int>();
            }
            spatialGrid[gridCoord].Add(i);
        }
        
        Debug.Log($"[PointCloudManager] Created {spatialGrid.Count} spatial cells");
        
        // 每个空间格子独立成一个Mesh（不合并，保持空间连续性）
        int meshIndex = 0;
        foreach (var kvp in spatialGrid)
        {
            List<int> cellVertices = kvp.Value;
            Vector3Int gridCoord = kvp.Key;
            
            // 如果格子点数超过限制，分割成多个Mesh
            if (cellVertices.Count > maxVerticesPerMesh)
            {
                for (int i = 0; i < cellVertices.Count; i += maxVerticesPerMesh)
                {
                    List<int> splitBatch = cellVertices.GetRange(i, Mathf.Min(maxVerticesPerMesh, cellVertices.Count - i));
                    CreateMeshBatch(meshIndex++, splitBatch, gridCoord);
                }
            }
            else
            {
                CreateMeshBatch(meshIndex++, cellVertices, gridCoord);
            }
        }
        
        Debug.Log($"[PointCloudManager] Created {meshIndex} mesh batches (each spatial cell is independent)");
        
        // 不添加碰撞器（由用户按需调用RunDelaunayTriangulation后添加）
        // AddPointCloudCollider(minBounds, maxBounds);
        
        Debug.Log($"[PointCloudManager] Created {pointCloudMeshes.Count} meshes");
    }
    
    /// <summary>
    /// 创建Mesh批次
    /// </summary>
    void CreateMeshBatch(int batchIndex, List<int> indices, Vector3Int gridCoord)
    {
        // 创建子对象，使用网格坐标命名便于识别
        GameObject meshObj = new GameObject($"PointCloudMesh_{gridCoord.x}_{gridCoord.y}_{gridCoord.z}_{batchIndex}");
        meshObj.transform.SetParent(transform);
        meshObj.transform.localPosition = Vector3.zero;
        
        int vertexCount = indices.Count;
        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] meshIndices = new int[vertexCount];
        
        for (int i = 0; i < vertexCount; i++)
        {
            int sourceIndex = indices[i];
            vertices[i] = pointCloudData[sourceIndex].position;
            colors[i] = pointCloudData[sourceIndex].color;
            meshIndices[i] = i;
        }
        
        // 创建Mesh（使用Points拓扑，GPU渲染）
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.SetIndices(meshIndices, MeshTopology.Points, 0);
        mesh.UploadMeshData(true);
        
        pointCloudMeshes.Add(mesh);
        
        // 添加组件
        MeshFilter filter = meshObj.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        
        MeshRenderer renderer = meshObj.AddComponent<MeshRenderer>();
        if (pointMaterial == null)
        {
            // 使用自定义点云Shader
            pointMaterial = new Material(Shader.Find("Custom/PointCloud"));
            pointMaterial.SetFloat("_PointSize", pointSize);
            pointMaterial.SetFloat("_UseVertexColor", 1);
        }
        renderer.material = pointMaterial;
        
        // 缓存GameObject引用（用于区域筛选）
        if (!gridToGameObjects.ContainsKey(gridCoord))
        {
            gridToGameObjects[gridCoord] = new List<GameObject>();
        }
        gridToGameObjects[gridCoord].Add(meshObj);
        
        // 预计算格子边界（用于快速区域查询）
        if (!gridBoundsCache.ContainsKey(gridCoord))
        {
            Vector3 cellMin = gridMinBounds + Vector3.Scale(gridCoord, gridCellSize);
            Vector3 cellMax = cellMin + gridCellSize;
            gridBoundsCache[gridCoord] = new Bounds((cellMin + cellMax) / 2, cellMax - cellMin);
        }
    }
    
    /*
    [Header("Collider Settings")]
    [Tooltip("碰撞器半径（每个空间格子的球体半径）")]
    public float colliderRadius = 0.5f;
    
    /// <summary>
    /// 为每个PointCloudMesh创建碰撞器
    /// 使用SphereCollider（球形不会跨越到其他区域）
    /// </summary>
    [ContextMenu("Create Colliders")]
    public void CreateColliders()
    {
        if (pointCloudMeshes.Count == 0)
        {
            Debug.LogWarning("[PointCloudManager] No point cloud meshes available");
            return;
        }
        
        int colliderCount = 0;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("PointCloudMesh_") && child.GetComponent<MeshFilter>() != null)
            {
                // 移除旧的碰撞器
                Collider[] oldColliders = child.GetComponents<Collider>();
                foreach (var col in oldColliders)
                {
                    DestroyImmediate(col);
                }
                
                // 获取Mesh并计算中心点
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Mesh mesh = filter.sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    
                    if (vertices.Length > 0)
                    {
                        // 计算中心点（平均位置）
                        Vector3 center = Vector3.zero;
                        foreach (var v in vertices)
                        {
                            center += v;
                        }
                        center /= vertices.Length;
                        
                        // 计算最大距离（从中心到最远顶点的距离）
                        float maxDist = 0;
                        foreach (var v in vertices)
                        {
                            float dist = Vector3.Distance(v, center);
                            if (dist > maxDist) maxDist = dist;
                        }
                        
                        // 使用SphereCollider，半径取最大距离 + 一点padding
                        SphereCollider sphereCol = child.gameObject.AddComponent<SphereCollider>();
                        sphereCol.center = center - child.position; // 转换为局部坐标
                        sphereCol.radius = Mathf.Min(maxDist * 1.1f, colliderRadius);
                        
                        colliderCount++;
                    }
                }
            }
        }
        
        Debug.Log($"[PointCloudManager] Added {colliderCount} SphereColliders to point cloud meshes");
    }
    
    /// <summary>
    /// 安全设置Tag（如果不存在则不设置，避免报错）
    /// </summary>
    void SetTagIfExists(string tagName)
    {
        try
        {
            gameObject.tag = tagName;
        }
        catch (UnityException)
        {
            // Tag不存在，忽略错误
            Debug.LogWarning($"[PointCloudManager] Tag '{tagName}' not defined. Please create it in Unity Editor.");
        }
    }
    */
    
    /// <summary>
    /// 加载PLY文件
    /// </summary>
    bool LoadPLYFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[PointCloudManager] File not found: {path}");
                return false;
            }
            
            byte[] fileData = File.ReadAllBytes(path);
            int headerEndIndex = FindHeaderEnd(fileData);
            
            if (headerEndIndex < 0)
            {
                Debug.LogError("[PointCloudManager] Could not find end_header");
                return false;
            }
            
            string header = System.Text.Encoding.ASCII.GetString(fileData, 0, headerEndIndex);
            PLYHeaderInfo headerInfo = ParseHeader(header);
            
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
            for (int i = 0; i < pointCloudData.Count; i++)
            {
                var point = pointCloudData[i];
                point.position *= scaleFactor;
                pointCloudData[i] = point;
            }
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PointCloudManager] Error loading PLY: {e.Message}");
            return false;
        }
    }
    
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
    
    struct PLYHeaderInfo
    {
        public int vertexCount;
        public bool isBinary;
        public bool isLittleEndian;
    }
    
    PLYHeaderInfo ParseHeader(string header)
    {
        PLYHeaderInfo info = new PLYHeaderInfo();
        
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
        }
        
        return info;
    }
    
    void ReadBinaryData(byte[] data, int startIndex, PLYHeaderInfo info)
    {
        int index = startIndex;
        
        for (int i = 0; i < info.vertexCount && index < data.Length; i++)
        {
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
                
                byte r = data[index + 24];
                byte g = data[index + 25];
                byte b = data[index + 26];
                
                Vector3 pos = new Vector3((float)x, (float)y, (float)z);
                Color col = new Color(r / 255f, g / 255f, b / 255f);
                
                pointCloudData.Add(new PointData(pos, col));
                
                index += 27;
            }
        }
    }
    
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
                
                Color color = Color.white;
                if (parts.Length >= 6)
                {
                    float r = float.Parse(parts[3]);
                    float g = float.Parse(parts[4]);
                    float b = float.Parse(parts[5]);
                    color = new Color(r / 255f, g / 255f, b / 255f);
                }
                
                pointCloudData.Add(new PointData(new Vector3(x, y, z), color));
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
    
    #region Algorithms
    /*
    /// <summary>
    /// 构建KD树
    /// </summary>
    [ContextMenu("Build KD Tree")]
    public async void BuildKDTree()
    {
        if (isProcessing || pointCloudData.Count == 0) return;
        
        isProcessing = true;
        currentStage = ProcessingStage.BuildingKDTree;
        
        Debug.Log("[PointCloudManager] Building KD Tree...");
        
        await Task.Run(() =>
        {
            List<Vector3> positions = new List<Vector3>();
            foreach (var point in pointCloudData)
            {
                positions.Add(point.position);
            }
            kdTree = new KDTree(positions, kdTreeLeafSize);
        });
        
        Debug.Log("[PointCloudManager] KD Tree built");
        
        isProcessing = false;
        currentStage = ProcessingStage.Complete;
    }
    
    /// <summary>
    /// 运行Delaunay三角剖分
    /// </summary>
    [ContextMenu("Run Delaunay Triangulation")]
    public async void RunDelaunayTriangulation()
    {
        if (isProcessing || pointCloudData.Count == 0) return;
        
        if (kdTree == null)
        {
            Debug.LogWarning("[PointCloudManager] Building KD Tree first...");
            BuildKDTree();
            while (currentStage == ProcessingStage.BuildingKDTree) await Task.Yield();
        }
        
        isProcessing = true;
        currentStage = ProcessingStage.Triangulating;
        
        Debug.Log("[PointCloudManager] Running Delaunay Triangulation...");
        
        List<int> triangles = await Task.Run(() =>
        {
            return DelaunayTriangulation.Triangulate(pointCloudData, kdTree, delaunayNeighbors, maxEdgeLength);
        });
        
        await Task.Yield();
        GenerateMesh(triangles);
        
        Debug.Log($"[PointCloudManager] Generated mesh with {triangles.Count / 3} triangles");
        
        isProcessing = false;
        currentStage = ProcessingStage.Complete;
    }
    
    void GenerateMesh(List<int> triangles)
    {
        if (triangles.Count == 0) return;
        
        // 清理点云Mesh
        ClearPointCloudMeshes();
        
        generatedMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[pointCloudData.Count];
        Color[] colors = new Color[pointCloudData.Count];
        
        for (int i = 0; i < pointCloudData.Count; i++)
        {
            vertices[i] = pointCloudData[i].position;
            colors[i] = pointCloudData[i].color;
        }
        
        generatedMesh.vertices = vertices;
        generatedMesh.colors = colors;
        generatedMesh.triangles = triangles.ToArray();
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        
        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        filter.mesh = generatedMesh;
        
        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        
        MeshCollider collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = generatedMesh;
    }
    */
    #endregion
    
    /// <summary>
    /// 清理数据
    /// </summary>
    void ClearData()
    {
        pointCloudData.Clear();
        // kdTree = null;
        
        // 清理旧的Material
        if (pointMaterial != null)
        {
            DestroyImmediate(pointMaterial);
            pointMaterial = null;
        }
        
        ClearPointCloudMeshes();
        
        if (generatedMesh != null)
        {
            DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
        
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null) DestroyImmediate(filter);
        
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) DestroyImmediate(renderer);
        
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            DestroyImmediate(col);
        }
        
        // 清理空间网格数据
        spatialGrid?.Clear();
        gridBoundsCache?.Clear();
        gridToGameObjects?.Clear();
    }
    
    /// <summary>
    /// 清理点云Mesh
    /// </summary>
    void ClearPointCloudMeshes()
    {
        foreach (var mesh in pointCloudMeshes)
        {
            if (mesh != null) DestroyImmediate(mesh);
        }
        pointCloudMeshes.Clear();
        
        // 删除子对象
        List<Transform> childrenToDestroy = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("PointCloudMesh_"))
            {
                childrenToDestroy.Add(child);
            }
        }
        
        foreach (var child in childrenToDestroy)
        {
            DestroyImmediate(child.gameObject);
        }
    }
    
    public ProcessingStage GetCurrentStage() { return currentStage; }
    public bool IsProcessing() { return isProcessing; }
    public IReadOnlyList<PointData> GetPointCloudData() { return pointCloudData.AsReadOnly(); }
    
    #region Region Filtering
    
    /// <summary>
    /// 世界坐标转网格坐标
    /// </summary>
    private Vector3Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 boundsSize = gridMaxBounds - gridMinBounds;
        if (boundsSize.x <= 0 || boundsSize.y <= 0 || boundsSize.z <= 0)
            return Vector3Int.zero;
            
        Vector3 normalized = new Vector3(
            (worldPos.x - gridMinBounds.x) / boundsSize.x,
            (worldPos.y - gridMinBounds.y) / boundsSize.y,
            (worldPos.z - gridMinBounds.z) / boundsSize.z
        );
        Vector3Int gridCoord = new Vector3Int(
            Mathf.FloorToInt(normalized.x * spatialDivisions),
            Mathf.FloorToInt(normalized.y * spatialDivisions),
            Mathf.FloorToInt(normalized.z * spatialDivisions)
        );
        
        // 限制在有效范围内
        gridCoord.x = Mathf.Clamp(gridCoord.x, 0, spatialDivisions - 1);
        gridCoord.y = Mathf.Clamp(gridCoord.y, 0, spatialDivisions - 1);
        gridCoord.z = Mathf.Clamp(gridCoord.z, 0, spatialDivisions - 1);
        return gridCoord;
    }
    
    /// <summary>
    /// 获取与目标边界相交的所有格子坐标
    /// </summary>
    public List<Vector3Int> GetGridCellsInRegion(Bounds region)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        
        if (spatialGrid == null || gridBoundsCache == null)
        {
            Debug.LogWarning("[PointCloudManager] Point cloud not loaded or spatial grid not initialized");
            return cells;
        }
        
        // 将区域边界转换为网格坐标
        Vector3Int minCell = WorldToGrid(region.min);
        Vector3Int maxCell = WorldToGrid(region.max);
        
        // 遍历所有可能相交的格子
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    Vector3Int cellCoord = new Vector3Int(x, y, z);
                    
                    // 检查边界是否有效且实际相交
                    if (gridBoundsCache.TryGetValue(cellCoord, out Bounds cellBounds))
                    {
                        if (cellBounds.Intersects(region))
                        {
                            cells.Add(cellCoord);
                        }
                    }
                }
            }
        }
        
        return cells;
    }
    
    /// <summary>
    /// 根据边界框筛选显示点云（只显示区域内的点）
    /// </summary>
    public void FilterByBounds(Bounds region)
    {
        if (spatialGrid == null || gridToGameObjects == null)
        {
            Debug.LogWarning("[PointCloudManager] Point cloud not loaded");
            return;
        }
        
        Debug.Log($"[PointCloudManager] Filtering by region: center={region.center}, size={region.size}");
        
        // 1. 获取所有需要显示的格子
        List<Vector3Int> targetCells = GetGridCellsInRegion(region);
        HashSet<Vector3Int> cellSet = new HashSet<Vector3Int>(targetCells);
        
        Debug.Log($"[PointCloudManager] Target cells: {targetCells.Count}");
        
        // 2. 遍历所有GameObject，根据格子坐标决定是否显示
        int hiddenCount = 0;
        int visibleCount = 0;
        
        foreach (var kvp in gridToGameObjects)
        {
            Vector3Int cellCoord = kvp.Key;
            List<GameObject> meshObjects = kvp.Value;
            
            bool inRegion = cellSet.Contains(cellCoord);
            
            foreach (var meshObj in meshObjects)
            {
                if (meshObj != null)
                {
                    meshObj.SetActive(inRegion);
                    if (inRegion) visibleCount++;
                    else hiddenCount++;
                }
            }
        }
        
        Debug.Log($"[PointCloudManager] Filter complete: Visible={visibleCount}, Hidden={hiddenCount}");
    }
    
    /// <summary>
    /// 根据边界框精确筛选点云（只显示区域内的点）- 精确到每个点
    /// </summary>
    public void FilterByBoundsPrecise(Bounds region)
    {
        if (pointCloudData == null || pointCloudData.Count == 0)
        {
            Debug.LogWarning("[PointCloudManager] Point cloud data not loaded");
            return;
        }
        
        if (spatialGrid == null || gridToGameObjects == null)
        {
            Debug.LogWarning("[PointCloudManager] Spatial grid not initialized");
            return;
        }
        
        Debug.Log($"[PointCloudManager] Precise filtering by region: center={region.center}, size={region.size}");
        
        // 记录开始时间
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        // 1. 先获取所有可能与区域相交的格子（粗筛）
        List<Vector3Int> candidateCells = GetGridCellsInRegion(region);
        Debug.Log($"[PointCloudManager] Candidate cells for precise filter: {candidateCells.Count}");
        
        // 2. 精确筛选：在每个格子内检查每个点是否在边界内
        int totalPointsChecked = 0;
        int pointsInRegion = 0;
        int meshesRebuilt = 0;
        
        // 记录需要显示的点的索引（用于后续恢复）
        filteredPointIndices = new HashSet<int>();
        
        foreach (Vector3Int cellCoord in candidateCells)
        {
            if (!spatialGrid.TryGetValue(cellCoord, out List<int> pointIndices))
                continue;
            
            if (!gridToGameObjects.TryGetValue(cellCoord, out List<GameObject> meshObjects))
                continue;
            
            // 收集在此区域内的点的索引
            List<int> indicesInRegion = new List<int>();
            foreach (int pointIndex in pointIndices)
            {
                totalPointsChecked++;
                Vector3 pointPos = pointCloudData[pointIndex].position;
                
                // 精确检查点是否在边界框内
                if (region.Contains(pointPos))
                {
                    indicesInRegion.Add(pointIndex);
                    filteredPointIndices.Add(pointIndex);
                    pointsInRegion++;
                }
            }
            
            // 重建Mesh，只包含在区域内的点
            if (indicesInRegion.Count > 0)
            {
                RebuildMeshWithPoints(meshObjects, indicesInRegion);
                meshesRebuilt++;
            }
            else
            {
                // 没有点在区域内，隐藏所有mesh
                foreach (var meshObj in meshObjects)
                {
                    if (meshObj != null)
                        meshObj.SetActive(false);
                }
            }
        }
        
        // 3. 隐藏不在候选格子中的mesh（确定不在区域内）
        HashSet<Vector3Int> candidateCellSet = new HashSet<Vector3Int>(candidateCells);
        int hiddenCount = 0;
        foreach (var kvp in gridToGameObjects)
        {
            if (!candidateCellSet.Contains(kvp.Key))
            {
                foreach (var meshObj in kvp.Value)
                {
                    if (meshObj != null && meshObj.activeSelf)
                    {
                        meshObj.SetActive(false);
                        hiddenCount++;
                    }
                }
            }
        }
        
        stopwatch.Stop();
        
        Debug.Log($"[PointCloudManager] Precise filter complete:\n" +
                  $"  - Points checked: {totalPointsChecked}\n" +
                  $"  - Points in region: {pointsInRegion}\n" +
                  $"  - Meshes rebuilt: {meshesRebuilt}\n" +
                  $"  - Meshes hidden: {hiddenCount}\n" +
                  $"  - Time elapsed: {stopwatch.ElapsedMilliseconds}ms");
        
        isPreciseFilterApplied = true;
    }
    
    /// <summary>
    /// 根据指定的点索引重建Mesh
    /// </summary>
    private void RebuildMeshWithPoints(List<GameObject> meshObjects, List<int> pointIndices)
    {
        if (meshObjects == null || meshObjects.Count == 0 || pointIndices == null || pointIndices.Count == 0)
            return;
        
        // 创建新的vertices、colors和indices数组
        int vertexCount = pointIndices.Count;
        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] indices = new int[vertexCount];
        
        for (int i = 0; i < vertexCount; i++)
        {
            int sourceIndex = pointIndices[i];
            vertices[i] = pointCloudData[sourceIndex].position;
            colors[i] = pointCloudData[sourceIndex].color;
            indices[i] = i;
        }
        
        // 创建新的Mesh
        Mesh newMesh = new Mesh();
        newMesh.vertices = vertices;
        newMesh.colors = colors;
        newMesh.SetIndices(indices, MeshTopology.Points, 0);
        newMesh.UploadMeshData(false);
        
        // 应用Mesh到第一个GameObject，隐藏其他的（因为我们是按单元存储的）
        for (int i = 0; i < meshObjects.Count; i++)
        {
            GameObject meshObj = meshObjects[i];
            if (meshObj == null) continue;
            
            MeshFilter meshFilter = meshObj.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = meshObj.AddComponent<MeshFilter>();
            
            // 只在第一个对象上设置Mesh，其他对象隐藏
            if (i == 0)
            {
                meshFilter.sharedMesh = newMesh;
                meshObj.SetActive(true);
                
                // 确保有MeshRenderer
                MeshRenderer renderer = meshObj.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = meshObj.AddComponent<MeshRenderer>();
                
                if (pointMaterial != null)
                    renderer.material = pointMaterial;
            }
            else
            {
                meshObj.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 清除筛选，显示所有点云
    /// </summary>
    public void ClearFilter()
    {
        if (gridToGameObjects == null)
        {
            Debug.LogWarning("[PointCloudManager] Point cloud not loaded");
            return;
        }
        
        Debug.Log("[PointCloudManager] Clearing filter, showing all points");
        
        // 如果应用了精确筛选，需要重建所有Mesh
        if (isPreciseFilterApplied && spatialGrid != null)
        {
            Debug.Log("[PointCloudManager] Rebuilding all meshes to restore original point cloud");
            
            int meshesRebuilt = 0;
            foreach (var kvp in spatialGrid)
            {
                Vector3Int cellCoord = kvp.Key;
                List<int> pointIndices = kvp.Value;
                
                if (gridToGameObjects.TryGetValue(cellCoord, out List<GameObject> meshObjects))
                {
                    RebuildMeshWithPoints(meshObjects, pointIndices);
                    meshesRebuilt++;
                }
            }
            
            isPreciseFilterApplied = false;
            filteredPointIndices = null;
            
            Debug.Log($"[PointCloudManager] Rebuilt {meshesRebuilt} meshes to restore all points");
        }
        else
        {
            // 普通筛选，只需激活所有GameObject
            int restoredCount = 0;
            foreach (var kvp in gridToGameObjects)
            {
                foreach (var meshObj in kvp.Value)
                {
                    if (meshObj != null && !meshObj.activeSelf)
                    {
                        meshObj.SetActive(true);
                        restoredCount++;
                    }
                }
            }
            
            if (restoredCount > 0)
            {
                Debug.Log($"[PointCloudManager] Restored {restoredCount} mesh batches");
            }
        }
    }
    
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(PointCloudManager))]
public class PointCloudManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PointCloudManager manager = (PointCloudManager)target;
        
        EditorGUILayout.LabelField("Point Cloud Manager", EditorStyles.boldLabel);
        
        // 文件选择
        EditorGUILayout.BeginHorizontal();
        manager.filePath = EditorGUILayout.TextField("File Path", manager.filePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select PLY File", "Assets/Data", "ply");
            if (!string.IsNullOrEmpty(path)) manager.filePath = path;
        }
        EditorGUILayout.EndHorizontal();
        
        // 设置
        manager.pointSize = EditorGUILayout.FloatField("Point Size", manager.pointSize);
        manager.scaleFactor = EditorGUILayout.FloatField("Scale Factor", manager.scaleFactor);
        
        // 状态
        EditorGUILayout.Space();
        PointCloudManager.ProcessingStage stage = manager.GetCurrentStage();
        EditorGUILayout.LabelField($"Status: {stage}");
        
        // 按钮
        EditorGUILayout.Space();
        GUI.enabled = !manager.IsProcessing();
        
        if (GUILayout.Button("Load Point Cloud", GUILayout.Height(30)))
        {
            manager.LoadPointCloud();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        
        /*
        if (GUILayout.Button("Create Colliders"))
        {
            manager.CreateColliders();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Algorithms", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Build KD Tree"))
        {
            manager.BuildKDTree();
        }
        */
        
        GUI.enabled = true;
        
        if (GUI.changed) EditorUtility.SetDirty(manager);
    }
}
#endif
