using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlockShooter.Editor
{
    public static class MacaronSimpleRoundSetup
    {
        [MenuItem("Macaron Factory/UI/Apply GUI-SimpleRound")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying the UI assets.");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new InvalidOperationException("Open the MacaronFactory scene first.");
            Undo.RecordObject(factory, "Apply GUI-SimpleRound");
            Assign(factory);
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            Debug.Log("GUI-SimpleRound assigned to MacaronFactory. Save the scene to keep the references.");
        }

        private static void Assign(MacaronFactory factory)
        {
            const string sprites = "Assets/GUI-SimpleRound/ResourceData/Sprites/Components/";
            factory.hudButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprites + "Button/Btn_Oval00_Sky_n.png");
            factory.hudButtonPressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprites + "Button/Btn_Oval00_Sky_s.png");
            factory.hudPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprites + "Popup/Popup02.png");
            factory.hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GUI-SimpleRound/ResourceData/Fonts/Quicksand-Bold SDF.asset");
            if (factory.hudButtonSprite == null || factory.hudButtonPressedSprite == null || factory.hudPanelSprite == null || factory.hudFont == null)
                throw new InvalidOperationException("Missing GUI-SimpleRound assets.");
        }

        [MenuItem("Macaron Factory/Checks/GUI-SimpleRound")]
        public static void Check()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
            var root = new GameObject("SimpleRound check");
            root.SetActive(false);
            try
            {
                var factory = root.AddComponent<MacaronFactory>();
                Assign(factory);
                var canvas = new GameObject("Canvas", typeof(Canvas));
                canvas.transform.SetParent(root.transform);
                // ShowOverlay looks up an active Canvas, so test the shared button builder directly.
                bool clicked = false;
                var method = typeof(MacaronFactory).GetMethod("Button", BindingFlags.NonPublic | BindingFlags.Instance);
                var button = (UnityEngine.UI.Button)method.Invoke(factory, new object[] {
                    canvas.transform, "RETRY", Vector2.one * .5f, new Vector2(180, 58),
                    new UnityEngine.Events.UnityAction(() => clicked = true)
                });
                var image = button.GetComponent<UnityEngine.UI.Image>();
                if (image.sprite != factory.hudButtonSprite || image.type != UnityEngine.UI.Image.Type.Sliced ||
                    button.spriteState.pressedSprite != factory.hudButtonPressedSprite ||
                    button.GetComponentInChildren<TextMeshProUGUI>(true).font != factory.hudFont)
                    throw new Exception("SimpleRound button sprite, pressed state or font was not applied.");
                button.onClick.Invoke();
                if (!clicked) throw new Exception("Button action was lost.");
                Debug.Log("PASS: GUI-SimpleRound assets, sliced button, pressed sprite, font and click action.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
