using System;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    [DisallowMultipleComponent]
    public sealed class MacaronLevel : MonoBehaviour
    {
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

        public MacaronTray[] GetTrays() => trayRoot.GetComponentsInChildren<MacaronTray>(true);

        public void ValidateLayout()
        {
            if (conveyorPath == null || conveyorPath.Spline.Count < 2 || conveyorPath.Spline.Closed ||
                trayRoot == null || GetTrays().Length == 0 || waitingSlots == null || waitingSlots.Length != 6 ||
                Array.Exists(waitingSlots, slot => slot == null) || counterAnchor == null)
                throw new InvalidOperationException($"{name}: assign an open conveyor, trays, six waiting slots and counter anchor.");
        }
    }
}
