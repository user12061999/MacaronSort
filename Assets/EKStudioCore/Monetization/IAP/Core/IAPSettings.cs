using System.Collections.Generic;
using UnityEngine;

namespace EKStudio.IAP
{
    /// <summary>
    /// IAP (In-App Purchase) Settings ScriptableObject
    /// Configure your IAP products here for easy management
    /// </summary>
    [CreateAssetMenu(fileName = "IAPSettings", menuName = "EKStudio/IAP Settings", order = 1)]
    public class IAPSettings : ScriptableObject
    {
        [Header("IAP System Configuration")]
        [Tooltip("Enable/Disable entire IAP system")]
        public bool enableIAP = true;

        [Header("PlayerPrefs Save Keys Mapping")]
        [Tooltip("PlayerPrefs key used for Coins in your game (e.g., Coins, TotalCoins, Gold)")]
        public string coinSaveKey = "Coins";

        [Tooltip("PlayerPrefs key used for Gems in your game (e.g., Gems, TotalGems)")]
        public string gemSaveKey = "Gems";

        [Tooltip("Whether to prepend a prefix to booster save keys (e.g. 'Booster_')")]
        public string boosterKeyPrefix = "Booster_";

        [Tooltip("List of all booster types in your game")]
        public List<string> allBoosterTypes = new List<string>();
        
        [Header("No Ads Feature")]
        [Tooltip("Enable No Ads IAP product (shows No Ads button in game)")]
        public bool enableNoAds = true;
        
        [Tooltip("Product ID for Remove Ads (must match Store product ID)\nExample: com.yourcompany.yourapp.removeads")]
        public string noAdsProductId = "com.yourcompany.yourapp.removeads";
        
        [Tooltip("Display price for No Ads (e.g., $2.99)")]
        public string noAdsPrice = "$2.99";
        
        [Header("Custom IAP Products")]
        [Tooltip("Add your custom IAP products here")]
        public List<IAPProduct> customProducts = new List<IAPProduct>();
        
        [Header("Store Configuration")]
        [Tooltip("Google Play Public Key (RSA public key from Google Play Console)\nUsed for receipt validation on Android\nLeave empty for testing, but REQUIRED for production")]
        [TextArea(3, 5)]
        public string googlePlayPublicKey = "";
        
        [Tooltip("Apple App Store Shared Secret (from App Store Connect)\nRequired for auto-renewable subscriptions\nOptional for other product types")]
        public string appleSharedSecret = "";
        
        [Header("Platform-Specific Product IDs")]
        [Tooltip("Enable if your Android and iOS products have different IDs\nExample: com.company.app.product_android vs com.company.app.product_ios")]
        public bool usePlatformSpecificIds = false;
        
        [Tooltip("Enable debug logs for IAP operations")]
        public bool debugMode = true;

        /// <summary>
        /// Get all active products (including No Ads if enabled)
        /// </summary>
        public List<IAPProduct> GetAllProducts()
        {
            List<IAPProduct> allProducts = new List<IAPProduct>();
            
            // Add No Ads product if enabled
            if (enableNoAds)
            {
                allProducts.Add(new IAPProduct
                {
                    productId = noAdsProductId,
                    productName = "Remove Ads",
                    productType = IAPProductType.NonConsumable,
                    price = noAdsPrice
                });
            }
            
            // Add custom products
            if (customProducts != null)
            {
                allProducts.AddRange(customProducts);
            }
            
            return allProducts;
        }
        
        /// <summary>
        /// Find product by ID
        /// </summary>
        public IAPProduct FindProduct(string productId)
        {
            var products = GetAllProducts();
            return products.Find(p => p.productId == productId);
        }
        
        /// <summary>
        /// Check if a product ID is valid
        /// </summary>
        public bool IsValidProductId(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return false;
                
            return FindProduct(productId) != null;
        }
    }

    /// <summary>
    /// IAP Product Type
    /// </summary>
    public enum IAPProductType
    {
        [Tooltip("One-time purchase, never expires (e.g., Remove Ads, Unlock Level Pack)")]
        NonConsumable,
        
        [Tooltip("Can be purchased multiple times (e.g., Coin Packs, Extra Lives)")]
        Consumable,
        
        [Tooltip("Recurring subscription (e.g., VIP Membership, Monthly Premium)")]
        Subscription
    }
    

    
    /// <summary>
    /// IAP Purchase Method - How can this product be obtained?
    /// </summary>
    public enum IAPPurchaseMethod
    {
        [Tooltip("Buy with real money via App Store/Google Play")]
        RealMoney,
        
        [Tooltip("Can be claimed free once, then timer-based (e.g., 1 hour cooldown)")]
        TimedFree,
        
        [Tooltip("Watch rewarded video to get this product")]
        RewardedVideo,
        
        [Tooltip("Multiple methods available (Real Money + Rewarded Video)")]
        Multiple
    }

    [System.Serializable]
    public class BoosterReward
    {
        [Tooltip("Booster type matching the save key")]
        public string boosterType = "";
        
        [Tooltip("Amount to give")]
        public int amount = 1;
    }
    
    /// <summary>
    /// IAP Product Data - Enhanced for Store System
    /// </summary>
    [System.Serializable]
    public class IAPProduct
    {
        [Header("Product Identification")]
        [Tooltip("Unique product ID (must match Store product ID)\nFormat: com.company.app.productname\nUsed for both platforms if platform-specific IDs are disabled")]
        public string productId = "com.yourcompany.yourapp.product";
        
        [Tooltip("Display name shown to players")]
        public string productName = "Product Name";
        
        [Header("Platform-Specific IDs (Optional)")]
        [Tooltip("Android-specific product ID (Google Play)\nOnly used if 'Use Platform Specific IDs' is enabled in settings")]
        public string androidProductId = "";
        
        [Tooltip("iOS-specific product ID (App Store)\nOnly used if 'Use Platform Specific IDs' is enabled in settings")]
        public string iosProductId = "";
        
        [Header("Product Configuration")]
        [Tooltip("Type of IAP product")]
        public IAPProductType productType = IAPProductType.Consumable;
        
        [Tooltip("How can this product be obtained?")]
        public IAPPurchaseMethod purchaseMethod = IAPPurchaseMethod.RealMoney;
        
        [Tooltip("Display price (e.g., $0.99, $4.99, $9.99)")]
        public string price = "$0.99";
        
        [Header("Reward Configuration")]
        [Tooltip("Coins to give on purchase (0 if not applicable)")]
        public int coinReward = 0;
        
        [Tooltip("Gems to give on purchase (0 if not applicable)")]
        public int gemReward = 0;
        
        [Tooltip("Booster rewards")]
        public BoosterReward[] boosterRewards = new BoosterReward[0];
        
        [Header("Timed Free Configuration")]
        [Tooltip("Cooldown time in seconds for TimedFree products (default: 3600 = 1 hour)")]
        public int timedFreeCooldown = 3600; // 1 hour
        
        [Header("Special Features")]
        [Tooltip("Include No Ads removal in this product")]
        public bool includesNoAds = false;
        
        /// <summary>
        /// Get the appropriate product ID for current platform
        /// </summary>
        public string GetPlatformProductId(bool usePlatformSpecific)
        {
            if (!usePlatformSpecific)
                return productId;
            
#if UNITY_ANDROID
            return string.IsNullOrEmpty(androidProductId) ? productId : androidProductId;
#elif UNITY_IOS
            return string.IsNullOrEmpty(iosProductId) ? productId : iosProductId;
#else
            return productId;
#endif
        }
    }
}

#if UNITY_EDITOR
namespace EKStudio.IAP
{
    using UnityEditor;

    [CustomPropertyDrawer(typeof(IAPProduct))]
    public class IAPProductDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get serialized properties
            SerializedProperty productId = property.FindPropertyRelative("productId");
            SerializedProperty productName = property.FindPropertyRelative("productName");
            SerializedProperty androidProductId = property.FindPropertyRelative("androidProductId");
            SerializedProperty iosProductId = property.FindPropertyRelative("iosProductId");
            SerializedProperty productType = property.FindPropertyRelative("productType");
            SerializedProperty purchaseMethod = property.FindPropertyRelative("purchaseMethod");
            SerializedProperty price = property.FindPropertyRelative("price");
            SerializedProperty coinReward = property.FindPropertyRelative("coinReward");
            SerializedProperty gemReward = property.FindPropertyRelative("gemReward");
            SerializedProperty boosterRewards = property.FindPropertyRelative("boosterRewards");
            SerializedProperty timedFreeCooldown = property.FindPropertyRelative("timedFreeCooldown");
            SerializedProperty includesNoAds = property.FindPropertyRelative("includesNoAds");

            IAPSettings settings = property.serializedObject.targetObject as IAPSettings;
            bool usePlatformSpecific = settings != null && settings.usePlatformSpecificIds;

            // Draw foldout header with product ID/name
            string headerText = string.IsNullOrEmpty(productName.stringValue) ? "New Product" : productName.stringValue;
            headerText += $" ({productId.stringValue})";
            
            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, headerText, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = position.y + EditorGUIUtility.singleLineHeight + 2;

                // Helper to draw property field
                void DrawField(SerializedProperty prop, string displayName = null)
                {
                    float height = EditorGUI.GetPropertyHeight(prop, true);
                    Rect indentRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, height));
                    
                    if (displayName != null)
                        EditorGUI.PropertyField(indentRect, prop, new GUIContent(displayName), true);
                    else
                        EditorGUI.PropertyField(indentRect, prop, true);
                    
                    yOffset += height + 2;
                }

                DrawField(productId);
                DrawField(productName);

                if (usePlatformSpecific)
                {
                    DrawField(androidProductId);
                    DrawField(iosProductId);
                }

                DrawField(productType);
                DrawField(purchaseMethod);

                IAPPurchaseMethod method = (IAPPurchaseMethod)purchaseMethod.enumValueIndex;
                if (method == IAPPurchaseMethod.RealMoney || method == IAPPurchaseMethod.Multiple)
                {
                    DrawField(price, "Store Price Fallback");
                }

                if (method == IAPPurchaseMethod.TimedFree || method == IAPPurchaseMethod.Multiple)
                {
                    DrawField(timedFreeCooldown, "Free Cooldown (s)");
                }

                DrawField(coinReward);
                DrawField(gemReward);
                DrawField(boosterRewards);
                DrawField(includesNoAds);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            float height = EditorGUIUtility.singleLineHeight + 2; // header

            SerializedProperty purchaseMethod = property.FindPropertyRelative("purchaseMethod");
            SerializedProperty boosterRewards = property.FindPropertyRelative("boosterRewards");

            IAPSettings settings = property.serializedObject.targetObject as IAPSettings;
            bool usePlatformSpecific = settings != null && settings.usePlatformSpecificIds;

            // Base fields
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("productId"), true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("productName"), true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("productType"), true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("purchaseMethod"), true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("coinReward"), true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("gemReward"), true) + 2;
            height += EditorGUI.GetPropertyHeight(boosterRewards, true) + 2;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("includesNoAds"), true) + 2;

            if (usePlatformSpecific)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("androidProductId"), true) + 2;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("iosProductId"), true) + 2;
            }

            IAPPurchaseMethod method = (IAPPurchaseMethod)purchaseMethod.enumValueIndex;
            if (method == IAPPurchaseMethod.RealMoney || method == IAPPurchaseMethod.Multiple)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("price"), true) + 2;
            }

            if (method == IAPPurchaseMethod.TimedFree || method == IAPPurchaseMethod.Multiple)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("timedFreeCooldown"), true) + 2;
            }

            return height;
        }
    }
}
#endif


