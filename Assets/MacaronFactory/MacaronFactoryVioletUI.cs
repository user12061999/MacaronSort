using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockShooter
{
    public sealed partial class MacaronFactory
    {
        [Header("Violet scene popups")]
        [SerializeField] private RectTransform settingsPopup, winPopup, losePopup;
        private bool _closingSettings;

        private static Button PopupButton(RectTransform popup, string name) => popup.Find("Card/" + name).GetComponent<Button>();
        private static TextMeshProUGUI PopupText(RectTransform popup, string name) => popup.Find("Card/" + name).GetComponent<TextMeshProUGUI>();
        private static void BindPopupButton(RectTransform popup, string name, UnityEngine.Events.UnityAction action)
        {
            var button = PopupButton(popup, name);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        private void RefreshSettingsLabels()
        {
            PopupButton(settingsPopup, "Sound").GetComponentInChildren<TextMeshProUGUI>().text =
                PlayerPrefs.GetInt("SoundButton", 0) == 0 ? "SOUND   ON" : "SOUND   OFF";
            PopupButton(settingsPopup, "Haptic").GetComponentInChildren<TextMeshProUGUI>().text =
                PlayerPrefs.GetInt("HapticButton", 0) == 0 ? "HAPTIC   ON" : "HAPTIC   OFF";
        }
        private static void RevealPopup(RectTransform popup)
        {
            popup.gameObject.SetActive(true);
            popup.SetAsLastSibling();
            var card = popup.Find("Card");
            card.DOKill();
            card.localScale = Vector3.one * .85f;
            card.DOScale(Vector3.one, .25f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(popup.gameObject);
        }
        public void OpenSettings()
        {
            if (settingsPopup == null || settingsPopup.gameObject.activeSelf || !GameManager.Instance.IsPlaying) return;
            GameManager.Instance.SetState(GameState.Paused);
            Time.timeScale = 0;
            _closingSettings = false;
            BindPopupButton(settingsPopup, "Close", CloseSettings);
            BindPopupButton(settingsPopup, "Resume", CloseSettings);
            BindPopupButton(settingsPopup, "Retry", Reload);
            BindPopupButton(settingsPopup, "Next", () => { Stage++; Reload(); });
            BindPopupButton(settingsPopup, "Sound", () => {
                int next = PlayerPrefs.GetInt("SoundButton", 0) == 0 ? 1 : 0;
                PlayerPrefs.SetInt("SoundButton", next);
                AudioListener.volume = next == 0 ? 1 : 0;
                if (EKStudio.Audio.AudioController.Instance != null) EKStudio.Audio.AudioController.Instance.IsMasterMuted = next != 0;
                PlayerPrefs.Save(); RefreshSettingsLabels();
            });
            BindPopupButton(settingsPopup, "Haptic", () => {
                int next = PlayerPrefs.GetInt("HapticButton", 0) == 0 ? 1 : 0;
                PlayerPrefs.SetInt("HapticButton", next);
                PlayerPrefs.SetInt("IsHapticOpen", next == 0 ? 1 : 0);
                PlayerPrefs.Save(); RefreshSettingsLabels();
            });
            RefreshSettingsLabels();
            RevealPopup(settingsPopup);
        }
        public void CloseSettings()
        {
            if (settingsPopup == null || !settingsPopup.gameObject.activeSelf || _closingSettings) return;
            _closingSettings = true;
            var card = settingsPopup.Find("Card");
            card.DOKill();
            card.DOScale(Vector3.one * .85f, .15f).SetUpdate(true).SetLink(settingsPopup.gameObject).OnComplete(() => {
                settingsPopup.gameObject.SetActive(false);
                _closingSettings = false;
                Time.timeScale = 1;
                GameManager.Instance.SetState(GameState.Playing);
            });
        }
        private void ShowOverlay(string title, string message, string button, UnityEngine.Events.UnityAction action, bool isWin = false)
        {
            var popup = isWin ? winPopup : losePopup;
            if (popup == null) { Debug.LogError("Assign Violet scene popups."); return; }
            PopupText(popup, "Title").text = title;
            PopupText(popup, "Message").text = message;
            PopupButton(popup, "Continue").GetComponentInChildren<TextMeshProUGUI>().text = button;
            if (isWin) PopupText(popup, "Reward").text = $"+{shippingReward} COINS";
            BindPopupButton(popup, "Continue", action);
            RevealPopup(popup);
        }

#if UNITY_EDITOR
        [ContextMenu("Check Violet UI references")]
        public void CheckVioletUI()
        {
            foreach (var popup in new[] { settingsPopup, winPopup, losePopup })
            {
                if (popup == null || popup.Find("Card") == null || PopupText(popup, "Title") == null)
                    throw new InvalidOperationException("Missing scene popup references.");
                foreach (var text in popup.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (text.font == null || text.rectTransform.rect.width <= 0)
                        throw new InvalidOperationException("Invalid TMP label: " + text.name);
            }
            foreach (string name in new[] { "Sound", "Haptic", "Retry", "Next", "Resume", "Close" })
                if (PopupButton(settingsPopup, name) == null) throw new InvalidOperationException(name);
            if (PopupButton(winPopup, "Continue") == null || PopupButton(losePopup, "Continue") == null || _comboTimer.fillRect == null)
                throw new InvalidOperationException("Missing action or timer.");
            Debug.Log("PASS: Violet HUD, TMP labels and popup controls are bound.");
        }
        private static Sprite VioletSprite(string path)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/" + path + ".png");
            if (sprite == null) throw new InvalidOperationException("Missing Violet sprite: " + path);
            return sprite;
        }
        [ContextMenu("Design Violet HUD and Popups")]
        public void DesignVioletUI()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Design outside Play Mode.");
            if (_hudRoot == null) BuildHud();
            hudButtonSprite = VioletSprite("Buttons/Button Violet");
            hudButtonPressedSprite = hudButtonSprite;
            hudPanelSprite = VioletSprite("Panels/Dark Panel Violet");
            hudGreenButtonSprite = VioletSprite("Buttons/Button Green");
            hudGreenButtonPressedSprite = hudGreenButtonSprite;
            hudCoinIcon = VioletSprite("Colored Icons/Coin");
            hudSettingIcon = VioletSprite("White Icons/White Gear 1");
            foreach (var text in _hudRoot.GetComponentsInChildren<TextMeshProUGUI>(true)) text.color = Color.white;
            foreach (var label in new[] { _stageText, _coins })
            {
                var image = label.transform.parent.GetComponent<Image>();
                image.sprite = hudButtonSprite; image.color = Color.white; image.type = Image.Type.Sliced;
            }
            _settingsButton.image.sprite = VioletSprite("Buttons/Misc/Small Square Button Violet");
            _settingsButton.image.color = Color.white;
            _settingsButton.transition = Selectable.Transition.ColorTint;
            _settingsButton.transform.Find("Icon").GetComponent<Image>().sprite = hudSettingIcon;
            _coins.transform.parent.Find("Coin Icon").GetComponent<Image>().sprite = hudCoinIcon;
            foreach (var plate in new[] { _comboBackplate, _statusBackplate })
            { plate.sprite = VioletSprite("Buttons/Misc/Background"); plate.color = new Color(.32f,.16f,.48f,.97f); }
            _comboText.color = new Color(1,.91f,.47f);
            _comboTimer.image.sprite = VioletSprite("Sliders/Slider Background");
            _comboTimer.image.color = Color.white;
            _comboTimer.fillRect.GetComponent<Image>().sprite = VioletSprite("Sliders/Fill Yellow");
            _comboTimer.fillRect.GetComponent<Image>().color = Color.white;
            var outline = _comboTimer.GetComponent<Outline>(); if (outline != null) DestroyImmediate(outline);
            _progress.color = new Color(.18f,.08f,.28f);
            foreach(var label in _slotLabels) label.color = new Color(.18f,.08f,.28f);
            foreach (var popup in new[] { settingsPopup, winPopup, losePopup }) if (popup != null) DestroyImmediate(popup.gameObject);
            settingsPopup = CreateVioletPopup("Settings Popup", "SETTINGS", "", "White Icons/White Gear 1", 700);
            AddVioletButton(settingsPopup, "Sound", "SOUND   ON", .69f, hudButtonSprite);
            AddVioletButton(settingsPopup, "Haptic", "HAPTIC   ON", .57f, hudButtonSprite);
            AddVioletButton(settingsPopup, "Retry", "RETRY STAGE", .45f, hudButtonSprite);
            AddVioletButton(settingsPopup, "Next", "NEXT LEVEL", .33f, hudButtonSprite);
            AddVioletButton(settingsPopup, "Resume", "RESUME", .17f, hudGreenButtonSprite);
            var close = StyledButton(settingsPopup.Find("Card"), "X", new Vector2(.85f,.91f), new Vector2(48,48), null, VioletSprite("Buttons/Button Red"));
            close.name = "Close"; close.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
            winPopup = CreateVioletPopup("Win Popup", "ORDER COMPLETE!", "All delicious macarons shipped!", "White Icons/White Trophy", 590);
            var reward = Text(winPopup.Find("Card"), $"+{shippingReward} COINS", new Vector2(.5f,.34f), new Vector2(360,55), 34);
            reward.name="Reward"; reward.color=new Color(1,.88f,.3f); reward.fontStyle=FontStyles.Bold;
            AddVioletButton(winPopup, "Continue", "NEXT STAGE", .16f, hudGreenButtonSprite);
            losePopup = CreateVioletPopup("Lose Popup", "PACKING JAM!", "All open slots are full.\nTry a different tray order.", "White Icons/White Warning", 550);
            AddVioletButton(losePopup, "Continue", "TRY AGAIN", .18f, VioletSprite("Buttons/Button Orange"));
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        private RectTransform CreateVioletPopup(string name, string title, string message, string icon, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_hudRoot.parent, false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color=new Color(.07f,.03f,.14f,.82f);
            var card=Panel(rect,new Vector2(.5f,.5f),new Vector2(600,height),hudPanelSprite);card.name="Card";
            card.GetComponent<Image>().type=Image.Type.Simple;
            var heading=Text(card.transform,title,new Vector2(.5f,.91f),new Vector2(430,60),36);heading.name="Title";heading.color=Color.white;heading.fontStyle=FontStyles.Bold;
            var badge=Panel(card.transform,new Vector2(.5f,.72f),new Vector2(78,78),VioletSprite(icon));badge.name="Emblem";badge.GetComponent<Image>().preserveAspect=true;badge.GetComponent<Image>().raycastTarget=false;
            if(name=="Settings Popup") badge.SetActive(false);
            var body=Text(card.transform,message,new Vector2(.5f,.5f),new Vector2(440,90),25);body.name="Message";body.color=new Color(.94f,.88f,1);
            go.SetActive(false);return rect;
        }
        private void AddVioletButton(RectTransform popup,string name,string label,float y,Sprite sprite)
        {
            var button=StyledButton(popup.Find("Card"),label,new Vector2(.5f,y),new Vector2(370,66),null,sprite);
            button.name=name;button.transition=Selectable.Transition.ColorTint;
            var text=button.GetComponentInChildren<TextMeshProUGUI>();text.name="Label TMP";text.color=Color.white;text.fontSize=26;text.fontStyle=FontStyles.Bold;
        }
#endif
    }
}

