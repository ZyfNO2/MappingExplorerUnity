using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 点云区域选择器 - 主控制器，管理选择框和筛选操作
/// </summary>
public class PointCloudRegionSelector : MonoBehaviour
{
    [Header("Selection Box")]
    [Tooltip("选择框预制体")]
    public GameObject selectionBoxPrefab;
    
    [Tooltip("是否自动创建选择框")]
    public bool autoCreateBox = true;
    
    [Tooltip("选择框初始大小（相对于点云边界）")]
    public float initialBoxScale = 0.3f;
    
    [Header("Point Cloud")]
    [Tooltip("关联的点云管理器")]
    public PointCloudManager pointCloudManager;
    
    [Tooltip("应用筛选时是否隐藏选择框")]
    public bool hideBoxOnApply = false;
    
    [Tooltip("筛选过渡时间（秒）")]
    public float filterTransitionTime = 0.1f;
    
    // 私有字段
    private GameObject selectionBox;
    public SelectionBoxController boxController;
    public bool isFiltering = false;
    
    void Start()
    {
        if (autoCreateBox && selectionBox == null)
        {
            CreateSelectionBox();
        }
    }
    
    void Update()
    {
        // 实时预览模式（可选）
        if (boxController != null && Input.GetKey(KeyCode.LeftShift))
        {
            UpdateRealTimePreview();
        }
    }
    
    #region Public Methods
    
    /// <summary>
    /// 创建选择框
    /// </summary>
    public void CreateSelectionBox()
    {
        if (selectionBox != null)
        {
            Debug.LogWarning("[PointCloudRegionSelector] Selection box already exists");
            return;
        }
        
        if (pointCloudManager == null)
        {
            pointCloudManager = FindObjectOfType<PointCloudManager>();
            if (pointCloudManager == null)
            {
                Debug.LogError("[PointCloudRegionSelector] No PointCloudManager found!");
                return;
            }
        }
        
        // 创建或加载预制体
        if (selectionBoxPrefab != null)
        {
            selectionBox = Instantiate(selectionBoxPrefab, transform);
        }
        else
        {
            selectionBox = CreateDefaultSelectionBox();
        }
        
        selectionBox.name = "PointCloudSelectionBox";
        boxController = selectionBox.GetComponent<SelectionBoxController>();
        
        if (boxController == null)
        {
            boxController = selectionBox.AddComponent<SelectionBoxController>();
        }
        
        // 自动适配到点云大小
        boxController.FitToPointCloud(pointCloudManager);
        
        Debug.Log("[PointCloudRegionSelector] Selection box created");
    }
    
    /// <summary>
    /// 删除选择框
    /// </summary>
    public void DestroySelectionBox()
    {
        if (selectionBox != null)
        {
            DestroyImmediate(selectionBox);
            selectionBox = null;
            boxController = null;
            Debug.Log("[PointCloudRegionSelector] Selection box destroyed");
        }
    }
    
    /// <summary>
    /// 应用区域筛选
    /// </summary>
    public void ApplyRegionFilter()
    {
        if (isFiltering) return;
        
        if (boxController == null)
        {
            Debug.LogError("[PointCloudRegionSelector] No selection box available");
            return;
        }
        
        if (pointCloudManager == null)
        {
            pointCloudManager = FindObjectOfType<PointCloudManager>();
            if (pointCloudManager == null)
            {
                Debug.LogError("[PointCloudRegionSelector] No PointCloudManager found!");
                return;
            }
        }
        
        isFiltering = true;
        
        // 获取选择框边界
        Bounds region = boxController.GetWorldBounds();
        
        // 应用精确筛选（只显示边界内的点）
        pointCloudManager.FilterByBoundsPrecise(region);
        
        // 可选：隐藏选择框
        if (hideBoxOnApply && selectionBox != null)
        {
            selectionBox.SetActive(false);
        }
        
        isFiltering = false;
        
        Debug.Log($"[PointCloudRegionSelector] Precise region filter applied: {region}");
    }
    
    /// <summary>
    /// 清除筛选
    /// </summary>
    public void ClearRegionFilter()
    {
        if (pointCloudManager == null)
        {
            pointCloudManager = FindObjectOfType<PointCloudManager>();
        }
        
        if (pointCloudManager != null)
        {
            pointCloudManager.ClearFilter();
        }
        
        // 恢复显示选择框
        if (selectionBox != null && hideBoxOnApply)
        {
            selectionBox.SetActive(true);
        }
        
        Debug.Log("[PointCloudRegionSelector] Region filter cleared");
    }
    
    /// <summary>
    /// 重置选择框到初始状态
    /// </summary>
    public void ResetSelectionBox()
    {
        if (boxController != null)
        {
            boxController.FitToPointCloud(pointCloudManager);
            Debug.Log("[PointCloudRegionSelector] Selection box reset");
        }
    }
    
    /// <summary>
    /// 获取当前选择框的边界
    /// </summary>
    public Bounds GetCurrentRegionBounds()
    {
        if (boxController != null)
        {
            return boxController.GetWorldBounds();
        }
        
        return new Bounds(Vector3.zero, Vector3.zero);
    }
    
    /// <summary>
    /// 设置选择框边界
    /// </summary>
    public void SetRegionBounds(Bounds bounds)
    {
        if (boxController != null)
        {
            boxController.SetBounds(bounds);
        }
    }
    
    /// <summary>
    /// 实时预览筛选效果
    /// </summary>
    public void UpdateRealTimePreview()
    {
        if (boxController != null && pointCloudManager != null)
        {
            Bounds region = boxController.GetWorldBounds();
            pointCloudManager.FilterByBounds(region);
        }
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// 创建默认选择框
    /// </summary>
    private GameObject CreateDefaultSelectionBox()
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        // 移除碰撞器（我们会添加自定义的）
        Collider col = box.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        
        // 设置材质 - 使用URP兼容的透明shader
        MeshRenderer renderer = box.GetComponent<MeshRenderer>();
        Material mat = null;
        
        // 尝试使用URP Lit shader（透明模式）
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader != null)
        {
            mat = new Material(urpLitShader);
            // 设置透明表面类型
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.color = new Color(0.3f, 0.6f, 1.0f, 0.3f);
        }
        else
        {
            // 降级到Standard shader（Built-in）
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                mat = new Material(standardShader);
                mat.SetFloat("_Mode", 3); // 3 = Transparent mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.6f, 1.0f, 0.3f);
            }
            else
            {
                // 最后的降级方案
                Debug.LogWarning("[PointCloudRegionSelector] URP Lit and Standard shaders not found. Using fallback.");
                mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = new Color(0.3f, 0.6f, 1.0f, 0.3f);
            }
        }
        
        renderer.material = mat;
        
        // 添加BoxCollider用于交互
        BoxCollider boxCol = box.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        
        return box;
    }
    
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(PointCloudRegionSelector))]
public class PointCloudRegionSelectorEditor : Editor
{
    private PointCloudRegionSelector selector;
    
    void OnEnable()
    {
        selector = (PointCloudRegionSelector)target;
    }
    
    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Point Cloud Region Selector", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // 选择框设置
        EditorGUILayout.LabelField("Selection Box", EditorStyles.boldLabel);
        selector.selectionBoxPrefab = (GameObject)EditorGUILayout.ObjectField("Selection Box Prefab", selector.selectionBoxPrefab, typeof(GameObject), false);
        selector.autoCreateBox = EditorGUILayout.Toggle("Auto Create Box", selector.autoCreateBox);
        selector.initialBoxScale = EditorGUILayout.Slider("Initial Box Scale", selector.initialBoxScale, 0.1f, 1.0f);
        
        EditorGUILayout.Space();
        
        // 点云设置
        EditorGUILayout.LabelField("Point Cloud", EditorStyles.boldLabel);
        selector.pointCloudManager = (PointCloudManager)EditorGUILayout.ObjectField("Point Cloud Manager", selector.pointCloudManager, typeof(PointCloudManager), true);
        selector.hideBoxOnApply = EditorGUILayout.Toggle("Hide Box On Apply", selector.hideBoxOnApply);
        
        EditorGUILayout.Space();
        
        // 状态显示
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        if (selector.GetCurrentRegionBounds().size != Vector3.zero)
        {
            Bounds bounds = selector.GetCurrentRegionBounds();
            EditorGUILayout.LabelField($"Region Center: {bounds.center:F2}");
            EditorGUILayout.LabelField($"Region Size: {bounds.size:F2}");
        }
        else
        {
            EditorGUILayout.LabelField("No selection box active");
        }
        
        EditorGUILayout.Space();
        
        // 控制按钮
        GUI.enabled = !Application.isPlaying || selector.boxController == null;
        if (GUILayout.Button("Create Selection Box", GUILayout.Height(30)))
        {
            selector.CreateSelectionBox();
        }
        GUI.enabled = selector.boxController != null;
        
        EditorGUILayout.Space();
        
        // 操作按钮
        EditorGUILayout.LabelField("Operations", EditorStyles.boldLabel);
        
        GUI.enabled = selector.boxController != null && !selector.isFiltering;
        if (GUILayout.Button("Apply Region Filter", GUILayout.Height(35)))
        {
            selector.ApplyRegionFilter();
        }
        
        GUI.enabled = selector.pointCloudManager != null;
        if (GUILayout.Button("Clear Filter", GUILayout.Height(35)))
        {
            selector.ClearRegionFilter();
        }
        
        GUI.enabled = selector.boxController != null;
        if (GUILayout.Button("Reset Box", GUILayout.Height(30)))
        {
            selector.ResetSelectionBox();
        }
        
        if (GUILayout.Button("Destroy Box", GUILayout.Height(30)))
        {
            selector.DestroySelectionBox();
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // 帮助信息
        EditorGUILayout.LabelField("Help", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Click 'Create Selection Box' to create a selection box\n" +
            "2. Drag the box to position it over desired region\n" +
            "3. Use yellow handles to resize the box\n" +
            "4. Click 'Apply Region Filter' to filter point cloud\n" +
            "5. Click 'Clear Filter' to restore all points",
            MessageType.Info
        );
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(selector);
        }
    }
    
    void OnSceneGUI()
    {
        // 在Scene视图中绘制选择框信息
        if (selector.boxController != null)
        {
            Bounds bounds = selector.GetCurrentRegionBounds();
            
            Handles.color = Color.cyan;
            Handles.DrawWireCube(bounds.center, bounds.size);
            
            Handles.Label(bounds.center + Vector3.up * bounds.extents.y, "Selection Region");
        }
    }
}
#endif
