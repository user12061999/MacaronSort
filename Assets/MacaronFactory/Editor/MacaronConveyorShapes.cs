using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    internal static class MacaronConveyorShapes
    {
        public static Spline Create(ConveyorShape shape, float width, float height, float radius,
            float centerX, float bottom)
        {
            var corners = shape == ConveyorShape.Triangle
                ? new[] { new Vector3(-width, 0, 0), new Vector3(0, 0, height), new Vector3(width, 0, 0) }
                : new[] { new Vector3(-width, 0, 0), new Vector3(-width, 0, height),
                    new Vector3(width, 0, height), new Vector3(width, 0, 0) };
            var points = new List<Vector3> { Vector3.zero };
            var incoming = new List<Vector3> { Vector3.zero };
            var outgoing = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < corners.Length; i++)
            {
                var previous = corners[(i + corners.Length - 1) % corners.Length];
                var next = corners[(i + 1) % corners.Length];
                var inDirection = (corners[i] - previous).normalized;
                var outDirection = (next - corners[i]).normalized;
                float turn = Vector3.Angle(inDirection, outDirection) * Mathf.Deg2Rad;
                float tangentDistance = Mathf.Min(radius * Mathf.Tan(turn * .5f),
                    .4f * Mathf.Min(Vector3.Distance(corners[i], previous), Vector3.Distance(corners[i], next)));
                float actualRadius = tangentDistance / Mathf.Tan(turn * .5f);
                float handle = 4f / 3 * actualRadius * Mathf.Tan(turn * .25f);
                points.Add(corners[i] - inDirection * tangentDistance);
                incoming.Add(Vector3.zero);
                outgoing.Add(inDirection * handle);
                points.Add(corners[i] + outDirection * tangentDistance);
                incoming.Add(-outDirection * handle);
                outgoing.Add(Vector3.zero);
            }
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                if (outgoing[i] != Vector3.zero) continue;
                outgoing[i] = (points[next] - points[i]) / 3;
                incoming[next] = -outgoing[i];
            }
            var spline = new Spline();
            for (int i = 0; i < points.Count; i++)
                spline.Add(new BezierKnot((float3)(points[i] + new Vector3(centerX, 0, bottom)),
                    (float3)incoming[i], (float3)outgoing[i], quaternion.identity), TangentMode.Broken);
            spline.Closed = true;
            return spline;
        }
    }
}
