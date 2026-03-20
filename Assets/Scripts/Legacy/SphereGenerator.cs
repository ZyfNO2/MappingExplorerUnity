using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class SphereGenerator : MonoBehaviour
{
    public float x = 0.0091f;
    public float y = 0.0080f;
    public float z = -1.1869f;
    public float size = 0.08f;
    public Material sphereMaterial;
    
    private GameObject sphereObject;
    
    public void GenerateSphere()
    {
        if (sphereObject == null)
        {
            CreateSphere();
        }
        else
        {
            UpdateSphere();
        }
    }
    
    void CreateSphere()
    {
        // 创建球形对象
        sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObject.name = "GeneratedSphere";
        sphereObject.transform.parent = transform;
        
        // 设置默认材质
        if (sphereMaterial == null)
        {
            // 创建默认的Unlit材质
            sphereMaterial = new Material(Shader.Find("Unlit/Color"));
            sphereMaterial.color = Color.white;
        }
        sphereObject.GetComponent<Renderer>().material = sphereMaterial;
        
        // 设置位置和大小
        UpdateSphere();
    }
    
    void UpdateSphere()
    {
        if (sphereObject != null)
        {
            // 更新位置
            sphereObject.transform.localPosition = new Vector3(x, y, z);
            
            // 更新大小
            sphereObject.transform.localScale = new Vector3(size, size, size);
        }
    }
    
    // 重置功能
    [ContextMenu("Reset Sphere")]
    public void ResetSphere()
    {
        if (sphereObject != null)
        {
            DestroyImmediate(sphereObject);
            sphereObject = null;
        }
    }
}

[CustomEditor(typeof(SphereGenerator))]
public class SphereGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SphereGenerator generator = (SphereGenerator)target;
        
        // 显示位置输入
        EditorGUILayout.LabelField("Position:");
        generator.x = EditorGUILayout.FloatField("X", generator.x);
        generator.y = EditorGUILayout.FloatField("Y", generator.y);
        generator.z = EditorGUILayout.FloatField("Z", generator.z);
        
        // 显示大小输入
        generator.size = EditorGUILayout.FloatField("Size", generator.size);
        
        // 显示材质输入
        generator.sphereMaterial = (Material)EditorGUILayout.ObjectField("Material", generator.sphereMaterial, typeof(Material), false);
        
        // 添加生成按钮
        if (GUILayout.Button("Generate Sphere"))
        {
            generator.GenerateSphere();
        }
        
        // 应用更改
        if (GUI.changed)
        {
            EditorUtility.SetDirty(generator);
        }
    }
}
