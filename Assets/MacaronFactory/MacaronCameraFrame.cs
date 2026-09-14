using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    [RequireComponent(typeof(Camera))]
    public sealed class MacaronCameraFrame : MonoBehaviour
    {
        private readonly List<Vector3> _points = new();
        private Camera _camera;
        private Vector4 _padding;
        private bool _ready;
        private int _width, _height;
        private Rect _safeArea;
        private MacaronLevel _level;
        private readonly List<ConveyorTrackMeshBuilder> _extensions = new();

        public static Bounds[] CoreBounds(MacaronLevel level)
        {
            return level.GetComponentsInChildren<Renderer>().Where(r =>
                (r.enabled || r.gameObject == level.conveyorPath.gameObject) &&
                r.name != "Factory floor" && !r.name.StartsWith("Conveyor board") &&
                !level.feederBranches.Any(f => r.transform.IsChildOf(f.transform)))
                .Select(r => r.bounds).ToArray();
        }

        public void FrameLevel(MacaronLevel level)
        {
            _level = level;
            Frame(CoreBounds(level), level.cameraTilt, level.cameraFieldOfView, level.cameraPadding);
        }

        public void Frame(IEnumerable<Bounds> bounds, float tilt, float fieldOfView, Vector4 padding)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = Mathf.Clamp(fieldOfView, 20, 60);
            _camera.nearClipPlane = .1f;
            transform.rotation = Quaternion.Euler(tilt, 0, 0);
            _padding = padding;
            _points.Clear();
            var inverse = Quaternion.Inverse(transform.rotation);
            foreach (var box in bounds)
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            _points.Add(inverse * (box.center + Vector3.Scale(box.extents, new Vector3(x, y, z))));
            _ready = _points.Count > 0;
            if (_ready) Apply();
        }

        private void LateUpdate()
        {
            if (_ready && (_width != Screen.width || _height != Screen.height || _safeArea != Screen.safeArea)) Apply();
        }

        private void Apply()
        {
            _width = Mathf.Max(1, Screen.width);
            _height = Mathf.Max(1, Screen.height);
            _safeArea = Screen.safeArea;
            Rect safe = _safeArea.width > 0 ? _safeArea : new Rect(0, 0, _width, _height);
            float left = (safe.xMin + safe.width * Mathf.Clamp(_padding.x, 0, .2f)) / _width;
            float right = (safe.xMax - safe.width * Mathf.Clamp(_padding.y, 0, .2f)) / _width;
            float bottom = (safe.yMin + safe.height * Mathf.Clamp(_padding.z, 0, .25f)) / _height;
            float top = (safe.yMax - safe.height * Mathf.Clamp(_padding.w, 0, .25f)) / _height;
            float tangent = Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad);
            var slopes = new Vector4((2 * left - 1) * tangent * _camera.aspect,
                (2 * right - 1) * tangent * _camera.aspect, (2 * bottom - 1) * tangent, (2 * top - 1) * tangent);
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var point in _points) { minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z); }
            float near = .2f - minZ, far = near + 1;
            while (!Fits(far, slopes, out _)) far = near + (far - near) * 2;
            // Find the closest perspective camera that contains the actual renderer corners.
            for (int i = 0; i < 24; i++)
            {
                float middle = (near + far) * .5f;
                if (Fits(middle, slopes, out _)) far = middle; else near = middle;
            }
            Fits(far + .02f, slopes, out var center);
            transform.position = transform.rotation * new Vector3(center.x, center.y, -far - .02f);
            _camera.farClipPlane = Mathf.Max(100, maxZ + far + 10);
            ExtendFeeders();
        }

        private void ExtendFeeders()
        {
            if (_level == null) return;
            while (_extensions.Count > _level.feederBranches.Length)
            {
                var extra = _extensions[_extensions.Count - 1];
                if (extra != null)
                {
                    if (extra.GetComponent<MeshFilter>().sharedMesh != null) DestroyImmediate(extra.GetComponent<MeshFilter>().sharedMesh);
                    DestroyImmediate(extra.gameObject);
                }
                _extensions.RemoveAt(_extensions.Count - 1);
            }
            for (int i = 0; i < _level.feederBranches.Length; i++)
            {
                var source = _level.feederBranches[i].Branch;
                var path = source.GetComponent<SplineContainer>();
                path.Spline.Evaluate(0, out var p, out var tangent, out _);
                var start = path.transform.TransformPoint((Vector3)p);
                var outward = -path.transform.TransformDirection((Vector3)tangent).normalized;
                float length = 1;
                // A bounded search also covers feeders approaching the top/bottom of the screen.
                for (int step = 0; step < 14; step++)
                {
                    var view = _camera.WorldToViewportPoint(start + outward * length);
                    if (view.z <= 0 || view.x < -.15f || view.x > 1.15f || view.y < -.15f || view.y > 1.15f) break;
                    length *= 2;
                }
                if (_extensions.Count <= i)
                {
                    var go = new GameObject("Offscreen feeder extension") { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(transform, false);
                    go.AddComponent<SplineContainer>();
                    _extensions.Add(go.AddComponent<ConveyorTrackMeshBuilder>());
                }
                var extension = _extensions[i];
                extension.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var end = extension.transform.InverseTransformPoint(start);
                var beginning = extension.transform.InverseTransformPoint(start + outward * length);
                var spline = new Spline();
                var handle = (Unity.Mathematics.float3)((end - beginning) / 3);
                spline.Add(new BezierKnot((Unity.Mathematics.float3)beginning, -handle, handle), TangentMode.Broken);
                spline.Add(new BezierKnot((Unity.Mathematics.float3)end, -handle, handle), TangentMode.Broken);
                extension.GetComponent<SplineContainer>().Spline = spline;
                extension.beltHalfWidth = source.beltHalfWidth;
                extension.railWidth = source.railWidth;
                extension.railHeight = source.railHeight;
                extension.wallAboveBelt = source.wallAboveBelt;
                extension.closeBottom = source.closeBottom;
                extension.resolution = 8;
                extension.GetComponent<Renderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
                var previous = extension.GetComponent<MeshFilter>().sharedMesh;
                extension.BuildMesh();
                if (previous != null) DestroyImmediate(previous);
            }
        }

        private void OnDestroy()
        {
            foreach (var extension in _extensions)
                if (extension != null && extension.GetComponent<MeshFilter>().sharedMesh != null)
                    DestroyImmediate(extension.GetComponent<MeshFilter>().sharedMesh);
        }

        private bool Fits(float distance, Vector4 slopes, out Vector2 center)
        {
            float minX = float.NegativeInfinity, maxX = float.PositiveInfinity;
            float minY = float.NegativeInfinity, maxY = float.PositiveInfinity;
            foreach (var point in _points)
            {
                float depth = point.z + distance;
                minX = Mathf.Max(minX, point.x - slopes.y * depth);
                maxX = Mathf.Min(maxX, point.x - slopes.x * depth);
                minY = Mathf.Max(minY, point.y - slopes.w * depth);
                maxY = Mathf.Min(maxY, point.y - slopes.z * depth);
            }
            center = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
            return minX <= maxX && minY <= maxY;
        }
    }
}
