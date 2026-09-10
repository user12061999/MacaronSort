using UnityEngine;
using System;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Reward Handler - Processes rewards from IAP purchases
    /// This system is GENERIC and can be used in any game template
    /// 
    /// Extend this class or use callbacks to handle game-specific rewards
    /// </summary>
    public static class IAPRewardHandler
    {
        // Events for custom reward processing
        public static event Action<int> OnCoinsAwarded;
        public static event Action<string, int> OnBoosterAwarded;  // (boosterType, amount)
        public static event Action<int> OnGemsAwarded;

        public static event Action OnAnyPurchaseProcessed;
        
        /// <summary>
        /// Process all rewards from a product
        /// </summary>
        public static void ProcessRewards(IAPProduct product)
        {
            if (product == null)
            {
                Debug.LogError("[IAPRewardHandler] Product is null!");
                return;
            }
            
            Debug.Log($"[IAPRewardHandler] Processing rewards for: {product.productName}");

            // Get settings mapping
            IAPSettings settings = null;
            if (IAPManager.Instance != null)
            {
                settings = IAPManager.Instance.iapSettings;
            }
            
            // Process coin reward
            if (product.coinReward > 0)
            {
                if (settings != null && !string.IsNullOrEmpty(settings.coinSaveKey))
                {
                    int current = PlayerPrefs.GetInt(settings.coinSaveKey, 0);
                    PlayerPrefs.SetInt(settings.coinSaveKey, current + product.coinReward);
                    Debug.Log($"[IAPRewardHandler] 💰 Awarded {product.coinReward} coins to '{settings.coinSaveKey}'");
                }
                else
                {
                    AwardCoins(product.coinReward);
                }
            }
            
            // Process booster rewards if configured
            if (product.boosterRewards != null && product.boosterRewards.Length > 0)
            {
                foreach (var boosterReward in product.boosterRewards)
                {
                    string boosterType = boosterReward.boosterType;
                    int amount = boosterReward.amount;

                    if (settings != null)
                    {
                        string prefix = settings.boosterKeyPrefix ?? "";
                        if (boosterType.ToLowerInvariant() == "all")
                        {
                            if (settings.allBoosterTypes != null)
                            {
                                foreach (var bType in settings.allBoosterTypes)
                                {
                                    string key = prefix + bType;
                                    int current = PlayerPrefs.GetInt(key, 0);
                                    PlayerPrefs.SetInt(key, current + amount);
                                    Debug.Log($"[IAPRewardHandler] 🔨 Awarded {amount}x '{bType}' to '{key}'");
                                }
                            }
                        }
                        else
                        {
                            string key = prefix + boosterType;
                            int current = PlayerPrefs.GetInt(key, 0);
                            PlayerPrefs.SetInt(key, current + amount);
                            Debug.Log($"[IAPRewardHandler] 🔨 Awarded {amount}x '{boosterType}' to '{key}'");
                        }
                    }
                    else
                    {
                        AwardBooster(boosterType, amount);
                    }
                }
            }
            
            // Process gem reward
            if (product.gemReward > 0)
            {
                if (settings != null && !string.IsNullOrEmpty(settings.gemSaveKey))
                {
                    int current = PlayerPrefs.GetInt(settings.gemSaveKey, 0);
                    PlayerPrefs.SetInt(settings.gemSaveKey, current + product.gemReward);
                    Debug.Log($"[IAPRewardHandler] 💎 Awarded {product.gemReward} gems to '{settings.gemSaveKey}'");
                }
                else
                {
                    AwardGems(product.gemReward);
                }
            }
            
            // Process No Ads (product includes it or matches the master no ads ID)
            bool isNoAds = product.includesNoAds || (settings != null && product.productId == settings.noAdsProductId);
            if (isNoAds)
            {
                DisableAds();
            }

            // Save player preferences immediately
            PlayerPrefs.Save();

            // Trigger event
            OnAnyPurchaseProcessed?.Invoke();
        }
        
        /// <summary>
        /// Award coins to player (MUST be handled via OnCoinsAwarded event)
        /// </summary>
        private static void AwardCoins(int amount)
        {
            Debug.Log($"[IAPRewardHandler] 💰 Awarding {amount} coins");
            
            // Invoke event for custom handling
            OnCoinsAwarded?.Invoke(amount);
            
            // Fallback: Save to PlayerPrefs if no handler registered
            if (OnCoinsAwarded == null || OnCoinsAwarded.GetInvocationList().Length == 0)
            {
                int currentCoins = PlayerPrefs.GetInt("IAP_TotalCoins", 0);
                currentCoins += amount;
                PlayerPrefs.SetInt("IAP_TotalCoins", currentCoins);
                PlayerPrefs.Save();
                
                Debug.LogWarning($"[IAPRewardHandler] ⚠ No coin handler registered! Saved to PlayerPrefs (IAP_TotalCoins={currentCoins})");
                Debug.LogWarning("[IAPRewardHandler] → Subscribe to OnCoinsAwarded event to handle coins in your game");
            }
        }
        
        /// <summary>
        /// Award booster to player (MUST be handled via OnBoosterAwarded event)
        /// </summary>
        private static void AwardBooster(string boosterType, int amount)
        {
            Debug.Log($"[IAPRewardHandler] 🔨 Awarding {amount}x {boosterType}");
            
            // Invoke event for custom handling
            OnBoosterAwarded?.Invoke(boosterType, amount);
            
            // Fallback: Save to PlayerPrefs if no handler registered
            if (OnBoosterAwarded == null || OnBoosterAwarded.GetInvocationList().Length == 0)
            {
                string key = $"IAP_Booster_{boosterType}";
                int currentCount = PlayerPrefs.GetInt(key, 0);
                currentCount += amount;
                PlayerPrefs.SetInt(key, currentCount);
                PlayerPrefs.Save();
                
                Debug.LogWarning($"[IAPRewardHandler] ⚠ No booster handler registered! Saved to PlayerPrefs ({key}={currentCount})");
                Debug.LogWarning("[IAPRewardHandler] → Subscribe to OnBoosterAwarded event to handle boosters in your game");
            }
        }
        
        /// <summary>
        /// Award gems to player (game-specific implementation via callback)
        /// </summary>
        private static void AwardGems(int amount)
        {
            Debug.Log($"[IAPRewardHandler] 💎 Awarding {amount} gems");
            
            // Invoke event for custom handling
            OnGemsAwarded?.Invoke(amount);
            
            // Default implementation - use PlayerPrefs
            if (OnGemsAwarded == null || OnGemsAwarded.GetInvocationList().Length == 0)
            {
                int currentGems = PlayerPrefs.GetInt("TotalGems", 0);
                currentGems += amount;
                PlayerPrefs.SetInt("TotalGems", currentGems);
                PlayerPrefs.Save();
                
                Debug.Log($"[IAPRewardHandler] ✅ Gems awarded successfully (Default implementation)");
            }
        }
        

        
        /// <summary>
        /// Disable ads (integrates with EKStudio.Monetization.AdvertisingSystem)
        /// </summary>
        private static void DisableAds()
        {
            Debug.Log("[IAPRewardHandler] 🚫 Disabling ads");
            
            try
            {
                Monetization.AdvertisingSystem.DisableAds();
                Debug.Log("[IAPRewardHandler] ✅ Ads disabled successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IAPRewardHandler] ⚠ Failed to disable ads: {e.Message}");
            }
        }
        
        /// <summary>
        /// Clear all event subscriptions (call when changing scenes)
        /// </summary>
        public static void ClearCallbacks()
        {
            OnCoinsAwarded = null;
            OnBoosterAwarded = null;
            OnGemsAwarded = null;
        }
    }
}
