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
            if (range != group.ActiveSettings.range) EditAnimationGroup(part, group, g => g.ActiveSettings.range = range, scalarEdit: true);
        }
        internal void SetAnimationRangeUnits(SocketSettings part, SocketAnimationGroup group, DepthUnits units)
        {
            if (!Enum.IsDefined(typeof(DepthUnits), units) ||
                group.kind == SocketAnimationKind.PlugEntryRadius && units == DepthUnits.Plugs || units == group.ActiveSettings.units) return;
            EditAnimationGroup(part, group, g => g.ActiveSettings.units = units);
        }
        private static bool AnimationFinite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private void EditAnimationGroup(SocketSettings part, SocketAnimationGroup group, Action<SocketAnimationGroup> edit, bool keepBlendPreview = false, bool scalarEdit = false)
        {
            Change(() => {
                part.InitializeAnimationGroups();
                var current = part.animationGroups.Single(g => g.id == group.id);
                edit(current);
            }, keepBlendPreview, scalarEdit && part.animationModelVersion == 1);
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
                            if (activeOnly) EditorGUILayout.LabelField(kind == SocketAnimationKind.Depth ? L("深度（限定）") : "Active");
                            else {
                            int selected = EditorGUILayout.Popup((int)group.kind, new[] { L("深度"), L("入口Plug半径"), L("アクティブ") });
                            if (selected != (int)group.kind) EditAnimationGroup(part, group, g => g.kind = (SocketAnimationKind)selected);
                            }
                            if (index > 0)
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
                        if (kind != SocketAnimationKind.Active && activeOnly) EditorGUILayout.HelpBox(L("基本 Socket の深度は Local 単位・他人 ON・重みなし・Object ON 1件のみ対応します。他の設定は保持し、生成時に適用しません。"), MessageType.Info);
                        if (kind != SocketAnimationKind.Active) DrawAnimationRange(part, group, activeOnly);
                        bool details = AnimationDetailsExpanded(part, group);
                        bool nextDetails = (!activeOnly || kind != SocketAnimationKind.Active) && EditorGUILayout.Foldout(details, L("詳細設定"), true);
                        if ((!activeOnly || kind != SocketAnimationKind.Active) && nextDetails != details) SetAnimationDetailsExpanded(part, group, nextDetails);
                        if (nextDetails)
                            using (new SettingsIndent())
                            {
                                if (!activeOnly) DrawBlendShapeInterference(part, group);
                                if (kind != SocketAnimationKind.Active)
                                {
                                    bool self = EditorGUILayout.ToggleLeft(L("自分に反応"), value.enableSelf);
                                    bool others = EditorGUILayout.ToggleLeft(L("他人に反応"), value.enableOthers);
                                    float smoothing = EditorGUILayout.FloatField(new GUIContent(L("スムージング（秒）"), L("反応の変化をなめらかにします。0秒で補間待ちをなくします。")), value.smoothingSeconds);
                                    if (self != value.enableSelf || others != value.enableOthers || AnimationFinite(smoothing) && smoothing != value.smoothingSeconds)
                                        EditAnimationGroup(part, group, g => { g.ActiveSettings.enableSelf = self; g.ActiveSettings.enableOthers = others;
                                            if (AnimationFinite(smoothing)) g.ActiveSettings.smoothingSeconds = smoothing; }, scalarEdit: true);
                                    if (!activeOnly && kind == SocketAnimationKind.Depth)
                                    {
                                        bool weighted = EditorGUILayout.ToggleLeft(new GUIContent(L("Plug半径で重み付け"), L("深度アニメーションの強さをPlug半径で調整します。入口Plug半径アニメーションとは別の設定です。")), value.radiusWeightEnabled);
                                        if (weighted != value.radiusWeightEnabled)
                                            EditAnimationGroup(part, group, g => g.depthSettings.radiusWeightEnabled = weighted);
                                        if (value.radiusWeightEnabled)
                                            using (new SettingsIndent())
                                            {
                                                DrawRadiusAutoToggle(part, group);
                                                DrawRadiusCorrection(part, group);
                                            }
                                    }
                                }
                            }
                        if (!activeOnly && kind == SocketAnimationKind.Depth && value.radiusWeightEnabled && !value.radiusAutoCorrection && value.radiusZero == 0 && value.radiusFull == 0)
                            EditorGUILayout.HelpBox(L("重み0と重み1の半径を入力するか、自動補正をオンにしてください。"), MessageType.Warning);
                        if (kind != SocketAnimationKind.Active && (value.range.x > value.range.y || value.smoothingSeconds < 0 ||
                            !activeOnly && kind == SocketAnimationKind.Depth && value.radiusWeightEnabled && !value.radiusAutoCorrection &&
                            (value.radiusFull < 0 || value.radiusZero < 0 || value.radiusZero > value.radiusFull)))
                            EditorGUILayout.HelpBox(L("このグループを反映するには、範囲・スムージング・半径の値を確認してください。"), MessageType.Warning);
                        if (kind == SocketAnimationKind.Depth) DrawBlendPreviewControls(part, group);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(new GUIContent(L("アクション"), kind == SocketAnimationKind.Active
                                ? L("Socketがオンの間に適用します。接触判定ではありません。時間変化にはAnimationClipを使用してください。") : ""), EditorStyles.boldLabel);
                            if (!activeOnly || kind != SocketAnimationKind.Depth)
                                if (GUILayout.Button(new GUIContent("+", L("アクションを追加")), EditorStyles.miniButton, GUILayout.Width(24)))
                                    EditAnimationGroup(part, group, g => g.actions.Add(new DepthActionSettings()));
                        }
                        foreach (var action in group.actions.ToArray())
                            if (action != null) DrawAction(part, action, kind == SocketAnimationKind.Active);
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                // Basic-only authoring keeps its existing Active route when Depth is unavailable.
                var kind = activeOnly && !basicDepth ? SocketAnimationKind.Active : SocketAnimationKind.Depth;
                var label = kind == SocketAnimationKind.Depth ? L("アニメーションを追加") : "+ " + L("アクティブ");
                if (GUILayout.Button(new GUIContent(label), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    Change(() => {
                        part.InitializeAnimationGroups();
                        var group = SocketAnimationGroup.Create(kind, part.id, part.custom);
                        if (activeOnly && kind == SocketAnimationKind.Depth) {
                            group.depthSettings.radiusWeightEnabled = false;
                            group.depthSettings.radiusAutoCorrection = false;
                            group.actions[0].kind = DepthActionKind.Object;
                        }
                        part.animationGroups.Add(group);
                    });
            }
        }

        private void DrawBlendShapeInterference(SocketSettings part, SocketAnimationGroup group)
        {
            if (!Sps3ControlsForDrawing || !CustomBlendShapeOutput.CanAuthorHold) return;
            bool enabled = group.interferenceEnabled || group.mouthNeutralEnabled;
            bool selected = EditorGUILayout.ToggleLeft(new GUIContent(L("アニメーション中のBlendShape干渉を抑止"),
                L("表情のうち変形範囲が大きく重なる形状だけを一時的に戻します。設定の変更には再生成が必要です。")), enabled);
            if (selected != enabled)
                EditAnimationGroup(part, group, g => {
                    g.interferenceEnabled = selected;
                    // Keep the saved legacy binding, but never emit it beside automatic output.
                    g.mouthNeutralEnabled = false;
                });
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
                if (range != value.range) EditAnimationGroup(part, group, g => g.ActiveSettings.range = range, scalarEdit: true);
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
                    int selected = basicOnly ? index : EditorGUILayout.Popup(index, labels, GUILayout.MinWidth(100));
                    if (basicOnly && index >= 0) GUILayout.Label(labels[index], EditorStyles.miniLabel);
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
