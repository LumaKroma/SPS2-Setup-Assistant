using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public static class FullSetupCatalog
    {
        public static readonly string[] Categories = { "顔", "上半身", "手", "下半身", "足", "カスタム" };

        public static SetupSettings CreateDefault()
        {
            var setup = new SetupSettings();
            Add(setup, "mouth", "口", 0); Add(setup, "earLeft", "左耳", 0); Add(setup, "earRight", "右耳", 0);
            Add(setup, "nippleLeft", "左乳首", 1); Add(setup, "nippleRight", "右乳首", 1); Add(setup, "chest", "胸", 1);
            Add(setup, "handLeft", "左手", 2); Add(setup, "handRight", "右手", 2); Add(setup, "hands", "両手", 2);
            Add(setup, "vagina", "膣", 3); Add(setup, "anus", "肛門", 3); Add(setup, "thighs", "ふとももの間", 3);
            Add(setup, "footLeft", "左足", 4); Add(setup, "footRight", "右足", 4); Add(setup, "feet", "両足", 4);
            ApplyPreset(setup, 1);
            return setup;
        }

        public static void UpgradeDisplayNames(SetupSettings setup)
        {
            foreach (var part in setup.parts)
                if (!part.custom && part.id == "chest" && part.name == "胸の間") part.name = "胸";
        }

        private static void Add(SetupSettings setup, string id, string name, int category)
        {
            var part = new SocketSettings { id = id, name = name, category = category, depth = id == "mouth" };
            if (part.depth) part.actions.Add(new DepthActionSettings());
            setup.parts.Add(part);
        }

        public static void ApplyPreset(SetupSettings setup, int preset)
        {
            if (preset < 0 || preset > 2) throw new ArgumentOutOfRangeException(nameof(preset));
            foreach (var part in setup.parts)
            {
                if (part.custom) continue;
                bool casual = part.id == "mouth" || part.id == "chest" || part.category == 2 || part.id == "vagina" || part.id == "anus";
                bool fullOnly = part.id == "earLeft" || part.id == "earRight" || part.id == "thighs";
                part.included = casual || preset == 2 || (preset == 1 && !fullOnly);
            }
        }

        public static void DetectMouth(SetupSettings setup, VRCAvatarDescriptor descriptor)
        {
            var mouth = setup.parts.Find(p => p.id == "mouth");
            if (mouth == null || descriptor == null || descriptor.VisemeSkinnedMesh == null) return;
            var mesh = descriptor.VisemeSkinnedMesh.sharedMesh;
            int index = (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.oh;
            var names = descriptor.VisemeBlendShapes;
            if (mesh == null || names == null || index >= names.Length || string.IsNullOrEmpty(names[index]) || mesh.GetBlendShapeIndex(names[index]) < 0) return;
            if (mouth.actions.Count == 0) mouth.actions.Add(new DepthActionSettings());
            var action = mouth.actions[0];
            if (action.renderer != null || !string.IsNullOrEmpty(action.shape)) return;
            action.renderer = descriptor.VisemeSkinnedMesh;
            action.shape = names[index];
        }

        public static float SliderToDistance(float position)
        {
            position = Mathf.Clamp01(position);
            return position < .25f ? -Mathf.Pow((.25f - position) / .25f, 3) : 3 * Mathf.Pow((position - .25f) / .75f, 3);
        }

        public static float DistanceToSlider(float distance)
        {
            distance = Mathf.Clamp(distance, -1, 3);
            return distance < 0 ? .25f * (1 - Mathf.Pow(-distance, 1f / 3)) : .25f + .75f * Mathf.Pow(distance / 3, 1f / 3);
        }
    }
}
