using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Component placed directly on the Combo Group Panel or a manager GameObject.
    /// Triggers 3D world-space combo particles when all grid and slot shooters are depleted.
    /// Prefabs are configured directly in the Inspector (no manual pool registration needed).
    /// </summary>
    public class EndGameComboManager : MonoBehaviour
    {
        public static EndGameComboManager Instance { get; private set; }

        [Header("Combo Particle Prefabs")]
        [Tooltip("Assign the 3D combo particle prefabs directly here in the Inspector.")]
        public List<ParticleSystem> comboParticlePrefabs = new List<ParticleSystem>();

        [Header("Position Settings")]
        [Tooltip("The fixed world position where the particles will spawn.")]
        public Vector3 spawnPosition = new Vector3(0f, 1.5f, 0f);

        [Tooltip("Optional transform to dynamically define the spawn position. If set, overrides spawnPosition.")]
        public Transform spawnPositionTransform;

        private ParticleSystem _lastShownComboPrefab;
        private ParticleSystem _activeComboInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            ShooterBlock.OnAnyBlockDepleted += HandleBlockDepleted;
        }

        private void OnDisable()
        {
            ShooterBlock.OnAnyBlockDepleted -= HandleBlockDepleted;
        }

        private void HandleBlockDepleted(ShooterBlock block)
        {
            // Only trigger combo if the depleted shooter was in a slot (not grid) and the grid has no InGrid shooters left
            if (block != null && IsEndGamePhase())
            {
                TriggerRandomCombo();
            }
        }

        /// <summary>
        /// Checks if we are in the end-game combo phase: grid is empty of InGrid shooters.
        /// </summary>
        public bool IsEndGamePhase()
        {
            bool gridEmpty = true;

            if (ShooterGrid.Instance != null)
            {
                foreach (var b in ShooterGrid.Instance.GetActiveBlocks())
                {
                    if (b != null && b.State == ShooterBlock.BlockState.InGrid)
                    {
                        gridEmpty = false;
                        break;
                    }
                }
            }
            else
            {
                gridEmpty = false;
            }

            return gridEmpty;
        }

        private void TriggerRandomCombo()
        {
            if (comboParticlePrefabs == null || comboParticlePrefabs.Count == 0) return;

            // Pick a random particle prefab, avoiding repeating the last shown one if possible
            List<ParticleSystem> candidates = new List<ParticleSystem>();
            foreach (var prefab in comboParticlePrefabs)
            {
                if (prefab != null) candidates.Add(prefab);
            }

            if (candidates.Count == 0) return;

            if (candidates.Count > 1 && _lastShownComboPrefab != null)
            {
                candidates.Remove(_lastShownComboPrefab);
            }

            ParticleSystem selectedPrefab = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            _lastShownComboPrefab = selectedPrefab;

            // Determine spawn position
            Vector3 finalSpawnPos = spawnPositionTransform != null ? spawnPositionTransform.position : spawnPosition;

            // Stop the previous combo if it is still playing/active
            if (_activeComboInstance != null)
            {
                _activeComboInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _activeComboInstance.gameObject.SetActive(false);
            }

            // Play the particle from the ParticlePoolManager (auto-prewarms and pools behind the scenes)
            if (ParticlePoolManager.Instance != null)
            {
                _activeComboInstance = ParticlePoolManager.Instance.Play(selectedPrefab, finalSpawnPos);
            }
            else
            {
                Debug.LogWarning("[EndGameComboManager] ParticlePoolManager.Instance is missing! Cannot play combo particles.");
            }
        }
    }
}
