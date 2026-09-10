using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace EKStudio.Monetization.Ads
{
    /// <summary>
    /// Generic Ad Reward Button - Allows players to watch a rewarded ad for gameplay actions.
    /// Completely independent of the In-App Purchase store/system.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AdRewardButton : MonoBehaviour
    {
        [Header("Ad Settings")]
        [Tooltip("Optional: Cooldown in seconds before the ad can be watched again")]
        public float cooldownTime = 0f;

        [Header("UI References (Optional)")]
        [Tooltip("Optional text field to display cooldown time or loading status")]
        public TMPro.TextMeshProUGUI statusTextTMP;

        [Header("Events")]
        [Tooltip("Triggered when the ad is watched successfully (give gameplay rewards here!)")]
        public UnityEvent onAdWatchedSuccess;

        [Tooltip("Triggered if the ad fails or is cancelled")]
        public UnityEvent onAdWatchedFailed;

        [Tooltip("Triggered if there are no ads available right now")]
        public UnityEvent onAdNotAvailable;

        private Button button;
        private float cooldownTimer = 0f;
        private string saveKey;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(WatchAd);
            saveKey = "AdCooldown_" + gameObject.name + "_" + transform.position.ToString().Replace(" ", "");
        }

        private void Start()
        {
            CheckCooldown();
            UpdateUI();
        }

        private void Update()
        {
            if (cooldownTimer > 0)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0)
                {
                    cooldownTimer = 0;
                    button.interactable = true;
                    PlayerPrefs.DeleteKey(saveKey);
                    PlayerPrefs.Save();
                }
                UpdateUI();
            }
        }

        private void WatchAd()
        {
            if (!AdvertisingSystem.IsRewardBasedVideoLoaded())
            {
                Debug.LogWarning("[AdRewardButton] Ad is not loaded yet (No Ads Available)!");
                onAdNotAvailable?.Invoke();
                return;
            }

            button.interactable = false;
            AdvertisingSystem.ShowRewardBasedVideo((bool success) =>
            {
                if (success)
                {
                    Debug.Log("[AdRewardButton] Ad watched successfully!");
                    if (cooldownTime > 0)
                    {
                        StartCooldown();
                    }
                    else
                    {
                        button.interactable = true;
                    }
                    onAdWatchedSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[AdRewardButton] Ad failed or cancelled.");
                    button.interactable = true;
                    onAdWatchedFailed?.Invoke();
                }
                
                // Request a new ad for next time
                AdvertisingSystem.RequestRewardBasedVideo();
                UpdateUI();
            });
        }

        private void StartCooldown()
        {
            cooldownTimer = cooldownTime;
            button.interactable = false;
            long nextAvailableTicks = System.DateTime.Now.AddSeconds(cooldownTime).Ticks;
            PlayerPrefs.SetString(saveKey, nextAvailableTicks.ToString());
            PlayerPrefs.Save();
        }

        private void CheckCooldown()
        {
            if (PlayerPrefs.HasKey(saveKey))
            {
                long nextTicks = long.Parse(PlayerPrefs.GetString(saveKey, "0"));
                System.DateTime nextTime = new System.DateTime(nextTicks);
                if (System.DateTime.Now < nextTime)
                {
                    cooldownTimer = (float)(nextTime - System.DateTime.Now).TotalSeconds;
                    button.interactable = false;
                }
                else
                {
                    PlayerPrefs.DeleteKey(saveKey);
                    PlayerPrefs.Save();
                }
            }
        }

        private void UpdateUI()
        {
            bool adLoaded = AdvertisingSystem.IsRewardBasedVideoLoaded();
            
            // If cooldown is active
            if (cooldownTimer > 0)
            {
                button.interactable = false;
                if (statusTextTMP != null)
                {
                    int minutes = (int)cooldownTimer / 60;
                    int seconds = (int)cooldownTimer % 60;
                    statusTextTMP.text = $"{minutes:00}:{seconds:00}";
                }
            }
            // If waiting for ad to load
            else if (!adLoaded)
            {
                button.interactable = false;
                if (statusTextTMP != null)
                {
                    statusTextTMP.text = "Loading...";
                }
            }
            // Ready to watch
            else
            {
                button.interactable = true;
                if (statusTextTMP != null)
                {
                    statusTextTMP.text = "Watch Ad";
                }
            }
        }
    }
}
