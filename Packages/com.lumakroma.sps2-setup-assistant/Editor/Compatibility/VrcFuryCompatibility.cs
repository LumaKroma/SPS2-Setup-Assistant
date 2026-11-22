using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Linq;


using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;

using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Issue 121's explicit exception. All dependency-private serialization stays here.
    public static partial class VrcFuryCompatibility
    {
        public const string SupportedVersion = "1.1403.0";
        internal static string SocketType => SpsBackend.SocketType(SpsBackend.ActiveFamily);
        internal static string PlugType => SpsBackend.PlugType(SpsBackend.ActiveFamily);

        public static void RequireVersion()
        {
            SpsAuthoringCompatibility.RequireVersion(SpsBackend.ActiveFamily, SpsAuthoringCompatibility.ActiveContract);
            var capabilities = VrcFuryCapabilities.Current;
            if (!capabilities.CanGenerate)
                throw new InvalidOperationException(capabilities.BlockReason + "\n" + SpsBackend.Guide(SpsBackend.ActiveFamily));
        }

        internal static SerializedProperty Require(SerializedObject data, string path, SerializedPropertyType type)
        {
            var p = data.FindProperty(path);
            if (p == null || p.propertyType != type)
                throw new InvalidOperationException(L("VRCFury の設定構造が一致しません: ") + path);
            return p;
        }

        private static void SetEnum(SerializedProperty property, string name)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException(L("VRCFury の列挙型構造が一致しません。"));
            int index = Array.IndexOf(property.enumNames, name);
            if (index < 0) throw new InvalidOperationException(L("VRCFury の列挙値がありません: ") + name);
            property.enumValueIndex = index;
        }

        public static Component FindSocket(GameObject obj) => obj.GetComponents<Component>()
            .SingleOrDefault(c => c != null && c.GetType().FullName == SocketType);

        internal static Component[] AllSockets(GameObject avatar) => avatar.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && c.GetType().FullName == SocketType).ToArray();

        private static SerializedObject SocketData(Component component)
        {
            if (component == null || component.GetType().FullName != SocketType)
                throw new InvalidOperationException(L("所有 Socket の参照が失われています。"));
            var data = new SerializedObject(component);
            Require(data, "name", SerializedPropertyType.String);
            Require(data, "addMenuItem", SerializedPropertyType.Boolean);
            return data;
        }

        public static Component CreateSocket(GameObject pose, SocketSettings part, SetupSettings setup, List<string> warnings, bool allowOptionalOmission = false)
        {
            RequireVersion();
            var wrapper = UndoComponentRegistration.Invoke(pose, L("SPS2 セットアップ"), () => VrcFuryApi.CreateSocket(pose));
            wrapper.SetName(SocketName(part, setup));
            var requestedMode = setup.penetration && (part.id == "mouth" || part.id == "anus") ? "Ring" : "Auto";
            var appliedMode = wrapper.SetMode(requestedMode);
            if (appliedMode != requestedMode) warnings?.Add(part.name + ": Socket mode " + requestedMode + " is unavailable; used " + appliedMode + ".");
            if (part.id == "chest" || part.id == "handLeft" || part.id == "handRight" || part.id == "hands" ||
                part.id == "footLeft" || part.id == "footRight" || part.id == "feet") {
                if (SpsBackend.ActiveFamily != SpsFamily.SpsForNdmf || VrcFuryCapabilities.Current.RadiusOffset) wrapper.UseRadiusOffset();
            }
            var component = FindSocket(pose);
            if (allowOptionalOmission && part.animationModelVersion != 0) {
                // This is a newly created component only. Capability failure is
                // decided before its feature write; write failures still abort.
                var requested = new[] { SocketAnimationKind.Depth, SocketAnimationKind.PlugEntryRadius, SocketAnimationKind.Active }
                    .Where(kind => part.AnimationEnabled && part.EffectiveAnimationGroups().Any(g => g.kind == kind)).ToList();
                bool sharedOrUnknown = requested.Contains(SocketAnimationKind.Depth) && requested.Contains(SocketAnimationKind.PlugEntryRadius) &&
                    Sps3AnimationGroupNative.CanWriteOptional(part, SocketAnimationKind.Depth, out _) &&
                    Sps3AnimationGroupNative.CanWriteOptional(part, SocketAnimationKind.PlugEntryRadius, out _) &&
                    !NewAnimationListsAreIndependent(component);
                bool depthApplied = false;
                OptionalFeatureSequence.Run(
                    () => Configure(component, part, setup, warnings, false, true, false, false), requested,
                    kind => Sps3AnimationGroupNative.CanWriteOptional(part, kind, out var reason) ? null : reason,
                    kind => {
                        if (kind == SocketAnimationKind.Active) ConfigureNewActiveSafely(component, part, warnings);
                        else if (kind == SocketAnimationKind.PlugEntryRadius && sharedOrUnknown && depthApplied)
                            warnings?.Add(part.name + ": Depth と Entry の独立した保存先を確認できないため、Depth を優先し Entry を省略します。入力設定は保持します。");
                        else {
                            var applied = ConfigureNewDepthOrEntrySafely(component, part, setup, warnings, kind);
                            if (kind == SocketAnimationKind.Depth) depthApplied = applied;
                        }
                    },
                    (kind, reason) => warnings?.Add(part.name + ": " + kind + " は利用できないため省略しました。入力設定は保持します: " + reason));
            } else Configure(component, part, setup, warnings);
            return component;
        }

        public static void Configure(Component component, SocketSettings part, SetupSettings setup, List<string> warnings, bool rewriteDepth = true, bool rewriteCommon = true, bool? rewriteActive = null, bool? rewriteEntry = null)
        {
            RequireVersion();
            if (SpsBackend.ActiveFamily == SpsFamily.SpsForNdmf) {
                ConfigureDirectNdmf(component, part, setup, warnings, rewriteDepth, rewriteCommon);
                return;
            }
            if (part.animationModelVersion != 0 && SpsAuthoringCompatibility.ActiveContract == SpsAuthoringContract.LegacySps2)
            {
                // Grouped depth, entry and active are SPS3 authoring. The legacy
                // flat action list may be an obsolete mirror; do not replay it.
                if (rewriteCommon)
                {
                    var targetData = SocketData(component);
                    Undo.RecordObject(component, L("SPS2 設定反映"));
                    SetFuryCommonFields(targetData, part, setup);
                    targetData.ApplyModifiedProperties();
                    targetData.Update();
                    if (targetData.FindProperty("name").stringValue != SocketName(part, setup) ||
                        !targetData.FindProperty("addMenuItem").boolValue)
                        throw new InvalidOperationException(L("SPS2 の基本 Socket 設定を読み戻せません。"));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                if (part.AnimationEnabled && (rewriteDepth || rewriteActive == true || rewriteEntry == true))
                    warnings?.Add(string.Format(L("{0}：SPS2 では SPS3 アニメーショングループを省略しました。設定は下書きに残ります。"), SocketDisplayNames.Resolve(part, setup.menuLanguage)));
                return;
            }
            if (FuryNativeFeatureShapes.MissingReferenceInspectionRequired(false, rewriteDepth,
                rewriteEntry ?? rewriteDepth, rewriteActive ?? (part.animationModelVersion != 0 && part.AnimationEnabled &&
                    part.animationGroups != null && part.animationGroups.Any(g => g.kind == SocketAnimationKind.Active))))
                Sps3NativeEditProtection.RequireNoMissingReferences(component);
            if (part.animationModelVersion != 0)
            {
                ConfigureAnimationGroups(component, part, setup, warnings, rewriteDepth, rewriteCommon, rewriteActive ?? (part.AnimationEnabled && part.animationGroups.Any(g=>g.kind==SocketAnimationKind.Active)), rewriteEntry ?? rewriteDepth);
                return;
            }
            var target = SocketData(component);
            if (rewriteDepth && target.FindProperty("depthActions2")?.arraySize > 1)
                throw new InvalidOperationException(part.name + L(": 手動で追加された深度グループがあります。設定を保持するため、変更を中止しました。"));
            var temporary = new GameObject("SPS2 configuration staging") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var wrapper = VrcFuryApi.CreateSocket(temporary);
                var offIndices = new List<int>();
                if (rewriteDepth && part.depth)
                {
                    var actions = wrapper.AddDepthActions(part.range, 0, true);
                    int count = 0;
                    foreach (var action in part.actions)
                    {
                        switch (action.kind)
                        {
                            case DepthActionKind.BlendShape:
                                if (action.renderer == null || action.renderer.sharedMesh == null ||
                                    string.IsNullOrEmpty(action.shape) || action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0)
                                { warnings.Add(part.name + L(": BlendShape が未設定のため、このアクションを省きました。")); continue; }
                                actions.AddBlendshape(action.shape, Mathf.Clamp(action.weight, 0, 100), action.renderer);
                                break;
                            case DepthActionKind.AnimationClip:
                                if (action.clip == null) { warnings.Add(part.name + L(": Clip が未設定です。")); continue; }
                                actions.AddAnimationClip(action.clip);
                                break;
                            case DepthActionKind.Object:
                                if (action.target == null) { warnings.Add(part.name + L(": オブジェクトが未設定です。")); continue; }
                                actions.AddTurnOn(action.target);
                                if (!action.objectOn) offIndices.Add(count);
                                break;
                            default: throw new InvalidOperationException(L("不明な深度アクションです。"));
                        }
                        count++;
                    }
                }
                var source = SocketData(FindSocket(temporary));
                if (rewriteDepth && part.depth)
                {
                    SetEnum(Require(source, "depthActions2.Array.data[0].units", SerializedPropertyType.Enum), part.units.ToString());
                    Require(source, "depthActions2.Array.data[0].enableSelf", SerializedPropertyType.Boolean).boolValue = true;
                    Require(source, "depthActions2.Array.data[0].smoothingSeconds", SerializedPropertyType.Float).floatValue = 0;
                    Require(source, "depthActions2.Array.data[0].reverseClip", SerializedPropertyType.Boolean).boolValue = false;
                    // Setup does not expose native radius weighting. A fresh generated group is
                    // explicitly unweighted; direct native edits are preserved/guarded separately.
                    foreach (var field in new[] { "plugRadiusForFullWeight", "plugRadiusForZeroWeight" })
                        if (source.FindProperty("depthActions2.Array.data[0]." + field) != null)
                            Require(source, "depthActions2.Array.data[0]." + field, SerializedPropertyType.Float).floatValue = 0;
                    foreach (int index in offIndices)
                        SetEnum(Require(source, $"depthActions2.Array.data[0].actionSet.actions.Array.data[{index}].mode", SerializedPropertyType.Enum), "TurnOff");
                    source.ApplyModifiedPropertiesWithoutUndo();
                    source.Update();
                }
                Undo.RecordObject(component, L("SPS2 設定反映"));
                if (rewriteCommon)
                {
                    SetFuryCommonFields(target, part, setup);
                }
                if (rewriteDepth && VrcFuryCapabilities.Current.Depth) CopyDepthActions(source, target);
                target.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static void SetFuryCommonFields(SerializedObject target, SocketSettings part, SetupSettings setup)
        {
            target.FindProperty("name").stringValue = SocketName(part, setup);
            SetOptionalBool(target, "enableAuto", SpsAuthoringCompatibility.ActiveContract == SpsAuthoringContract.Sps3Beta14 ? setup.Sps3IncludeInAuto(part) : setup.autoMode && !part.IsFingerRing);
            if (VrcFuryCapabilities.Current.Legacy) SetOptionalBool(target, "useLights", setup.legacy);
            target.FindProperty("addMenuItem").boolValue = true;
        }

        private static void ConfigureDirectNdmf(Component component, SocketSettings part, SetupSettings setup,
            List<string> warnings, bool depth, bool common)
        {
            var target = SocketData(component);
            var caps = VrcFuryCapabilities.Current;
            string displayName = SocketDisplayNames.Resolve(part, setup.menuLanguage);
            if (common) {
                target.FindProperty("name").stringValue = SocketName(part, setup);
                target.FindProperty("addMenuItem").boolValue = true;
                if (caps.AutoMode) SetOptionalBool(target, "enableAuto", setup.autoMode && !part.IsFingerRing);
                if (caps.Legacy) SetOptionalBool(target, "useLights", setup.legacy);
                Undo.RecordObject(component, L("SPS2 設定反映"));
                target.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            if (common) {
                target.Update();
                if (Require(target, "name", SerializedPropertyType.String).stringValue != SocketName(part, setup) ||
                    !Require(target, "addMenuItem", SerializedPropertyType.Boolean).boolValue)
                    throw new InvalidOperationException("SPS for NDMF の基本 Socket 設定を読み戻せません。");
            }
            if (part.animationModelVersion != 0 && (part.animationModelVersion != 1 || part.animationGroups == null)) {
                warnings?.Add(string.Format(L("{0}：{1}"), displayName, L("このアニメーションの保存形式を確認できません。")));
                return;
            }
            if (!depth) return;
            if (caps.Depth) ConfigureNdmfSlot(component, part, displayName, warnings, active: false);
            else if (part.AnimationEnabled && part.EffectiveAnimationGroups().Any(g => g?.kind != SocketAnimationKind.Active))
                warnings?.Add(string.Format(L("{0}：{1}"), displayName, L("深度の保存先を確認できません。")));
            if (part.AnimationEnabled && part.EffectiveAnimationGroups().Any(g => g?.kind == SocketAnimationKind.Active))
                ConfigureNdmfSlot(component, part, displayName, warnings, active: true);
        }

        private static void ConfigureNdmfSlot(Component component, SocketSettings part, string displayName, List<string> warnings, bool active)
        {
            string path = active ? "activeActions.actions" : "depthActions2";
            var staging = new GameObject("SPS for NDMF animation staging") { hideFlags = HideFlags.HideAndDontSave };
            int undo = -1;
            string before = null;
            try {
                var native = staging.AddComponent(component.GetType());
                NdmfDepthActions.Stage(native, part, warnings, active, displayName);
                var source = new SerializedObject(native);
                var target = new SerializedObject(component);
                var from = source.FindProperty(path);
                var to = target.FindProperty(path);
                if (to?.isArray != true || from?.isArray != true)
                    throw new InvalidOperationException("保存先を確認できません");
                if (to.arraySize != 0)
                    throw new InvalidOperationException("既存のアニメーションを保持しました");
                if (from.arraySize == 0 && to.arraySize == 0) return;
                string expected = SpsMigrationAdapter.State(native, path);
                if (active) {
                    to.arraySize = from.arraySize;
                    for (int i = 0; i < from.arraySize; i++)
                        to.GetArrayElementAtIndex(i).managedReferenceValue = from.GetArrayElementAtIndex(i).managedReferenceValue;
                } else CopyDepthActions(source, target);
                before = EditorJsonUtility.ToJson(component);
                Undo.IncrementCurrentGroup();
                undo = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(component, "Set SPS for NDMF animation");
                target.ApplyModifiedProperties();
                if (SpsMigrationAdapter.State(component, path) != expected)
                    throw new InvalidOperationException("保存先の読み戻し結果が一致しません");
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            } catch (Exception e) {
                if (undo >= 0) {
                    Undo.RevertAllDownToGroup(undo);
                    if (EditorJsonUtility.ToJson(component) != before)
                        throw new InvalidOperationException("SPS for NDMF のアニメーションを復元できません。生成を取り消します。", e);
                }
                warnings?.Add(string.Format(L("{0}：{1}"), displayName + " / " + (active ? L("アクティブ") : L("深度")), SetupFeedback.Error(e, "アニメーションを保存できませんでした。下書きは保持されています。", optional: true)));
            } finally { UnityEngine.Object.DestroyImmediate(staging); }
        }

        private static void CopyDepthActions(SerializedObject source, SerializedObject target)
        {
            var from = Require(source, "depthActions2", SerializedPropertyType.Generic);
            var to = Require(target, "depthActions2", SerializedPropertyType.Generic);
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                string path = $"depthActions2.Array.data[{i}].";
                Require(target, path + "range", SerializedPropertyType.Vector2).vector2Value = Require(source, path + "range", SerializedPropertyType.Vector2).vector2Value;
                Require(target, path + "units", SerializedPropertyType.Enum).enumValueIndex = Require(source, path + "units", SerializedPropertyType.Enum).enumValueIndex;
                foreach (string field in new[] { "enableSelf", "reverseClip" })
                    Require(target, path + field, SerializedPropertyType.Boolean).boolValue = Require(source, path + field, SerializedPropertyType.Boolean).boolValue;
                // Growing a serialized array does not run DepthActionNew's field initializers.
                // Preserve the native factory values, including beta14's true defaults; older
                // schemas omit these fields on both sides and remain supported.
                foreach (string field in new[] { "enableOthers", "allowHole", "allowRing" })
                    CopyOptionalDepthField(source, target, path + field, SerializedPropertyType.Boolean);
                CopyOptionalDepthField(source, target, path + "plugRadiusForFullWeight", SerializedPropertyType.Float);
                CopyOptionalDepthField(source, target, path + "plugRadiusForZeroWeight", SerializedPropertyType.Float);
                Require(target, path + "smoothingSeconds", SerializedPropertyType.Float).floatValue = Require(source, path + "smoothingSeconds", SerializedPropertyType.Float).floatValue;
                var actions = Require(source, path + "actionSet.actions", SerializedPropertyType.Generic);
                var destination = Require(target, path + "actionSet.actions", SerializedPropertyType.Generic);
                destination.arraySize = actions.arraySize;
                for (int n = 0; n < actions.arraySize; n++)
                {
                    string actionPath = path + $"actionSet.actions.Array.data[{n}]";
                    // Generic array copying cannot initialize polymorphic SerializeReference entries.
                    Require(target, actionPath, SerializedPropertyType.ManagedReference).managedReferenceValue =
                        Require(source, actionPath, SerializedPropertyType.ManagedReference).managedReferenceValue;
                }
            }
        }

        private static void CopyOptionalDepthField(SerializedObject source, SerializedObject target,
            string path, SerializedPropertyType type)
        {
            if (source.FindProperty(path) == null && target.FindProperty(path) == null) return;
            if (SpsBackend.ActiveFamily == SpsFamily.SpsForNdmf && source.FindProperty(path)?.propertyType != type &&
                source.FindProperty(path)?.propertyType == target.FindProperty(path)?.propertyType) return;
            // A one-sided field or changed type is not an equivalent schema. Fail before Apply.
            var from = Require(source, path, type);
            var to = Require(target, path, type);
            if (type == SerializedPropertyType.Boolean) to.boolValue = from.boolValue;
            else if (type == SerializedPropertyType.Float) to.floatValue = from.floatValue;
            else throw new InvalidOperationException("Unsupported optional depth field: " + path);
        }

        public static void ConfigurePenetrationMode(Component component, SetupSettings setup)
        {
            var data = SocketData(component);
            var mode = Require(data, "addLight", SerializedPropertyType.Enum);
            string expected = setup.penetration ? "Ring" : "Auto";
            if (!mode.enumNames.Contains(expected))
                expected = mode.enumNames.Contains("Ring") ? "Ring" : mode.enumNames.Contains("Hole") ? "Hole" : throw new InvalidOperationException("No usable Socket mode is available.");
            if (mode.enumNames[mode.enumValueIndex] == expected) return;
            Undo.RecordObject(component, L("SPS2 貫通モード"));
            SetEnum(mode, expected);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        public static void ConfigureCommon(Component component, SetupSettings setup, bool allowAuto = true)
        {
            var data = SocketData(component);
            Undo.RecordObject(component, L("SPS2 共通設定"));
            SetOptionalBool(data, "enableAuto", setup.autoMode && allowAuto);
            if (VrcFuryCapabilities.Current.Legacy) SetOptionalBool(data, "useLights", setup.legacy);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        internal static bool SameDisplayName(SocketSettings before, SetupSettings oldSetup, SocketSettings after, SetupSettings setup) =>
            SocketName(before, oldSetup) == SocketName(after, setup);

        // Existing SPS3 sockets retain all native-only fields. Only a changed Setup control
        // grants a write to its corresponding field, after the transaction preflight.
        internal static void ConfigureSps3CommonChanges(Component component, SocketSettings before, SetupSettings oldSetup,
            SocketSettings after, SetupSettings setup)
        {
            bool name = !SameDisplayName(before, oldSetup, after, setup);
            bool auto = oldSetup.Sps3IncludeInAuto(before) != setup.Sps3IncludeInAuto(after);
            bool legacy = oldSetup.legacy != setup.legacy;
            if (!name && !auto && !legacy) return;
            var data = SocketData(component);
            Undo.RecordObject(component, "SPS3 changed settings");
            if (name) data.FindProperty("name").stringValue = SocketName(after, setup);
            if (auto) SetOptionalBool(data, "enableAuto", setup.Sps3IncludeInAuto(after));
            if (legacy) SetOptionalBool(data, "useLights", setup.legacy);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static string SocketName(SocketSettings part, SetupSettings setup) =>
            SocketDisplayNames.Resolve(part, setup.menuLanguage);

        // Both native backends consume this saved name directly. A label-only Apply
        // should not rebuild Depth Actions or disturb an adjusted Socket pose.
        internal static void ConfigureDisplayName(Component component, SocketSettings part, SetupSettings setup)
        {
            var data = SocketData(component);
            var name = SocketName(part, setup);
            if (data.FindProperty("name").stringValue == name) return;
            Undo.RecordObject(component, L("SPS2 設定反映"));
            data.FindProperty("name").stringValue = name;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static void SetOptionalBool(SerializedObject data, string path, bool value)
        {
            var field = data.FindProperty(path);
            if (field?.propertyType == SerializedPropertyType.Boolean) field.boolValue = value;
        }

        public static void SetPath(Component socket, Transform[] stops, Transform avatar, bool collapseInternal = false)
        {
            if (stops.Length > 3) throw new InvalidOperationException(L("Guided Path は最大3通過点です。"));
            foreach (var stop in stops)
                if (stop == null || !stop.IsChildOf(avatar) || AllSockets(stop.gameObject).Length != 0)
                    throw new InvalidOperationException(L("Guided Path の参照が不正です。"));
            var data = SocketData(socket);
            var caps = VrcFuryCapabilities.Current;
            var array = data.FindProperty(caps.PathStops ? "guidedPathStops" : "guidedPath");
            if (!caps.Path || array == null)
            {
                if (stops.Length != 0) throw new InvalidOperationException(L("この版では貫通経路を利用できません。"));
                return;
            }
            Undo.RecordObject(socket, L("SPS2 貫通設定"));
            array.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
            {
                if (!caps.PathStops) { array.GetArrayElementAtIndex(i).objectReferenceValue = stops[i]; continue; }
                var prefix = $"guidedPathStops.Array.data[{i}].";
                Require(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue = stops[i];
                if (caps.Collapse) SetOptionalBool(data, prefix + "shrink", collapseInternal);
                if (caps.Tangents)
                {
                    SetOptionalBool(data, prefix + "customizeTangentIn", false);
                    SetOptionalBool(data, prefix + "customizeTangentOut", false);
                    Require(data, prefix + (caps.LocalTangents ? "tangentInLocal" : "tangentIn"), SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
                    Require(data, prefix + (caps.LocalTangents ? "tangentOutLocal" : "tangentOut"), SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
                }
            }
            if (caps.LocalTangents) SetOptionalBool(data, "offsetsInLocalUnits", true);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        public static void SetPathCollapse(Component socket, bool collapse, int uncollapsedSegment = -1)
        {
            if (!VrcFuryCapabilities.Current.Collapse) return;
            RequireVersion(); var data = SocketData(socket);
            Undo.RecordObject(socket, L("SPS2 体内の太さ"));
            var count = data.FindProperty("guidedPathStops").arraySize;
            for (int i = 0; i < count; i++)
                Require(data, $"guidedPathStops.Array.data[{i}].shrink", SerializedPropertyType.Boolean).boolValue = collapse && i != uncollapsedSegment;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }
        public static void SetPathTangents(Component socket, int segment, Vector3 exit, Vector3 enter)
        {
            var caps = VrcFuryCapabilities.Current;
            if (!caps.Tangents) return;
            RequireVersion(); var data = SocketData(socket);
            if (segment < 0 || segment >= data.FindProperty("guidedPathStops").arraySize)
                throw new InvalidOperationException(L("貫通経路の区間がありません。"));
            Undo.RecordObject(socket, L("SPS2 貫通経路の接線"));
            string prefix = $"guidedPathStops.Array.data[{segment}].";
            Require(data, prefix + "customizeTangentOut", SerializedPropertyType.Boolean).boolValue = true;
            Require(data, prefix + "customizeTangentIn", SerializedPropertyType.Boolean).boolValue = true;
            if (caps.LocalTangents)
            {
                var stop = Require(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue as Transform;
                var previous = segment == 0 ? socket.transform :
                    Require(data, $"guidedPathStops.Array.data[{segment - 1}].transform", SerializedPropertyType.ObjectReference).objectReferenceValue as Transform;
                if (stop == null || previous == null || Mathf.Abs(stop.lossyScale.x) < 1e-6f || Mathf.Abs(previous.lossyScale.x) < 1e-6f)
                    throw new InvalidOperationException(L("経路の参照またはスケールが不正です。"));
                exit /= previous.lossyScale.x; enter /= stop.lossyScale.x;
                SetOptionalBool(data, "offsetsInLocalUnits", true);
            }
            Require(data, prefix + (caps.LocalTangents ? "tangentOutLocal" : "tangentOut"), SerializedPropertyType.Vector3).vector3Value = exit;
            Require(data, prefix + (caps.LocalTangents ? "tangentInLocal" : "tangentIn"), SerializedPropertyType.Vector3).vector3Value = enter;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static string ReadIdentity(Component socket)
        {
            var data = new SerializedObject(socket);
            var property = data.FindProperty("oscId");
            if (property != null && property.propertyType == SerializedPropertyType.String &&
                Sps2SetupStorage.TryToken(property.stringValue, out _, out _)) return property.stringValue;
            var name = data.FindProperty("name");
            return name != null && name.propertyType == SerializedPropertyType.String &&
                Sps2SetupStorage.TryToken(name.stringValue, out _, out _) ? name.stringValue : null;
        }
        internal static void SetAuthoringIdentity(Component socket, string identity)
        {
            var data = SocketData(socket);
            var property = data.FindProperty("oscId") ?? data.FindProperty("name");
            if (property.stringValue == identity) return;
            Undo.RecordObject(socket, L("SPS2 設定識別子"));
            property.stringValue = identity;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static int AutoSocketCount(GameObject avatar) => AllSockets(avatar).Count(s =>
        {
            var data = SocketData(s);
            return data.FindProperty("addMenuItem").boolValue && data.FindProperty("enableAuto")?.boolValue == true;
        });

        public static Component CreateTestPlug(GameObject obj, Renderer[] renderers, string label = "SPS2 テストプラグ")
        {
            RequireVersion();
            if (!VrcFuryCapabilities.Current.TestPlug) throw new InvalidOperationException(L("この版ではテストプラグを生成できません。") + VrcFuryCapabilities.UpdateGuide);
            var type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().SingleOrDefault(t => t.FullName == PlugType);
            if (type == null) throw new InvalidOperationException(L("対応する VRCFury Plug が見つかりません。"));
            var plug = Undo.AddComponent(obj, type);
            var data = new SerializedObject(plug);
            Require(data, data.FindProperty("enableSps") != null ? "enableSps" : "configureSps", SerializedPropertyType.Boolean).boolValue = true;
            Require(data, "autoRenderer", SerializedPropertyType.Boolean).boolValue = false;
            var list = Require(data, "configureTpsMesh", SerializedPropertyType.Generic);
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            Require(data, "name", SerializedPropertyType.String).stringValue = label;
            data.ApplyModifiedProperties();
            return plug;
        }

        internal static bool ParticipatesInAuto(Component socket)
        {
            var data = new SerializedObject(socket);
            var menu = data.FindProperty("addMenuItem"); var auto = data.FindProperty("enableAuto");
            if (menu?.propertyType != SerializedPropertyType.Boolean || auto?.propertyType != SerializedPropertyType.Boolean)
                throw new InvalidOperationException("Unverified native Auto fields");
            return menu.boolValue && auto.boolValue;
        }

        internal static void RequireSavedAutoFlags(Component socket, SetupSettings settings, SocketSettings part)
        {
            var data = new SerializedObject(socket);
            // Customized native flags can survive unrelated Apply. In that case a
            // draft-only replacement estimate would undercount, so report uncertainty.
            if (data.FindProperty("addMenuItem")?.boolValue != true ||
                data.FindProperty("enableAuto")?.boolValue != settings.Sps3IncludeInAuto(part))
                throw new InvalidOperationException("Native customization must be retained");
        }

        internal static bool HasUnconvertedOffset(Component socket)
        {
            var data = new SerializedObject(socket);
            var flag = data.FindProperty("offsetsInLocalUnits");
            var oldOffset = data.FindProperty("legacyOffset");
            return flag != null && flag.propertyType == SerializedPropertyType.Boolean && !flag.boolValue &&
                    oldOffset != null && oldOffset.propertyType == SerializedPropertyType.Vector3 &&
                    !oldOffset.vector3Value.Equals(Vector3.zero);
        }

        internal static bool IsNativeSocketType(Component socket) =>
            socket.GetType().FullName == "VF.Component.VRCFuryHapticSocket";

        internal const string NativeValuePath = "depthActions2.Array.data[0].actionSet.actions.Array.data[0].blendShapeValue";
        private const string NativeActionPath = "depthActions2.Array.data[0].actionSet.actions.Array.data[0]";

        internal static SerializedProperty UniqueNativeWeight(Component socket)
        {
            var data = new SerializedObject(socket);
            var action = data.FindProperty(NativeActionPath); var value = data.FindProperty(NativeValuePath);
            if (action == null || action.propertyType != SerializedPropertyType.ManagedReference ||
                !action.managedReferenceFullTypename.EndsWith(" VF.Model.StateAction.BlendShapeAction", StringComparison.Ordinal) ||
                value == null || value.propertyType != SerializedPropertyType.Float || float.IsNaN(value.floatValue) || float.IsInfinity(value.floatValue)) return null;
            long id = action.managedReferenceId; int aliases = 0; var iterator = data.GetIterator();
            while (iterator.Next(true)) if (iterator.propertyType == SerializedPropertyType.ManagedReference && iterator.managedReferenceId == id) aliases++;
            if (aliases != 1) return null;
            return value;
        }

        internal static bool TryReadAuto(Component socket, out bool value)
        {
            value = false;
            var field = new SerializedObject(socket).FindProperty("enableAuto");
            if (field?.propertyType != SerializedPropertyType.Boolean) return false;
            value = field.boolValue;
            return true;
        }

        internal static void ApplySocketLeaves(Component socket, bool? auto, float? weight, string before)
        {
            var data = new SerializedObject(socket);
            if (auto.HasValue) Require(data, "enableAuto", SerializedPropertyType.Boolean).boolValue = auto.Value;
            if (weight.HasValue) Require(data, NativeValuePath, SerializedPropertyType.Float).floatValue = weight.Value;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
            data.Update();
            string Retained(string state) => string.Join("\n", state.Split('\n').Where(line =>
                !(auto.HasValue && line.StartsWith("enableAuto:", StringComparison.Ordinal)) &&
                !(weight.HasValue && line.StartsWith(NativeValuePath + ":", StringComparison.Ordinal))));
            if (auto.HasValue && data.FindProperty("enableAuto").boolValue != auto.Value ||
                weight.HasValue && data.FindProperty(NativeValuePath).floatValue != weight.Value ||
                Retained(SpsMigrationAdapter.State(socket)) != Retained(before))
                throw new InvalidOperationException("Socket の読み戻し結果が異なるため反映を取り消しました。");
        }

        internal static SerializedProperty UpdateVerifiedWeight(Component socket, float previous, string fullState, float next)
        {
            var data = new SerializedObject(socket); var value = data.FindProperty(NativeValuePath);
            if (value == null || value.propertyType != SerializedPropertyType.Float || value.floatValue != previous ||
                SpsMigrationAdapter.State(socket) != fullState) throw new InvalidOperationException("Socket changed during validation.");
            value.floatValue = next; data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
            return new SerializedObject(socket).FindProperty(NativeValuePath);
        }
    }
}
