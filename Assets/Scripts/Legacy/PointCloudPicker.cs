using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

[ExecuteInEditMode]
public class PointCloudPicker : MonoBehaviour
{
    [SerializeField] public PointCloudRenderer pointCloudRenderer;
    [SerializeField] public Camera raycastCamera;
    
    // 在Update中检测鼠标点击，只在游戏运行时刻执行
    void Update()
    {
        // 只在游戏运行时刻使用
        if (Application.isPlaying)
        {
            // 检测鼠标点击
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log($"=== PointCloudPicker: Mouse down detected at {Input.mousePosition} ===");
                
                // 尝试自动设置引用
                if (pointCloudRenderer == null)
                {
                    Debug.Log("=== PointCloudPicker: Attempting to find PointCloudRenderer ===");
                    pointCloudRenderer = FindObjectOfType<PointCloudRenderer>();
                    if (pointCloudRenderer != null)
                    {
                        Debug.Log("=== PointCloudPicker: Found PointCloudRenderer ===");
                    }
                    else
                    {
                        Debug.Log("=== PointCloudPicker: ERROR - pointCloudRenderer is null ===");
                        return;
                    }
                }
                
                if (raycastCamera == null)
                {
                    Debug.Log("=== PointCloudPicker: Attempting to find camera ===");
                    // 尝试获取主相机
                    raycastCamera = Camera.main;
                    if (raycastCamera != null)
                    {
                        Debug.Log("=== PointCloudPicker: Found main camera ===");
                    }
                    else
                    {
                        // 尝试获取任何相机
                        raycastCamera = FindObjectOfType<Camera>();
                        if (raycastCamera != null)
                        {
                            Debug.Log("=== PointCloudPicker: Found any camera ===");
                        }
                        else
                        {
                            Debug.Log("=== PointCloudPicker: ERROR - raycastCamera is null ===");
                            return;
                        }
                    }
                }
                
                if (pointCloudRenderer != null && raycastCamera != null)
                {
                    Debug.Log("=== PointCloudPicker: References are set ===");
                    
                    // 输出摄像机信息
                    Debug.Log($"=== PointCloudPicker: Camera position = {raycastCamera.transform.position}, rotation = {raycastCamera.transform.rotation.eulerAngles} ===");
                    
                    // 检查点云数据
                    if (pointCloudRenderer.vertices != null && pointCloudRenderer.vertices.Count > 0)
                    {
                        Debug.Log($"=== PointCloudPicker: Point cloud has {pointCloudRenderer.vertices.Count} vertices ===");
                        Debug.Log($"=== PointCloudPicker: First vertex position = {pointCloudRenderer.vertices[0]} ===");
                        
                        try
                        {
                            // 计算射线
                            Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
                            Debug.Log($"=== PointCloudPicker: Ray origin = {ray.origin}, direction = {ray.direction} ===");
                            
                            // 直接计算射线与点云中所有点的距离，找到最近的点
                            int closestPointIndex = FindClosestPointToRay(ray);
                            Debug.Log($"=== PointCloudPicker: Closest point index = {closestPointIndex} ===");
                            
                            if (closestPointIndex != -1 && pointCloudRenderer.vertices != null && pointCloudRenderer.colors != null && closestPointIndex < pointCloudRenderer.vertices.Count && closestPointIndex < pointCloudRenderer.colors.Count)
                            {
                                // 获取点的坐标（PLY文件中的原始坐标）
                                Vector3 pointCloudCoordinate = pointCloudRenderer.vertices[closestPointIndex];
                                // 由于点云对象的transform已设置为identity，局部坐标就等于世界坐标
                                Vector3 worldCoordinate = pointCloudCoordinate;
                                Color color = pointCloudRenderer.colors[closestPointIndex];
                                
                                // Log信息
                                Debug.Log($"=== Point Cloud Picker: SUCCESS ===");
                                Debug.Log($"=== PLY File Coordinate = ({pointCloudCoordinate.x}, {pointCloudCoordinate.y}, {pointCloudCoordinate.z}) ===");
                                Debug.Log($"=== World Coordinate = ({worldCoordinate.x}, {worldCoordinate.y}, {worldCoordinate.z}) ===");
                                Debug.Log($"=== Color = ({color.r}, {color.g}, {color.b}) ===");
                            }
                            else
                            {
                                Debug.Log("=== PointCloudPicker: No closest point found ===");
                                if (pointCloudRenderer.vertices == null)
                                    Debug.Log("=== PointCloudPicker: vertices is null ===");
                                if (pointCloudRenderer.colors == null)
                                    Debug.Log("=== PointCloudPicker: colors is null ===");
                                if (pointCloudRenderer.vertices != null && closestPointIndex >= pointCloudRenderer.vertices.Count)
                                    Debug.Log($"=== PointCloudPicker: closestPointIndex {closestPointIndex} out of bounds (vertices count: {pointCloudRenderer.vertices.Count}) ===");
                                if (pointCloudRenderer.colors != null && closestPointIndex >= pointCloudRenderer.colors.Count)
                                    Debug.Log($"=== PointCloudPicker: closestPointIndex {closestPointIndex} out of bounds (colors count: {pointCloudRenderer.colors.Count}) ===");
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"=== PointCloudPicker error: {e.Message} ===");
                            Debug.LogError($"=== Stack trace: {e.StackTrace} ===");
                        }
                    }
                    else
                    {
                        Debug.Log("=== PointCloudPicker: Point cloud has no vertices ===");
                        if (pointCloudRenderer.vertices == null)
                            Debug.Log("=== PointCloudPicker: vertices is null ===");
                        else
                            Debug.Log($"=== PointCloudPicker: vertices count = {pointCloudRenderer.vertices.Count} ===");
                    }
                }
                else
                {
                    Debug.Log("=== PointCloudPicker: Missing references ===");
                    Debug.Log($"=== pointCloudRenderer = {pointCloudRenderer} ===");
                    Debug.Log($"=== raycastCamera = {raycastCamera} ===");
                }
            }
        }
    }
    
    int FindClosestPoint(Vector3 hitPoint)
    {
        if (pointCloudRenderer == null || pointCloudRenderer.vertices == null || pointCloudRenderer.vertices.Count == 0)
            return -1;
        
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < pointCloudRenderer.vertices.Count; i++)
        {
            float distance = Vector3.Distance(hitPoint, pointCloudRenderer.vertices[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        
        // 只返回距离足够近的点
        if (closestDistance < 0.1f)
            return closestIndex;
        else
            return -1;
    }
    
    // 找到射线附近最近的点
    int FindClosestPointToRay(Ray ray)
    {
        if (pointCloudRenderer == null || pointCloudRenderer.vertices == null || pointCloudRenderer.vertices.Count == 0)
        {
            Debug.Log("=== FindClosestPointToRay: No point cloud data ===");
            return -1;
        }
        
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        Vector3 closestPoint = Vector3.zero;
        
        for (int i = 0; i < pointCloudRenderer.vertices.Count; i++)
        {
            // 计算点到射线的距离
            Vector3 point = pointCloudRenderer.vertices[i];
            Vector3 toPoint = point - ray.origin;
            float distance = Vector3.Cross(ray.direction, toPoint).magnitude;
            
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
                closestPoint = point;
            }
        }
        
        Debug.Log($"=== FindClosestPointToRay: Closest distance = {closestDistance}, closest point = {closestPoint} ===");
        
        // 只返回距离足够近的点
        if (closestDistance < 1.0f) // 进一步增加距离阈值
        {
            Debug.Log($"=== FindClosestPointToRay: Returning closest point index {closestIndex} ===");
            return closestIndex;
        }
        else
        {
            Debug.Log("=== FindClosestPointToRay: Distance too far, returning -1 ===");
            return -1;
        }
    }
    
    // 在编辑器中选择点云渲染器
    [ContextMenu("Select Point Cloud Renderer")]
    public void SelectPointCloudRenderer()
    {
        pointCloudRenderer = GetComponent<PointCloudRenderer>();
        if (pointCloudRenderer == null)
        {
            pointCloudRenderer = FindObjectOfType<PointCloudRenderer>();
            if (pointCloudRenderer == null)
            {
                Debug.LogError("No PointCloudRenderer found in the scene.");
            }
            else
            {
                // 确保点云对象有碰撞器
                EnsureColliderExists();
            }
        }
        else
        {
            // 确保点云对象有碰撞器
            EnsureColliderExists();
        }
    }
    
    // 确保点云对象有碰撞器 - 已禁用，使用精确射线检测代替
    public void EnsureColliderExists()
    {
        // 不再添加BoxCollider，使用精确射线检测算法
        // 这样可以避免碰撞体不贴合点云形状导致的偏移问题
        Debug.Log("=== PointCloudPicker: Using precise ray-point detection, no collider needed ===");
    }
    
    // 在编辑器中选择相机
    [ContextMenu("Select Raycast Camera")]
    public void SelectRaycastCamera()
    {
        raycastCamera = Camera.main;
        if (raycastCamera == null)
        {
            raycastCamera = FindObjectOfType<Camera>();
            if (raycastCamera == null)
            {
                Debug.LogError("No camera found in the scene.");
            }
        }
    }
}

