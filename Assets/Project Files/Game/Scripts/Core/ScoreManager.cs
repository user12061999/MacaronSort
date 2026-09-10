using System;
using UnityEngine;

namespace BlockShooter
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public int BlocksDestroyed { get; private set; }

        public static event Action<int> OnBlocksDestroyedChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddBlockDestroyed()
        {
            BlocksDestroyed++;
            OnBlocksDestroyedChanged?.Invoke(BlocksDestroyed);
        }

        public void Reset()
        {
            BlocksDestroyed = 0;
        }
    }
}
