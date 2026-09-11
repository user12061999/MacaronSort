using CandyBlast.Cartoon;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CartonDeliverySequence))]
public sealed class CartonDeliverySequenceEditor : Editor
{
    private float preview;
    private bool previewing;
    public override void OnInspectorGUI()
    {
        var sequence = (CartonDeliverySequence)target;
        // Restore temporary transforms before serializing edited configuration.
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && previewing) { sequence.ResetSequence(); previewing = false; }
        EditorGUILayout.Space();
        if (Application.isPlaying)
        {
            if (GUILayout.Button("Play delivery")) sequence.Play();
            if (GUILayout.Button("Reset delivery")) sequence.ResetSequence();
        }
        else
        {
            EditorGUILayout.HelpBox("Preview is temporary and restored on deselect. Do not save or export while previewing. Cake inputs should be visual proxies with no active gameplay or physics.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            preview = EditorGUILayout.Slider("Preview timeline", preview, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                sequence.SampleTime(preview * sequence.TotalDuration);
                previewing = true; SceneView.RepaintAll();
            }
            if (GUILayout.Button("Restore preview")) { sequence.ResetSequence(); preview = 0f; previewing = false; }
        }
    }
    private void OnDisable()
    {
        if (previewing && target != null) ((CartonDeliverySequence)target).ResetSequence();
    }
}
