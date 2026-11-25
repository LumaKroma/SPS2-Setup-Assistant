using System;
using System.Linq;
using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        [NonSerialized] private FuryActiveShapeProbe.Capabilities activeShapeCache;
        [NonSerialized] private int activeShapeRevision;
        [NonSerialized] private bool activeShapeCached;
        private FuryActiveShapeProbe.Capabilities ActiveShapeForDrawing
        {
            get {
                if (!activeShapeCached || activeShapeRevision != autoContextRevision) {
                    activeShapeCache = FuryActiveShapeProbe.Inspect();
                    activeShapeRevision = autoContextRevision; activeShapeCached = true;
                }
                return activeShapeCache;
            }
        }
        private readonly HashSet<string> expandedAnimationDetails = new HashSet<string>();
        private static string AnimationDetailsKey(SocketSettings part, SocketAnimationGroup group) =>
            part.id.Length + ":" + part.id + group.id;
        internal bool AnimationDetailsExpanded(SocketSettings part, SocketAnimationGroup group) =>
            expandedAnimationDetails.Contains(AnimationDetailsKey(part, group));
        internal void SetAnimationDetailsExpanded(SocketSettings part, SocketAnimationGroup group, bool expanded)
        {
            string key = AnimationDetailsKey(part, group);
            if (expanded) expandedAnimationDetails.Add(key); else expandedAnimationDetails.Remove(key);
        }
        internal static bool AnimationRangeInputEvent(EventType type) =>
            type == EventType.MouseDown || type == EventType.MouseDrag || type == EventType.MouseUp ||
            type == EventType.KeyDown || type == EventType.KeyUp || type == EventType.ScrollWheel || type == EventType.ExecuteCommand;
        internal static Vector2 AnimationSliderRange(float min, float max) =>
            new Vector2(FullSetupCatalog.SliderToDistance(min), FullSetupCatalog.SliderToDistance(max));
        internal void SetAnimationRangeFields(SocketSettings part, SocketAnimationGroup group, float start, float end)
        {
            Vector2 range;
            if (group.kind == SocketAnimationKind.Depth)
            {
                if (!FullSetupCatalog.TryDepthRangeFields(start, end, out range)) return;
            }
            else
            {
                if (!AnimationFinite(start) || !AnimationFinite(end)) return;
                start = Mathf.Clamp(start, -1, 3); end = Mathf.Clamp(end, -1, 3);
                range = new Vector2(Mathf.Min(start, end), Mathf.Max(start, end));
            }
            if (range != group.ActiveSettings.range) EditAnimationGroup(part, group, g => g.ActiveSettings.range = range);
        }
        internal void SetAnimationRangeUnits(SocketSettings part, SocketAnimationGroup group, DepthUnits units)
        {
            if (!Enum.IsDefined(typeof(DepthUnits), units) ||
                group.kind == SocketAnimationKind.PlugEntryRadius && units == DepthUnits.Plugs || units == group.ActiveSettings.units) return;
            EditAnimationGroup(part, group, g => g.ActiveSettings.units = units);
        }
        private static bool AnimationFinite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private void EditAnimationGroup(SocketSettings part, SocketAnimationGroup group, Action<SocketAnimationGroup> edit)
        {
            Change(() => {
                part.InitializeAnimationGroups();
                var current = part.animationGroups.Single(g => g.id == group.id);
                edit(current);
            });
        }
        private static void RemoveAnimationAction(SocketSettings part, DepthActionSettings action)
        {
            if (part.animationModelVersion == 0) part.actions.Remove(action);
            else foreach (var group in part.animationGroups) group.actions?.Remove(action);
        }
        private void DrawAnimationGroups(SocketSettings part, bool activeOnly = false)
        {
            if (part.animationModelVersion != 0 && part.animationModelVersion != 1)
            {
                EditorGUILayout.HelpBox(L("未対応のアニメーション設定です。元データは保持されています。"), MessageType.Warning);
                return;
            }
            if (part.animationModelVersion == 1 && (part.animationGroups == null ||
                part.animationGroups.Any(g => g == null || string.IsNullOrEmpty(g.id) ||
                    g.kind != SocketAnimationKind.Depth && g.kind != SocketAnimationKind.PlugEntryRadius && g.kind != SocketAnimationKind.Active) ||
                part.animationGroups.GroupBy(g => g.id).Any(g => g.Count() != 1)))
            {
                EditorGUILayout.HelpBox(L("アニメーショングループが欠損または重複しています。該当グループは反映できません。元データは保持されます。"), MessageType.Warning);
                return;
            }
            bool basicDepth = activeOnly && FuryBasicDepthProbe.Available();
            foreach (var kind in activeOnly ? (basicDepth ? new[] { SocketAnimationKind.Depth, SocketAnimationKind.Active } : new[] { SocketAnimationKind.Active }) : new[] { SocketAnimationKind.Depth, SocketAnimationKind.PlugEntryRadius, SocketAnimationKind.Active })
            {
                var groups = part.EffectiveAnimationGroups().Where(g => g != null && g.kind == kind).ToArray();
                for (int index = 0; index < groups.Length; index++)
                {
                    var group = groups[index];
                    GUILayout.Space(8);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (activeOnly) EditorGUILayout.LabelField(kind == SocketAnimationKind.Depth ? "深度（限定）" : "Active");
                            else {
                            int selected = EditorGUILayout.Popup((int)group.kind, new[] { L("深度"), L("入口Plug半径"), L("アクティブ") });
                            if (selected != (int)group.kind) EditAnimationGroup(part, group, g => g.kind = (SocketAnimationKind)selected);
                            }
                            using (new EditorGUI.DisabledScope(index == 0))
                                if (GUILayout.Button("↑", GUILayout.Width(26)))
                                {
                                    var previous = groups[index - 1];
                                    Change(() => { part.InitializeAnimationGroups(); var list = part.animationGroups;
                                        int a = list.FindIndex(g => g.id == group.id), b = list.FindIndex(g => g.id == previous.id);
                                        var swap = list[a]; list[a] = list[b]; list[b] = swap; });
                                }
                            if (GUILayout.Button("−", GUILayout.Width(26))) EditAnimationGroup(part, group, g => part.animationGroups.Remove(g));
                        }
                        var value = group.ActiveSettings;
                        if (value == null || group.actions == null)
                        {
                            EditorGUILayout.HelpBox(L("グループの設定が欠損しているため、このグループは反映できません。"), MessageType.Warning);
                            continue;
                        }
                        if(kind != SocketAnimationKind.Active) {
                        if (activeOnly) EditorGUILayout.HelpBox(L("基本 Socket の深度は Local 単位・他人 ON・重みなし・Object ON 1件のみ対応します。他の設定は保持し、生成時に適用しません。"), MessageType.Info);
                        DrawAnimationRange(part, group, activeOnly);
                        bool details = AnimationDetailsExpanded(part, group);
                        bool nextDetails = EditorGUILayout.Foldout(details, L("詳細設定"), true);
                        if (nextDetails != details) SetAnimationDetailsExpanded(part, group, nextDetails);
                        if (nextDetails)
                        {
                            bool self = EditorGUILayout.Toggle(L("自分に反応"), value.enableSelf);
                            bool others = EditorGUILayout.Toggle(L("他人に反応"), value.enableOthers);
                            float smoothing = EditorGUILayout.FloatField(new GUIContent(L("スムージング（秒）"), L("反応の変化をなめらかにします。0秒で補間待ちをなくします。")), value.smoothingSeconds);
                            if (self != value.enableSelf || others != value.enableOthers || AnimationFinite(smoothing) && smoothing != value.smoothingSeconds)
                                EditAnimationGroup(part, group, g => { g.ActiveSettings.enableSelf = self; g.ActiveSettings.enableOthers = others;
                                    if (AnimationFinite(smoothing)) g.ActiveSettings.smoothingSeconds = smoothing; });
                            if (kind == SocketAnimationKind.Depth)
                            {
                                using (new EditorGUI.DisabledScope(activeOnly))
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    bool weighted = EditorGUILayout.ToggleLeft(new GUIContent(L("Plug半径で重み付け"), L("深度アニメーションの強さをPlug半径で調整します。入口Plug半径アニメーションとは別の設定です。")), value.radiusWeightEnabled);
                                    if (weighted != value.radiusWeightEnabled)
                                        EditAnimationGroup(part, group, g => g.depthSettings.radiusWeightEnabled = weighted);
                                    using (new EditorGUI.DisabledScope(!value.radiusWeightEnabled))
                                        DrawRadiusAutoToggle(part, group);
                                }
                                if (value.radiusWeightEnabled) DrawRadiusCorrection(part, group);
                            }
                        }
                        if (kind == SocketAnimationKind.Depth && value.radiusWeightEnabled && !value.radiusAutoCorrection && value.radiusZero == 0 && value.radiusFull == 0)
                            EditorGUILayout.HelpBox(L("重み0と重み1の半径を入力するか、自動補正をオンにしてください。"), MessageType.Warning);
                        if (value.range.x > value.range.y || value.smoothingSeconds < 0 || kind == SocketAnimationKind.Depth && value.radiusWeightEnabled &&
                            (value.radiusFull < 0 || value.radiusZero < 0 || value.radiusZero > value.radiusFull))
                            EditorGUILayout.HelpBox(L("このグループを反映するには、範囲・スムージング・半径の値を確認してください。"), MessageType.Warning);
                        }
                        if (kind == SocketAnimationKind.Depth && part.id == "mouth" && !part.custom && !activeOnly &&
                            Sps3ControlsForDrawing) DrawMouthNeutral(part, group);
                        if (kind == SocketAnimationKind.Depth) DrawBlendPreviewControls(part, group);
                        EditorGUILayout.LabelField(new GUIContent(L("アクション"),kind==SocketAnimationKind.Active?L("Socketがオンの間に適用します。接触判定ではありません。時間変化にはAnimationClipを使用してください。"):""), EditorStyles.boldLabel);
                        foreach (var action in group.actions.ToArray())
                            if (action != null) DrawAction(part, action, kind == SocketAnimationKind.Active);
                        using (new EditorGUI.DisabledScope(activeOnly && kind == SocketAnimationKind.Depth))
                            if (GUILayout.Button(L("アクションを追加")))
                                EditAnimationGroup(part, group, g => g.actions.Add(new DepthActionSettings()));
                    }
                }
            }
            if (GUILayout.Button(L("アニメーションを追加")))
                Change(() => { part.InitializeAnimationGroups(); part.animationGroups.Add(SocketAnimationGroup.Create(activeOnly ? SocketAnimationKind.Active : SocketAnimationKind.Depth, part.id, part.custom)); });
            if (basicDepth && GUILayout.Button("深度アニメーションを追加（限定）"))
                Change(() => { part.InitializeAnimationGroups(); var g = SocketAnimationGroup.Create(SocketAnimationKind.Depth, part.id, part.custom);
                    g.depthSettings.radiusWeightEnabled = false; g.depthSettings.radiusAutoCorrection = false;
                    g.actions[0].kind = DepthActionKind.Object; part.animationGroups.Add(g); });
        }
        private void DrawMouthNeutral(SocketSettings part, SocketAnimationGroup group)
        {
            bool enabled = EditorGUILayout.Toggle(new GUIContent(L("口形状の固定"), L("深度の範囲内だけ固定します。固定値には Plug 半径の重みを使いません。")), group.mouthNeutralEnabled);
            if (enabled != group.mouthNeutralEnabled)
                EditAnimationGroup(part, group, g => {
                    g.mouthNeutralEnabled = enabled;
                    if (enabled && g.mouthNeutral == null) g.mouthNeutral = new DepthActionSettings { weight = 0 };
                });
            if (!group.mouthNeutralEnabled || group.mouthNeutral == null) return;
            if (!CustomBlendShapeOutput.CanAuthorHold)
                EditorGUILayout.HelpBox(L("この環境では口形状の固定に必要なクリップを設定できません。"), MessageType.Warning);
            var binding = group.mouthNeutral;
            var renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(L("メッシュ"), binding.renderer, typeof(SkinnedMeshRenderer), true);
            if (renderer != binding.renderer && (renderer == null ||
                renderer.GetComponentInParent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true) == descriptor))
                EditAnimationGroup(part, group, g => { g.mouthNeutral.renderer = renderer; g.mouthNeutral.shape = ""; });
            var mesh = binding.renderer != null ? binding.renderer.sharedMesh : null;
            using (new EditorGUI.DisabledScope(mesh == null))
            {
                if (GUILayout.Button(string.IsNullOrEmpty(binding.shape) ? "BlendShape…" : binding.shape, EditorStyles.popup))
                {
                    var menu = new GenericMenu();
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string name = mesh.GetBlendShapeName(i);
                        menu.AddItem(new GUIContent(name), binding.shape == name, () => {
                            if (this != null && settings.parts.Contains(part) && part.animationGroups.Contains(group) &&
                                group.mouthNeutralEnabled && binding.renderer != null && binding.renderer.sharedMesh == mesh &&
                                mesh.GetBlendShapeIndex(name) >= 0)
                                EditAnimationGroup(part, group, g => g.mouthNeutral.shape = name);
                        });
                    }
                    menu.ShowAsContext();
                }
            }
            float value = EditorGUILayout.Slider(L("固定値"), binding.weight, 0, 100);
            if (value != binding.weight) EditAnimationGroup(part, group, g => g.mouthNeutral.weight = value);
            if (mesh != null && !string.IsNullOrEmpty(binding.shape) && group.actions.Any(a => a?.kind == DepthActionKind.BlendShape && (a.renderer == null || a.renderer == binding.renderer) && a.shape == binding.shape))
                EditorGUILayout.HelpBox(L("口開きと同じ形状は固定しません。"), MessageType.Info);
            if (mesh == null || string.IsNullOrEmpty(binding.shape))
                EditorGUILayout.HelpBox(L("口形状の固定には、有効なメッシュ、BlendShape、固定値を指定してください。"), MessageType.Warning);
        }
        private void DrawAnimationRange(SocketSettings part, SocketAnimationGroup group, bool basicOnly = false)
        {
            var value = group.ActiveSettings;
            // A slider displays a clamped projection, but read-only IMGUI events must
            // never write that projection back into an existing out-of-range value.
            bool input = Event.current != null && AnimationRangeInputEvent(Event.current.type);
            bool isDepth = group.kind == SocketAnimationKind.Depth;
            var projection = isDepth ? FullSetupCatalog.DepthRangeToSlider(value.range) :
                new Vector2(FullSetupCatalog.DistanceToSlider(value.range.x), FullSetupCatalog.DistanceToSlider(value.range.y));
            float min = projection.x, max = projection.y;
            if (isDepth) using (new EditorGUILayout.HorizontalScope()) { EditorGUILayout.LabelField(L("外側（遠）"), EditorStyles.miniLabel); GUILayout.FlexibleSpace(); EditorGUILayout.LabelField(L("内側（近）"), EditorStyles.miniLabel, GUILayout.Width(70)); }
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref min, ref max, 0, 1);
            bool sliderChanged = EditorGUI.EndChangeCheck();
            if (input && sliderChanged)
            {
                var range = isDepth ? FullSetupCatalog.DepthRangeFromSlider(min, max) : AnimationSliderRange(min, max);
                EditAnimationGroup(part, group, g => g.ActiveSettings.range = range);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                try
                {
                    EditorGUIUtility.labelWidth = 50;
                    float displayedStart = isDepth ? value.range.y : value.range.x, displayedEnd = isDepth ? value.range.x : value.range.y;
                    float start = EditorGUILayout.FloatField(isDepth ? L("外側") : L("始"), displayedStart), end = EditorGUILayout.FloatField(isDepth ? L("内側") : L("終"), displayedEnd);
                    var units = group.kind == SocketAnimationKind.Depth
                        ? new[] { DepthUnits.Meters, DepthUnits.Plugs, DepthUnits.Local }
                        : new[] { DepthUnits.Meters, DepthUnits.Local };
                    var labels = group.kind == SocketAnimationKind.Depth ? DepthUnitLabels : new[] { DepthUnitLabels[0], DepthUnitLabels[2] };
                    int index = Array.IndexOf(units, value.units);
                    int selected;
                    using (new EditorGUI.DisabledScope(basicOnly))
                        selected = EditorGUILayout.Popup(index, labels, GUILayout.MinWidth(100));
                    if (input && (start != displayedStart || end != displayedEnd)) SetAnimationRangeFields(part, group, start, end);
                    if (input && selected >= 0 && selected < units.Length && selected != index) SetAnimationRangeUnits(part, group, units[selected]);
                }
                finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
            }
        }
        private float? AnimationSocketScale(SocketSettings part)
        {
            // Native default is expressed using the actual socket owner, not the avatar root.
            try
            {
                var root = Generation.Sps2SetupStorage.Find(descriptor);
                var socket = root?.sockets?.SingleOrDefault(s => s.id == part.id)?.socket;
                if (socket != null) return Mathf.Max(Mathf.Abs(socket.transform.lossyScale.x), .000001f);
            }
            catch { /* An unresolved owner cannot be silently treated as an inspected live owner. */ }
            return null;
        }
    }
}
