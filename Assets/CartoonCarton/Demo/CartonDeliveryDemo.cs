using UnityEngine;

namespace CandyBlast.Cartoon.Demo
{
    public sealed class CartonDeliveryDemo : MonoBehaviour
    {
        [SerializeField] private CartonDeliverySequence sequence;
        [SerializeField] private bool autoReplay = true;
        [SerializeField, Min(0f)] private float replayDelay = 1.2f;
        private float hold;
        private void Start() { if (sequence != null) sequence.Play(); }
        private void Update()
        {
            if (sequence == null || !autoReplay || sequence.IsPlaying || sequence.Elapsed < sequence.TotalDuration) return;
            hold += Time.deltaTime;
            if (hold >= replayDelay) { hold = 0f; sequence.Play(); }
        }
        private void OnGUI()
        {
            if (sequence == null) return;
            GUILayout.BeginArea(new Rect(16,16,300,120), GUI.skin.box);
            GUILayout.Label("CARTOON CARTON — DELIVERY DEMO");
            GUILayout.Label("Fly in / Pack cakes / Close / Fly away");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Replay")) { hold = 0f; sequence.Play(); }
            if (GUILayout.Button("Reset")) { hold = 0f; sequence.ResetSequence(); }
            GUILayout.EndHorizontal();
            autoReplay = GUILayout.Toggle(autoReplay, "Auto replay");
            GUILayout.EndArea();
        }
    }
}
