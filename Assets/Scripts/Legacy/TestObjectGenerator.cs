using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class TestObjectGenerator : MonoBehaviour
{
    // 此脚本已废弃，功能已移植到PointCloudRenderer中
}

[CustomEditor(typeof(TestObjectGenerator))]
public class TestObjectGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("此脚本已废弃，功能已移植到PointCloudRenderer中", MessageType.Info);
    }
}