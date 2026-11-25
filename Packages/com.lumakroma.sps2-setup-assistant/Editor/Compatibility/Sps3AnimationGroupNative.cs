using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Only the pinned native schema is writable. All capability/input checks run before Undo/assets.
    internal static class Sps3AnimationGroupNative
    {
        internal const string DepthList = "depthActions2", EntryList = "plugEntryRadiusActions";
        internal static string MouthNeutralProblem(SetupSettings setup, SocketSettings part, SocketAnimationGroup group)
        {
            var neutral = group.mouthNeutral;
            if (neutral?.renderer == null || neutral.renderer.sharedMesh == null || string.IsNullOrEmpty(neutral.shape) ||
                neutral.renderer.sharedMesh.GetBlendShapeIndex(neutral.shape) < 0 || !Finite(neutral.weight) ||
                neutral.weight < 0 || neutral.weight > 100)
                return "口形状の固定には、有効なメッシュ、BlendShape、固定値を指定してください。";
            var owner = neutral.renderer.GetComponentInParent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true);
            if (owner == null || !group.actions.Any(a => a?.kind == DepthActionKind.BlendShape && a.renderer != null && (a.blendShapeMode == BlendShapeMode.Custom || a.weight > 0 && Finite(a.weight)) &&
                a.renderer.GetComponentInParent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true) == owner && a.renderer.sharedMesh != null &&
                !string.IsNullOrEmpty(a.shape) && a.renderer.sharedMesh.GetBlendShapeIndex(a.shape) >= 0))
                return "口形状の固定には、同じアバターで口を動かす深度アクションが必要です。";
            foreach (var other in part.EffectiveAnimationGroups())
            {
                if (other.actions == null) continue;
                foreach (var a in other.actions.Where(a => a != null)) {
                    if (a.kind == DepthActionKind.AnimationClip && a.clip != null && AnimationUtility.GetCurveBindings(a.clip).Any(b =>
                        b.type == typeof(SkinnedMeshRenderer) && b.propertyName == "blendShape." + neutral.shape))
                        return "口のクリップと書き込み先が重ならないことを確認できません。";
                    if (a.kind == DepthActionKind.BlendShape && a.shape == neutral.shape && (a.renderer == neutral.renderer || a.renderer == null))
                        return "固定先とアニメーションに同じ BlendShape が選ばれています。";
                }
                if (other.id != group.id && other.kind == SocketAnimationKind.Depth && other.mouthNeutralEnabled &&
                    other.mouthNeutral?.renderer == neutral.renderer && other.mouthNeutral.shape == neutral.shape)
                    return "複数の深度グループが同じ表情抑制先を指定しています。";
            }
            foreach (var otherPart in setup.parts.Where(p => p != null && p.id != part.id && p.included && p.AnimationEnabled))
                if (otherPart.EffectiveAnimationGroups().Where(g => g.actions != null).SelectMany(g => g.actions).Any(a => a?.kind == DepthActionKind.BlendShape &&
                    a.shape == neutral.shape && (a.renderer == neutral.renderer || a.renderer == null)))
                    return "固定先とアニメーションに同じ BlendShape が選ばれています。";
            return null;
        }
        internal static IEnumerable<SocketAnimationGroup> Ordered(SocketSettings part) =>
            part.EffectiveAnimationGroups().OrderBy(g => g.kind);
        internal static bool WeightPending(SocketAnimationSettings value) => value != null &&
            value.radiusWeightEnabled && value.radiusZero == 0 && value.radiusFull == 0;
        internal static bool WeightActive(SocketAnimationSettings value) => value != null &&
            value.radiusWeightEnabled && value.radiusFull > 0;
        internal static bool CanWriteOptional(SocketSettings part, SocketAnimationKind kind, out string reason)
        {
            reason = null;
            try {
                RequireCapabilities(part, kind == SocketAnimationKind.Depth,
                    kind == SocketAnimationKind.Active, kind == SocketAnimationKind.PlugEntryRadius);
                return true;
            } catch (InvalidOperationException e) { reason = e.Message; return false; }
        }
        internal static void RequireNoPendingWeightLoss(SocketSettings part, Sps2SetupContext existing)
        {
            if (part.animationModelVersion == 0 || existing?.settings?.parts == null) return;
            var saved = existing.settings.parts.SingleOrDefault(p => p != null && p.id == part.id);
            if (saved?.animationModelVersion != 1 || saved.animationGroups == null) return;
            foreach (var group in part.animationGroups)
            {
                if (group.kind != SocketAnimationKind.Depth || !WeightPending(group.depthSettings)) continue;
                var prior = saved.animationGroups.SingleOrDefault(g => g != null && g.id == group.id);
                if (prior != null && WeightActive(prior.depthSettings))
                    throw new InvalidOperationException("An existing radius weight cannot be replaced by an unset pending value. Enter a valid threshold or explicitly disable weighting: " + part.id + ":" + group.id);
            }
        }
        internal static bool HasLocal(SocketSettings part) => part.AnimationEnabled &&
            part.EffectiveAnimationGroups().Any(g => g.kind != SocketAnimationKind.Active && (g.ActiveSettings.units == DepthUnits.Local ||
                g.kind == SocketAnimationKind.Depth && WeightActive(g.ActiveSettings)));
        internal static IEnumerable<DepthActionSettings> AllSavedActions(SocketSettings part) =>
            (part.actions ?? new List<DepthActionSettings>()).Concat(
                (part.animationGroups ?? new List<SocketAnimationGroup>()).Where(g => g != null)
                .SelectMany(g => g.actions ?? new List<DepthActionSettings>()));

        internal static void RequireModel(SocketSettings part, bool depth = true, bool active = true, bool entry = true)
        {
            if (part.animationModelVersion == 0) return;
            if (part.animationModelVersion != 1 || part.animationGroups == null)
                throw new InvalidOperationException("Unsupported or missing animation groups: " + part.id);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in part.animationGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.id) ||
                    group.id != SocketAnimationGroup.LegacyId && !Guid.TryParseExact(group.id, "N", out _) ||
                    !ids.Add(group.id) ||
                    group.kind != SocketAnimationKind.Depth && group.kind != SocketAnimationKind.PlugEntryRadius && group.kind != SocketAnimationKind.Active)
                    throw new InvalidOperationException("Incomplete, duplicate or unknown animation group: " + part.id);
                bool requested = group.kind == SocketAnimationKind.Depth ? depth : group.kind == SocketAnimationKind.Active ? active : entry;
                if (!requested || !part.AnimationEnabled) continue;
                if (group.actions == null || group.actions.Any(a => a == null) ||
                    (group.kind == SocketAnimationKind.Depth && group.depthSettings == null) ||
                    (group.kind == SocketAnimationKind.PlugEntryRadius && group.entrySettings == null))
                    throw new InvalidOperationException("Incomplete animation group: " + part.id + ":" + group.id);
                // Inactive banks remain verbatim; never reinterpret Depth Plugs as Entry Local.
                if (!part.AnimationEnabled || group.kind == SocketAnimationKind.Active) continue;
                var value = group.ActiveSettings;
                if (!Finite(value.range.x) || !Finite(value.range.y) || value.range.x > value.range.y ||
                    !Finite(value.smoothingSeconds) || value.smoothingSeconds < 0 ||
                    value.units != DepthUnits.Meters && value.units != DepthUnits.Local &&
                    (group.kind != SocketAnimationKind.Depth || value.units != DepthUnits.Plugs))
                    throw new InvalidOperationException("Invalid animation range, units or smoothing: " + part.id + ":" + group.id);
                if (group.kind == SocketAnimationKind.Depth && value.radiusWeightEnabled && !WeightPending(value) &&
                    (!Finite(value.radiusZero) || !Finite(value.radiusFull) || value.radiusZero < 0 ||
                     value.radiusFull <= 0 || value.radiusZero > value.radiusFull))
                    throw new InvalidOperationException("Radius weighting requires 0 <= zero <= full, full > 0: " + part.id);
            }
        }
        internal static void RequireCapabilities(SocketSettings part, bool depth = true, bool active = true, bool entry = true)
        {
            RequireModel(part, depth, active, entry);
            if (part.animationModelVersion == 0 || !FuryNativeFeatureShapes.Requested(part.included, part.AnimationEnabled, depth || active || entry)) return;
            if (SpsBackend.ActiveFamily != SpsFamily.VrcFury ||
                SpsAuthoringCompatibility.ActiveContract != SpsAuthoringContract.Sps3Beta14)
                throw new InvalidOperationException("Animation groups require a reviewed SPS3 setup. Legacy/NDMF output is unchanged.");
            var type = VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury));
            if (!SpsBackend.PackageMatches(type, SpsFamily.VrcFury, out _) ||
                !FuryPersistentScriptProbe.HasPersistentBinding(type, SpsBackend.PackageId(SpsFamily.VrcFury), out _))
                throw new InvalidOperationException("Animation groups require a saveable Fury Socket.");
            if (!part.AnimationEnabled) return;
            foreach (var group in part.animationGroups)
            {
                if (!FuryNativeFeatureShapes.Requested(part.included, part.AnimationEnabled,
                    group.kind == SocketAnimationKind.Active ? active : group.kind == SocketAnimationKind.Depth ? depth : entry)) continue;
                if (group.kind == SocketAnimationKind.Active) {
                    var stateType=VrcFuryApi.FindType("VF.Model.State");
                    var activeActionType=VrcFuryApi.FindType("VF.Model.StateAction.Action");
                    var api=VrcFuryApi.FindType("com.vrcfury.api.Components.FurySocket");
                    if(stateType==null||activeActionType==null||api?.GetMethod("GetActiveActions",Type.EmptyTypes)==null)
                        throw new InvalidOperationException("Active animation public API is unavailable.");
                    RequireField(type,"activeActions",stateType);
                    RequireField(stateType,"actions",typeof(List<>).MakeGenericType(activeActionType));
                    continue;
                }
                RequireList(type, DepthList); // Public AddDepthActions stages both depth and entry groups.
                var native = RequireList(type, group.kind == SocketAnimationKind.Depth ? DepthList : EntryList);
                RequireField(native, "range", typeof(Vector2));
                RequireField(native, "reverseClip", typeof(bool));
                var state = VrcFuryApi.FindType("VF.Model.State");
                if (state == null) throw new InvalidOperationException("Native animation State is unavailable.");
                RequireField(native, "actionSet", state);
                var actionType = VrcFuryApi.FindType("VF.Model.StateAction.Action");
                if (actionType == null) throw new InvalidOperationException("Native animation action type is unavailable.");
                RequireField(state, "actions", typeof(List<>).MakeGenericType(actionType));
                if (native.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException("Native animation group constructor is unavailable.");
                foreach (var key in new[] { "allowHole", "allowRing" })
                    if (native.GetField(key) != null) RequireField(native, key, typeof(bool));
                foreach (var key in new[] { "plugRadiusForFullWeight", "plugRadiusForZeroWeight" })
                    if (native.GetField(key) != null) RequireField(native, key, typeof(float));
                var units = native.GetField("units")?.FieldType;
                if (units == null || !units.IsEnum || !Enum.GetNames(units).Contains(group.ActiveSettings.units.ToString()))
                    throw new InvalidOperationException("Native animation units are unavailable.");
                RequireField(native, "enableSelf", typeof(bool));
                RequireField(native, "enableOthers", typeof(bool));
                RequireField(native, "smoothingSeconds", typeof(float));
                if (group.kind == SocketAnimationKind.Depth && group.ActiveSettings.radiusWeightEnabled &&
                    !group.ActiveSettings.radiusKeepNative)
                {
                    RequireField(native, "plugRadiusForFullWeight", typeof(float));
                    if (group.ActiveSettings.radiusZero != 0)
                        RequireField(native, "plugRadiusForZeroWeight", typeof(float));
                }
            }
        }
        private static Type RequireList(Type native, string name)
        {
            var list = native?.GetField(name)?.FieldType;
            if (list == null || !list.IsGenericType || list.GetGenericTypeDefinition() != typeof(List<>))
                throw new InvalidOperationException("This reviewed version does not support animation field: " + name);
            return list.GetGenericArguments()[0];
        }
        private static void RequireField(Type native, string name, Type type)
        {
            if (native.GetField(name)?.FieldType != type)
                throw new InvalidOperationException("This reviewed version does not support animation field: " + name);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool SameOutput(SocketSettings a, SocketSettings b, int subset = 0)
        {
            if (a == null || b == null) return false;
            if (subset == 2 && !Ordered(a).Any(g=>g.kind==SocketAnimationKind.Active) &&
                !Ordered(b).Any(g=>g.kind==SocketAnimationKind.Active)) return true;
            if (a.AnimationEnabled != b.AnimationEnabled) return false;
            if (!a.AnimationEnabled) return true;
            bool Include(SocketAnimationGroup g) => subset == 0 || (subset == 1 ? g.kind != SocketAnimationKind.Active :
                subset == 2 ? g.kind == SocketAnimationKind.Active : subset == 3 ? g.kind == SocketAnimationKind.Depth : g.kind == SocketAnimationKind.PlugEntryRadius);
            var left = Ordered(a).Where(Include).ToArray(); var right = Ordered(b).Where(Include).ToArray();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                var x = left[i]; var y = right[i]; var xs = x?.ActiveSettings; var ys = y?.ActiveSettings;
                if (a.id == "mouth" && x?.kind == SocketAnimationKind.Depth && y?.kind == SocketAnimationKind.Depth &&
                    (x.mouthNeutralEnabled != y.mouthNeutralEnabled || x.mouthNeutralEnabled &&
                    (x.mouthNeutral?.renderer != y.mouthNeutral?.renderer || x.mouthNeutral?.shape != y.mouthNeutral?.shape ||
                    x.mouthNeutral?.weight != y.mouthNeutral?.weight))) return false;
                if (x == null || y == null || xs == null || ys == null || x.kind != y.kind ||
                    x.kind != SocketAnimationKind.Active && (xs.range.x != ys.range.x || xs.range.y != ys.range.y || xs.units != ys.units || xs.enableSelf != ys.enableSelf ||
                    xs.enableOthers != ys.enableOthers || xs.smoothingSeconds != ys.smoothingSeconds) ||
                    x.kind == SocketAnimationKind.Depth && (WeightActive(xs) != WeightActive(ys) ||
                    WeightActive(xs) && (xs.radiusZero != ys.radiusZero || xs.radiusFull != ys.radiusFull)) ||
                    x.actions == null || y.actions == null || x.actions.Count != y.actions.Count) return false;
                for (int n = 0; n < x.actions.Count; n++)
                {
                    var u = x.actions[n]; var v = y.actions[n];
                    if (u == null || v == null || u.kind != v.kind || u.renderer != v.renderer || u.shape != v.shape ||
                        u.weight != v.weight || x.kind != SocketAnimationKind.Active && !BlendShapePointPlan.Same(u,v) || u.clip != v.clip || u.target != v.target || u.objectOn != v.objectOn) return false;
                }
            }
            return true;
        }
    }

    public static partial class VrcFuryCompatibility
    {
        private static bool NewAnimationListsAreIndependent(Component component)
        {
            try {
                var type = component.GetType();
                var depth = type.GetField(Sps3AnimationGroupNative.DepthList)?.GetValue(component);
                var entry = type.GetField(Sps3AnimationGroupNative.EntryList)?.GetValue(component);
                return NativeSlotIsolation.Separate(depth, entry);
            } catch { return false; }
        }

        // New native Socket only. Asset-producing custom curves keep the ordinary
        // fail-whole-generation path until their asset lifecycle can be verified.
        private static bool ConfigureNewDepthOrEntrySafely(Component component, SocketSettings part,
            SetupSettings setup, List<string> warnings, SocketAnimationKind kind)
        {
            bool depth = kind == SocketAnimationKind.Depth;
            var groups = part.EffectiveAnimationGroups().Where(g => g.kind == kind).ToArray();
            if (groups.SelectMany(g => g.actions).Any(a => a != null && a.kind == DepthActionKind.BlendShape &&
                    a.blendShapeMode == BlendShapeMode.Custom)) {
                Configure(component, part, setup, warnings, depth, false, false, !depth);
                return true;
            }
            string path = depth ? Sps3AnimationGroupNative.DepthList : Sps3AnimationGroupNative.EntryList;
            string before;
            try {
                var list = new SerializedObject(component).FindProperty(path);
                if (list == null || !list.isArray || list.arraySize != 0)
                    throw new InvalidOperationException("対象リストが空と確認できません。");
                before = SpsMigrationAdapter.State(component);
            } catch (Exception e) {
                warnings?.Add(part.name + ": " + kind + " を安全に準備できないため省略しました: " + e.Message);
                return false;
            }
            GameObject scratch = null;
            bool attemptedCommit = false;
            var localWarnings = new List<string>();
            try {
                scratch = new GameObject("Owned SPS optional action staging") { hideFlags = HideFlags.HideAndDontSave };
                VrcFuryApi.CreateSocket(scratch);
                var staged = FindSocket(scratch);
                if (staged == null) throw new InvalidOperationException("仮 Socket を作成できません。");
                Configure(staged, part, setup, localWarnings, depth, false, false, !depth);
                var expected = SpsMigrationAdapter.State(staged, path);
                attemptedCommit = true;
                Configure(component, part, setup, localWarnings, depth, false, false, !depth);
                if (SpsMigrationAdapter.State(component, path) != expected)
                    throw new InvalidOperationException("書き込み後の値または参照が一致しません。");
                string WithoutPath(string state) => string.Join("\n", state.Split('\n')
                    .Where(line => !line.StartsWith(path + ":", StringComparison.Ordinal) &&
                        !line.StartsWith(path + ".", StringComparison.Ordinal)));
                if (WithoutPath(SpsMigrationAdapter.State(component)) != WithoutPath(before))
                    throw new InvalidOperationException("対象以外の Socket 設定が変わりました。");
                if (warnings != null) warnings.AddRange(localWarnings.Distinct());
                return true;
            } catch (Exception e) {
                if (attemptedCommit) {
                    try {
                        var restore = new SerializedObject(component);
                        var list = restore.FindProperty(path);
                        if (list == null || !list.isArray) throw new InvalidOperationException("対象リストを復元できません。");
                        list.arraySize = 0;
                        restore.ApplyModifiedPropertiesWithoutUndo();
                        if (SpsMigrationAdapter.State(component) != before)
                            throw new InvalidOperationException("元の Socket 状態を確認できません。");
                    } catch (Exception restoreError) {
                        throw new InvalidOperationException(part.name + ": " + kind + " の失敗後に復元できませんでした。生成を中止します。", restoreError);
                    }
                }
                warnings?.Add(part.name + ": " + kind + " を適用できなかったため省略しました。入力設定は保持します: " + e.Message);
                return false;
            } finally {
                if (scratch != null) {
                    foreach (var c in scratch.GetComponents<Component>()) if (c != null) Undo.ClearUndo(c);
                    Undo.ClearUndo(scratch);
                    UnityEngine.Object.DestroyImmediate(scratch);
                }
            }
        }

        // Only for a freshly created native Socket. Existing components always
        // use the preservation-aware ConfigureAnimationGroups path.
        private static void ConfigureNewActiveSafely(Component component, SocketSettings part, List<string> warnings)
        {
            const string path = "activeActions.actions";
            string before;
            try {
                var current = new SerializedObject(component).FindProperty(path);
                if (current == null || !current.isArray || current.arraySize != 0)
                    throw new InvalidOperationException("新規 Socket の Active 領域が空と確認できません。");
                before = SpsMigrationAdapter.State(component);
            } catch (Exception e) {
                warnings?.Add(part.name + ": Active を安全に準備できないため省略しました: " + e.Message);
                return;
            }
            try {
                var actions = Sps3AnimationGroupNative.Ordered(part)
                    .Where(g => g.kind == SocketAnimationKind.Active).SelectMany(g => g.actions);
                using var draft = Sps3ActiveAnimationDraft.Create(actions, warnings);
                var expected = SpsMigrationAdapter.State(draft.Native, path);
                var target = new SerializedObject(component);
                Sps3ActiveActionTransfer.Stage(target, draft);
                Undo.RecordObject(component, "SPS3 new Active actions");
                target.ApplyModifiedProperties();
                if (SpsMigrationAdapter.State(component, path) != expected)
                    throw new InvalidOperationException("Active の書き込み結果が入力と一致しません。");
                // Also make sure other native data was not altered by this commit.
                string WithoutActive(string state) => string.Join("\n", state.Split('\n')
                    .Where(line => !line.StartsWith(path + ":", StringComparison.Ordinal) &&
                        !line.StartsWith(path + ".", StringComparison.Ordinal)));
                if (WithoutActive(SpsMigrationAdapter.State(component)) != WithoutActive(before))
                    throw new InvalidOperationException("Active 以外の Socket 設定が変わりました。");
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            } catch (Exception e) {
                // The precondition proves an empty original list. If a partial
                // commit cannot be restored and verified, fail the whole run.
                try {
                    var restore = new SerializedObject(component);
                    var list = restore.FindProperty(path);
                    if (list == null || !list.isArray) throw new InvalidOperationException("Active 領域を復元できません。");
                    list.arraySize = 0;
                    restore.ApplyModifiedPropertiesWithoutUndo();
                    if (SpsMigrationAdapter.State(component) != before)
                        throw new InvalidOperationException("元の Socket の状態を確認できません。");
                } catch (Exception restoreError) {
                    throw new InvalidOperationException(part.name + ": Active の失敗後に Socket を復元できませんでした。生成を中止します。", restoreError);
                }
                warnings?.Add(part.name + ": Active を適用できなかったため省略しました。入力設定は保持します: " + e.Message);
            }
        }

        private static void ConfigureAnimationGroups(Component component, SocketSettings part, SetupSettings setup,
            List<string> warnings, bool rewrite, bool common, bool rewriteActive, bool rewriteEntry)
        {
            Sps3AnimationGroupNative.RequireCapabilities(part, rewrite, rewriteActive, rewriteEntry);
            var target = SocketData(component);
            // Existing depth groups may contain values not represented in the draft.
            // Do not replace them with an unverified native default.
            if (rewrite && part.AnimationEnabled && part.EffectiveAnimationGroups().Any(g =>
                    g.kind == SocketAnimationKind.Depth && g.depthSettings != null && g.depthSettings.radiusWeightEnabled && g.depthSettings.radiusKeepNative) &&
                target.FindProperty(Sps3AnimationGroupNative.DepthList)?.arraySize > 0)
                throw new InvalidOperationException("The existing depth settings cannot be replaced while the automatic radius is unavailable. Existing values are preserved.");
            var temporary = new GameObject("SPS3 animation staging") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var wrapper = VrcFuryApi.CreateSocket(temporary);
                var native = FindSocket(temporary);
                var depthField = native.GetType().GetField(Sps3AnimationGroupNative.DepthList);
                var depth = depthField?.GetValue(native) as IList;
                var entryField = native.GetType().GetField(Sps3AnimationGroupNative.EntryList);
                var entry = entryField?.GetValue(native) as IList;
                if ((rewrite || rewriteEntry) && part.AnimationEnabled)
                foreach (var group in CustomBlendShapeOutput.EmissionGroups(part).Where(g=>
                    g.kind == SocketAnimationKind.Depth ? rewrite : g.kind == SocketAnimationKind.PlugEntryRadius && rewriteEntry))
                {
                    var value = group.ActiveSettings;
                    var actions = wrapper.AddDepthActions(value.range, value.smoothingSeconds, value.enableSelf);
                    depth = (IList)native.GetType().GetField(Sps3AnimationGroupNative.DepthList).GetValue(native);
                    var payload = depth[depth.Count - 1];
                    int count = 0;
                    var off = new List<int>();
                    foreach (var action in group.actions)
                    {
                        if (GenerationWarningPolicy.Unassigned(action)) { warnings?.Add(part.name + ": Unassigned animation action skipped; input retained."); continue; }
                        switch (action.kind)
                        {
                            case DepthActionKind.BlendShape:
                                if (action.blendShapeMode == BlendShapeMode.Custom) actions.AddAnimationClip(CustomBlendShapeOutput.RequireClip(action));
                                else actions.AddBlendshape(action.shape, Mathf.Clamp(action.weight, 0, 100), action.renderer);
                                break;
                            case DepthActionKind.AnimationClip: actions.AddAnimationClip(action.clip); break;
                            case DepthActionKind.Object: actions.AddTurnOn(action.target); if (!action.objectOn) off.Add(count); break;
                            default: throw new InvalidOperationException("Unknown animation action.");
                        }
                        count++;
                    }
                    // TurnOff is not exposed by the pinned public API; modify only this owned staging row.
                    var staged = SocketData(native);
                    foreach (int index in off)
                        SetEnum(Require(staged, $"depthActions2.Array.data[{depth.Count - 1}].actionSet.actions.Array.data[{index}].mode", SerializedPropertyType.Enum), "TurnOff");
                    staged.ApplyModifiedPropertiesWithoutUndo();
                    // SerializedObject may replace the managed payload. Fetch it again after applying.
                    depth = (IList)native.GetType().GetField(Sps3AnimationGroupNative.DepthList).GetValue(native);
                    entry = entryField?.GetValue(native) as IList;
                    payload = depth[depth.Count - 1];
                    if (group.kind == SocketAnimationKind.PlugEntryRadius)
                    {
                        var item = Activator.CreateInstance(entryField.FieldType.GetGenericArguments()[0]);
                        item.GetType().GetField("actionSet").SetValue(item, payload.GetType().GetField("actionSet").GetValue(payload));
                        depth.RemoveAt(depth.Count - 1); entry.Add(item); payload = item;
                    }
                    void Set(string field, object input) => payload.GetType().GetField(field).SetValue(payload, input);
                    Set("range", value.range); Set("units", Enum.Parse(payload.GetType().GetField("units").FieldType, value.units.ToString()));
                    Set("enableSelf", value.enableSelf); Set("enableOthers", value.enableOthers); Set("smoothingSeconds", value.smoothingSeconds);
                    Set("reverseClip", false);
                    bool weighted = group.kind == SocketAnimationKind.Depth && Sps3AnimationGroupNative.WeightActive(value);
                    if (!value.radiusWeightEnabled || !value.radiusKeepNative)
                    {
                        payload.GetType().GetField("plugRadiusForFullWeight")?.SetValue(payload, weighted ? value.radiusFull : 0f);
                        payload.GetType().GetField("plugRadiusForZeroWeight")?.SetValue(payload, weighted ? value.radiusZero : 0f);
                    }
                }
                var source = SocketData(native);
                // Validate destination schema before the first Undo write. Each native list is independent.
                if (FuryNativeFeatureShapes.WriteList(true, rewrite, rewriteEntry)) {
                    if (source.FindProperty(Sps3AnimationGroupNative.DepthList) != null || target.FindProperty(Sps3AnimationGroupNative.DepthList) != null)
                        CopyAnimationList(source, target, Sps3AnimationGroupNative.DepthList);
                }
                if (FuryNativeFeatureShapes.WriteList(false, rewrite, rewriteEntry)) {
                    if (source.FindProperty(Sps3AnimationGroupNative.EntryList) != null || target.FindProperty(Sps3AnimationGroupNative.EntryList) != null)
                        CopyAnimationList(source, target, Sps3AnimationGroupNative.EntryList);
                }
                if (rewriteActive) {
                    if (!common && part.AnimationEnabled && part.animationGroups.Any(g=>g.kind==SocketAnimationKind.Active) &&
                        target.FindProperty("addMenuItem")?.boolValue == false)
                        warnings?.Add("Active AnimationにはFury側のSocketメニューが必要です。FuryのAdd Menu Itemを確認してください。手動設定は変更しません。");
                    var active = part.AnimationEnabled ? Sps3AnimationGroupNative.Ordered(part).Where(g=>g.kind==SocketAnimationKind.Active).SelectMany(g=>g.actions) : Enumerable.Empty<DepthActionSettings>();
                    var activeItems = active.ToArray();
                    var activeCaps = FuryActiveShapeProbe.Inspect();
                    var existingActive = target.FindProperty("activeActions.actions");
                    bool incomplete = activeItems.Any(a => a != null && !GenerationWarningPolicy.Unassigned(a) && !activeCaps.Supports(a));
                    if (FuryActiveShapeProbe.PreserveExisting(activeCaps.State, incomplete, existingActive != null && existingActive.isArray ? existingActive.arraySize : 0)) {
                        warnings?.Add("Active actions were retained because the current Fury cannot safely represent every saved action.");
                    } else using(var draft = Sps3ActiveAnimationDraft.Create(activeItems,warnings)) {
                        Sps3ActiveActionTransfer.Stage(target, draft);

                    }
                }
                Undo.RecordObject(component, "SPS3 animation groups");
                if (common)
                {
                    target.FindProperty("name").stringValue = SocketName(part, setup);
                    SetOptionalBool(target, "enableAuto", setup.Sps3IncludeInAuto(part));
                    if (VrcFuryCapabilities.Current.Legacy) SetOptionalBool(target, "useLights", setup.legacy);
                    target.FindProperty("addMenuItem").boolValue = true;
                }
                target.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }
        private static void CopyAnimationList(SerializedObject source, SerializedObject target, string list)
        {
            var from = Require(source, list, SerializedPropertyType.Generic);
            var to = Require(target, list, SerializedPropertyType.Generic);
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                string path = list + $".Array.data[{i}].";
                Require(target, path + "range", SerializedPropertyType.Vector2).vector2Value = Require(source, path + "range", SerializedPropertyType.Vector2).vector2Value;
                SetEnum(Require(target, path + "units", SerializedPropertyType.Enum), Require(source, path + "units", SerializedPropertyType.Enum).enumNames[Require(source, path + "units", SerializedPropertyType.Enum).enumValueIndex]);
                foreach (string key in new[] { "enableSelf", "enableOthers", "allowHole", "allowRing", "reverseClip" })
                    CopyOptionalDepthField(source, target, path + key, SerializedPropertyType.Boolean);
                foreach (string key in new[] { "smoothingSeconds", "plugRadiusForFullWeight", "plugRadiusForZeroWeight" })
                    CopyOptionalDepthField(source, target, path + key, SerializedPropertyType.Float);
                var actions = Require(source, path + "actionSet.actions", SerializedPropertyType.Generic);
                Require(target, path + "actionSet.actions", SerializedPropertyType.Generic).arraySize = actions.arraySize;
                for (int n = 0; n < actions.arraySize; n++)
                {
                    string action = path + $"actionSet.actions.Array.data[{n}]";
                    Require(target, action, SerializedPropertyType.ManagedReference).managedReferenceValue = Require(source, action, SerializedPropertyType.ManagedReference).managedReferenceValue;
                }
            }
        }
    }
}
