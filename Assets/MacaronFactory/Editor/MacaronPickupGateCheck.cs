using System;
using BlockShooter.SodaConveyor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    public static class MacaronPickupGateCheck
    {
        [MenuItem("Macaron Factory/Checks/Pickup gate geometry")]
        public static void Run()
        {
            var root = new GameObject("Pickup gate check");
            Mesh mesh = null;
            try
            {
                var spline = root.AddComponent<SplineContainer>();
                var builder = root.AddComponent<ConveyorMeshBuilder>();
                var collider = root.AddComponent<MeshCollider>();
                var filter = root.GetComponent<MeshFilter>();
                for (int preset = 0; preset < TrackShapePresets.TemplateCount; preset++)
                {
                    spline.Spline = TrackShapePresets.Build(preset, 1f, .43f);
                    foreach (float window in new[] { 0f, .08f })
                    {
                        builder.pickupWindowFraction = window;
                        builder.BuildMesh();
                        var previous = mesh;
                        mesh = filter.sharedMesh;
                        collider.sharedMesh = mesh;
                        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
                        foreach (float t in new[] { .91f, .93f, .96f, .99f, .01f })
                        {
                            spline.Spline.Evaluate(t, out var p, out var tangent, out _);
                            var right = Vector3.Cross(Vector3.up, ((Vector3)tangent).normalized);
                            foreach (int side in new[] { -1, 1 })
                            {
                                var origin = (Vector3)p + right * (side * (builder.OuterRadius + .1f)) + Vector3.up * .03f;
                                bool hit = collider.Raycast(new Ray(origin, -right * side), out _, builder.RailWidth + .2f);
                                bool expected = !(window > 0 && side == 1 && t >= 1f - window);
                                if (hit != expected) throw new Exception($"Stage {preset + 1}: wall at t={t}, side={side}, window={window}: hit={hit}.");
                                if (!collider.Raycast(new Ray(origin - Vector3.up * .2f, -right * side), out _, builder.RailWidth + .2f))
                                    throw new Exception($"Stage {preset + 1}: lower wall missing at t={t}, side={side}.");
                            }
                            if (window > 0 && t >= 1f - window)
                            {
                                var sill = (Vector3)p + right * (builder.BeltHalfWidth + builder.RailWidth * .5f);
                                if (!collider.Raycast(new Ray(sill + Vector3.up, Vector3.down), out var sillHit, 1.1f) ||
                                    Mathf.Abs(sillHit.point.y - sill.y) > .001f)
                                    throw new Exception($"Stage {preset + 1}: gate sill is not flush with the belt at t={t}.");
                            }
                            if (!collider.Raycast(new Ray((Vector3)p + Vector3.up, Vector3.down), out _, 1.1f))
                                throw new Exception($"Stage {preset + 1}: belt surface missing at t={t}.");
                        }
                        if (window > 0)
                        {
                            foreach (float t in new[] { 1f - window, 1f })
                            {
                                spline.Spline.Evaluate(t, out var p, out var tangent, out _);
                                var forward = ((Vector3)tangent).normalized;
                                var right = Vector3.Cross(Vector3.up, forward);
                                var direction = t == 1f ? forward : -forward;
                                var cap = (Vector3)p + right * (builder.BeltHalfWidth + builder.RailWidth * .5f) + Vector3.up * .03f;
                                if (!collider.Raycast(new Ray(cap - direction * .01f, direction), out _, .02f))
                                    throw new Exception($"Stage {preset + 1}: gate end cap missing at t={t}.");
                            }
                        }
                    }
                }
                Debug.Log("PASS: Pickup gate on all 10 stages; upper wall cut, lower wall retained, sill flush with belt, opposite wall and belt intact, cut ends capped.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
