using UnityEngine;

/// <summary>
/// 点云选择功能测试脚本
/// </summary>
public class PointCloudSelectionTest : MonoBehaviour
{
    [Header("测试设置")]
    [Tooltip("区域选择器")]
    public PointCloudRegionSelector regionSelector;
    
    [Tooltip("自动运行测试")]
    public bool autoRunTest = false;
    
    [Tooltip("测试延迟（秒）")]
    public float testDelay = 2.0f;
    
    private float testTimer = 0;
    private bool testStarted = false;
    
    void Start()
    {
        if (regionSelector == null)
        {
            regionSelector = FindObjectOfType<PointCloudRegionSelector>();
        }
        
        if (autoRunTest && regionSelector != null)
        {
            Debug.Log("[PointCloudSelectionTest] Auto test enabled, will run in " + testDelay + " seconds");
            testTimer = testDelay;
            testStarted = true;
        }
    }
    
    void Update()
    {
        // 自动测试
        if (testStarted && testTimer > 0)
        {
            testTimer -= Time.deltaTime;
            if (testTimer <= 0)
            {
                RunAutoTest();
                testStarted = false;
            }
        }
        
        // 手动测试快捷键
        if (Input.GetKeyDown(KeyCode.F1))
        {
            TestCreateBox();
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            TestApplyFilter();
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            TestClearFilter();
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            TestPrintBounds();
        }
    }
    
    void RunAutoTest()
    {
        Debug.Log("[PointCloudSelectionTest] Starting auto test...");
        
        // 1. 创建选择框
        TestCreateBox();
        
        // 2. 等待一段时间后应用筛选
        Invoke("TestApplyFilter", 1.0f);
        
        // 3. 等待一段时间后清除筛选
        Invoke("TestClearFilter", 3.0f);
        
        Debug.Log("[PointCloudSelectionTest] Auto test sequence completed");
    }
    
    void TestCreateBox()
    {
        if (regionSelector == null)
        {
            Debug.LogError("[PointCloudSelectionTest] No region selector assigned!");
            return;
        }
        
        regionSelector.CreateSelectionBox();
        Debug.Log("[PointCloudSelectionTest] Selection box created");
    }
    
    void TestApplyFilter()
    {
        if (regionSelector == null)
        {
            Debug.LogError("[PointCloudSelectionTest] No region selector assigned!");
            return;
        }
        
        regionSelector.ApplyRegionFilter();
        Debug.Log("[PointCloudSelectionTest] Region filter applied");
    }
    
    void TestClearFilter()
    {
        if (regionSelector == null)
        {
            Debug.LogError("[PointCloudSelectionTest] No region selector assigned!");
            return;
        }
        
        regionSelector.ClearRegionFilter();
        Debug.Log("[PointCloudSelectionTest] Region filter cleared");
    }
    
    void TestPrintBounds()
    {
        if (regionSelector == null)
        {
            Debug.LogError("[PointCloudSelectionTest] No region selector assigned!");
            return;
        }
        
        Bounds bounds = regionSelector.GetCurrentRegionBounds();
        Debug.Log($"[PointCloudSelectionTest] Current region bounds:");
        Debug.Log($"  Center: {bounds.center}");
        Debug.Log($"  Size: {bounds.size}");
        Debug.Log($"  Min: {bounds.min}");
        Debug.Log($"  Max: {bounds.max}");
    }
}
