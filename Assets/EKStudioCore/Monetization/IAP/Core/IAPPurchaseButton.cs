using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;
using UnityEngine.Events;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP Purchase Button Component
    /// Drop this on any button, select a product, and it's ready to use!
    /// NO CODE NEEDED - just configure in Inspector
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class IAPPurchaseButton : MonoBehaviour
    {
        [Header("Product Selection")]
        [Tooltip("Select IAP product from your IAPSettings asset")]
        public IAPSettings iapSettings;
        
        [Tooltip("Enter Product ID (must match IAPSettings product)\nExample: com.yourcompany.yourapp.coins100")]
        public string productId;
        
        [Header("UI References (Optional)")]
        [Tooltip("TextMeshPro text for product price (will auto-update from store)")]
        public TextMeshProUGUI priceTextTMP;
        
        [Header("Events (Optional)")]
        [Tooltip("Events to trigger when the purchase or ad watch succeeds")]
        public UnityEvent onPurchaseSuccess;

        private Button button;
        private IAPProduct product;
        private float timerRemaining = 0f;
        private bool isTimerActive = false;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnPurchaseButtonClick);
            
            // Find product in settings
            if (iapSettings != null && !string.IsNullOrEmpty(productId))
            {
                product = iapSettings.FindProduct(productId);
                if (product == null)
                {
                    Debug.LogWarning($"[IAPPurchaseButton] Product '{productId}' not found in IAPSettings!");
                }
            }
            
            // Subscribe to purchase success event
            SubscribeToPurchaseEvents();
        }
        
        /// <summary>
        /// Subscribe to IAPManager purchase events
        /// </summary>
        private void SubscribeToPurchaseEvents()
        {
            IAPManager.OnPurchaseSuccess += OnPurchaseSuccessHandler;
        }

        private void OnDestroy()
        {
            IAPManager.OnPurchaseSuccess -= OnPurchaseSuccessHandler;
        }
        
        /// <summary>
        /// Handle purchase success event
        /// </summary>
        private void OnPurchaseSuccessHandler(string purchasedProductId)
        {
            // Only process if it's our product
            if (purchasedProductId == productId)
            {
                Debug.Log($"[IAPPurchaseButton] ✅ Purchase successful: {product?.productName}");
                onPurchaseSuccess?.Invoke();
            }
        }

        private void Start()
        {
            UpdateProductInfo();
            LoadTimerState(); // Load saved timer state if TimedFree product
        }

        /// <summary>
        /// Update UI with product info
        /// </summary>
        private void UpdateProductInfo()
        {
            if (product == null) return;
            
            // Update price from store (dynamic, localized)
            string storePrice = GetStorePriceForProduct(productId);
            string displayPrice = string.IsNullOrEmpty(storePrice) ? product.price : storePrice;
            
            if (priceTextTMP != null)
                priceTextTMP.text = displayPrice;
        }

        /// <summary>
        /// Update timer display for TimedFree products
        /// </summary>
        private void Update()
        {
            if (isTimerActive && product?.purchaseMethod == IAPPurchaseMethod.TimedFree)
            {
                timerRemaining -= Time.deltaTime;
                
                if (timerRemaining <= 0)
                {
                    // Timer finished, button is FREE again
                    isTimerActive = false;
                    timerRemaining = 0;
                    PlayerPrefs.DeleteKey($"TimedFreeTimer_{productId}");
                    PlayerPrefs.Save();
                    UpdateButtonUI();
                }
                else
                {
                    // Update button text with countdown
                    UpdateTimerDisplay();
                }
            }
        }

        /// <summary>
        /// Load timer state from PlayerPrefs (if product was previously claimed)
        /// </summary>
        private void LoadTimerState()
        {
            if (product == null || product.purchaseMethod != IAPPurchaseMethod.TimedFree)
                return;

            string timerKey = $"TimedFreeTimer_{productId}";
            if (PlayerPrefs.HasKey(timerKey))
            {
                long savedTime = long.Parse(PlayerPrefs.GetString(timerKey, "0"));
                long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long elapsed = currentTime - savedTime;
                
                timerRemaining = product.timedFreeCooldown - elapsed;
                
                if (timerRemaining > 0)
                {
                    isTimerActive = true;
                    button.interactable = false;
                }
                else
                {
                    // Timer expired, reset
                    PlayerPrefs.DeleteKey(timerKey);
                    PlayerPrefs.Save();
                    isTimerActive = false;
                    timerRemaining = 0;
                    button.interactable = true;
                }
            }
            else
            {
                // No timer set, button is FREE
                isTimerActive = false;
                button.interactable = true;
            }
            
            UpdateButtonUI();
        }

        /// <summary>
        /// Update timer display text
        /// </summary>
        private void UpdateTimerDisplay()
        {
            int minutes = (int)timerRemaining / 60;
            int seconds = (int)timerRemaining % 60;
            string timerText = $"{minutes:00}:{seconds:00}";
            
            if (priceTextTMP != null)
                priceTextTMP.text = timerText;
        }

        /// <summary>
        /// Update button UI based on timer state
        /// </summary>
        private void UpdateButtonUI()
        {
            if (isTimerActive)
            {
                button.interactable = false;
                UpdateTimerDisplay();
            }
            else
            {
                button.interactable = true;
                if (priceTextTMP != null)
                    priceTextTMP.text = "FREE";
            }
        }

        /// <summary>
        /// Called when button is clicked
        /// </summary>
        public void OnPurchaseButtonClick()
        {
            if (product == null)
            {
                Debug.LogError($"[IAPPurchaseButton] Cannot purchase: Product '{productId}' not configured!");
                return;
            }

            Debug.Log($"[IAPPurchaseButton] 🛒 Initiating purchase: {product.productName} ({productId})");
            
            // Handle TimedFree products
            if (product.purchaseMethod == IAPPurchaseMethod.TimedFree)
            {
                HandleTimedFreePurchase();
                return;
            }
            
            // Handle other purchase types
            HandleStandardPurchase();
        }

        /// <summary>
        /// Handle TimedFree product purchase
        /// </summary>
        private void HandleTimedFreePurchase()
        {
            // Process rewards through IAPRewardHandler (triggers events for game-specific handling)
            Debug.Log($"[IAPPurchaseButton] 🎁 Free reward claimed: {product.productName}");
            IAPRewardHandler.ProcessRewards(product);
            
            // Start cooldown timer
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString($"TimedFreeTimer_{productId}", currentTime.ToString());
            PlayerPrefs.Save();
            
            timerRemaining = product.timedFreeCooldown;
            isTimerActive = true;
            UpdateButtonUI();
            
            Debug.Log($"[IAPPurchaseButton] ⏱ Timer started: {product.timedFreeCooldown}s cooldown");
            
            onPurchaseSuccess?.Invoke();
        }

        /// <summary>
        /// Handle standard purchase (RealMoney or RewardedVideo)
        /// </summary>
        private void HandleStandardPurchase()
        {
            if (IAPManager.Instance == null)
            {
                Debug.LogError("[IAPPurchaseButton] ❌ IAPManager.Instance is null! Make sure IAPManager is initialized.");
                return;
            }
            
            Debug.Log("[IAPPurchaseButton] ✅ IAPManager found, calling PurchaseProduct...");
            IAPManager.Instance.PurchaseProduct(productId);
        }



        #region Helper Methods

        private string GetStorePriceForProduct(string prodId)
        {
            if (IAPManager.Instance != null)
            {
                return IAPManager.Instance.GetProductPrice(prodId);
            }
            return null;
        }

        #endregion
    }
}
