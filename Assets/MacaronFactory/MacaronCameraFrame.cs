using UnityEngine;

namespace BlockShooter
{
    [RequireComponent(typeof(Camera))]
    public sealed class MacaronCameraFrame : MonoBehaviour
    {
        private Bounds _bounds;
        private Camera _camera;
        private bool _ready;
        private int _width, _height;
        private Rect _safeArea;

        public void Frame(Bounds bounds, float tilt)
        {
            _bounds = bounds;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.Euler(tilt, 0, 0);
            _ready = true;
            Apply();
        }

        private void LateUpdate()
        {
            if (_ready && (_width != Screen.width || _height != Screen.height || _safeArea != Screen.safeArea)) Apply();
        }

        private void Apply()
        {
            _width = Screen.width;
            _height = Screen.height;
            _safeArea = Screen.safeArea;
            // Reserve the safe-area header and footer for HUD, then fit every board and tray corner.
            Rect safe = _safeArea.width > 0 ? _safeArea : new Rect(0, 0, _width, _height);
            float left = safe.xMin / _width + .035f;
            float right = safe.xMax / _width - .035f;
            float bottom = safe.yMin / _height + .14f;
            float top = safe.yMax / _height - .10f;
            var inverse = Quaternion.Inverse(transform.rotation);
            var projected = new Bounds(inverse * _bounds.center, Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        projected.Encapsulate(inverse * (_bounds.center + Vector3.Scale(_bounds.extents, new Vector3(x, y, z))));
            _camera.orthographicSize = Mathf.Max(projected.size.y / (2 * (top - bottom)),
                projected.size.x / (2 * _camera.aspect * (right - left))) * 1.03f;
            Vector3 center = transform.rotation * projected.center;
            center -= transform.up * ((top + bottom - 1) * _camera.orthographicSize);
            center -= transform.right * ((left + right - 1) * _camera.orthographicSize * _camera.aspect);
            transform.position = center - transform.forward * 25;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 100;
        }
    }
}
