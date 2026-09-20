// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/SodaGroup.cs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter.SodaConveyor
{
    public sealed class SodaGroup : MonoBehaviour
    {
        public BlockColorType colorType;
        public int rowCount = 1;
        public int laneCount = StageGroupSpec.LaneCount;
        public float laneSpacing = StageLayout.LaneSpacing;
        public float rowSpacing = StageTrackData.RowSpacing;

        public int RowCount => rowCount;
        public int LaneCount => laneCount;
        public float LaneSpacing => laneSpacing;
        public float SplineLength => rowCount * rowSpacing;

        ConveyorBlock3D[,] _items;
        int _aliveCount;

        public int AliveCount => _aliveCount;
        public bool IsEmpty => _aliveCount <= 0;

        public event Action<SodaGroup> OnGroupCleared;
        public void Initialize()
        {
            _items = new ConveyorBlock3D[rowCount, laneCount];
            _aliveCount = 0;

            foreach (var item in GetComponentsInChildren<ConveyorBlock3D>(true))
            {
                var r = item.RowIndex;
                var l = item.LaneIndex;
                if (r < 0 || r >= rowCount || l < 0 || l >= laneCount) continue;

                _items[r, l] = item;
                item.OnDestroyed += HandleItemPicked;
                _aliveCount++;
            }
        }
        public void RegisterMergedItem(ConveyorBlock3D item, int lane)
        {
            _items ??= new ConveyorBlock3D[rowCount, laneCount];
            if (_items[0, lane] == null)
            {
                _items[0, lane] = item;
                item.OnDestroyed += HandleItemPicked;
                _aliveCount++;
            }
        }

        public ConveyorBlock3D GetItem(int row, int lane)
        {
            if (_items == null) return null;
            if (row < 0 || row >= rowCount || lane < 0 || lane >= laneCount) return null;
            return _items[row, lane];
        }

        void HandleItemPicked(ConveyorBlock3D item)
        {
            item.OnDestroyed -= HandleItemPicked;
            _aliveCount--;
            if (_aliveCount <= 0)
                OnGroupCleared?.Invoke(this);
        }

        public IEnumerable<ConveyorBlock3D> AllItems()
        {
            if (_items == null) yield break;
            for (var r = 0; r < rowCount; r++)
                for (var l = 0; l < laneCount; l++)
                    if (_items[r, l] != null)
                        yield return _items[r, l];
        }
    }
}
