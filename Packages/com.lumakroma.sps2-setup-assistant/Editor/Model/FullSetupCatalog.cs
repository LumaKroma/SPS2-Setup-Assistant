using System;
using LumaKroma.Sps2SetupAssistant.Editor.Localization;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public static class FullSetupCatalog
    {
        public static readonly string[] Categories = { "顔", "上半身", "手", "下半身", "足", "カスタム" };

        public static SetupSettings CreateDefault()
        {
            var setup = new SetupSettings { displayNamesVersion = 1, menuLanguage = Sps2Localization.DefaultLanguage,
                modularAvatar = AttachmentBackendRegistry.IsAvailable(AttachmentBackend.VrcFuryWithModularAvatar) };
            Add(setup, "mouth", "口", 0); Add(setup, "earLeft", "左耳", 0); Add(setup, "earRight", "右耳", 0);
            Add(setup, "nippleLeft", "左乳首", 1); Add(setup, "nippleRight", "右乳首", 1); Add(setup, "chest", "胸", 1);
            Add(setup, "handLeft", "左手", 2); Add(setup, "handRight", "右手", 2); Add(setup, "hands", "両手", 2);
            Add(setup, "vagina", "膣", 3); Add(setup, "anus", "肛門", 3); Add(setup, "thighs", "ふとももの間", 3);
            Add(setup, "footLeft", "左足", 4); Add(setup, "footRight", "右足", 4); Add(setup, "feet", "両足", 4);
            EnsureFingerRings(setup);
            ApplyPreset(setup, 1);
            return setup;
        }

        public static void UpgradeDisplayNames(SetupSettings setup)
        {
            SocketDisplayNames.Upgrade(setup);
            EnsureFingerRings(setup);
        }

        public static void EnsureFingerRings(SetupSettings setup)
        {
            foreach (var id in new[] { "fingerRingLeft", "fingerRingRight" })
                if (!setup.parts.Exists(p => p.id == id))
                    setup.parts.Add(new SocketSettings { id = id, name = id == "fingerRingLeft" ? "左手の指わっか" : "右手の指わっか", category = 2, included = false });
        }

        private static void Add(SetupSettings setup, string id, string name, int category)
        {
            var part = new SocketSettings { id = id, name = name, category = category, depth = id == "mouth" };
            if (part.depth) part.actions.Add(new DepthActionSettings());
            setup.parts.Add(part);
        }

        // Same predicate as preset 0, including its early finger-ring and custom exclusions.
        public static bool IsCasualPart(SocketSettings part) => !part.custom && !part.IsFingerRing &&
            (part.id == "mouth" || part.id == "chest" || part.category == 2 || part.id == "vagina" || part.id == "anus");

        public static void InitializeSps3Controls(SetupSettings setup)
        {
            if (setup.sps3PolicyVersion != 0) return;
            foreach (var part in setup.parts) part.sps3AutoParticipant = setup.Sps3IncludeInAuto(part);
            setup.sps3AutoAvailable = true;
            setup.sps3AutoDefaultOn = setup.sps3AutoSaved = setup.sps3SaveSockets = false;
            setup.sps3LegacyAvailable = setup.sps3LegacyDefaultOn = setup.legacy;
            setup.sps3LegacySaved = false;
            setup.sps3PolicyVersion = 1;
        }

        public static void ApplySps3Preset(SetupSettings setup, int preset)
        {
            // Explicit user action only. Retain names, depth actions, targets and custom inclusion.
            // Auto participation is recommended for every part, including existing custom parts.
            ApplyPreset(setup, preset);
            InitializeSps3AutoDefaults(setup, true);
        }

        internal static void InitializeSps3AutoDefaults(SetupSettings setup, bool allowParticipation)
        {
            InitializeSps3Controls(setup);
            setup.BeginExplicitAutoSelection();
            setup.sps3AutoAvailable = setup.sps3AutoDefaultOn = true;
            foreach (var part in setup.parts)
                part.sps3AutoParticipant = allowParticipation && part.included && IsCasualPart(part);
        }

        public static SetupSettings CreateSps3Default()
        {
            var setup = CreateDefault();
            ApplySps3Preset(setup, 1);
            // New drafts prefer guided paths only when this reviewed Fury contract supports them.
            // Existing settings are copied, so explicit user choices are not overwritten.
            setup.penetration = VrcFuryCapabilities.ForAuthoring(SpsFamily.VrcFury, SpsAuthoringContract.Sps3Beta14).Path;
            foreach (var part in setup.parts) InitializeNewSps3Depth(part);
            return setup;
        }

        internal static void InitializeNewSps3Depth(SocketSettings part, bool keepRange = false, bool keepUnits = false)
        {
            part.InitializeAnimationGroups();
            foreach (var group in part.animationGroups) {
                var depth = SocketAnimationSettings.NewDepthForPart(part.id, part.custom);
                if (keepRange) depth.range = part.range;
                if (keepUnits) depth.units = part.units;
                group.depthSettings = depth;
            }
        }

        public static void ApplyPreset(SetupSettings setup, int preset)
        {
            if (preset < 0 || preset > 2) throw new ArgumentOutOfRangeException(nameof(preset));
            foreach (var part in setup.parts)
            {
                if (part.custom) continue;
                if (part.IsFingerRing)
                {
                    part.included = preset == 2;
                    continue;
                }
                bool casual = IsCasualPart(part);
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
            var actions = mouth.animationModelVersion == 0 ? mouth.actions :
                mouth.animationGroups.Find(g => g.kind == SocketAnimationKind.Depth)?.actions;
            if (actions == null) return;
            if (actions.Count == 0) actions.Add(new DepthActionSettings());
            var action = actions[0];
            if (action.kind != DepthActionKind.BlendShape || action.renderer != null) return;
            // A saved default shape can outlive its renderer reference. Only repair
            // that known default; never replace a manually chosen expression.
            if (!string.IsNullOrEmpty(action.shape) && action.shape != names[index]) return;
            action.renderer = descriptor.VisemeSkinnedMesh;
            action.shape = names[index];
        }

        // Project only depth ranges to reversed visual coordinates. Stored Fury values remain ordered.
        public static Vector2 DepthRangeToSlider(Vector2 range) => new Vector2(1f - DistanceToSlider(range.y), 1f - DistanceToSlider(range.x));
        public static Vector2 DepthRangeFromSlider(float left, float right) => new Vector2(SliderToDistance(1f - right), SliderToDistance(1f - left));

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
