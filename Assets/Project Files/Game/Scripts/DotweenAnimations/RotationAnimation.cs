using DG.Tweening;
using UnityEngine;

namespace EKStudio
{
    public enum RotationDirection { X, Y, Z }

    public class RotationAnimation : MonoBehaviour
    {
        public RotationDirection RotationDirection = RotationDirection.Y;
        public RotateMode RotateMode = RotateMode.FastBeyond360;
        public Ease Ease = Ease.Linear;
        public LoopType LoopType = LoopType.Restart;

        public float Distance = 360f;
        public float Duration = 1f;
        public float Delay = 0f;

        private Vector3 originalRotation;
        private Tween currentTween;

        private void Awake()
        {
            originalRotation = transform.localEulerAngles;
        }

        private void OnEnable()
        {
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            transform.localEulerAngles = originalRotation;

            Vector3 target = originalRotation;
            switch (RotationDirection)
            {
                case RotationDirection.X:
                    target.x += Distance;
                    break;
                case RotationDirection.Y:
                    target.y += Distance;
                    break;
                case RotationDirection.Z:
                    target.z += Distance;
                    break;
            }

            currentTween = transform.DOLocalRotate(target, Duration, RotateMode)
                .SetDelay(Delay)
                .SetEase(Ease)
                .SetLoops(-1, LoopType);
        }

        private void OnDisable()
        {
            currentTween?.Kill();
            transform.localEulerAngles = originalRotation;
        }
    }
}
