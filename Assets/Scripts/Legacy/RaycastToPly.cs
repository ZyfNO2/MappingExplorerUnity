// using UnityEngine;

// public class RaycastToPly : MonoBehaviour
// {
//     [Header("Camera")]
//     public Camera gameCamera;
    
//     [Header("Ray Visualization")]
//     public float rayLength = 100f;
//     public float rayWidth = 0.005f;
//     public Color rayColor = Color.yellow;
//     public Color crosshairColor = Color.red;
    
//     [Header("Point Cloud Raycast")]
//     public float maxRaycastDistance = 100f;
//     public float hitSphereSize = 0.02f;
//     public Color hitSphereColor = Color.green;
//     public Color missSphereColor = Color.red;
    
//     private LineRenderer rayRenderer;
//     private LineRenderer crosshairRendererX;
//     private LineRenderer crosshairRendererY;
//     private GameObject lastHitSphere;
    
//     void Start()
//     {
//         if (gameCamera == null)
//         {
//             gameCamera = Camera.main;
//             if (gameCamera == null)
//             {
//                 Debug.LogError("[RaycastToPly] No camera assigned!");
//                 enabled = false;
//                 return;
//             }
//         }
        
//         Debug.Log($"[RaycastToPly] Using camera: {gameCamera.name}");
//         Debug.Log($"[RaycastToPly] Camera projection: {(gameCamera.orthographic ? "Orthographic" : "Perspective")}");
//         Debug.Log($"[RaycastToPly] Camera rect: {gameCamera.rect}");
//         Debug.Log($"[RaycastToPly] Screen size: {Screen.width}x{Screen.height}");
        
//         CreateVisualizations();
//     }

//     void CreateVisualizations()
//     {
//         // 主射线
//         GameObject rayObj = new GameObject("DebugRay");
//         rayObj.transform.SetParent(transform);
//         rayRenderer = rayObj.AddComponent<LineRenderer>();
//         rayRenderer.material = new Material(Shader.Find("Unlit/Color"));
//         rayRenderer.material.color = rayColor;
//         rayRenderer.startWidth = rayWidth;
//         rayRenderer.endWidth = rayWidth;
//         rayRenderer.positionCount = 2;
        
//         // 十字准星 - X方向
//         GameObject crossXObj = new GameObject("CrosshairX");
//         crossXObj.transform.SetParent(transform);
//         crosshairRendererX = crossXObj.AddComponent<LineRenderer>();
//         crosshairRendererX.material = new Material(Shader.Find("Unlit/Color"));
//         crosshairRendererX.material.color = crosshairColor;
//         crosshairRendererX.startWidth = rayWidth * 2;
//         crosshairRendererX.endWidth = rayWidth * 2;
//         crosshairRendererX.positionCount = 2;
        
//         // 十字准星 - Y方向
//         GameObject crossYObj = new GameObject("CrosshairY");
//         crossYObj.transform.SetParent(transform);
//         crosshairRendererY = crossYObj.AddComponent<LineRenderer>();
//         crosshairRendererY.material = new Material(Shader.Find("Unlit/Color"));
//         crosshairRendererY.material.color = crosshairColor;
//         crosshairRendererY.startWidth = rayWidth * 2;
//         crosshairRendererY.endWidth = rayWidth * 2;
//         crosshairRendererY.positionCount = 2;
//     }

//     void Update()
//     {
//         if (gameCamera == null) return;
        
//         // 获取鼠标屏幕坐标
//         Vector3 mouseScreenPos = Input.mousePosition;
        
//         // 关键：检查鼠标是否在有效范围内
//         if (mouseScreenPos.x < 0 || mouseScreenPos.x > Screen.width ||
//             mouseScreenPos.y < 0 || mouseScreenPos.y > Screen.height)
//         {
//             return;
//         }
        
//         // 计算射线
//         Ray ray = gameCamera.ScreenPointToRay(mouseScreenPos);
        
//         // 更新射线可视化
//         UpdateRayVisual(ray);
        
//         // 更新十字准星（在射线终点显示）
//         UpdateCrosshair(ray);
        
//         // 点击时进行射线检测并放置小球
//         if (Input.GetMouseButtonDown(0))
//         {
//             PerformRaycast(ray);
//         }
//     }
    
//     void PerformRaycast(Ray ray)
//     {
//         // 方法1: 使用Physics.Raycast检测碰撞体
//         RaycastHit hit;
//         bool hasHit = Physics.Raycast(ray, out hit, maxRaycastDistance);
        
//         if (hasHit)
//         {
//             Debug.Log($"[RaycastToPly] HIT! Object: {hit.collider.name}, Tag: {hit.collider.tag}, Distance: {hit.distance:F3}");
//             Debug.Log($"[RaycastToPly] Hit Point: {hit.point}");
//             SpawnDebugSphere(hit.point, true);
//         }
//         else
//         {
//             // 如果没有击中，在射线检测的最大距离处放置红色小球
//             Vector3 missPoint = ray.origin + ray.direction * maxRaycastDistance;
//             Debug.Log($"[RaycastToPly] MISS - No collision detected within {maxRaycastDistance}m");
//             Debug.Log($"[RaycastToPly] Debug sphere placed at ray end: {missPoint}");
//             SpawnDebugSphere(missPoint, false);
//         }
//     }
    
//     void SpawnDebugSphere(Vector3 position, bool isHit)
//     {
//         // 删除之前的小球
//         if (lastHitSphere != null)
//         {
//             Destroy(lastHitSphere);
//         }
        
//         // 创建新的小球
//         lastHitSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//         lastHitSphere.name = isHit ? "HitSphere" : "MissSphere";
//         lastHitSphere.transform.position = position;
//         lastHitSphere.transform.localScale = Vector3.one * hitSphereSize;
        
//         // 设置材质颜色
//         Renderer renderer = lastHitSphere.GetComponent<Renderer>();
//         if (renderer != null)
//         {
//             renderer.material = new Material(Shader.Find("Unlit/Color"));
//             renderer.material.color = isHit ? hitSphereColor : missSphereColor;
//         }
        
//         // 移除碰撞体（我们不需要小球的碰撞体）
//         Collider collider = lastHitSphere.GetComponent<Collider>();
//         if (collider != null)
//         {
//             Destroy(collider);
//         }
        
//         Debug.Log($"[RaycastToPly] Spawned {(isHit ? "green" : "red")} sphere at {position}");
//     }
    
//     void UpdateRayVisual(Ray ray)
//     {
//         if (rayRenderer != null)
//         {
//             rayRenderer.SetPosition(0, ray.origin);
//             rayRenderer.SetPosition(1, ray.origin + ray.direction * rayLength);
//         }
//     }
    
//     void UpdateCrosshair(Ray ray)
//     {
//         // 在射线前方一定距离处绘制十字准星
//         float crosshairDistance = 10f;
//         float crosshairSize = 0.1f;
        
//         Vector3 center = ray.origin + ray.direction * crosshairDistance;
        
//         // 计算垂直于射线方向的平面上的两个轴
//         Vector3 up = Vector3.up;
//         if (Mathf.Abs(Vector3.Dot(ray.direction, up)) > 0.99f)
//         {
//             up = Vector3.right;
//         }
//         Vector3 right = Vector3.Cross(ray.direction, up).normalized;
//         up = Vector3.Cross(right, ray.direction).normalized;
        
//         if (crosshairRendererX != null)
//         {
//             crosshairRendererX.SetPosition(0, center - right * crosshairSize);
//             crosshairRendererX.SetPosition(1, center + right * crosshairSize);
//         }
        
//         if (crosshairRendererY != null)
//         {
//             crosshairRendererY.SetPosition(0, center - up * crosshairSize);
//             crosshairRendererY.SetPosition(1, center + up * crosshairSize);
//         }
//     }
    
//     // 在Scene视图中绘制辅助线
//     void OnDrawGizmos()
//     {
//         if (gameCamera == null) return;
        
//         // 绘制相机视锥体
//         Gizmos.color = Color.blue;
//         Gizmos.DrawLine(gameCamera.transform.position, 
//             gameCamera.transform.position + gameCamera.transform.forward * 5);
        
//         // 绘制屏幕边界（在远裁剪面处）
//         float dist = 10f;
//         Vector3[] corners = new Vector3[4];
//         corners[0] = gameCamera.ViewportToWorldPoint(new Vector3(0, 0, dist));
//         corners[1] = gameCamera.ViewportToWorldPoint(new Vector3(1, 0, dist));
//         corners[2] = gameCamera.ViewportToWorldPoint(new Vector3(1, 1, dist));
//         corners[3] = gameCamera.ViewportToWorldPoint(new Vector3(0, 1, dist));
        
//         Gizmos.color = Color.green;
//         for (int i = 0; i < 4; i++)
//         {
//             Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
//         }
//     }
// }
