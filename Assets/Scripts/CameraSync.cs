using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class CameraSync : MonoBehaviour
{
    [SerializeField] public Camera gameCamera;
    
    void Update()
    {
        if (gameCamera != null && Application.isEditor)
        {
            // 获取Scene视图的相机信息
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                // 同步位置和旋转
                gameCamera.transform.position = sceneView.camera.transform.position;
                gameCamera.transform.rotation = sceneView.camera.transform.rotation;
                
                // 同步相机参数
                gameCamera.fieldOfView = sceneView.camera.fieldOfView;
                gameCamera.nearClipPlane = sceneView.camera.nearClipPlane;
                gameCamera.farClipPlane = sceneView.camera.farClipPlane;
            }
        }
    }
    
    // 在编辑器中选择游戏相机
    [ContextMenu("Select Game Camera")]
    public void SelectGameCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            Debug.LogError("No main camera found in the scene.");
        }
    }
}

[CustomEditor(typeof(CameraSync))]
public class CameraSyncEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CameraSync sync = (CameraSync)target;
        
        // 显示游戏相机引用
        sync.gameCamera = (Camera)EditorGUILayout.ObjectField("Game Camera", sync.gameCamera, typeof(Camera), true);
        
        // 添加按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Select Game Camera"))
        {
            sync.SelectGameCamera();
        }
        
        // 应用更改
        if (GUI.changed)
        {
            EditorUtility.SetDirty(sync);
        }
    }
}