using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class MacaronWinFeedbackCheck
    {
        [MenuItem("Macaron Factory/Checks/Win effect before result (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode in MacaronFactory first.");
            var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null || factory.feedback == null) throw new InvalidOperationException("Missing factory/feedback.");
            factory.StartCoroutine(Check(factory));
        }

        private static IEnumerator Check(MacaronFactory factory)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var overlayField = typeof(MacaronFactory).GetField("_overlay", flags);
            var originalOverlay = overlayField.GetValue(factory);
            factory.feedback.Play(MacaronFeedbackEvent.Win, factory.transform.position);
            if (!factory.feedback.IsEffectPlaying(MacaronFeedbackEvent.Win))
                throw new Exception("Enable and assign the Win effect before running this check.");
            var routine = (IEnumerator)typeof(MacaronFactory).GetMethod("ShowFinishOverlay", flags)
                .Invoke(factory, new object[] { true });
            bool waited = false;
            float deadline = Time.realtimeSinceStartup + 10;
            while (routine.MoveNext())
            {
                waited = true;
                if (!ReferenceEquals(originalOverlay, overlayField.GetValue(factory)))
                    throw new Exception("Result appeared before the Win effect finished.");
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Result never appeared.");
                yield return routine.Current;
            }
            var result = (RectTransform)overlayField.GetValue(factory);
            if (!waited || factory.feedback.IsEffectPlaying(MacaronFeedbackEvent.Win) ||
                result == null || !result.gameObject.activeInHierarchy || ReferenceEquals(originalOverlay, result))
                throw new Exception("Win effect/result ordering failed.");
            Debug.Log("PASS: Win effect finished before the result appeared. No rewards or progress changed. Exit Play Mode to reset the preview.");
        }
    }
}
