using UnityEngine;
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
        private GameObject _cover;
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
            _cover = factory.Part("Mystery cover", transform,
                new Vector3(center.x, center.y + size.y / 2, center.z),
                new Vector3(size.x, .04f, size.z), factory.PaperMaterial);
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
            _tint.SetColor("_BaseColor", Hidden ? Factory.PaperMaterial.color : Factory.FlavorMaterial(Color).color);
            foreach (var renderer in tintRenderers) renderer.SetPropertyBlock(_tint, 0);
            _cover.SetActive(Hidden);
            lid.gameObject.SetActive(Hidden || Shipping);
            Label.transform.localPosition = new Vector3(_collider.center.x,
                _collider.center.y + _collider.size.y / 2 + .055f,
                Hidden ? _collider.size.z * .4f : -_collider.size.z * .47f);
            Label.fontSize = Hidden ? 2.4f : 1.2f;
            Label.text = Hidden ? "?" : $"{Factory.FlavorName(Color)}  {Filled}/{Capacity}";
            Label.color = Accessible || !OnTable ? new Color(.2f, .12f, .22f) : new Color(.45f, .4f, .43f);
        }

        public void LeaveTable()
        {
            OnTable = false;
            Accessible = false;
            Moving = true;
            _collider.enabled = false;
        }

        public bool TryReserve()
        {
            if (OnTable || Moving || Shipping || Filled + Reserved >= Capacity) return false;
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
        }

        private void OnMouseDown() => Factory.TrySelect(this);
    }
}
