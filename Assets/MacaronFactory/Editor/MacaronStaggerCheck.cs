using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class MacaronStaggerCheck
    {
        [MenuItem("Macaron Factory/Checks/Waiting cake stays on conveyor")]
        public static void CheckWaitingCake()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
            var root = new GameObject("Waiting cake check");
            root.SetActive(false);
            try
            {
                var factory = root.AddComponent<MacaronFactory>();
                var belt = new GameObject("Belt");
                belt.transform.SetParent(root.transform);
                var cake = new GameObject("Cake");
                cake.transform.SetParent(belt.transform);
                var block = cake.AddComponent<ConveyorBlock3D>();
                var tray = root.AddComponent<MacaronTray>();
                var method = typeof(MacaronFactory).GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic);
                var routine = (IEnumerator)method.Invoke(factory, new object[] { block, tray, .2f });
                if (!routine.MoveNext() || !(routine.Current is WaitForSeconds) || block.IsDestroyed ||
                    !block.IsTargeted || block.transform.parent != belt.transform)
                    throw new Exception("Waiting cake was removed from the conveyor or not reserved against duplicate pickup.");
                var before = block.transform.position;
                belt.transform.position += Vector3.right;
                if (block.transform.position != before + Vector3.right)
                    throw new Exception("Waiting cake no longer follows its conveyor parent.");
                UnityEngine.Object.DestroyImmediate(tray);
                if (routine.MoveNext() || block.IsTargeted || block.IsDestroyed)
                    throw new Exception("Cancelled pickup did not release the waiting cake.");
                Debug.Log("PASS: Delayed cake remains alive on conveyor, follows motion, prevents duplicate pickup and cancels safely.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Macaron Factory/Checks/Staggered pickup (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode, then select a tray matching a row of cakes.");
            var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new InvalidOperationException("Open MacaronFactory first.");
            factory.StartCoroutine(Check(factory));
        }

        private static IEnumerator Check(MacaronFactory factory)
        {
            var transfers = typeof(MacaronFactory).GetField("_transfers", BindingFlags.Instance | BindingFlags.NonPublic);
            float deadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < deadline)
            {
                if ((int)transfers.GetValue(factory) > 1)
                {
                    Debug.Log("PASS: Multiple cakes are reserved/in flight together; pickup no longer waits for the previous landing. Verify near-to-far animation visually.");
                    yield break;
                }
                yield return null;
            }
            throw new Exception("No overlapping pickup observed. Select a tray matching at least two cakes in a pickup row and rerun.");
        }
    }
}
