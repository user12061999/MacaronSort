using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

namespace BlockShooter
{
    public class UnlockFeaturePanel : MonoBehaviour
    {
        public static UnlockFeaturePanel Instance { get; private set; }

        [Header("Feature Panels List")]
        [Tooltip("List of feature panels in order: [0] = Mystery Shooter, [1] = Freeze Shooter, [2] = Tunnel.")]
        public List<GameObject> featurePanels = new();

        public bool IsPanelOpen { get; private set; }
        private GameObject activePanel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var panel in featurePanels)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        public bool TryShowUnlockPanelForLevel(int level)
        {
            if (GameManager.Instance == null || GameManager.Instance.config == null) return false;
            var config = GameManager.Instance.config;

            int panelIndex = -1;

            // Map level thresholds to feature panel indexes (0 = Mystery, 1 = Freeze, 2 = Tunnel)
            if (level == config.mysteryShooterUnlockLevel)
            {
                panelIndex = 0;
            }
            else if (level == config.freezeShooterUnlockLevel)
            {
                panelIndex = 1;
            }
            else if (level == config.tunnelUnlockLevel)
            {
                panelIndex = 2;
            }

            if (panelIndex >= 0 && panelIndex < featurePanels.Count)
            {
                ShowPanel(panelIndex);
                return true;
            }

            return false;
        }

        private void ShowPanel(int index)
        {
            IsPanelOpen = true;
            activePanel = featurePanels[index];

            for (int i = 0; i < featurePanels.Count; i++)
            {
                if (featurePanels[i] != null)
                {
                    featurePanels[i].SetActive(i == index);
                }
            }

            if (activePanel != null)
            {
                activePanel.transform.localScale = Vector3.zero;
                activePanel.transform.DOKill();
                activePanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        public void OnContinueClicked()
        {
            IsPanelOpen = false;

            if (activePanel != null)
            {
                activePanel.transform.DOKill();
                activePanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        activePanel.SetActive(false);
                        activePanel = null;
                        ResumeGame();
                    });
            }
            else
            {
                ResumeGame();
            }
        }

        private void ResumeGame()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadNextLevel();
            }
        }
    }
}
