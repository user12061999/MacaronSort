using UnityEngine;
using DG.Tweening;

namespace EKStudio
{
    public enum ScaleDirection { ScaleUp, ScaleDown }
    public enum ScaleAxis { X, Y, Z, All } // "All" option added to represent all axes

    public class ScaleAnimation : MonoBehaviour
    {
        public ScaleDirection ScaleDirection = ScaleDirection.ScaleUp;
        public ScaleAxis ScaleAxis = ScaleAxis.All;
        public Ease Ease = Ease.Linear;
        public float Duration = 1f;
        public float ScaleFactor = 1.1f;
        public float Delay = 0f;

        private Vector3 originalScale;
        private Tween currentTween;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            ApplyScale();
        }

        private void ApplyScale()
        {
            transform.localScale = originalScale;

            switch (ScaleDirection)
            {
                case ScaleDirection.ScaleUp:
                    ScaleUp();
                    break;
                case ScaleDirection.ScaleDown:
                    ScaleDown();
                    break;
            }
        }

        void ScaleUp()
        {
            Vector3 targetScale = originalScale;

            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.X)
            {
                targetScale.x *= ScaleFactor;
            }
            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.Y)
            {
                targetScale.y *= ScaleFactor;
            }
            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.Z)
            {
                targetScale.z *= ScaleFactor;
            }

            currentTween = transform.DOScale(targetScale, Duration).SetDelay(Delay)
                .SetEase(Ease)
                .SetLoops(-1, LoopType.Yoyo);
        }

        void ScaleDown()
        {
            Vector3 targetScale = originalScale;

            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.X)
            {
                targetScale.x /= ScaleFactor;
            }
            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.Y)
            {
                targetScale.y /= ScaleFactor;
            }
            if (ScaleAxis == ScaleAxis.All || ScaleAxis == ScaleAxis.Z)
            {
                targetScale.z /= ScaleFactor;
            }

            currentTween = transform.DOScale(targetScale, Duration).SetDelay(Delay)
                .SetEase(Ease)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDisable()
        {
            currentTween?.Kill();
            transform.localScale = originalScale;
        }
    }
}
