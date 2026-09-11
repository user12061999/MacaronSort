using System;
using CandyBlast.Cartoon;
using CandyBlast.Cartoon.Demo;
using UnityEditor;
using UnityEngine;

public static class CartonRuntimeValidation
{
    // Invoke only while the isolated demo is running. No changes are saved.
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in the delivery demo first.");
        var sequence = UnityEngine.Object.FindFirstObjectByType<CartonDeliverySequence>();
        if(sequence == null) throw new InvalidOperationException("Delivery demo not loaded.");
        var demo = UnityEngine.Object.FindFirstObjectByType<CartonDeliveryDemo>();
        if(demo != null) demo.enabled = false;
        sequence.ResetSequence(); var origin = sequence.Carton.transform.localPosition;
        int packed = 0, completed = 0, phase = 0;
        sequence.OnPacked.AddListener(()=>packed++); sequence.OnCompleted.AddListener(()=>completed++);
        sequence.Play(); double began = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction tick = null;
        tick = () => {
            try
            {
                if(!EditorApplication.isPlaying) { EditorApplication.update -= tick; return; }
                if(EditorApplication.timeSinceStartup-began > 20) throw new Exception("Timeout");
                if(phase==0 && sequence.Elapsed > .3f)
                {
                    if(Vector3.Distance(sequence.Carton.transform.localPosition,origin)<.01f) throw new Exception("No arrival motion");
                    sequence.enabled=false;
                    if(sequence.IsPlaying || sequence.Carton.transform.localPosition!=origin) throw new Exception("Disable did not restore");
                    sequence.enabled=true; sequence.Play(); phase=1;
                }
                else if(phase==1 && !sequence.IsPlaying)
                {
                    if(sequence.Carton.Openness!=0f || packed!=1 || completed!=1) throw new Exception("Completion/events failed");
                    SessionState.SetString("CartonRuntimeResult","PASS: real-time arrival, disable/reset, re-enable/replay, full delivery, events exactly once");
                    Debug.Log("CARTON_RUNTIME_PASS"); EditorApplication.update-=tick;
                }
            }
            catch(Exception e) { SessionState.SetString("CartonRuntimeResult","FAIL: "+e.Message); Debug.LogException(e); EditorApplication.update-=tick; }
        };
        SessionState.SetString("CartonRuntimeResult","running"); EditorApplication.update+=tick;
    }
}
