using CandyBlast.Cartoon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ProceduralCarton)), CanEditMultipleObjects]
public sealed class ProceduralCartonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying && GUILayout.Button("Close and slide out"))
            foreach (var item in targets)
            {
                var box = (ProceduralCarton)item;
                var exit = box.GetComponent<CartonExitAnimation>();
                if (exit != null) exit.Play();
            }
        EditorGUILayout.HelpBox("Length = X, Width = Z, Height = Y. Openness: 0 closed, 1 open. Use Play Mode for animation. Geometry is regenerated automatically.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open")) Set(true);
            if (GUILayout.Button("Close")) Set(false);
        }
    }
    private void Set(bool open)
    {
        foreach (var item in targets)
        {
            var box = (ProceduralCarton)item;
            Undo.RecordObject(box, "Set carton lid pose");
            if (Application.isPlaying) { if (open) box.Open(); else box.Close(); }
            else { box.SetOpenness(open ? 1f : 0f); EditorUtility.SetDirty(box); }
        }
    }
    [MenuItem("GameObject/3D Object/Procedural Carton", false, 10)]
    private static void Create()
    {
        var go = new GameObject("Procedural Carton");
        Undo.RegisterCreatedObjectUndo(go, "Create carton");
        var box = go.AddComponent<ProceduralCarton>();
        Configure(box);
        Selection.activeGameObject = go;
    }
    private static void Configure(ProceduralCarton box)
    {
        const string folder = "Assets/CartoonCarton/Content";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/CartoonCarton", "Content");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Cardboard.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")) { color = new Color(0.64f, 0.37f, 0.13f) };
            mat.SetFloat("_Glossiness", 0f);
            AssetDatabase.CreateAsset(mat, folder + "/Cardboard.mat");
        }
        var so = new SerializedObject(box);
        so.FindProperty("cardboardMaterial").objectReferenceValue = mat;
        so.ApplyModifiedPropertiesWithoutUndo(); box.Rebuild();
    }
    [MenuItem("Tools/Carton/Create Demo Assets")]
    public static void CreateDemo()
    {
        const string scenePath = "Assets/CartoonCarton/Content/CartonDemo.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
        { Debug.Log("Carton demo already exists: " + scenePath); return; }
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var go = new GameObject("Procedural Carton");
            var box = go.AddComponent<ProceduralCarton>(); Configure(box);
            go.AddComponent<CartonExitAnimation>();
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/CartoonCarton/Content/ProceduralCarton.prefab");
            var so = new SerializedObject(box);
            so.FindProperty("loopDemo").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            var camera = new GameObject("Carton Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(4.5f,4f,-5.5f);
            camera.transform.LookAt(new Vector3(0,1,0));
            camera.orthographic = true; camera.orthographicSize = 3.2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.19f,.25f);
            camera.gameObject.tag = "MainCamera";
            var light = new GameObject("Carton Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45,-35,0);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        Debug.Log("Created carton prefab, material and demo scene in Assets/CartoonCarton/Content.");
    }
}
