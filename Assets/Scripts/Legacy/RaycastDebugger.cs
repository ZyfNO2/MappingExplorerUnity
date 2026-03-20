/*
using UnityEngine;

/// <summary>
/// 射线调试器 - 可视化射线并在击中点生成调试小球
/// </summary>
public class RaycastDebugger : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("主相机，不设置则使用Camera.main")]
    public Camera gameCamera;
    
    [Header("Ray Visualization")]
    [Tooltip("射线长度")]
    public float rayLength = 100f;
    
    [Tooltip("射线宽度")]
    public float rayWidth = 0.005f;
    
    [Tooltip("射线颜色")]
    public Color rayColor = Color.yellow;
    
    [Header("Hit Visualization")]
    [Tooltip("击中时生成的小球大小")]
    public float hitSphereSize = 0.02f;
    
    [Tooltip("击中时小球颜色")]
    public Color hitSphereColor = Color.green;
    
    [Tooltip("未击中时小球颜色")]
    public Color missSphereColor = Color.red;
    
    [Tooltip("小球持续时间（秒），0表示永久")]
    public float sphereLifetime = 0f;
    
    // 组件引用
    private LineRenderer rayRenderer;
    private GameObject lastHitSphere;
    
    void Start()
    {
        // 获取相机
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                Debug.LogError("[RaycastDebugger] No camera assigned!");
                enabled = false;
                return;
            }
        }
        
        // 创建射线可视化
        CreateRayRenderer();
        
        Debug.Log($"[RaycastDebugger] Initialized with camera: {gameCamera.name}");
    }
    
    /// <summary>
    /// 创建射线渲染器
    /// </summary>
    void CreateRayRenderer()
    {
        GameObject rayObj = new GameObject("DebugRay");
        rayObj.transform.SetParent(transform);
        
        rayRenderer = rayObj.AddComponent<LineRenderer>();
        rayRenderer.material = new Material(Shader.Find("Unlit/Color"));
        rayRenderer.material.color = rayColor;
        rayRenderer.startWidth = rayWidth;
        rayRenderer.endWidth = rayWidth;
        rayRenderer.positionCount = 2;
    }
    
    void Update()
    {
        if (gameCamera == null) return;
        
        // 获取鼠标屏幕坐标
        Vector3 mousePos = Input.mousePosition;
        
        // 检查鼠标是否在屏幕内
        if (mousePos.x < 0 || mousePos.x > Screen.width ||
            mousePos.y < 0 || mousePos.y > Screen.height)
        {
            return;
        }
        
        // 计算射线
        Ray ray = gameCamera.ScreenPointToRay(mousePos);
        
        // 更新射线可视化
        UpdateRayVisual(ray);
        
        // 点击时进行射线检测
        if (Input.GetMouseButtonDown(0))
        {
            PerformRaycast(ray);
        }
    }
    
    /// <summary>
    /// 执行射线检测
    /// </summary>
    void PerformRaycast(Ray ray)
    {
        RaycastHit hit;
        bool hasHit = Physics.Raycast(ray, out hit, rayLength);
        
        if (hasHit)
        {
            Debug.Log($"[RaycastDebugger] HIT! Object: {hit.collider.name}, Tag: {hit.collider.tag}");
            Debug.Log($"[RaycastDebugger] Hit Point: {hit.point}, Distance: {hit.distance:F3}");
            
            // 在击中点生成绿色小球
            SpawnDebugSphere(hit.point, true);
        }
        else
        {
            // 计算射线末端位置
            Vector3 rayEnd = ray.origin + ray.direction * rayLength;
            Debug.Log($"[RaycastDebugger] MISS - Ray end at: {rayEnd}");
            
            // 在射线末端生成红色小球
            SpawnDebugSphere(rayEnd, false);
        }
    }
    
    /// <summary>
    /// 生成调试小球
    /// </summary>
    void SpawnDebugSphere(Vector3 position, bool isHit)
    {
        // 删除之前的小球（如果不需要保留多个）
        if (lastHitSphere != null && sphereLifetime == 0)
        {
            Destroy(lastHitSphere);
        }
        
        // 创建小球
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = isHit ? "HitSphere" : "MissSphere";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * hitSphereSize;
        
        // 设置材质
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = isHit ? hitSphereColor : missSphereColor;
        }
        
        // 移除碰撞体
        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
        
        // 保存引用
        lastHitSphere = sphere;
        
        // 设置自动销毁
        if (sphereLifetime > 0)
        {
            Destroy(sphere, sphereLifetime);
        }
        
        Debug.Log($"[RaycastDebugger] Spawned {(isHit ? "green" : "red")} sphere at {position}");
    }
    
    /// <summary>
    /// 更新射线可视化
    /// </summary>
    void UpdateRayVisual(Ray ray)
    {
        if (rayRenderer != null)
        {
            rayRenderer.SetPosition(0, ray.origin);
            rayRenderer.SetPosition(1, ray.origin + ray.direction * rayLength);
        }
    }
    
    /// <summary>
    /// 在Scene视图中绘制辅助线
    /// </summary>
    void OnDrawGizmos()
    {
        if (gameCamera == null) return;
        
        // 绘制相机前方指示线
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(gameCamera.transform.position, 
            gameCamera.transform.position + gameCamera.transform.forward * 5);
    }
    
    /// <summary>
    /// 清理所有调试小球
    /// </summary>
    [ContextMenu("Clear Debug Spheres")]
    public void ClearDebugSpheres()
    {
        GameObject[] spheres = GameObject.FindObjectsOfType<GameObject>();
        foreach (var sphere in spheres)
        {
            if (sphere.name == "HitSphere" || sphere.name == "MissSphere")
            {
                DestroyImmediate(sphere);
            }
        }
        lastHitSphere = null;
        Debug.Log("[RaycastDebugger] Cleared all debug spheres");
    }
}
*/
