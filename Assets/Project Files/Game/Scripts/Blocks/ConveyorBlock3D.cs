using System;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    public enum ConveyorItemPhase
    {
        OnBranch,
        OnLoop,
        Picked,
    }

    /// <summary>
    /// A single 3D colored block/macaron sitting on the conveyor track.
    /// Destroyed when hit by projectile (BlockShooter) or collected into tray (MacaronSort).
    /// </summary>
    public class ConveyorBlock3D : MonoBehaviour
    {
        [Header("Visuals")]
        public MeshRenderer blockRenderer;

        private BlockColorType _colorType;
        private bool _isDestroyed;
        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

        public BlockColorType ColorType => _colorType;
        public bool IsDestroyed => _isDestroyed;
        public bool IsTargeted { get; private set; }
        public bool HasEnteredFireRange { get; private set; }

        public void SetTargeted(bool v) => IsTargeted = v;
        public void MarkEnteredFireRange() => HasEnteredFireRange = true;

        public ConveyorItemPhase Phase { get; set; } = ConveyorItemPhase.OnLoop;
        public float PathT { get; set; }

        [HideInInspector] public Vector3 transitionOffset = Vector3.zero;
        [HideInInspector] public Quaternion transitionRotOffset = Quaternion.identity;

        [HideInInspector] public Vector3 jumpStartPos;
        [HideInInspector] public Quaternion jumpStartRot = Quaternion.identity;
        [HideInInspector] public float jumpProgress = 1f;

        public Vector3 JumpStartPos { get => jumpStartPos; set => jumpStartPos = value; }
        public Quaternion JumpStartRot { get => jumpStartRot; set => jumpStartRot = value; }
        public float JumpProgress { get => jumpProgress; set => jumpProgress = value; }

        [SerializeField] private int _rowIndex;
        [SerializeField] private int _laneIndex;
        public int RowIndex => _rowIndex;
        public int LaneIndex => _laneIndex;

        public void SetGroupIndex(int row, int lane) { _rowIndex = row; _laneIndex = lane; }

        public event Action<ConveyorBlock3D> OnDestroyed;

        private void Awake()
        {
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<BoxCollider>();
                col.size = Vector3.one * 0.9f;
            }
            if (GetComponent<Rigidbody>() == null)
            {
                var rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public void Initialize(BlockColorType colorType, Color color)
        {
            _colorType = colorType;
            _isDestroyed = false;
            Phase = ConveyorItemPhase.OnLoop;

            if (blockRenderer != null)
            {
                var mat = GameManager.Instance?.config?.GetMaterial(colorType);
                if (mat != null)
                {
                    blockRenderer.sharedMaterial = mat;
                    blockRenderer.SetPropertyBlock(null);
                }
                else
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor(ColorProp, color);
                    blockRenderer.SetPropertyBlock(mpb);
                }
            }
        }

        public void TakeHit()
        {
            if (_isDestroyed) return;
            DestroyBlock();
        }

        public void TriggerDestroy()
        {
            if (_isDestroyed) return;
            DestroyBlock();
        }

        public bool TryCollect()
        {
            if (_isDestroyed || IsTargeted) return false;
            DestroyBlock(collected: true);
            return true;
        }

        public void SetPicked()
        {
            Phase = ConveyorItemPhase.Picked;
            TryCollect();
        }

        private void DestroyBlock(bool collected = false)
        {
            _isDestroyed = true;
            Phase = ConveyorItemPhase.Picked;

            if (collected)
            {
                OnDestroyed?.Invoke(this);
                return;
            }

            ScoreManager.Instance?.AddBlockDestroyed();

            if (ShooterGrid.Instance != null)
            {
                var activeBlocks = ShooterGrid.Instance.GetActiveBlocks();
                for (int i = 0; i < activeBlocks.Count; i++)
                {
                    var sb = activeBlocks[i];
                    if (sb != null && sb.TryGetComponent<FreezeBlockFeature>(out var f))
                    {
                        if (f.isFrozen)
                        {
                            f.DecrementCount();
                        }
                    }
                }
            }

            OnDestroyed?.Invoke(this);

            if (GameManager.Instance != null)
                GameManager.Instance.CheckWinCondition();

            transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void OnDisable()
        {
            DOTween.Kill(transform);
        }
    }
}
