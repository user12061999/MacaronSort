using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlockShooter.Editor
{
    public static class MacaronColorRegistryCheck
    {
        [MenuItem("Macaron Factory/Checks/Color registry")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run this check outside Play Mode.");
            var root = new GameObject("Color registry check");
            root.SetActive(false);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var registry = ScriptableObject.CreateInstance<ColorRegistryConfig>();
            var custom = ScriptableObject.CreateInstance<ColorRegistryConfig>();
            var shell = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red };
            var filling = new Material(shell) { color = Color.white };
            try
            {
                var factory = root.AddComponent<MacaronFactory>();
                root.GetComponent<GameManager>().config = config;
                config.colorRegistry = registry;
                registry.colors.Clear();
                registry.colors.Add(new ColorRegistryConfig.ColorDefinition {
                    colorType = BlockColorType.Red, material = shell, editorColor = Color.blue
                });
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { filling, filling };
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                Check(factory.ColorRegistry == registry && renderer.sharedMaterials[0] == shell,
                    "Cake must use the base registry material.");
                Check(renderer.sharedMaterials[1] == filling && filling.color == Color.white,
                    "Filling and source materials must stay unchanged.");
                Check(factory.FlavorColor(BlockColorType.Red) == shell.color, "Tray must match cake material.");
                factory.colorRegistry = custom;
                custom.colors.Clear();
                custom.colors.Add(new ColorRegistryConfig.ColorDefinition {
                    colorType = BlockColorType.Red, editorColor = Color.green
                });
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                var tint = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(tint, 0);
                Check(factory.ColorRegistry == custom && tint.GetColor("_BaseColor") == Color.green &&
                    factory.FlavorColor(BlockColorType.Red) == Color.green, "Override must support color without material.");
                factory.colorRegistry = null;
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                renderer.GetPropertyBlock(tint, 0);
                Check(tint.isEmpty, "Material assignment must clear the previous tint.");
                registry.colors[0].overrideColor = true;
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                renderer.GetPropertyBlock(tint, 0);
                Check(renderer.sharedMaterials[0] == shell && renderer.sharedMaterials[1] == filling &&
                    tint.GetColor("_BaseColor") == Color.blue && tint.GetColor("_Color") == Color.blue &&
                    factory.FlavorColor(BlockColorType.Red) == Color.blue && shell.color == Color.red,
                    "Cake override must use Editor Color without changing materials or filling.");
                registry.colors[0].overrideColor = false;
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                renderer.GetPropertyBlock(tint, 0);
                Check(tint.isEmpty && factory.FlavorColor(BlockColorType.Red) == Color.red,
                    "Disabling cake override must restore material color.");
                Check(factory.TrayColor(BlockColorType.Red) == shell.color, "Unconfigured tray must keep cake color.");
                registry.colors[0].trayMaterial = filling;
                factory.ApplyTrayAppearance(new[] { renderer }, BlockColorType.Red);
                Check(renderer.sharedMaterials[0] == filling && renderer.sharedMaterials[1] == filling &&
                    factory.TrayColor(BlockColorType.Red) == Color.white, "Tray must use its own material and color.");
                registry.colors[0].overrideTrayColor = true;
                registry.colors[0].trayColor = Color.yellow;
                factory.ApplyTrayAppearance(new[] { renderer }, BlockColorType.Red);
                renderer.GetPropertyBlock(tint, 0);
                Check(tint.GetColor("_BaseColor") == Color.yellow && tint.GetColor("_Color") == Color.yellow &&
                    factory.TrayColor(BlockColorType.Red) == Color.yellow && filling.color == Color.white &&
                    factory.FlavorColor(BlockColorType.Red) == Color.red, "Tray override must not modify cake or shared material.");
                registry.colors[0].trayMaterial = null;
                Check(factory.TrayColor(BlockColorType.Red) == Color.yellow, "Tray color must work without a material.");
                Check(registry.GetTrayMaterial(BlockColorType.Blue) == null &&
                    registry.GetTrayColor(BlockColorType.Blue, Color.cyan) == Color.cyan, "Missing tray entry must fall back.");
                factory.ApplyMacaronColor(renderer, BlockColorType.Red);
                config.colorRegistry = null;
                factory.macaronPrefabs[0] = root;
                Check(factory.FlavorColor(BlockColorType.Red) == shell.color, "Missing registry must retain prefab color.");
                Debug.Log("PASS: Cake registry, independent tray material/color, fallbacks and shared material preservation.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(custom);
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(filling);
            }
        }

        private static void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
        }
    }
}
