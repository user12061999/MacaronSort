using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CandyBlast.Cartoon
{
    /// <summary>Standalone presentation. Only the assigned carton and cake visuals are animated.</summary>
    [DisallowMultipleComponent]
    public sealed class CartonDeliverySequence : MonoBehaviour
    {
        [Header("Owned visuals (carton must be a direct child)")]
        [SerializeField] private ProceduralCarton carton;
        [SerializeField] private Transform[] cakes = Array.Empty<Transform>();
        [Header("Flight path — local to this root")]
        [SerializeField] private Vector3 entryPosition = new Vector3(-6f, 3f, 0f);
        [SerializeField] private Vector3 dockPosition = new Vector3(1.8f, 0f, 0f);
        [SerializeField] private Vector3 exitPosition = new Vector3(8f, 4f, 0f);
        [SerializeField, Min(0f)] private float flightArc = 1.2f;
        [Header("Timing (seconds)")]
        [SerializeField, Min(.05f)] private float arrivalDuration = .75f;
        [SerializeField, Min(.05f)] private float settleDuration = .35f;
        [SerializeField, Min(.05f)] private float cakeJumpDuration = .65f;
        [SerializeField, Min(0f)] private float cakeStagger = .12f;
        [SerializeField, Min(.05f)] private float closeDuration = .65f;
        [SerializeField, Min(.05f)] private float anticipationDuration = .25f;
        [SerializeField, Min(.05f)] private float departureDuration = .65f;
        [Header("Cartoon motion")]
        [SerializeField, Range(0f, .4f)] private float squash = .18f;
        [SerializeField, Range(0f, 35f)] private float tilt = 16f;
        [SerializeField, Min(0f)] private float cakeJumpHeight = 2.4f;
        [SerializeField, Range(0f, 1f)] private float cakeSpinTurns = .25f;
        [SerializeField, Range(.1f, .9f)] private float packingScale = .48f;
        [Header("Playback")]
        [SerializeField] private bool playOnStart;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private UnityEvent onPacked = new UnityEvent();
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private struct CakePose
        {
            public Transform visual, parent;
            public Vector3 position, localPosition, scale, slot;
            public Quaternion rotation, localRotation;
            public float fit, clearance;
        }
        private CakePose[] poses;
        private Vector3 originalPosition, originalScale;
        private Quaternion originalRotation;
        private float originalOpen, time;
        private bool originalLoop, captured, playing, packed;
        public bool IsPlaying => playing;
        public UnityEvent OnPacked => onPacked;
        public UnityEvent OnCompleted => onCompleted;
        public float Elapsed => time;
        public ProceduralCarton Carton => carton;
        public float LoadStart => Positive(arrivalDuration) + Positive(settleDuration);
        public float LoadEnd => LoadStart + (CakeCount == 0 ? 0f : Positive(cakeJumpDuration) + Mathf.Max(0f, cakeStagger) * (CakeCount - 1));
        public float PackEnd => LoadEnd + Positive(closeDuration);
        public float DepartureStart => PackEnd + Positive(anticipationDuration);
        public float TotalDuration => DepartureStart + Positive(departureDuration);
        private int CakeCount => poses != null ? poses.Length : cakes == null ? 0 : cakes.Length;
        private static float Positive(float value) => Mathf.Max(.05f, value);
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void Start() { if (playOnStart) Play(); }
        private void Update() { if (playing) Advance(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }

        public void Play()
        {
            ResetSequence();
            if (!Capture()) return;
            playing = true; packed = false; time = 0f;
            Evaluate(0f);
        }
        /// <summary>Call when a tray is full. Pass visual proxies, not gameplay objects with active logic/physics.</summary>
        public void PlayAt(Vector3 worldDockPosition, Transform[] cakeVisuals)
        {
            ResetSequence();
            dockPosition = transform.InverseTransformPoint(worldDockPosition);
            cakes = cakeVisuals == null ? Array.Empty<Transform>() : (Transform[])cakeVisuals.Clone();
            Play();
        }
        /// <summary>Explicit clock step, also usable for deterministic animation verification.</summary>
        public void Advance(float deltaTime)
        {
            if (!playing || deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            time = Mathf.Min(time + deltaTime, TotalDuration);
            Evaluate(time);
            bool notifyPacked = !packed && time >= PackEnd;
            bool notifyComplete = time >= TotalDuration;
            if (notifyPacked) packed = true;
            if (notifyComplete) playing = false;
            // State is committed before callbacks; callbacks may reset or disable the object.
            if (notifyPacked) onPacked.Invoke();
            if (notifyComplete && captured && time >= TotalDuration) onCompleted.Invoke();
        }
        /// <summary>Editor preview without events. ResetSequence restores every captured pose.</summary>
        public void SampleTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (!captured && !Capture()) return;
            playing = false;
            time = Mathf.Clamp(seconds, 0f, TotalDuration);
            Evaluate(time);
        }
        public void ResetSequence()
        {
            playing = false; time = 0f; packed = false;
            if (!captured) return;
            if (carton != null)
            {
                carton.transform.localPosition = originalPosition;
                carton.transform.localRotation = originalRotation;
                carton.transform.localScale = originalScale;
                carton.SetOpenness(originalOpen);
                carton.LoopDemo = originalLoop;
            }
            foreach (var pose in poses)
            {
                if (pose.visual == null) continue;
                // Hierarchy is never changed by this component.
                if (pose.visual.parent == pose.parent)
                {
                    pose.visual.localPosition = pose.localPosition;
                    pose.visual.localRotation = pose.localRotation;
                }
                else pose.visual.SetPositionAndRotation(pose.position, pose.rotation);
                pose.visual.localScale = pose.scale;
            }
            poses = null; captured = false;
        }
        private void OnDisable() { ResetSequence(); }
        private bool Capture()
        {
            if (carton == null || carton.transform.parent != transform || !carton.isActiveAndEnabled)
            {
                Debug.LogWarning("Assign an enabled carton directly under the delivery root.", this); return false;
            }
            originalPosition = carton.transform.localPosition;
            originalRotation = carton.transform.localRotation;
            originalScale = carton.transform.localScale;
            originalOpen = carton.Openness; originalLoop = carton.LoopDemo;
            carton.LoopDemo = false;
            var valid = new List<Transform>();
            if (cakes != null)
                foreach (var cake in cakes)
                    if (cake != null && cake != transform && !transform.IsChildOf(cake) && cake != carton.transform && !cake.IsChildOf(carton.transform) && !valid.Contains(cake)) valid.Add(cake);
            // Parent/child pairs cannot be animated independently without double transforms.
            for (int i = valid.Count - 1; i >= 0; i--)
                for (int j = 0; j < valid.Count; j++)
                    if (i != j && valid[i].IsChildOf(valid[j])) { valid.RemoveAt(i); break; }
            poses = new CakePose[valid.Count];
            Vector3 dimensions = carton.Dimensions;
            int columns = Mathf.Max(1, Mathf.Min(valid.Count, Mathf.CeilToInt(Mathf.Sqrt(valid.Count * dimensions.x / dimensions.z))));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)valid.Count / columns));
            float cellX = dimensions.x * .72f / columns, cellZ = dimensions.z * .72f / rows;
            for (int i = 0; i < valid.Count; i++)
            {
                var visual = valid[i];
                var bounds = new Bounds(visual.position, Vector3.zero);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                Vector3 scale = carton.transform.lossyScale;
                float fit = Mathf.Min(packingScale,
                    cellX * Mathf.Abs(scale.x) / Mathf.Max(.001f, bounds.size.x),
                    cellZ * Mathf.Abs(scale.z) / Mathf.Max(.001f, bounds.size.z),
                    dimensions.y * .5f * Mathf.Abs(scale.y) / Mathf.Max(.001f, bounds.size.y));
                poses[i] = new CakePose {
                    visual = visual, parent = visual.parent, position = visual.position, rotation = visual.rotation,
                    localPosition = visual.localPosition, localRotation = visual.localRotation, scale = visual.localScale, fit = fit,
                    clearance = bounds.extents.magnitude * (1f + squash),
                    slot = new Vector3((i % columns - (columns - 1) * .5f) * cellX, dimensions.y * .32f, (i / columns - (rows - 1) * .5f) * cellZ)
                };
            }
            captured = true; return true;
        }
        private void Evaluate(float seconds)
        {
            if (carton == null) { playing = false; return; }
            float arrive = Positive(arrivalDuration);
            Vector3 position = dockPosition;
            float lean = 0f, compress = 0f, open = 1f;
            if (seconds < arrive)
            {
                float t = seconds / arrive;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                position = Vector3.Lerp(entryPosition, dockPosition, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * flightArc);
                lean = -tilt * Mathf.Sin(t * Mathf.PI);
                compress = -squash * .5f * Mathf.Sin(t * Mathf.PI);
                open = Mathf.Lerp(.2f, 1f, Ease(t));
            }
            else if (seconds < LoadStart)
            {
                float t = (seconds - arrive) / Positive(settleDuration);
                compress = squash * Mathf.Sin(t * Mathf.PI * 3f) * (1f-t);
                position.y += Mathf.Sin(t * Mathf.PI) * .16f;
                lean = tilt * .3f * Mathf.Sin(t * Mathf.PI * 2f) * (1f-t);
            }
            else if (seconds < LoadEnd)
            {
                for (int i = 0; i < poses.Length; i++)
                {
                    float sinceLanding = seconds - (LoadStart + i * Mathf.Max(0f,cakeStagger) + Positive(cakeJumpDuration));
                    if (sinceLanding >= 0f && sinceLanding < .3f)
                    {
                        float t = sinceLanding / .3f;
                        compress += squash * .55f * Mathf.Sin(t * Mathf.PI * 2f) * (1f-t);
                        lean += tilt * .15f * (i % 2 == 0 ? 1f : -1f) * Mathf.Sin(t * Mathf.PI) * (1f-t);
                    }
                }
            }
            else if (seconds < PackEnd)
            {
                float t = (seconds - LoadEnd) / Positive(closeDuration);
                open = 1f - Ease(Mathf.Clamp01(t / .78f));
                float recoil = Mathf.Clamp01((t - .65f) / .35f);
                compress = squash * .8f * Mathf.Sin(recoil * Mathf.PI * 2f) * (1f-recoil);
                lean = tilt * .18f * Mathf.Sin(t * Mathf.PI * 2f) * (1f-t);
            }
            else if (seconds < DepartureStart)
            {
                float t = (seconds - PackEnd) / Positive(anticipationDuration);
                open = 0f; compress = squash * Ease(t); lean = tilt * .45f * Ease(t);
                position -= Vector3.right * (.2f * Ease(t));
            }
            else
            {
                float t = Mathf.Clamp01((seconds - DepartureStart) / Positive(departureDuration));
                open = 0f;
                position = Vector3.Lerp(dockPosition - Vector3.right * .2f, exitPosition, t*t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * flightArc * .5f);
                compress = Mathf.Lerp(squash, -squash * .8f, Ease(Mathf.Clamp01(t * 4f))) * (1f-t);
                lean = Mathf.Lerp(tilt * .45f, -tilt * 1.4f, Ease(t));
            }
            compress = Mathf.Clamp(compress, -.4f, .4f);
            carton.transform.localPosition = position;
            carton.transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, lean);
            carton.transform.localScale = Vector3.Scale(originalScale, new Vector3(1f + compress * .5f, 1f-compress, 1f+compress*.5f));
            carton.SetOpenness(open);
            for (int i = 0; i < poses.Length; i++)
            {
                CakePose pose = poses[i];
                if (pose.visual == null) continue;
                float t = Mathf.Clamp01((seconds - LoadStart - i * Mathf.Max(0f, cakeStagger)) / Positive(cakeJumpDuration));
                Vector3 target = carton.transform.TransformPoint(pose.slot);
                // Clear the rim before crossing the wall, then descend vertically through the opening.
                Vector3 up = transform.up;
                Vector3 rim = carton.transform.TransformPoint(Vector3.up * carton.Dimensions.y);
                float peak = Mathf.Max(Vector3.Dot(pose.position, up), Vector3.Dot(rim, up))
                    + pose.clearance + cakeJumpHeight;
                Vector3 aboveStart = pose.position + up * (peak - Vector3.Dot(pose.position, up));
                Vector3 aboveBox = target + up * (peak - Vector3.Dot(target, up));
                Vector3 cakePosition = t < .3f ? Vector3.Lerp(pose.position, aboveStart, Ease(t / .3f))
                    : t < .75f ? Vector3.Lerp(aboveStart, aboveBox, Ease((t - .3f) / .45f))
                    : Vector3.Lerp(aboveBox, target, Ease((t - .75f) / .25f));
                float packingProgress = Mathf.Clamp01(t / .75f);
                Quaternion rotation = Quaternion.Slerp(pose.rotation, carton.transform.rotation, Ease(packingProgress)) * Quaternion.AngleAxis(Mathf.Sin(packingProgress*Mathf.PI) * cakeSpinTurns * 360f, Vector3.forward);
                pose.visual.SetPositionAndRotation(cakePosition, rotation);
                float size = Mathf.Lerp(1f, pose.fit, Ease(packingProgress));
                float stretch = Mathf.Sin(packingProgress * Mathf.PI) * squash;
                pose.visual.localScale = Vector3.Scale(pose.scale * size, new Vector3(1f-stretch*.5f, 1f+stretch, 1f-stretch*.5f));
            }
        }
    }
}
