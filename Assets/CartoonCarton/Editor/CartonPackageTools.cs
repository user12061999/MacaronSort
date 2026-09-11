using System;
using System.IO;
using CandyBlast.Cartoon;
using CandyBlast.Cartoon.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CartonPackageTools
{
    public const string Root = "Assets/CartoonCarton";
    public const string DemoPath = Root + "/Demo/CartonDeliveryDemo.unity";
    [MenuItem("Tools/Cartoon Carton/Create Delivery Demo")]
    public static void CreateDemo()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoPath) != null) { Debug.Log("Delivery demo already exists."); return; }
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Material cardboard = Material("Soft Cardboard", new Color(.76f,.53f,.28f));
            Material dough = Material("Cake Base", new Color(.87f,.55f,.22f));
            Material cream = Material("Pink Icing", new Color(1f,.32f,.51f));
            Material mint = Material("Mint Icing", new Color(.28f,.85f,.64f));
            Material berry = Material("Cherry", new Color(.72f,.035f,.1f));
            Material trayMat = Material("Tray", new Color(.95f,.83f,.61f));
            Material floorMat = Material("Backdrop", new Color(.20f,.31f,.39f));
            var root = new GameObject("Carton Delivery");
            var boxObject = new GameObject("Carton Visual"); boxObject.transform.SetParent(root.transform, false);
            boxObject.transform.localPosition = new Vector3(1.8f,0f,0f);
            var box = boxObject.AddComponent<ProceduralCarton>();
            var boxSO = new SerializedObject(box);
            boxSO.FindProperty("cardboardMaterial").objectReferenceValue = cardboard;
            boxSO.ApplyModifiedPropertiesWithoutUndo(); box.SetDimensions(2.4f, 1.8f, 1.3f);
            var sequence = root.AddComponent<CartonDeliverySequence>();
            var so = new SerializedObject(sequence);
            so.FindProperty("carton").objectReferenceValue = box; so.ApplyModifiedPropertiesWithoutUndo();
            // Exportable controller prefab has no reference to the demo tray or its cakes.
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Content/CartonDelivery.prefab");
            var tray = new GameObject("Full Cake Tray (demo visuals)");
            tray.transform.position = new Vector3(-2.1f, .08f, 0f);
            Primitive("Tray base", PrimitiveType.Cube, tray.transform, Vector3.zero, new Vector3(2.2f,.16f,2.2f), trayMat);
            Primitive("Tray inset", PrimitiveType.Cube, tray.transform, new Vector3(0,.09f,0), new Vector3(2f,.04f,2f), dough);
            var inputs = so.FindProperty("cakes"); inputs.arraySize = 4;
            for(int i=0;i<4;i++)
            {
                var cake = new GameObject("Cake " + (i+1)); cake.transform.SetParent(tray.transform,false);
                cake.transform.localPosition = new Vector3((i%2-.5f)*.95f,.33f,(i/2-.5f)*.95f);
                Primitive("Sponge",PrimitiveType.Cylinder,cake.transform,Vector3.zero,new Vector3(.68f,.21f,.68f),dough);
                Primitive("Icing",PrimitiveType.Sphere,cake.transform,new Vector3(0,.22f,0),new Vector3(.72f,.29f,.72f),i%2==0?cream:mint);
                Primitive("Cherry",PrimitiveType.Sphere,cake.transform,new Vector3(0,.40f,0),Vector3.one*.18f,berry);
                inputs.GetArrayElementAtIndex(i).objectReferenceValue = cake.transform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var demo = root.AddComponent<CartonDeliveryDemo>();
            var demoSO = new SerializedObject(demo); demoSO.FindProperty("sequence").objectReferenceValue=sequence; demoSO.ApplyModifiedPropertiesWithoutUndo();
            Primitive("Stage",PrimitiveType.Cube,null,new Vector3(0,-.16f,0),new Vector3(40f,.2f,30f),floorMat);
            var camera = new GameObject("Demo Camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(7f,8f,-12f); camera.transform.LookAt(new Vector3(0,1f,0));
            camera.orthographic=true; camera.orthographicSize=5.5f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.14f,.22f,.29f);
            camera.gameObject.tag="MainCamera";
            var light=new GameObject("Demo Key Light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.1f; light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(48f,-32f,0f);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            EditorSceneManager.SaveScene(scene,DemoPath); AssetDatabase.SaveAssets();
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
        Debug.Log("Created standalone carton delivery demo and prefab.");
    }
    private static Material Material(string name,Color color)
    {
        string path=Root+"/Content/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null) return material;
        material=new Material(Shader.Find("Standard")){name=name,color=color}; material.SetFloat("_Glossiness",.12f);
        AssetDatabase.CreateAsset(material,path); return material;
    }
    private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    public static string[] ExternalDependencies()
    {
        var paths=AssetDatabase.FindAssets("",new[]{Root});
        var external=new System.Collections.Generic.HashSet<string>();
        foreach(var guid in paths)
            foreach(var dependency in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid),true))
                if(dependency.StartsWith("Assets/",StringComparison.Ordinal) && !dependency.StartsWith(Root+"/",StringComparison.Ordinal) && dependency!=Root) external.Add(dependency);
        var result=new string[external.Count]; external.CopyTo(result); return result;
    }
    [MenuItem("Tools/Cartoon Carton/Export Unity Package")]
    public static void Export()
    {
        var external=ExternalDependencies();
        if(external.Length!=0) throw new InvalidOperationException("External dependencies: "+string.Join(", ",external));
        string directory=Path.GetFullPath("Exports"); Directory.CreateDirectory(directory);
        AssetDatabase.ExportPackage(Root,Path.Combine(directory,"CartoonCarton.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("Exported Exports/CartoonCarton.unitypackage (isolated assets only).");
    }
}
