using UnityEngine;

namespace CandyBlast.Cartoon
{
    [RequireComponent(typeof(ProceduralCarton))]
    public sealed class CartonExitAnimation : MonoBehaviour
    {
        [SerializeField] private Vector3 localExitOffset = new Vector3(4f, 0f, 0f);
        [SerializeField, Min(0.05f)] private float moveDuration = 0.45f;
        [SerializeField] private bool playOnStart;
        private ProceduralCarton carton;
        private Vector3 origin;
        private float elapsed;
        private int phase;
        private void Awake() { carton = GetComponent<ProceduralCarton>(); origin = transform.localPosition; }
        private void Start() { if (playOnStart) Play(); }
        public void Play()
        {
            if (carton == null) { carton = GetComponent<ProceduralCarton>(); origin = transform.localPosition; }
            transform.localPosition = origin;
            carton.LoopDemo = false;
            carton.Close(); elapsed = 0f; phase = 1;
        }
        public void ResetPose()
        {
            phase = 0; transform.localPosition = origin;
            if (carton != null) carton.SetOpenness(1f);
        }
        private void Update()
        {
            if (phase == 1 && !carton.IsAnimating) phase = 2;
            if (phase != 2) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, moveDuration));
            transform.localPosition = origin + localExitOffset * t * t;
            if (t >= 1f) phase = 0;
        }
        private void OnDisable() { phase = 0; }
    }
}
