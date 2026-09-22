using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class MacaronTrayAnimationCheck
    {
        [MenuItem("Macaron Factory/Checks/Tray flight and packing (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Tray animation check");
            root.SetActive(false);
            Tween flight = null, packing = null;
            try
            {
                var factory = root.AddComponent<MacaronFactory>();
                var tray = new GameObject("Tray").AddComponent<MacaronTray>();
                tray.transform.SetParent(root.transform);
                tray.transform.position = new Vector3(3, 2, 4);
                tray.lid = new GameObject("Lid").transform;
                tray.lid.SetParent(tray.transform);
                tray.lid.localScale = new Vector3(2, 1, 3);
                var label = new GameObject("Label").AddComponent<TextMeshPro>();
                label.transform.SetParent(tray.transform);
                typeof(MacaronTray).GetProperty("Label").SetValue(tray, label);
                var start = tray.transform.position;
                var routine = Invoke(factory, "MoveToSlot", tray, 0);
                routine.MoveNext();
                tray.transform.DOKill(true);
                var before = DOTween.PlayingTweens()?.ToArray() ?? Array.Empty<Tween>();
                routine.MoveNext();
                flight = DOTween.PlayingTweens().Single(t => !before.Contains(t));
                flight.Pause();
                flight.Goto(factory.trayJumpTime * .125f);
                Require(Mathf.Abs(tray.transform.position.x - start.x) < .001f &&
                    Mathf.Abs(tray.transform.position.z - start.z) < .001f && tray.transform.position.y > start.y,
                    "Tray must rise vertically before moving sideways.");
                flight.Goto(factory.trayJumpTime * .5f);
                Require(tray.transform.position.y >= start.y + factory.trayJumpHeight - .001f,
                    "Tray must stay elevated during horizontal travel.");
                flight.Goto(factory.trayJumpTime);
                var target = (Vector3)typeof(MacaronFactory).GetMethod("SlotPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(factory, new object[] { 0 });
                Require(Vector3.Distance(tray.transform.position, target) < .001f &&
                    Vector3.Distance(tray.transform.localScale, Vector3.one * factory.trayWaitingScale) < .001f,
                    "Tray must land at the slot with its waiting scale.");
                flight.Kill();
                var rest = tray.lid.localScale;
                routine = Invoke(factory, "Ship", tray);
                routine.MoveNext();
                before = DOTween.PlayingTweens()?.ToArray() ?? Array.Empty<Tween>();
                routine.MoveNext();
                packing = DOTween.PlayingTweens().Single(t => !before.Contains(t));
                packing.Pause();
                Require(Vector3.Distance(tray.lid.localScale, rest * factory.trayPackingScaleMultiplier) < .001f,
                    "Packing lid must appear enlarged.");
                packing.Goto(factory.trayPackingTime * .5f);
                Require(tray.lid.localScale.x > rest.x && tray.lid.localScale.x < rest.x * factory.trayPackingScaleMultiplier,
                    "Packing lid must shrink during travel.");
                packing.Goto(factory.trayPackingTime);
                Require(Vector3.Distance(tray.lid.localScale, rest) < .001f &&
                    Vector3.Distance(tray.lid.localPosition, tray.ClosedLidPosition) < .001f,
                    "Packing must preserve the authored lid scale and closed position.");
                Debug.Log("PASS: Tray rises before travel, lands correctly, and packing shrinks to the authored pose.");
            }
            finally
            {
                flight?.Kill();
                packing?.Kill();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator Invoke(MacaronFactory factory, string name, params object[] args) =>
            (IEnumerator)typeof(MacaronFactory).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(factory, args);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
