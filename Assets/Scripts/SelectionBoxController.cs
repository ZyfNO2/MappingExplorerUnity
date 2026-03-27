using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 选择框控制器 - 支持拖拽和调整大小的半透明立方体
/// </summary>
public class SelectionBoxController : MonoBehaviour
{
    [Header("Appearance")]
    [Tooltip("立方体颜色")]
    public Color boxColor = new Color(0.3f, 0.6f, 1.0f, 0.3f);
    
    [Tooltip("边框颜色")]
    public Color wireColor = new Color(0.5f, 0.8f, 1.0f, 0.8f);
    
    [Tooltip("是否显示边框")]
    public bool showWireframe = true;
    
    [Header("Interaction")]
    [Tooltip("调整大小的手柄大小")]
    public float handleSize = 0.1f;
    
    [Tooltip("是否启用拖拽")]
    public bool enableDragging = true;
    
    [Tooltip("是否启用缩放")]
    public bool enableScaling = true;
    
    [Header("Snapping")]
    [Tooltip("是否对齐到网格")]
    public bool snapToGrid = false;
    
    [Tooltip("网格大小")]
    public float gridSize = 0.1f;
    
    // 私有字段
    private BoxCollider boxCollider;
    private Vector3 dragStartPos;
    private Vector3 dragOffset;
    private bool isDragging = false;
    private Camera sceneCamera;
    
    // 缩放控制
    private enum ResizeHandle
    {
        None,
        MinX, MaxX,
        MinY, MaxY,
        MinZ, MaxZ
    }
    private ResizeHandle activeHandle = ResizeHandle.None;
    private Vector3 resizeStartCenter;
    private Vector3 resizeStartSize;
    
    void Start()
    {
        SetupComponents();
    }
    
    void SetupComponents()
    {
        // 添加BoxCollider用于交互
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }
        boxCollider.isTrigger = true;
        
        // 获取场景相机（用于拖拽）
        if (Camera.main != null)
        {
            sceneCamera = Camera.main;
        }
        
        // 确保材质存在
        UpdateMaterial();
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (!enableDragging && !enableScaling) return;
        
        // 在编辑器模式下也支持
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif
        
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (boxCollider.Raycast(ray, out hit, Mathf.Infinity))
            {
                // 检查是否点击了缩放手柄
                if (enableScaling && TryGetResizeHandle(ray, hit.point, out activeHandle))
                {
                    resizeStartCenter = transform.position;
                    resizeStartSize = transform.localScale;
                }
                // 否则开始拖拽
                else if (enableDragging)
                {
                    isDragging = true;
                    dragStartPos = transform.position;
                    dragOffset = hit.point - transform.position;
                }
            }
        }
        
        if (Input.GetMouseButton(0))
        {
            if (isDragging)
            {
                DragObject();
            }
            else if (activeHandle != ResizeHandle.None)
            {
                ResizeObject();
            }
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            activeHandle = ResizeHandle.None;
        }
    }
    
    bool TryGetResizeHandle(Ray ray, Vector3 hitPoint, out ResizeHandle handle)
    {
        handle = ResizeHandle.None;
        
        // 计算每个面的中心位置
        Bounds bounds = boxCollider.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;
        
        // 简化的手柄检测（基于距离）
        float minDist = handleSize * 2;
        
        // 检查6个面的中心点
        Vector3[] handlePositions = new Vector3[]
        {
            center - Vector3.right * size.x / 2,  // MinX
            center + Vector3.right * size.x / 2,  // MaxX
            center - Vector3.up * size.y / 2,     // MinY
            center + Vector3.up * size.y / 2,     // MaxY
            center - Vector3.forward * size.z / 2, // MinZ
            center + Vector3.forward * size.z / 2  // MaxZ
        };
        
        for (int i = 0; i < handlePositions.Length; i++)
        {
            float dist = Vector3.Distance(hitPoint, handlePositions[i]);
            if (dist < minDist)
            {
                minDist = dist;
                handle = (ResizeHandle)(i + 1); // +1 because None = 0
            }
        }
        
        return handle != ResizeHandle.None;
    }
    
    void DragObject()
    {
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        Plane dragPlane = new Plane(sceneCamera.transform.forward, transform.position);
        float enter;
        
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 targetPos = ray.GetPoint(enter) - dragOffset;
            
            // 对齐到网格
            if (snapToGrid && gridSize > 0)
            {
                targetPos = SnapToGrid(targetPos);
            }
            
            transform.position = targetPos;
        }
    }
    
    void ResizeObject()
    {
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        Plane resizePlane = new Plane(sceneCamera.transform.forward, resizeStartCenter);
        float enter;
        
        if (resizePlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 offset = hitPoint - resizeStartCenter;
            
            Vector3 newSize = resizeStartSize;
            Vector3 newCenter = resizeStartCenter;
            
            // 根据激活的手柄调整大小
            switch (activeHandle)
            {
                case ResizeHandle.MinX:
                    newSize.x = resizeStartSize.x - offset.x;
                    newCenter.x = resizeStartCenter.x + offset.x / 2;
                    break;
                case ResizeHandle.MaxX:
                    newSize.x = resizeStartSize.x + offset.x;
                    newCenter.x = resizeStartCenter.x + offset.x / 2;
                    break;
                case ResizeHandle.MinY:
                    newSize.y = resizeStartSize.y - offset.y;
                    newCenter.y = resizeStartCenter.y + offset.y / 2;
                    break;
                case ResizeHandle.MaxY:
                    newSize.y = resizeStartSize.y + offset.y;
                    newCenter.y = resizeStartCenter.y + offset.y / 2;
                    break;
                case ResizeHandle.MinZ:
                    newSize.z = resizeStartSize.z - offset.z;
                    newCenter.z = resizeStartCenter.z + offset.z / 2;
                    break;
                case ResizeHandle.MaxZ:
                    newSize.z = resizeStartSize.z + offset.z;
                    newCenter.z = resizeStartCenter.z + offset.z / 2;
                    break;
            }
            
            // 确保最小尺寸
            newSize = Vector3.Max(newSize, Vector3.one * handleSize);
            
            // 对齐到网格
            if (snapToGrid && gridSize > 0)
            {
                newSize = SnapToGrid(newSize);
                newCenter = SnapToGrid(newCenter);
            }
            
            transform.localScale = newSize;
            transform.position = newCenter;
        }
    }
    
    Vector3 SnapToGrid(Vector3 value)
    {
        return new Vector3(
            Mathf.Round(value.x / gridSize) * gridSize,
            Mathf.Round(value.y / gridSize) * gridSize,
            Mathf.Round(value.z / gridSize) * gridSize
        );
    }
    
    public void UpdateMaterial()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        // 使用透明材质
        Material mat = renderer.material;
        if (mat == null || mat.shader.name != "Transparent/Diffuse")
        {
            mat = new Material(Shader.Find("Transparent/Diffuse"));
            renderer.material = mat;
        }
        
        mat.color = boxColor;
    }
    
    void OnDrawGizmos()
    {
        if (!showWireframe) return;
        
        Gizmos.color = wireColor;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        
        // 绘制线框立方体
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        
        // 绘制缩放手柄
        if (enableScaling && Selection.activeGameObject == gameObject)
        {
            Gizmos.color = Color.yellow;
            float size = handleSize / transform.lossyScale.magnitude;
            
            // 8个角的手柄
            Vector3[] corners = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f)
            };
            
            foreach (var corner in corners)
            {
                Gizmos.DrawSphere(corner, size);
            }
        }
        
        Gizmos.matrix = oldMatrix;
    }
    
    #region Public Methods
    
    /// <summary>
    /// 获取选择框的世界空间边界
    /// </summary>
    public Bounds GetWorldBounds()
    {
        Bounds localBounds = new Bounds(Vector3.zero, transform.localScale);
        return TransformBounds(localBounds, transform.localToWorldMatrix);
    }
    
    /// <summary>
    /// 设置选择框的大小和位置
    /// </summary>
    public void SetBounds(Bounds bounds)
    {
        transform.position = bounds.center;
        transform.localScale = bounds.size;
    }
    
    /// <summary>
    /// 从点云数据设置合适的初始大小
    /// </summary>
    public void FitToPointCloud(PointCloudManager pointCloudManager)
    {
        var pointData = pointCloudManager.GetPointCloudData();
        if (pointData == null || pointData.Count == 0) return;
        
        Vector3 min = pointData[0].position;
        Vector3 max = pointData[0].position;
        
        foreach (var point in pointData)
        {
            min = Vector3.Min(min, point.position);
            max = Vector3.Max(max, point.position);
        }
        
        Vector3 center = (min + max) / 2;
        Vector3 size = max - min;
        
        // 设置初始大小为点云范围的50%
        SetBounds(new Bounds(center, size * 0.5f));
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// 变换边界框
    /// </summary>
    private Bounds TransformBounds(Bounds localBounds, Matrix4x4 transformMatrix)
    {
        Vector3 center = transformMatrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 worldExtents = new Vector3(
            Mathf.Abs(transformMatrix.m00) * extents.x + Mathf.Abs(transformMatrix.m01) * extents.y + Mathf.Abs(transformMatrix.m02) * extents.z,
            Mathf.Abs(transformMatrix.m10) * extents.x + Mathf.Abs(transformMatrix.m11) * extents.y + Mathf.Abs(transformMatrix.m12) * extents.z,
            Mathf.Abs(transformMatrix.m20) * extents.x + Mathf.Abs(transformMatrix.m21) * extents.y + Mathf.Abs(transformMatrix.m22) * extents.z
        );
        
        return new Bounds(center, worldExtents * 2);
    }
    
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(SelectionBoxController))]
public class SelectionBoxControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SelectionBoxController controller = (SelectionBoxController)target;
        
        EditorGUILayout.LabelField("Selection Box Controller", EditorStyles.boldLabel);
        
        // 外观设置
        EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
        controller.boxColor = EditorGUILayout.ColorField("Box Color", controller.boxColor);
        controller.wireColor = EditorGUILayout.ColorField("Wire Color", controller.wireColor);
        controller.showWireframe = EditorGUILayout.Toggle("Show Wireframe", controller.showWireframe);
        
        EditorGUILayout.Space();
        
        // 交互设置
        EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
        controller.enableDragging = EditorGUILayout.Toggle("Enable Dragging", controller.enableDragging);
        controller.enableScaling = EditorGUILayout.Toggle("Enable Scaling", controller.enableScaling);
        controller.handleSize = EditorGUILayout.FloatField("Handle Size", controller.handleSize);
        
        EditorGUILayout.Space();
        
        // 对齐设置
        EditorGUILayout.LabelField("Snapping", EditorStyles.boldLabel);
        controller.snapToGrid = EditorGUILayout.Toggle("Snap to Grid", controller.snapToGrid);
        controller.gridSize = EditorGUILayout.FloatField("Grid Size", controller.gridSize);
        
        EditorGUILayout.Space();
        
        // 当前状态
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        Bounds bounds = controller.GetWorldBounds();
        EditorGUILayout.LabelField($"Position: {bounds.center}");
        EditorGUILayout.LabelField($"Size: {bounds.size}");
        
        EditorGUILayout.Space();
        
        // 快捷按钮
        if (GUILayout.Button("Apply Selection", GUILayout.Height(30)))
        {
            ApplySelection(controller);
        }
        
        if (GUILayout.Button("Clear Selection", GUILayout.Height(30)))
        {
            ClearSelection(controller);
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(controller);
            controller.UpdateMaterial();
        }
    }
    
    void ApplySelection(SelectionBoxController controller)
    {
        PointCloudManager pointCloudManager = FindObjectOfType<PointCloudManager>();
        if (pointCloudManager == null)
        {
            EditorUtility.DisplayDialog("Error", "No PointCloudManager found in scene!", "OK");
            return;
        }
        
        Bounds selectionBounds = controller.GetWorldBounds();
        pointCloudManager.FilterByBoundsPrecise(selectionBounds);
        
        Debug.Log($"[SelectionBoxController] Applied precise selection: {selectionBounds}");
    }
    
    void ClearSelection(SelectionBoxController controller)
    {
        PointCloudManager pointCloudManager = FindObjectOfType<PointCloudManager>();
        if (pointCloudManager == null)
        {
            EditorUtility.DisplayDialog("Error", "No PointCloudManager found in scene!", "OK");
            return;
        }
        
        pointCloudManager.ClearFilter();
        Debug.Log("[SelectionBoxController] Cleared selection");
    }
}
#endif
