using UnityEngine;
using System.Collections.Generic;

public class RaycastToPly : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float maxDistance = 100f;
    public float pointClickRadius = 0.05f; // 点击点云的有效半径

    [Header("Sphere Settings")]
    public float sphereRadius = 0.01f;
    public Material sphereMaterial;
    public Color defaultSphereColor = Color.red;

    [Header("Input")]
    public KeyCode triggerKey = KeyCode.Mouse0; // 鼠标左键

    [Header("Visualization")]
    public bool showVisualRay = true;
    public float visualRayWidth = 0.005f;
    public Color visualRayColor = Color.yellow;
    public Color hitRayColor = Color.green;
    public Color missRayColor = Color.red;
    
    private LineRenderer visualRayRenderer;
    private List<LineRenderer> debugRayRenderers = new List<LineRenderer>();

    void Start()
    {
        // 创建持久的可视化射线
        if (showVisualRay)
        {
            CreateVisualRayRenderer();
        }
    }

    void Update()
    {
        // 更新可视化射线位置（跟随鼠标）
        if (showVisualRay && Camera.main != null && visualRayRenderer != null)
        {
            UpdateVisualRay();
        }

        // 检测按键触发
        if (Input.GetKeyDown(triggerKey))
        {
            PerformRaycast();
        }
    }

    void CreateVisualRayRenderer()
    {
        GameObject rayObj = new GameObject("VisualRay");
        rayObj.transform.SetParent(transform);
        
        visualRayRenderer = rayObj.AddComponent<LineRenderer>();
        visualRayRenderer.material = new Material(Shader.Find("Unlit/Color"));
        visualRayRenderer.material.color = visualRayColor;
        visualRayRenderer.startWidth = visualRayWidth;
        visualRayRenderer.endWidth = visualRayWidth;
        visualRayRenderer.positionCount = 2;
    }

    void UpdateVisualRay()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // Debug: 输出射线信息
        Debug.Log($"=== RaycastToPly: UpdateVisualRay - Origin: {ray.origin}, Direction: {ray.direction} ===");
        
        visualRayRenderer.SetPosition(0, ray.origin);
        visualRayRenderer.SetPosition(1, ray.origin + ray.direction * maxDistance);
    }

    void PerformRaycast()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 执行射线检测
        PerformRaycastToPointCloud(ray);
    }

    void PerformRaycastToPointCloud(Ray ray)
    {
        // Debug: 输出射线信息
        Debug.Log($"=== RaycastToPly: PerformRaycast - Origin: {ray.origin}, Direction: {ray.direction} ===");
        
        // 首先更新可视化射线，确保方向正确
        if (visualRayRenderer != null)
        {
            visualRayRenderer.SetPosition(0, ray.origin);
            visualRayRenderer.SetPosition(1, ray.origin + ray.direction * maxDistance);
            Debug.Log($"=== RaycastToPly: Visual ray updated to max distance ===");
        }

        // 查找所有点云渲染器
        PointCloudRenderer[] pointCloudRenderers = FindObjectsOfType<PointCloudRenderer>();
        
        if (pointCloudRenderers.Length == 0)
        {
            Debug.Log("=== RaycastToPly: No PointCloudRenderer found ===");
            CreateDebugRay(ray.origin, ray.origin + ray.direction * maxDistance, missRayColor);
            return;
        }

        Vector3? closestPoint = null;
        float closestDistance = float.MaxValue;
        PointCloudRenderer hitRenderer = null;

        // 遍历所有点云渲染器
        foreach (PointCloudRenderer renderer in pointCloudRenderers)
        {
            // 检查该点云是否有顶点数据
            if (renderer.vertices == null || renderer.vertices.Count == 0)
                continue;

            // 遍历该点云的所有顶点
            for (int i = 0; i < renderer.vertices.Count; i++)
            {
                Vector3 point = renderer.vertices[i];
                
                // 计算点到射线的距离
                float distanceToRay = Vector3.Cross(ray.direction, point - ray.origin).magnitude;
                
                // 检查点是否在射线前方且在有效半径内
                if (distanceToRay < pointClickRadius)
                {
                    float distanceAlongRay = Vector3.Dot(point - ray.origin, ray.direction);
                    if (distanceAlongRay > 0 && distanceAlongRay < maxDistance)
                    {
                        // 找到更近的点
                        if (distanceAlongRay < closestDistance)
                        {
                            closestDistance = distanceAlongRay;
                            closestPoint = point;
                            hitRenderer = renderer;
                        }
                    }
                }
            }
        }

        // 如果找到最近的点，创建小球
        if (closestPoint.HasValue)
        {
            Debug.Log($"=== RaycastToPly: Hit point cloud at {closestPoint.Value}, distance: {closestDistance} ===");
            CreateSphereAtPoint(closestPoint.Value);
            
            // 创建持久的命中射线（绿色）- 从相机指向命中点
            CreateDebugRay(ray.origin, closestPoint.Value, hitRayColor);
            
            // 同时更新可视化射线指向命中点
            if (visualRayRenderer != null)
            {
                visualRayRenderer.material.color = hitRayColor;
                visualRayRenderer.SetPosition(1, closestPoint.Value);
                Debug.Log($"=== RaycastToPly: Visual ray updated to hit point ===");
            }
        }
        else
        {
            Debug.Log($"=== RaycastToPly: No point cloud hit. Ray origin: {ray.origin}, direction: {ray.direction} ===");
            
            // 创建持久的未命中射线（红色）- 从相机指向最大距离
            Vector3 missPoint = ray.origin + ray.direction * maxDistance;
            CreateDebugRay(ray.origin, missPoint, missRayColor);
            
            // 更新可视化射线为未命中状态
            if (visualRayRenderer != null)
            {
                visualRayRenderer.material.color = missRayColor;
            }
        }
    }

    void CreateDebugRay(Vector3 start, Vector3 end, Color color)
    {
        GameObject rayObj = new GameObject($"DebugRay_{debugRayRenderers.Count}");
        rayObj.transform.SetParent(transform);
        
        LineRenderer debugRay = rayObj.AddComponent<LineRenderer>();
        debugRay.material = new Material(Shader.Find("Unlit/Color"));
        debugRay.material.color = color;
        debugRay.startWidth = visualRayWidth;
        debugRay.endWidth = visualRayWidth;
        debugRay.positionCount = 2;
        debugRay.SetPosition(0, start);
        debugRay.SetPosition(1, end);
        
        debugRayRenderers.Add(debugRay);
    }

    void CreateSphereAtPoint(Vector3 point)
    {
        // 创建球体
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"HitSphere_{Time.time}";

        // 设置位置和大小
        sphere.transform.position = point;
        sphere.transform.localScale = Vector3.one * sphereRadius * 2f; // 直径 = 半径 * 2

        // 设置材质 - 使用Unlit Shader
        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        Material mat;
        if (sphereMaterial != null)
        {
            mat = new Material(sphereMaterial);
        }
        else
        {
            mat = new Material(Shader.Find("Unlit/Color"));
        }
        mat.color = defaultSphereColor;
        sphereRenderer.sharedMaterial = mat;

        // 移除碰撞器（小球不需要碰撞）
        Destroy(sphere.GetComponent<Collider>());

        // 可选：设置父对象，方便管理
        sphere.transform.SetParent(transform);

        Debug.Log($"=== RaycastToPly: Created sphere at {point} with radius {sphereRadius} ===");
    }

    // 公共方法：清除所有调试射线
    public void ClearDebugRays()
    {
        foreach (LineRenderer ray in debugRayRenderers)
        {
            if (ray != null)
            {
                Destroy(ray.gameObject);
            }
        }
        debugRayRenderers.Clear();
    }

    // 公共方法：可以手动调用射线检测
    public void TriggerRaycast()
    {
        PerformRaycast();
    }

    // 公共方法：从指定位置发射射线
    public void RaycastFromPosition(Vector3 origin, Vector3 direction)
    {
        Ray ray = new Ray(origin, direction);
        PerformRaycastToPointCloud(ray);
    }
}
