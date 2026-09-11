using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    [DisallowMultipleComponent]
    public sealed class MacaronLevel : MonoBehaviour
    {
        [Serializable]
        public struct MacaronBatch
        {
            public BlockColorType color;
            [Min(1)] public int count;
        }

        [Header("Macaron supply, first batch reaches the exit first")]
        public bool useCustomMacaronOrder;
        [Tooltip("Ordered color batches. Batches may cross row boundaries; set Count=1 to author individual cakes.")]
        public MacaronBatch[] macaronOrder = Array.Empty<MacaronBatch>();
        [TextArea] public string instruction;
        [Tooltip("Editable open spline, ordered from entrance to exit.")]
        public SplineContainer conveyorPath;
        public Transform trayRoot;
        public Transform[] waitingSlots = new Transform[6];
        public Transform counterAnchor;
        [Range(1, 5)] public int columns = 3;
        [Min(.22f)] public float laneSpacing = .25f;
        [Min(.24f)] public float rowSpacing = .32f;
        [Min(0)] public float stopBeforeExit = .5f;
        [Min(.1f)] public float exitZoneLength = 1.4f;
        [Range(55, 85)] public float cameraTilt = 72;
        [Range(20, 60)] public float cameraFieldOfView = 35;
        [Tooltip("Safe-area margins: left, right, bottom, top. Smaller margins make the board fill more of the screen.")]
        public Vector4 cameraPadding = new Vector4(.015f, .015f, .12f, .065f);

        public MacaronTray[] GetTrays() => trayRoot.GetComponentsInChildren<MacaronTray>(true);

        public List<BlockColorType> BuildMacaronOrder()
        {
            var trays = GetTrays();
            if (!useCustomMacaronOrder)
                return trays.OrderByDescending(tray => tray.stackLayer)
                    .SelectMany(tray => Enumerable.Repeat(tray.levelColor, tray.Capacity)).ToList();
            if (macaronOrder == null || macaronOrder.Length == 0)
                throw new InvalidOperationException($"{name}: custom Macaron Order is empty.");
            var capacity = new int[7];
            var supply = new int[7];
            foreach (var tray in trays)
            {
                int color = (int)tray.levelColor;
                if (color < 1 || color > 6) throw new InvalidOperationException($"{name}: unsupported tray color {tray.levelColor}.");
                capacity[color] = checked(capacity[color] + tray.Capacity);
            }
            for (int i = 0; i < macaronOrder.Length; i++)
            {
                var batch = macaronOrder[i];
                int color = (int)batch.color;
                if (color < 1 || color > 6 || batch.count < 1)
                    throw new InvalidOperationException($"{name}: batch {i + 1} needs a supported color and Count >= 1.");
                if (batch.count > capacity[color] - supply[color])
                    throw new InvalidOperationException($"{name}: batch {i + 1} exceeds tray capacity for {batch.color} ({capacity[color]}).");
                supply[color] += batch.count;
            }
            for (int color = 1; color <= 6; color++)
                if (supply[color] != capacity[color])
                    throw new InvalidOperationException($"{name}: {(BlockColorType)color} has {supply[color]} cakes but {capacity[color]} tray pockets.");
            return macaronOrder.SelectMany(batch => Enumerable.Repeat(batch.color, batch.count)).ToList();
        }

        public void AlignExitToWaitingSlots()
        {
            if (conveyorPath == null || waitingSlots == null || waitingSlots.Length == 0 ||
                Array.Exists(waitingSlots, slot => slot == null) || conveyorPath.Spline.Count < 2) return;
            Vector3 center = Vector3.zero;
            foreach (var slot in waitingSlots) center += transform.InverseTransformPoint(slot.position);
            center /= waitingSlots.Length;
            var spline = conveyorPath.Spline;
            Vector3 end = transform.InverseTransformPoint(conveyorPath.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position));
            Vector3 shift = conveyorPath.transform.InverseTransformVector(transform.TransformVector(Vector3.right * (center.x - end.x)));
            if (shift.sqrMagnitude < .00000001f) return;
            // Move the exit bend with its endpoint so the rounded inner wall keeps its radius.
            int first = spline.Count >= 4 ? spline.Count - 3 : spline.Count - 1;
            for (int i = first; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position += (Unity.Mathematics.float3)shift;
                spline[i] = knot;
            }
        }

        public void ValidateLayout()
        {
            if (conveyorPath == null || conveyorPath.Spline.Count < 2 || conveyorPath.Spline.Closed ||
                trayRoot == null || GetTrays().Length == 0 || waitingSlots == null || waitingSlots.Length != 6 ||
                Array.Exists(waitingSlots, slot => slot == null) || counterAnchor == null)
                throw new InvalidOperationException($"{name}: assign an open conveyor, trays, six waiting slots and counter anchor.");
            if (columns < 1 || columns > 5) throw new InvalidOperationException($"{name}: Columns must be between 1 and 5.");
            if (Array.Exists(GetTrays(), tray => tray.pockets == null || tray.pockets.Length < 4 || tray.pockets.Length > 8 ||
                Array.Exists(tray.pockets, pocket => pocket == null) || tray.lid == null || tray.GetComponent<BoxCollider>() == null))
                throw new InvalidOperationException($"{name}: each tray needs 4–8 assigned pockets, a lid and a BoxCollider.");
            if (useCustomMacaronOrder) BuildMacaronOrder();
        }
    }
}
