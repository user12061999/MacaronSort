using System;
using UnityEngine;
#if MACARON_MAX_ADJUST
using AdjustSdk;
#endif

/// <summary>
/// Attach to a dedicated root GameObject. Fill the current platform's ad unit IDs in Inspector.
/// Set SDK key in AppLovin > Integration Manager. Call Init() after consent, or enable Initialize On Start.
/// Adjust is optional: install Adjust 5.x and define MACARON_MAX_ADJUST before supplying its token.
/// Editor ads are MAX simulations. Use MAX test mode on a device to verify real integration.
/// TryShow returns false without callbacks if unavailable/busy. Call APIs on Unity's main thread.
/// This component owns initialization and these ad units; don't also initialize them elsewhere.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdUtil : MonoBehaviour
{
    [Header("MAX ad unit IDs for the current platform (blank = disabled)")]
    [SerializeField] private string bannerAdUnitId = "";
    [SerializeField] private string interstitialAdUnitId = "";
    [SerializeField] private string rewardedAdUnitId = "";
    [SerializeField] private bool initializeOnStart;
    [SerializeField] private bool showBannerOnStart;
    [Header("Optional Adjust 5.x")]
    [SerializeField] private string adjustAppToken = "";
    [SerializeField] private bool adjustSandbox;

    private static AdUtil _instance;
    public static bool AdjustStartRequested { get; private set; }
    public static bool IsMaxInitialized => _instance != null && _instance._ready;
    public static bool IsShowingFullScreen => _instance != null && _instance._showing != 0;
    public static bool IsInterstitialReady => IsMaxInitialized && !IsShowingFullScreen &&
        !string.IsNullOrEmpty(_instance.interstitialAdUnitId) && MaxSdk.IsInterstitialReady(_instance.interstitialAdUnitId);
    public static bool IsRewardedReady => IsMaxInitialized && !IsShowingFullScreen &&
        !string.IsNullOrEmpty(_instance.rewardedAdUnitId) && MaxSdk.IsRewardedAdReady(_instance.rewardedAdUnitId);

    private bool _started, _ready, _bannerCreated, _bannerWanted, _earned;
    private int _showing; // 0 = none, 1 = interstitial, 2 = rewarded.
    private int _interstitialRetries, _rewardedRetries;
    private double _interstitialRetryAt = double.PositiveInfinity, _rewardedRetryAt = double.PositiveInfinity;
    private Action _onReward;
    private Action<bool> _onClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { _instance = null; AdjustStartRequested = false; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        if (_instance == this && initializeOnStart) Init(adjustAppToken, adjustSandbox);
    }
    private static AdUtil GetOrCreate() => _instance != null ? _instance : new GameObject("Ads").AddComponent<AdUtil>();

    /// <summary>Alternative to Inspector configuration. Call before Init with IDs for the current OS.</summary>
    public static void ConfigureAds(string bannerId = "", string interstitialId = "", string rewardedId = "")
    {
        var ads = GetOrCreate();
        if (ads._started) throw new InvalidOperationException("Configure ad units before Init.");
        ads.bannerAdUnitId = bannerId?.Trim() ?? "";
        ads.interstitialAdUnitId = interstitialId?.Trim() ?? "";
        ads.rewardedAdUnitId = rewardedId?.Trim() ?? "";
    }

    /// <summary>Repeated calls do not register callbacks or start SDKs twice. Adjust is optional.</summary>
    public static void Init(string adjustAppToken = null, bool sandbox = false)
    {
        if (!string.IsNullOrEmpty(adjustAppToken))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(adjustAppToken, "\\A[a-zA-Z0-9]{12}\\z") ||
                string.Equals(adjustAppToken, "xxxxxxxxxxxx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Enter the real 12-character Adjust app token.", nameof(adjustAppToken));
#if MACARON_MAX_ADJUST
            if (!AdjustStartRequested && !Application.isEditor)
            {
                var config = new AdjustConfig(adjustAppToken, sandbox ? AdjustEnvironment.Sandbox : AdjustEnvironment.Production)
                { LogLevel = sandbox ? AdjustLogLevel.Verbose : AdjustLogLevel.Info, IsSendingInBackgroundEnabled = true };
                if (UnityEngine.Object.FindFirstObjectByType<Adjust>() == null)
                    new GameObject("Adjust").AddComponent<Adjust>();
                Adjust.InitSdk(config);
                AdjustStartRequested = true;
            }
#else
            throw new InvalidOperationException("Install Adjust 5.x and define MACARON_MAX_ADJUST, or call Init() without a token for MAX only.");
#endif
        }
        var ads = GetOrCreate();
        if (ads._started) return;
        ads.bannerAdUnitId = ads.bannerAdUnitId?.Trim() ?? "";
        ads.interstitialAdUnitId = ads.interstitialAdUnitId?.Trim() ?? "";
        ads.rewardedAdUnitId = ads.rewardedAdUnitId?.Trim() ?? "";
        ads._bannerWanted |= ads.showBannerOnStart;
        ads.Subscribe();
        ads._started = true;
        try
        {
            if (MaxSdk.IsInitialized()) ads.OnInitialized(null);
            else { MaxSdk.SetVerboseLogging(Debug.isDebugBuild); MaxSdk.InitializeSdk(); }
        }
        catch { ads.Unsubscribe(); ads._started = false; throw; }
    }

    public static void ShowBanner()
    {
        var ads = GetOrCreate();
        ads._bannerWanted = true;
        if (!ads._ready || string.IsNullOrEmpty(ads.bannerAdUnitId)) return;
        if (!ads._bannerCreated)
        {
            MaxSdk.CreateBanner(ads.bannerAdUnitId, new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.BottomCenter));
            MaxSdk.SetBannerBackgroundColor(ads.bannerAdUnitId, Color.black);
            MaxSdk.SetBannerExtraParameter(ads.bannerAdUnitId, "allow_pause_auto_refresh_immediately", "true");
            ads._bannerCreated = true;
        }
        MaxSdk.ShowBanner(ads.bannerAdUnitId);
        MaxSdk.StartBannerAutoRefresh(ads.bannerAdUnitId);
    }
    public static void HideBanner()
    {
        if (_instance == null) return;
        _instance._bannerWanted = false;
        if (!_instance._bannerCreated) return;
        MaxSdk.HideBanner(_instance.bannerAdUnitId);
        MaxSdk.StopBannerAutoRefresh(_instance.bannerAdUnitId);
    }

    /// <param name="onClosed">true on normal close; false on display failure.</param>
    public static bool TryShowInterstitial(Action<bool> onClosed = null, string placement = null)
    {
        if (!IsInterstitialReady) return false;
        var ads = _instance;
        ads._showing = 1;
        ads._onClosed = onClosed;
        try { MaxSdk.ShowInterstitial(ads.interstitialAdUnitId, placement); }
        catch (Exception error) { Debug.LogException(error); ads.FinishAd(false); }
        return true;
    }
    /// <param name="onReward">Called once, only when MAX confirms the reward.</param>
    /// <param name="onClosed">Whether a reward was earned; do not award again here.</param>
    public static bool TryShowRewarded(Action onReward, Action<bool> onClosed = null, string placement = null)
    {
        if (onReward == null) throw new ArgumentNullException(nameof(onReward));
        if (!IsRewardedReady) return false;
        var ads = _instance;
        ads._showing = 2;
        ads._earned = false;
        ads._onReward = onReward;
        ads._onClosed = onClosed;
        try { MaxSdk.ShowRewardedAd(ads.rewardedAdUnitId, placement); }
        catch (Exception error) { Debug.LogException(error); ads.FinishAd(false); }
        return true;
    }
    public static void ShowMediationDebugger() { if (IsMaxInitialized) MaxSdk.ShowMediationDebugger(); }

    private void OnInitialized(MaxSdkBase.SdkConfiguration config)
    {
        if (_ready) return;
        _ready = true;
        LoadInterstitial();
        LoadRewarded();
        if (_bannerWanted) ShowBanner();
    }
    private void Update()
    {
        if (!_ready) return;
        if (Time.realtimeSinceStartupAsDouble >= _interstitialRetryAt) LoadInterstitial();
        if (Time.realtimeSinceStartupAsDouble >= _rewardedRetryAt) LoadRewarded();
    }
    private void LoadInterstitial()
    {
        _interstitialRetryAt = double.PositiveInfinity;
        if (!string.IsNullOrEmpty(interstitialAdUnitId)) MaxSdk.LoadInterstitial(interstitialAdUnitId);
    }
    private void LoadRewarded()
    {
        _rewardedRetryAt = double.PositiveInfinity;
        if (!string.IsNullOrEmpty(rewardedAdUnitId)) MaxSdk.LoadRewardedAd(rewardedAdUnitId);
    }
    private void OnInterstitialLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (id != interstitialAdUnitId) return;
        _interstitialRetries = 0; _interstitialRetryAt = double.PositiveInfinity;
    }
    private void OnRewardedLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedAdUnitId) return;
        _rewardedRetries = 0; _rewardedRetryAt = double.PositiveInfinity;
    }
    private void OnInterstitialLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (id != interstitialAdUnitId) return;
        _interstitialRetries = Math.Min(6, _interstitialRetries + 1);
        _interstitialRetryAt = Time.realtimeSinceStartupAsDouble + Math.Pow(2, _interstitialRetries);
        Debug.LogWarning($"[AdUtil] Interstitial load failed: {error}");
    }
    private void OnRewardedLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (id != rewardedAdUnitId) return;
        _rewardedRetries = Math.Min(6, _rewardedRetries + 1);
        _rewardedRetryAt = Time.realtimeSinceStartupAsDouble + Math.Pow(2, _rewardedRetries);
        Debug.LogWarning($"[AdUtil] Rewarded load failed: {error}");
    }
    private void OnReward(string id, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedAdUnitId || _showing != 2 || _earned) return;
        _earned = true;
        var callback = _onReward; _onReward = null;
        try { callback?.Invoke(); } catch (Exception error) { Debug.LogException(error); }
    }
    private void OnInterstitialHidden(string id, MaxSdkBase.AdInfo info)
    { if (id == interstitialAdUnitId && _showing == 1) FinishAd(true); }
    private void OnRewardedHidden(string id, MaxSdkBase.AdInfo info)
    { if (id == rewardedAdUnitId && _showing == 2) FinishAd(_earned); }
    private void OnInterstitialDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
    {
        if (id != interstitialAdUnitId || _showing != 1) return;
        Debug.LogWarning($"[AdUtil] Interstitial display failed: {error}");
        FinishAd(false);
    }
    private void OnRewardedDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedAdUnitId || _showing != 2) return;
        Debug.LogWarning($"[AdUtil] Rewarded display failed: {error}");
        FinishAd(_earned);
    }
    private void FinishAd(bool success)
    {
        int previous = _showing;
        var callback = _onClosed;
        _showing = 0; _onClosed = null; _onReward = null; _earned = false;
        // Reload next frame outside SDK callbacks, including while Time.timeScale is zero.
        if (previous == 1) _interstitialRetryAt = Time.realtimeSinceStartupAsDouble;
        if (previous == 2) _rewardedRetryAt = Time.realtimeSinceStartupAsDouble;
        try { callback?.Invoke(success); } catch (Exception error) { Debug.LogException(error); }
    }
    private void Subscribe()
    {
        MaxSdkCallbacks.OnSdkInitializedEvent += OnInitialized;
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedLoaded;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedLoadFailed;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHidden;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnReward;
    }
    private void Unsubscribe()
    {
        MaxSdkCallbacks.OnSdkInitializedEvent -= OnInitialized;
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoaded;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailed;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHidden;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedLoaded;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedLoadFailed;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedHidden;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnReward;
    }
    private void OnDestroy()
    {
        if (_instance != this) return;
        Unsubscribe();
        if (_bannerCreated) MaxSdk.DestroyBanner(bannerAdUnitId);
        _onReward = null; _onClosed = null; _instance = null;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Macaron Factory/Checks/Ads reward callbacks")]
    public static void CheckRewardCallbacks()
    {
        var root = new GameObject("Ads callback check");
        root.SetActive(false);
        try
        {
            var ads = root.AddComponent<AdUtil>();
            ads.rewardedAdUnitId = "test-rewarded";
            ads._showing = 2;
            int rewards = 0, closes = 0;
            bool earnedOnClose = false;
            ads._onReward = () => rewards++;
            ads._onClosed = earned => { earnedOnClose = earned; closes++; };
            ads.OnReward("unrelated", default, null);
            if (rewards != 0) throw new Exception("Another ad unit granted a reward.");
            ads.OnReward("test-rewarded", default, null);
            ads.OnReward("test-rewarded", default, null);
            ads.OnRewardedHidden("test-rewarded", null);
            ads.OnRewardedHidden("test-rewarded", null);
            if (rewards != 1 || closes != 1 || !earnedOnClose || ads._showing != 0)
                throw new Exception("Duplicate reward/close, lost reward, or stale full-screen lock.");
            ads._showing = 2; ads._onReward = () => rewards++;
            ads.OnRewardedHidden("test-rewarded", null);
            ads.OnReward("test-rewarded", default, null);
            if (rewards != 1) throw new Exception("Dismissed ad granted a reward.");
            ads._showing = 2; ads._onReward = () => rewards++;
            ads.OnRewardedDisplayFailed("test-rewarded", null, null);
            ads.OnReward("test-rewarded", default, null);
            if (rewards != 1 || ads._showing != 0) throw new Exception("Failed ad granted reward or blocked future ads.");
            ads.interstitialAdUnitId = "test-interstitial";
            ads._showing = 1;
            ads._onClosed = success => { earnedOnClose = success; closes++; };
            ads.OnRewardedHidden("test-rewarded", null);
            if (ads._showing != 1) throw new Exception("Rewarded callback closed an interstitial.");
            ads.OnInterstitialHidden("test-interstitial", null);
            ads.OnInterstitialHidden("test-interstitial", null);
            if (closes != 2 || !earnedOnClose || ads._showing != 0) throw new Exception("Interstitial close callback is not exactly once.");
            Debug.Log("PASS: Reward once, ignore foreign/late callbacks, release lock on close/failure.");
        }
        finally { DestroyImmediate(root); }
    }
#endif
}
