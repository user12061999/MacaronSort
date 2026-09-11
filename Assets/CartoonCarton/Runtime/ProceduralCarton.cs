using UnityEngine;

namespace CandyBlast.Cartoon
{
    /// <summary>Parametric hollow carton. Dimensions are outside dimensions in local Unity units.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ProceduralCarton : MonoBehaviour
    {
        [Header("Dimensions (local units)")]
        [SerializeField, Min(0.1f)] private float length = 2.4f;
        [SerializeField, Min(0.1f)] private float width = 1.7f;
        [SerializeField, Min(0.1f)] private float height = 1.5f;
        [SerializeField, Min(0.005f)] private float thickness = 0.035f;
        [Header("Appearance")]
        [SerializeField] private Material cardboardMaterial;
        [Header("Lids")]
        [SerializeField, Range(0f, 1f)] private float openness = 1f;
        [SerializeField, Range(95f, 170f)] private float openAngle = 125f;
        [SerializeField, Min(0.05f)] private float duration = 0.8f;
        [SerializeField] private bool openOnStart;
        [SerializeField] private bool loopDemo;
        [SerializeField, Min(0f)] private float holdDuration = 1f;

        private GameObject generated;
        private Transform[] panels;
        private Mesh cube;
        private bool dirty = true;
        private bool animating;
        private float startOpen, targetOpen, elapsed, hold;
        public Vector3 Dimensions => new Vector3(length, height, width);
        public float Openness => openness;
        public bool IsAnimating => animating;
        public bool LoopDemo { get => loopDemo; set => loopDemo = value; }

        private void OnEnable() { dirty = true; }
        private void OnValidate() { dirty = true; }
        private void Start() { if (Application.isPlaying && openOnStart) { SetOpenness(0f); Open(); } }
        private void Update()
        {
            if (generated == null || dirty) Rebuild();
            if (!Application.isPlaying) return;
            if (animating)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
                openness = Mathf.Lerp(startOpen, targetOpen, t * t * (3f - 2f * t));
                Pose();
                if (t >= 1f) { animating = false; hold = 0f; }
            }
            else if (loopDemo)
            {
                hold += Time.deltaTime;
                if (hold >= holdDuration) { if (openness > 0.5f) Close(); else Open(); }
            }
        }

        public void SetDimensions(float newLength, float newWidth, float newHeight)
        {
            length = SafeSize(newLength); width = SafeSize(newWidth); height = SafeSize(newHeight);
            dirty = true;
            if (isActiveAndEnabled) Rebuild();
        }
        public void SetOpenness(float value)
        {
            animating = false;
            openness = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
            if (!isActiveAndEnabled) { dirty = true; return; }
            if (generated == null || dirty) Rebuild(); else Pose();
        }
        public void Open() { AnimateTo(1f); }
        public void Close() { AnimateTo(0f); }
        public void Toggle() { AnimateTo((animating ? targetOpen : openness) > 0.5f ? 0f : 1f); }
        private void AnimateTo(float target)
        {
            startOpen = openness; targetOpen = target; elapsed = 0f; animating = true;
        }
        private static float SafeSize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0.1f, value);

        public void Rebuild()
        {
            length = SafeSize(length); width = SafeSize(width); height = SafeSize(height);
            thickness = Mathf.Clamp(float.IsNaN(thickness) || float.IsInfinity(thickness) ? 0.035f : thickness, 0.005f, Mathf.Min(length, width, height) * 0.15f);
            if (generated == null)
            {
                cube = MakeCube();
                generated = new GameObject("Carton geometry (generated)") { hideFlags = HideFlags.HideAndDontSave };
                generated.transform.SetParent(transform, false);
                panels = new Transform[9];
                string[] names = { "Bottom", "Front", "Back", "Left", "Right", "Front lid", "Back lid", "Left lid", "Right lid" };
                for (int i = 0; i < panels.Length; i++)
                {
                    var panel = new GameObject(names[i]) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                    panel.transform.SetParent(generated.transform, false);
                    panel.AddComponent<MeshFilter>().sharedMesh = cube;
                    panel.AddComponent<MeshRenderer>().sharedMaterial = cardboardMaterial;
                    panels[i] = panel.transform;
                }
            }
            foreach (var panel in panels)
            {
                panel.gameObject.layer = gameObject.layer;
                panel.GetComponent<MeshRenderer>().sharedMaterial = cardboardMaterial;
            }
            Pose(); dirty = false;
        }

        private void Panel(int i, Vector3 center, Vector3 size, Quaternion rotation)
        {
            panels[i].localPosition = center;
            panels[i].localRotation = rotation;
            panels[i].localScale = size;
        }
        private void Pose()
        {
            float t = thickness, x = length * 0.5f, z = width * 0.5f;
            Panel(0, new Vector3(0, t / 2, 0), new Vector3(length, t, width), Quaternion.identity);
            Panel(1, new Vector3(0, height / 2, -z + t / 2), new Vector3(length, height, t), Quaternion.identity);
            Panel(2, new Vector3(0, height / 2, z - t / 2), new Vector3(length, height, t), Quaternion.identity);
            Panel(3, new Vector3(-x + t / 2, height / 2, 0), new Vector3(t, height, width - 2 * t), Quaternion.identity);
            Panel(4, new Vector3(x - t / 2, height / 2, 0), new Vector3(t, height, width - 2 * t), Quaternion.identity);
            // Inner pair moves first when closing, outer pair first when opening.
            float outer = Mathf.Clamp01(openness / 0.75f) * openAngle;
            float inner = Mathf.Clamp01((openness - 0.25f) / 0.75f) * openAngle;
            Lid(5, new Vector3(0, height + 1.5f*t, -z+t/2), new Vector3(0,0,(width-t)/4), new Vector3(length,t,(width-t)/2), Vector3.left, outer);
            Lid(6, new Vector3(0, height + 1.5f*t, z-t/2), new Vector3(0,0,-(width-t)/4), new Vector3(length,t,(width-t)/2), Vector3.right, outer);
            Lid(7, new Vector3(-x+t/2,height+t/2,0), new Vector3((length-t)/4,0,0), new Vector3((length-t)/2,t,width-2*t), Vector3.forward, inner);
            Lid(8, new Vector3(x-t/2,height+t/2,0), new Vector3(-(length-t)/4,0,0), new Vector3((length-t)/2,t,width-2*t), Vector3.back, inner);
        }
        private void Lid(int i, Vector3 hinge, Vector3 offset, Vector3 size, Vector3 axis, float angle)
        {
            Quaternion rotation = Quaternion.AngleAxis(angle, axis);
            Panel(i, hinge + rotation * offset, size, rotation);
        }
        private static Mesh MakeCube()
        {
            var mesh = new Mesh { name = "Carton unit panel", hideFlags = HideFlags.HideAndDontSave };
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            int[] faces = { 0,3,2,1, 5,6,7,4, 4,7,3,0, 1,2,6,5, 3,7,6,2, 4,0,1,5 };
            var vertices = new Vector3[24]; var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                for (int j = 0; j < 4; j++) vertices[f*4+j] = corners[faces[f*4+j]];
                int v=f*4, k=f*6;
                triangles[k]=v; triangles[k+1]=v+1; triangles[k+2]=v+2;
                triangles[k+3]=v; triangles[k+4]=v+2; triangles[k+5]=v+3;
            }
            mesh.vertices=vertices; mesh.triangles=triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        private void OnDisable()
        {
            animating = false;
            if (generated != null) { if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated); }
            if (cube != null) { if (Application.isPlaying) Destroy(cube); else DestroyImmediate(cube); }
            generated = null; cube = null; panels = null;
        }
    }
}
