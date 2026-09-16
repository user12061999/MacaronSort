using UnityEngine;
using DG.Tweening;
using TMPro;

namespace BlockShooter
{
    public sealed class MacaronTray : MonoBehaviour
    {
        [Header("Level authoring")]
        public BlockColorType levelColor = BlockColorType.Red;
        [Min(0)] public int stackLayer;
        public bool mystery;
        [Header("Authored prefab poses")]
        public Transform[] pockets;
        public Transform lid;
        public Renderer[] tintRenderers;
        public MacaronFactory Factory { get; private set; }
        public BlockColorType Color { get; private set; }
        public int Capacity => pockets.Length;
        public int Filled { get; private set; }
        public int Reserved { get; private set; }
        public int Layer { get; private set; }
        public bool Hidden { get; private set; }
        public bool OnTable { get; private set; } = true;
        public bool Moving { get; set; }
        public bool Shipping { get; set; }
        public bool Accessible { get; private set; }
        public Rect Footprint { get; private set; }
        public TextMeshPro Label { get; private set; }
        public Transform Lid => lid;
        public Vector3 ClosedLidPosition { get; private set; }
        private Renderer[] _lidRenderers;
        private Material[][] _lidMaterials;
        private Renderer[] _interiorRenderers;
        private BoxCollider _collider;
        private MaterialPropertyBlock _tint;
        private readonly Vector2[] _corners = new Vector2[4];

        public void Initialize(MacaronFactory factory, BlockColorType color,
            int layer, bool hidden, Vector3 position)
        {
            if (pockets == null || pockets.Length == 0 || System.Array.Exists(pockets, p => p == null) || lid == null)
                throw new System.InvalidOperationException("Tray prefab needs its authored pockets and lid.");
            Factory = factory;
            _tint = new MaterialPropertyBlock();
            Color = color;
            Layer = layer;
            Hidden = hidden;
            transform.localPosition = position;
            _collider = GetComponent<BoxCollider>();
            var center = _collider.center;
            var size = _collider.size;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = transform.TransformPoint(center + new Vector3(
                    (i == 0 || i == 3 ? -.5f : .5f) * size.x, 0, (i < 2 ? -.5f : .5f) * size.z));
                _corners[i] = new Vector2(corner.x, corner.z);
            }
            Vector2 min = _corners[0], max = min;
            foreach (var corner in _corners) { min = Vector2.Min(min, corner); max = Vector2.Max(max, corner); }
            Footprint = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            ClosedLidPosition = lid.localPosition;
            factory.ApplyTrayAppearance(tintRenderers, color);
            _lidRenderers = lid.GetComponentsInChildren<Renderer>(true);
            _interiorRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name.StartsWith("Macaron_Row"));
            _lidMaterials = new Material[_lidRenderers.Length][];
            for (int i = 0; i < _lidRenderers.Length; i++) _lidMaterials[i] = _lidRenderers[i].sharedMaterials;
            Label = factory.WorldText("Tray label", transform, Vector3.zero, "", .12f);
            Refresh(false);
        }

        public Transform GetPocket(int index) => pockets[index];

        public static bool Blocks(Rect above, int aboveLayer, Rect below, int belowLayer)
            => aboveLayer > belowLayer && above.Overlaps(below);

        public bool IsBlockedBy(MacaronTray above)
        {
            if (!above.OnTable || !Blocks(above.Footprint, above.Layer, Footprint, Layer)) return false;
            return OverlapsOnAxes(_corners, above._corners) && OverlapsOnAxes(above._corners, _corners);
        }

        private static bool OverlapsOnAxes(Vector2[] first, Vector2[] second)
        {
            for (int edge = 0; edge < 2; edge++)
            {
                Vector2 direction = first[edge + 1] - first[edge];
                Vector2 axis = new Vector2(-direction.y, direction.x).normalized;
                float minA = float.PositiveInfinity, maxA = float.NegativeInfinity;
                float minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
                for (int i = 0; i < 4; i++)
                {
                    float a = Vector2.Dot(first[i], axis), b = Vector2.Dot(second[i], axis);
                    minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
                    minB = Mathf.Min(minB, b); maxB = Mathf.Max(maxB, b);
                }
                if (maxA <= minB + .001f || maxB <= minA + .001f) return false;
            }
            return true;
        }

        public void Refresh(bool accessible)
        {
            Accessible = accessible && OnTable;
            if (Accessible) Hidden = false;
            var color = Hidden ? Factory.PaperMaterial.color : Factory.TrayColor(Color);
            if (OnTable && !Accessible)
                color = UnityEngine.Color.Lerp(color, new UnityEngine.Color(0, 0, 0, color.a), Mathf.Clamp01(Factory.coveredTrayDarkness));
            _tint.SetColor("_BaseColor", color);
            _tint.SetColor("_Color", color);
            foreach (var renderer in tintRenderers) renderer.SetPropertyBlock(_tint, 0);
            var lining = UnityEngine.Color.Lerp(Hidden ? Factory.PaperMaterial.color : Factory.TrayColor(Color),
                new UnityEngine.Color(1f, .95f, .85f), .6f);
            if (OnTable && !Accessible) lining *= 1 - Mathf.Clamp01(Factory.coveredTrayDarkness);
            lining.a = 1;
            _tint.SetColor("_BaseColor", lining);
            _tint.SetColor("_Color", lining);
            foreach (var renderer in _interiorRenderers) renderer.SetPropertyBlock(_tint, 0);
            _tint.SetColor("_BaseColor", color);
            _tint.SetColor("_Color", color);
            // Use the actual lid with opaque paper while hidden, including its window.
            for (int r = 0; r < _lidRenderers.Length; r++)
            {
                var materials = (Material[])_lidMaterials[r].Clone();
                for (int i = 0; i < materials.Length; i++)
                {
                    if (Hidden) materials[i] = Factory.PaperMaterial;
                    _lidRenderers[r].SetPropertyBlock(Hidden ? _tint : null, i);
                }
                _lidRenderers[r].sharedMaterials = materials;
            }
            foreach (var renderer in tintRenderers) renderer.SetPropertyBlock(_tint, 0);
            lid.gameObject.SetActive(Hidden || Shipping);
            Label.transform.localPosition = new Vector3(_collider.center.x,
                _collider.center.y + _collider.size.y / 2 + .055f,
                Hidden ? _collider.center.z : _collider.center.z - _collider.size.z * .4f);
            Label.fontSize = Hidden ? 3.4f : 1.6f;
            Label.text = Hidden ? "?" : $"{Filled}/{Capacity}";
            Label.color = Accessible || !OnTable ? new Color(.2f, .12f, .22f) : new Color(.45f, .4f, .43f);
            Label.gameObject.SetActive(false);
        }

        public void LeaveTable()
        {
            OnTable = false;
            Accessible = false;
            Moving = true;
            _collider.enabled = false;
        }

        public bool CanReceive => !OnTable && !Moving && !Shipping && Filled + Reserved < Capacity;

        public bool TryReserve()
        {
            if (!CanReceive) return false;
            Reserved++;
            return true;
        }

        public void CancelReservation() => Reserved = Mathf.Max(0, Reserved - 1);

        public void Receive()
        {
            if (Reserved <= 0) throw new System.InvalidOperationException("Macaron arrival has no reserved pocket.");
            Reserved--;
            Filled++;
            Refresh(false);
            PlayReceiveBounce();
        }

        private Tween _receiveBounce;
        private Vector3 _receiveRestScale;

        private void PlayReceiveBounce()
        {
            if (_receiveBounce != null && _receiveBounce.IsActive()) _receiveBounce.Kill();
            else _receiveRestScale = transform.localScale;
            float duration = Mathf.Max(.01f, Factory.trayReceiveBounceTime);
            // Repeated arrivals reuse the resting scale, never multiply the previous bounce.
            _receiveBounce = DOTween.Sequence().SetLink(gameObject)
                .Append(transform.DOScale(_receiveRestScale * Mathf.Max(1, Factory.trayReceiveScaleMultiplier), duration * .35f).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(_receiveRestScale, duration * .65f).SetEase(Ease.InOutQuad))
                .OnComplete(() => transform.localScale = _receiveRestScale);
        }

        private void OnDisable() => StopReceiveBounce();

        public void StopReceiveBounce()
        {
            if (_receiveBounce == null || !_receiveBounce.IsActive()) return;
            _receiveBounce.Kill();
            transform.localScale = _receiveRestScale;
        }

        private Tween _clickFeedback;
        private Quaternion _clickRotation;

        public void PlayInvalidClick(float duration, float angle)
        {
            if (_clickFeedback != null && _clickFeedback.IsActive()) return;
            _clickRotation = transform.localRotation;
            _clickFeedback = transform.DOPunchRotation(new Vector3(0, Mathf.Max(0, angle), 0),
                    Mathf.Max(.01f, duration), 8, .5f)
                .SetLink(gameObject).OnComplete(() => transform.localRotation = _clickRotation);
        }

        public void StopClickFeedback()
        {
            if (_clickFeedback == null || !_clickFeedback.IsActive()) return;
            _clickFeedback.Kill();
            transform.localRotation = _clickRotation;
            _clickFeedback = null;
        }

        private void OnMouseDown()
        {
            if (Factory != null) Factory.TrySelect(this);
        }
    }
}
