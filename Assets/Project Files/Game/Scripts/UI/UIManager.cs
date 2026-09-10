using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace BlockShooter
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("HUD Panel")]
        public TextMeshProUGUI levelText;
        public Image progressBarFill;
        public TextMeshProUGUI progressText;
        public TextMeshProUGUI coinText;
        public Button settingsBtn;
        public GameObject settingsPanel;
        public Button settingsCloseButton;
        public Button speedBtn;
        public GameObject speedX1Group;
        public GameObject speedX2Group;

        [Header("Win Panel")]
        public GameObject winPanel;
        public TextMeshProUGUI winCoinText;
        public Button winNextButton;
        public Button winDoubleCoinButton;
        public TextMeshProUGUI winDoubleCoinText;

        [Header("Fail Panel")]
        public GameObject failPanel;
        public Button failRetryButton;
        public Button failHomeButton;

        [Header("Keep Playing Panel")]
        public GameObject keepPlayingPanel;
        public Button playOnButton;
        public Button watchAdButton;
        public Button keepPlayingCloseButton;
        public TextMeshProUGUI playOnCostText;

        [Header("Hard Level UI")]
        public GameObject sliderOut;
        public GameObject hardLevelSliderOut;
        public GameObject hardLevelOpen;

        private Color _defaultLevelTextColor;
        private bool _hasStoredDefaultTextColor = false;

        // Runtime states
        public bool HasRevivedThisLevel { get; private set; }
        public static float SpeedMultiplier { get; private set; } = 1f;
        private bool _isSpeedX2;
        private int _currentDisplayCoins;
        private Tween _coinTween;
        private int _totalConveyorBlocks;
        private float _currentDisplayProgress;
        private Tween _progressTween;

        private Vector3 _playOnButtonOriginalPosition;
        private Vector3 _coinTextOriginalPosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Reset timescales on load just in case
            Time.timeScale = 1f;
        }

        private System.Collections.IEnumerator Start()
        {
            if (playOnButton != null)
            {
                _playOnButtonOriginalPosition = playOnButton.transform.localPosition;
            }
            if (coinText != null)
            {
                _coinTextOriginalPosition = coinText.transform.localPosition;
            }

            // Reset revive state
            HasRevivedThisLevel = false;

            // Ensure speed is reset to x1 at level start
            _isSpeedX2 = false;
            SpeedMultiplier = 1f;
            if (ConveyorController.Instance != null)
            {
                ConveyorController.Instance.SetSpeedMultiplier(1f);
            }
            if (speedX1Group != null) speedX1Group.SetActive(true);
            if (speedX2Group != null)
            {
                speedX2Group.SetActive(false);
                speedX2Group.transform.localScale = Vector3.zero;
            }

            // Set dynamic button listeners in code
            settingsBtn?.onClick.AddListener(OpenSettings);
            settingsCloseButton?.onClick.AddListener(CloseSettings);
            speedBtn?.onClick.AddListener(ToggleSpeed);

            winNextButton?.onClick.AddListener(OnNextLevel);

            // Dynamically search and bind double coin button in Win panel if not assigned
            if (winDoubleCoinButton == null && winPanel != null)
            {
                Transform doubleCoinTrans = winPanel.transform.Find("DoubleCoin_BTN");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("DoubleCoinButton");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("x2Coin");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("x2Coin_BTN");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("x2_BTN");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("Reward_BTN");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("Double_BTN");
                if (doubleCoinTrans == null) doubleCoinTrans = winPanel.transform.Find("DoubleButton");

                if (doubleCoinTrans != null)
                {
                    winDoubleCoinButton = doubleCoinTrans.GetComponent<Button>();
                }
            }

            if (winDoubleCoinButton != null)
            {
                winDoubleCoinButton.onClick.AddListener(OnDoubleCoinClicked);
                
                if (winDoubleCoinText == null)
                {
                    winDoubleCoinText = winDoubleCoinButton.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            failRetryButton?.onClick.AddListener(OnRetryLevel);
            failHomeButton?.onClick.AddListener(OnHome);

            playOnButton?.onClick.AddListener(OnPlayOnClicked);
            keepPlayingCloseButton?.onClick.AddListener(OnDeclineKeepPlaying);

            // Setup level info
            if (levelText != null)
                levelText.text = SaveManager.CurrentLevel.ToString();

            // Reset panel active states
            if (settingsPanel != null)
            {
                if (!settingsPanel.TryGetComponent<EKStudio.SettingsManager>(out var settingsMgr))
                {
                    settingsPanel.AddComponent<EKStudio.SettingsManager>();
                }
                settingsPanel.SetActive(false);
            }
            winPanel?.SetActive(false);
            failPanel?.SetActive(false);
            keepPlayingPanel?.SetActive(false);

            // Initialize coins count
            UpdateCoinUI(animate: false);

            // Wait for end of frame to ensure LevelManager has fully spawned the level
            yield return new UnityEngine.WaitForEndOfFrame();

            ConfigureHardLevelUI();

            InitializeProgress();
        }

        private void ConfigureHardLevelUI()
        {
            if (levelText != null && !_hasStoredDefaultTextColor)
            {
                _defaultLevelTextColor = levelText.color;
                _hasStoredDefaultTextColor = true;
            }

            LevelRoot currentLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelRoot : null;
            bool isHard = currentLevel != null && currentLevel.isHardLevel;

            if (isHard)
            {
                // Active level is hard: show hard slider and hide normal slider
                if (sliderOut != null) sliderOut.SetActive(false);
                if (hardLevelSliderOut != null) hardLevelSliderOut.SetActive(true);

                // Change level text color to white
                if (levelText != null)
                {
                    levelText.color = Color.white;
                }

                // Activate the HardLevel_Open root group under HUD_Panel
                if (hardLevelOpen != null) hardLevelOpen.SetActive(true);
            }
            else
            {
                // Normal level: show normal slider and hide hard slider
                if (sliderOut != null) sliderOut.SetActive(true);
                if (hardLevelSliderOut != null) hardLevelSliderOut.SetActive(false);

                // Restore level text color
                if (levelText != null && _hasStoredDefaultTextColor)
                {
                    levelText.color = _defaultLevelTextColor;
                }

                // Deactivate the HardLevel_Open root group
                if (hardLevelOpen != null) hardLevelOpen.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GameManager.OnLevelWin += HandleLevelWin;
            GameManager.OnLevelFail += ShowFailPanel;
            ScoreManager.OnBlocksDestroyedChanged += UpdateProgressDisplay;
            EKStudio.IAP.IAPRewardHandler.OnAnyPurchaseProcessed += HandleIAPPurchaseProcessed;
        }

        private void OnDisable()
        {
            GameManager.OnLevelWin -= HandleLevelWin;
            GameManager.OnLevelFail -= ShowFailPanel;
            ScoreManager.OnBlocksDestroyedChanged -= UpdateProgressDisplay;
            EKStudio.IAP.IAPRewardHandler.OnAnyPurchaseProcessed -= HandleIAPPurchaseProcessed;
        }

        private void HandleIAPPurchaseProcessed()
        {
            UpdateCoinUI(animate: true);
        }

        private void InitializeProgress()
        {
            var blocks = FindObjectsByType<ConveyorBlock3D>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var b in blocks)
            {
                if (b != null && !b.IsDestroyed)
                {
                    count++;
                }
            }
            _totalConveyorBlocks = Mathf.Max(1, count);
            _currentDisplayProgress = 0f;
            SetProgress(0f, animate: false);
        }



        private void UpdateProgressDisplay(int destroyedCount)
        {
            if (_totalConveyorBlocks <= 0) return;

            // Increment in groups of 50 blocks destroyed in total
            int groupSize = 50;
            int groupsDestroyed = destroyedCount / groupSize;
            int totalGroups = _totalConveyorBlocks / groupSize;

            float progress = 0f;
            if (destroyedCount >= _totalConveyorBlocks)
            {
                progress = 1.0f; // 100% on actual completion
            }
            else if (totalGroups > 0)
            {
                progress = (float)groupsDestroyed / totalGroups;
                progress = Mathf.Clamp(progress, 0f, 0.99f); // Clamp until actually completed
            }
            else
            {
                // If total blocks is less than 50, it remains at 0% until completed
                progress = 0f;
            }

            SetProgress(progress, animate: true);
        }

        // ── HUD Panel Control ──────────────────────────────────────────────────

        public void SetProgress(float value, bool animate = true)
        {
            if (!animate)
            {
                _progressTween?.Kill();
                _currentDisplayProgress = value;
                if (progressBarFill != null)
                    progressBarFill.fillAmount = value;

                if (progressText != null)
                    progressText.text = $"{Mathf.RoundToInt(value * 100f)}%";
                return;
            }

            _progressTween?.Kill();
            _progressTween = DOTween.To(() => _currentDisplayProgress, x =>
            {
                _currentDisplayProgress = x;
                if (progressBarFill != null)
                    progressBarFill.fillAmount = _currentDisplayProgress;

                if (progressText != null)
                    progressText.text = $"{Mathf.RoundToInt(_currentDisplayProgress * 100f)}%";
            }, value, 0.6f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void UpdateCoinUI(bool animate = true)
        {
            int targetCoins = SaveManager.Coins;
            if (!animate || coinText == null)
            {
                _currentDisplayCoins = targetCoins;
                if (coinText != null) coinText.text = _currentDisplayCoins.ToString();
                return;
            }

            _coinTween?.Kill();
            _coinTween = DOTween.To(() => _currentDisplayCoins, x =>
            {
                _currentDisplayCoins = x;
                coinText.text = _currentDisplayCoins.ToString();
            }, targetCoins, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void ToggleSpeed()
        {
            _isSpeedX2 = !_isSpeedX2;
            SpeedMultiplier = _isSpeedX2 ? 2f : 1f;

            if (ConveyorController.Instance != null)
            {
                ConveyorController.Instance.UpdateConveyorSpeed();
            }

            // Subtle mechanical punch on the parent button
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.05f, 0.15f, 1, 0.5f).SetUpdate(true);

            if (_isSpeedX2)
            {
                // Transition to X2
                if (speedX1Group != null) speedX1Group.SetActive(false);
                if (speedX2Group != null)
                {
                    speedX2Group.SetActive(true);
                    speedX2Group.transform.DOKill();
                    speedX2Group.transform.localScale = Vector3.one * 0.8f;
                    speedX2Group.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
            else
            {
                // Transition to X1
                if (speedX2Group != null) speedX2Group.SetActive(false);
                if (speedX1Group != null)
                {
                    speedX1Group.SetActive(true);
                    speedX1Group.transform.DOKill();
                    speedX1Group.transform.localScale = Vector3.one * 0.8f;
                    speedX1Group.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
        }

        // ── Settings Panel Control ─────────────────────────────────────────────

        private void OpenSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.transform.DOKill();
            settingsPanel.SetActive(true);
            settingsPanel.transform.localScale = Vector3.one;

            // Make sure all animators in the settings panel run on unscaled time so they aren't paused by Time.timeScale = 0
            var anims = settingsPanel.GetComponentsInChildren<Animator>(true);
            foreach (var anim in anims)
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            Time.timeScale = 0f;
            GameManager.Instance?.SetState(GameState.Paused);
        }

        private void CloseSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    settingsPanel.SetActive(false);
                    Time.timeScale = 1f;
                    GameManager.Instance?.SetState(GameState.Playing);
                });
        }

        // ── Win Panel Control ──────────────────────────────────────────────────

        private void ShowWinPanel()
        {
            if (winPanel == null) return;

            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                if (winCoinText != null)
                    winCoinText.text = $"+{GameManager.Instance.config.winRewardCoins}";

                // Double coin button check
                int completedLevel = SaveManager.CurrentLevel - 1;
                bool enableDoubleCoin = GameManager.Instance.config.enableDoubleCoinFeature;
                int unlockLevel = GameManager.Instance.config.doubleCoinUnlockLevel;

                if (winDoubleCoinButton != null)
                {
                    bool shouldShowDouble = enableDoubleCoin && (completedLevel >= unlockLevel);
                    winDoubleCoinButton.gameObject.SetActive(shouldShowDouble);
                    winDoubleCoinButton.interactable = shouldShowDouble;

                    // Add CanvasGroup dynamically if missing, and use it to control visibility/interactability to bypass Animator keyframe overrides
                    var cg = winDoubleCoinButton.GetComponent<CanvasGroup>();
                    if (cg == null) cg = winDoubleCoinButton.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = shouldShowDouble ? 1f : 0f;
                    cg.interactable = shouldShowDouble;
                    cg.blocksRaycasts = shouldShowDouble;

                    if (shouldShowDouble && winDoubleCoinText != null)
                    {
                        winDoubleCoinText.text = $"+{GameManager.Instance.config.winRewardCoins * 2}";
                    }
                }
            }

            winPanel.SetActive(true);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnDoubleCoinClicked()
        {
            if (GameManager.Instance == null || GameManager.Instance.config == null) return;

            // Prevent multiple clicks while showing ad
            if (winDoubleCoinButton != null)
            {
                winDoubleCoinButton.interactable = false;
                var cg = winDoubleCoinButton.GetComponent<CanvasGroup>();
                if (cg != null) cg.interactable = false;
            }

            EKStudio.Monetization.AdvertisingSystem.ShowRewardBasedVideo(success =>
            {
                if (success)
                {
                    // Reward the player with another 1x of the win reward coins (making it 2x total with the auto reward)
                    SaveManager.Coins += GameManager.Instance.config.winRewardCoins;
                    UpdateCoinUI(animate: true);

                    // Disable/Hide button after reward
                    if (winDoubleCoinButton != null)
                    {
                        winDoubleCoinButton.gameObject.SetActive(false);
                        var cg = winDoubleCoinButton.GetComponent<CanvasGroup>();
                        if (cg != null)
                        {
                            cg.alpha = 0f;
                            cg.interactable = false;
                            cg.blocksRaycasts = false;
                        }
                    }
                }
                else
                {
                    // Re-enable button if the ad failed or was cancelled
                    if (winDoubleCoinButton != null)
                    {
                        winDoubleCoinButton.interactable = true;
                        var cg = winDoubleCoinButton.GetComponent<CanvasGroup>();
                        if (cg != null)
                        {
                            cg.alpha = 1f;
                            cg.interactable = true;
                            cg.blocksRaycasts = true;
                        }
                    }

                    if (WarningManager.Instance != null)
                    {
                        WarningManager.Instance.ShowWarning("Ad not available. Try again later.");
                    }
                }
            });
        }

        private void HandleLevelWin()
        {
            ShowWinPanel();
        }

        private void OnNextLevel()
        {
            DOTween.KillAll();
            if (UnlockFeaturePanel.Instance != null && UnlockFeaturePanel.Instance.TryShowUnlockPanelForLevel(SaveManager.CurrentLevel))
            {
                winPanel?.SetActive(false);
            }
            else
            {
                LevelManager.Instance?.LoadNextLevel();
            }
        }

        private void OnHome()
        {
            DOTween.KillAll();
            LevelManager.Instance?.LoadMainMenu();
        }

        // ── Fail Panel Control ─────────────────────────────────────────────────

        private void ShowFailPanel()
        {
            if (failPanel == null) return;

            // Play fail sound when the fail overlay appears
            EKStudio.Audio.AudioHelper.PlaySound("FailSound");

            failPanel.SetActive(true);
            failPanel.transform.localScale = Vector3.zero;
            failPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnRetryLevel()
        {
            DOTween.KillAll();
            LevelManager.Instance?.RestartLevel();
        }

        // ── Keep Playing Panel (Revival) Control ──────────────────────────────

        public void ShowKeepPlayingPanel()
        {
            if (keepPlayingPanel == null) return;

            if (playOnButton != null)
            {
                playOnButton.transform.DOKill();
                playOnButton.transform.localPosition = _playOnButtonOriginalPosition;
            }
            
            if (playOnCostText != null)
            {
                int cost = 100;
                if (GameManager.Instance != null && GameManager.Instance.config != null)
                {
                    cost = GameManager.Instance.config.playOnCost;
                }
                playOnCostText.text = cost.ToString();
            }
            
            // Play fail sound when the keep playing overlay appears
            EKStudio.Audio.AudioHelper.PlaySound("FailSound");

            keepPlayingPanel.SetActive(true);
            keepPlayingPanel.transform.localScale = Vector3.zero;
            keepPlayingPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnPlayOnClicked()
        {
            int cost = 100;
            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                cost = GameManager.Instance.config.playOnCost;
            }

            if (SaveManager.Coins >= cost)
            {
                SaveManager.Coins -= cost;
                UpdateCoinUI(animate: true);

                // Add an extra slot to let the user keep playing
                if (SlotSystem.Instance != null)
                {
                    SlotSystem.Instance.AddExtraSlot();
                }

                // Hide KeepPlaying panel
                keepPlayingPanel.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        keepPlayingPanel.SetActive(false);
                        
                        // Mark revived so they cannot revive again
                        HasRevivedThisLevel = true;

                        // Unfreeze conveyor & resume
                        if (ConveyorController.Instance != null)
                        {
                            ConveyorController.Instance.IsFrozen = false;
                        }

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.SetState(GameState.Playing);
                        }
                    });
            }
            else
            {
                // Shake KeepPlaying window or button to show insufficient coins
                if (playOnButton != null)
                {
                    playOnButton.transform.DOKill();
                    playOnButton.transform.localPosition = _playOnButtonOriginalPosition;
                    playOnButton.transform.DOPunchPosition(Vector3.right * 15f, 0.3f, 5, 0.5f).SetUpdate(true);
                }

                // Show warning popup
                if (WarningManager.Instance != null)
                {
                    WarningManager.Instance.ShowWarning("Not enough coins!");
                }
            }
        }

        public void ReviveAndContinue()
        {
            // Add an extra slot to let the user keep playing
            if (SlotSystem.Instance != null)
            {
                SlotSystem.Instance.AddExtraSlot();
            }

            // Hide KeepPlaying panel
            if (keepPlayingPanel != null)
            {
                keepPlayingPanel.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        keepPlayingPanel.SetActive(false);
                        
                        // Mark revived so they cannot revive again
                        HasRevivedThisLevel = true;

                        // Unfreeze conveyor & resume
                        if (ConveyorController.Instance != null)
                        {
                            ConveyorController.Instance.IsFrozen = false;
                        }

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.SetState(GameState.Playing);
                        }
                    });
            }
        }

        public void ShowNoAdsWarning()
        {
            if (WarningManager.Instance != null)
            {
                WarningManager.Instance.ShowWarning("No ads available right now!");
            }
        }

        private void OnDeclineKeepPlaying()
        {
            // Decline revive, hide panel and trigger actual level fail
            keepPlayingPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    keepPlayingPanel.SetActive(false);
                    GameManager.Instance?.TriggerFail();
                });
        }

        public void PunchCoinText()
        {
            if (coinText != null)
            {
                coinText.transform.DOKill();
                coinText.transform.localPosition = _coinTextOriginalPosition;
                coinText.transform.DOPunchPosition(Vector3.right * 10f, 0.25f, 5, 0.5f).SetUpdate(true);
            }
        }
    }
}
