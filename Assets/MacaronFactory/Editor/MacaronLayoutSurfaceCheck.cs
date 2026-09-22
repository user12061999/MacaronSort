using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class MacaronLayoutSurfaceCheck
    {
        [MenuItem("Macaron Factory/Checks/Board alignment and full screen floor")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
            var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new InvalidOperationException("Open the MacaronFactory scene first.");
            int checks = 0;
            foreach (var prefab in factory.levels)
            {
                var level = UnityEngine.Object.Instantiate(prefab);
                var cameraObject = new GameObject("Surface check camera");
                try
                {
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    var frame = cameraObject.AddComponent<MacaronCameraFrame>();
                    var boards = level.transform.Cast<Transform>().Where(t => t.name.StartsWith("Tray board ")).ToArray();
                    var positions = boards.Select(t => t.position).ToArray();
                    camera.aspect = 9f / 16;
                    frame.FrameFactoryLayout(level, () => MacaronCameraFrame.CoreBounds(level));
                    var shift = boards[0].position - positions[0];
                    for (int i = 1; i < boards.Length; i++)
                        if (Vector3.Distance(boards[i].position - positions[i], shift) > .001f)
                            throw new Exception(prefab.name + ": board layers moved by different offsets.");
                    var floor = level.transform.Find("Factory floor");
                    var mesh = floor.GetComponent<MeshFilter>().sharedMesh.bounds;
                    foreach (float aspect in new[] { 9f / 20, 9f / 16, 3f / 4, 16f / 9 })
                    {
                        camera.aspect = aspect;
                        frame.Frame(MacaronCameraFrame.CoreBounds(level), level.cameraTilt, level.cameraFieldOfView, level.cameraPadding);
                        var plane = new Plane(floor.up, floor.TransformPoint(new Vector3(mesh.center.x, mesh.max.y, mesh.center.z)));
                        for (int corner = 0; corner < 4; corner++)
                        {
                            var ray = camera.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0));
                            if (!plane.Raycast(ray, out float distance)) throw new Exception("Camera misses floor plane.");
                            var point = floor.InverseTransformPoint(ray.GetPoint(distance)) - mesh.center;
                            if (Mathf.Abs(point.x) > mesh.size.x * .401f || Mathf.Abs(point.z) > mesh.size.z * .401f)
                                throw new Exception(prefab.name + ": viewport corner outside the floor's rounded-edge margin.");
                        }
                        var scale = floor.localScale;
                        frame.Frame(MacaronCameraFrame.CoreBounds(level), level.cameraTilt, level.cameraFieldOfView, level.cameraPadding);
                        if (Vector3.Distance(scale, floor.localScale) > .001f) throw new Exception("Repeated framing grows the floor.");
                        checks++;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(level.gameObject);
                }
            }
            Debug.Log($"PASS: Board layers stay aligned; floor covers every viewport corner across {checks} level/aspect combinations.");
        }
    }
}
