using System;
using System.IO;
using CandyBlast.Cartoon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CartonDeliveryValidation
{
    [MenuItem("Tools/Cartoon Carton/Validate Delivery")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(CartonPackageTools.DemoPath, OpenSceneMode.Additive);
        int checks = 0;
        try
        {
            CartonDeliverySequence sequence = null; Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if(root.GetComponent<CartonDeliverySequence>() != null) sequence = root.GetComponent<CartonDeliverySequence>();
                if(root.GetComponent<Camera>() != null) camera = root.GetComponent<Camera>();
                foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            }
            if(sequence == null || camera == null) throw new Exception("Demo references missing.");
            camera.cullingMask = 1 << 30;
            var so = new SerializedObject(sequence);
            var cakeArray = so.FindProperty("cakes");
            var cakes = new Transform[cakeArray.arraySize];
            var positions = new Vector3[cakes.Length]; var scales = new Vector3[cakes.Length];
            for(int i=0;i<cakes.Length;i++) { cakes[i]=(Transform)cakeArray.GetArrayElementAtIndex(i).objectReferenceValue; positions[i]=cakes[i].position; scales[i]=cakes[i].localScale; }
            Vector3 initialBox = sequence.Carton.transform.localPosition;
            int packed = 0, completed = 0;
            sequence.OnPacked.AddListener(()=>packed++); sequence.OnCompleted.AddListener(()=>completed++);
            sequence.Play();
            Check(sequence.IsPlaying,"Play starts",ref checks);
            sequence.Advance(sequence.LoadStart);
            Check(Vector3.Distance(sequence.Carton.transform.localPosition,initialBox)<.001f,"Arrival reaches dock",ref checks);
            for(int i=0;i<cakes.Length;i++) Check(Vector3.Distance(cakes[i].position,positions[i])<.001f,"Cakes wait for arrival",ref checks);
            sequence.Advance(sequence.LoadEnd-sequence.Elapsed);
            foreach(var cake in cakes)
            {
                Vector3 local=sequence.Carton.transform.InverseTransformPoint(cake.position);
                Vector3 dim=sequence.Carton.Dimensions;
                Check(Mathf.Abs(local.x)<dim.x*.5f && Mathf.Abs(local.z)<dim.z*.5f && local.y>0f && local.y<dim.y,"Packed cake inside carton",ref checks);
            }
            sequence.Advance(sequence.TotalDuration);
            Check(!sequence.IsPlaying && sequence.Carton.Openness==0f,"Sequence closes and finishes",ref checks);
            Check(packed==1 && completed==1,"Events once",ref checks);
            sequence.Advance(100f); Check(packed==1 && completed==1,"No repeated completion",ref checks);
            sequence.ResetSequence();
            Check(sequence.Carton.transform.localPosition==initialBox,"Reset carton",ref checks);
            for(int i=0;i<cakes.Length;i++) Check(Vector3.Distance(cakes[i].position,positions[i])<.001f && cakes[i].localScale==scales[i],"Reset cake",ref checks);
            sequence.Play(); sequence.Advance(.3f); sequence.Play();
            Check(sequence.Elapsed==0f && sequence.IsPlaying,"Restart during arrival",ref checks);
            sequence.ResetSequence();
            sequence.Play(); sequence.Advance(2f); Vector3 oneStep = cakes[0].position; sequence.ResetSequence();
            sequence.Play(); for(int i=0;i<120;i++) sequence.Advance(1f/60f);
            Check(Vector3.Distance(cakes[0].position,oneStep)<.002f,"Independent of frame steps",ref checks); sequence.ResetSequence();
            // Nulls and duplicates are accepted, no cake object is destroyed or reparented.
            sequence.PlayAt(sequence.transform.TransformPoint(initialBox),new[]{cakes[0],null,cakes[0]}); sequence.Advance(100f);
            Check(!sequence.IsPlaying,"Null and duplicate inputs",ref checks); sequence.ResetSequence();
            sequence.PlayAt(sequence.transform.TransformPoint(initialBox),Array.Empty<Transform>()); sequence.Advance(100f);
            Check(!sequence.IsPlaying,"Empty tray sequence",ref checks); sequence.ResetSequence();
            sequence.PlayAt(sequence.transform.TransformPoint(initialBox),cakes); sequence.ResetSequence();
            foreach(var dimensions in new[]{new Vector3(3f,1f,2f),new Vector3(1f,2f,1f)})
            {
                sequence.Carton.SetDimensions(dimensions.x,dimensions.z,dimensions.y); sequence.Play(); sequence.Advance(100f);
                Check(sequence.Carton.Openness==0f && !sequence.IsPlaying,"Dimension change sequence",ref checks); sequence.ResetSequence();
            }
            sequence.Carton.SetDimensions(2.4f,1.8f,1.3f);
            string directory=Path.GetFullPath(CartonPackageTools.Root+"/Documentation/Previews"); Directory.CreateDirectory(directory);
            float[] times={.27f,sequence.LoadStart,sequence.LoadStart+.38f,sequence.PackEnd,sequence.DepartureStart+.34f};
            for(int i=0;i<times.Length;i++) { sequence.SampleTime(times[i]); Capture(camera,Path.Combine(directory,"delivery-"+i+".png")); }
            sequence.ResetSequence();
            Check(CartonPackageTools.ExternalDependencies().Length==0,"No external project dependencies",ref checks);
            Debug.Log("CARTON_VALIDATION_PASS: "+checks+" checks; 5 rendered previews.");
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }
    private static void Check(bool pass,string message,ref int checks)
    { if(!pass) throw new Exception("Carton validation: "+message); checks++; }
    private static void Capture(Camera camera,string path)
    {
        var texture=new RenderTexture(1100,760,24); texture.antiAliasing=4;
        var oldActive=RenderTexture.active; var oldTarget=camera.targetTexture;
        var image=new Texture2D(1100,760,TextureFormat.RGB24,false);
        try { camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture;
            image.ReadPixels(new Rect(0,0,1100,760),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
        finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
