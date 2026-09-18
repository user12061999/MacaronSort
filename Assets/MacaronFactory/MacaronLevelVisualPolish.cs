using System.Collections.Generic;
using ToonShadersPro.URP;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlockShooter
{
    /// <summary>Applies the shared in-game worktop treatment without changing level layout data.</summary>
    internal static class MacaronLevelVisualPolish
    {
        private static readonly Color Floor = new(.72f, .68f, .62f);
        private static readonly Color Rim = new(.48f, .20f, .07f);
        private static readonly Color Worktop = new(1f, .88f, .65f);
        private static readonly Color SlotRim = new(.91f, .46f, .10f);
        private static readonly Color SlotInset = new(1f, .78f, .38f);
        private static readonly Shader ToonShader = Shader.Find("Toon Shaders Pro/URP/Toon");
        private const string OutlineLayerName = "Macaron Outline";

        public static void Apply(MacaronLevel level)
        {
            SetSurface(level.transform.Find("Factory floor"), Floor, .08f);
            foreach (string board in new[] { "Conveyor board", "Tray board", "Waiting bench" })
            {
                SetSurface(level.transform.Find(board + " rim"), Rim, .3f);
                SetSurface(level.transform.Find(board + " inset"), Worktop, .12f);
            }
            foreach (var slot in level.waitingSlots)
            {
                if (slot == null) continue;
                SetSurface(slot, SlotRim, .35f);
                AddInset(slot);
            }
            ConfigureOutlines(level);
        }

        public static void MarkForOutline(Transform root)
        {
            int layer = LayerMask.NameToLayer(OutlineLayerName);
            if (layer < 0 || root == null) return;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = layer;
        }

        private static void ConfigureOutlines(MacaronLevel level)
        {
            int outlineLayer = LayerMask.NameToLayer(OutlineLayerName);
            if (outlineLayer < 0) return;
            var volume = level.GetComponent<Volume>() ?? level.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var settings = volume.profile.Add<OutlineSettings>(true);
            settings.outlineType.Override(OutlineType.HullOutlines);
            settings.outlineColor.Override(new Color(.20f, .08f, .12f));
            settings.objectMask.Override(1 << outlineLayer);
            settings.lightModes.Override(new List<LightModeType> {
                LightModeType.UniversalForwardOnly, LightModeType.UniversalForward });
            settings.outlineThickness.Override(.008f);
            settings.outlineLighting.Override(false);
        }

        private static void SetSurface(Transform surface, Color color, float smoothness)
        {
            if (surface == null) return;
            foreach (var renderer in surface.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.material;
                ApplyToon(material, color, smoothness);
            }
        }

        private static void AddInset(Transform slot)
        {
            if (slot.Find("Polished inset") != null) return;
            var source = slot.GetComponent<MeshFilter>();
            var renderer = slot.GetComponent<MeshRenderer>();
            if (source == null || renderer == null || source.sharedMesh == null) return;

            var inset = new GameObject("Polished inset", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            inset.SetParent(slot, false);
            inset.localPosition = Vector3.up * .53f;
            inset.localScale = new Vector3(.78f, .25f, .72f);
            inset.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var material = new Material(renderer.material);
            ApplyToon(material, SlotInset, .42f);
            inset.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void ApplyToon(Material material, Color color, float smoothness)
        {
            if (ToonShader != null) material.shader = ToonShader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (!material.HasProperty("_LightTint")) return;
            material.SetColor("_LightTint", Color.white);
            material.SetColor("_MiddleTint", Color.gray * .78f);
            material.SetColor("_ShadowTint", Color.black);
            material.SetVector("_ShadowThresholds", new Vector4(.08f, .14f, 0, 0));
            material.SetVector("_DiffuseThresholds", new Vector4(-.03f, .03f, 0, 0));
            material.SetColor("_RimColor", new Color(1f, .88f, .65f));
            material.SetVector("_RimThresholds", new Vector4(.78f, .82f, 0, 0));
            material.SetFloat("_RimExtension", .08f);
            material.EnableKeyword("_RECEIVE_SHADOWS_ON");
        }
    }
}
