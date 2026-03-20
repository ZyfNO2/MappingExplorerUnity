using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 点云点击检测器 - 直接对点云数据进行射线匹配
/// 不使用碰撞器，通过计算点到射线的距离来检测
/// </summary>
public class PointCloudClickDetector : MonoBehaviour
{
    [Header("Camera")]
    public Camera gameCamera;
    
    [Header("Click Detection")]
    [Tooltip("射线圆柱半径（用于筛选点云）")]
    public float cylinderRadius = 0.05f;
    
    [Tooltip("最大检测距离")]
    public float maxDistance = 100f;
    
    [Tooltip("最近点搜索范围（从射线起点开始）")]
    public float searchRange = 50f;
    
    [Header("Visualization")]
    [Tooltip("标记小球大小")]
    public float markerSize = 0.02f;
    
    [Tooltip("标记小球颜色")]
    public Color markerColor = Color.green;
    
    [Tooltip("是否显示调试射线")]
    public bool showDebugRay = true;
    
    [Tooltip("调试射线长度")]
    public float debugRayLength = 10f;
    
    private LineRenderer debugRayRenderer;
    private GameObject lastMarker;
    
    void Start()
    {
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }
        
        if (showDebugRay)
        {
            CreateDebugRayRenderer();
        }
    }
    
    void CreateDebugRayRenderer()
    {
        GameObject rayObj = new GameObject("DebugRay");
        rayObj.transform.SetParent(transform);
        debugRayRenderer = rayObj.AddComponent<LineRenderer>();
        debugRayRenderer.material = new Material(Shader.Find("Unlit/Color"));
        debugRayRenderer.material.color = Color.yellow;
        debugRayRenderer.startWidth = 0.005f;
        debugRayRenderer.endWidth = 0.005f;
        debugRayRenderer.positionCount = 2;
    }
    
    void Update()
    {
        if (gameCamera == null) return;
        
        // 获取鼠标射线
        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
        
        // 更新调试射线
        if (showDebugRay && debugRayRenderer != null)
        {
            debugRayRenderer.SetPosition(0, ray.origin);
            debugRayRenderer.SetPosition(1, ray.origin + ray.direction * debugRayLength);
        }
        
        // 点击检测
        if (Input.GetMouseButtonDown(0))
        {
            PerformClickDetection(ray);
        }
    }
    
    /// <summary>
    /// 执行点击检测 - 直接对点云数据进行射线匹配
    /// </summary>
    void PerformClickDetection(Ray ray)
    {
        // 获取场景中的所有点云
        PointCloudManager[] pointCloudManagers = FindObjectsOfType<PointCloudManager>();
        
        if (pointCloudManagers.Length == 0)
        {
            Debug.LogWarning("[PointCloudClickDetector] No PointCloudManager found");
            return;
        }
        
        // 收集所有候选点
        List<(Vector3 point, float distanceToRay, float distanceAlongRay)> candidates = 
            new List<(Vector3, float, float)>();
        
        foreach (var manager in pointCloudManagers)
        {
            var pointData = manager.GetPointCloudData();
            
            foreach (var point in pointData)
            {
                // 只处理有效点
                if (!point.position.Equals(Vector3.zero))
                {
                    // 计算点到射线的距离
                    float distToRay = DistancePointToLine(point.position, ray.origin, ray.direction);
                    
                    // 计算点在射线上的投影距离
                    float t = Vector3.Dot(point.position - ray.origin, ray.direction);
                    
                    // 如果在圆柱范围内且在搜索范围内
                    if (distToRay <= cylinderRadius && t >= 0 && t <= searchRange)
                    {
                        candidates.Add((point.position, distToRay, t));
                    }
                }
            }
        }
        
        if (candidates.Count == 0)
        {
            Debug.Log("[PointCloudClickDetector] No points within cylinder");
            return;
        }
        
        Debug.Log($"[PointCloudClickDetector] Found {candidates.Count} candidate points");
        
        // 按射线投影距离排序，找到最近的点
        var sortedCandidates = candidates.OrderBy(c => c.distanceAlongRay).ToList();
        
        // 取前几个最近的点，计算它们的中心
        int centerCount = Mathf.Min(10, sortedCandidates.Count);
        Vector3 centerPoint = Vector3.zero;
        
        for (int i = 0; i < centerCount; i++)
        {
            centerPoint += sortedCandidates[i].point;
        }
        centerPoint /= centerCount;
        
        Debug.Log($"[PointCloudClickDetector] Closest point at {centerPoint}, " +
                  $"distance along ray: {sortedCandidates[0].distanceAlongRay:F3}, " +
                  $"distance to ray: {sortedCandidates[0].distanceToRay:F3}");
        
        // 生成标记小球
        SpawnMarker(centerPoint);
    }
    
    /// <summary>
    /// 计算点到直线的距离
    /// </summary>
    float DistancePointToLine(Vector3 point, Vector3 lineOrigin, Vector3 lineDirection)
    {
        Vector3 pointToOrigin = point - lineOrigin;
        float t = Vector3.Dot(pointToOrigin, lineDirection);
        Vector3 projection = lineOrigin + lineDirection * t;
        return Vector3.Distance(point, projection);
    }
    
    /// <summary>
    /// 生成标记小球
    /// </summary>
    void SpawnMarker(Vector3 position)
    {
        // 删除旧标记
        if (lastMarker != null)
        {
            Destroy(lastMarker);
        }
        
        // 创建新标记
        lastMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lastMarker.name = "ClickMarker";
        lastMarker.transform.position = position;
        lastMarker.transform.localScale = Vector3.one * markerSize;
        
        // 设置材质
        Renderer renderer = lastMarker.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = markerColor;
        
        // 移除碰撞体
        Collider col = lastMarker.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        Debug.Log($"[PointCloudClickDetector] Spawned marker at {position}");
    }
    
    /// <summary>
    /// 清除所有标记
    /// </summary>
    [ContextMenu("Clear Markers")]
    public void ClearMarkers()
    {
        GameObject[] markers = GameObject.FindObjectsOfType<GameObject>();
        foreach (var marker in markers)
        {
            if (marker.name == "ClickMarker")
            {
                DestroyImmediate(marker);
            }
        }
        lastMarker = null;
    }
}
