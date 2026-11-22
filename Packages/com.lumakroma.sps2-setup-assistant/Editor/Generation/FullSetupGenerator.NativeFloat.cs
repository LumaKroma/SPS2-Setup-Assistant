using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        private sealed class NativeFloatTarget { internal GeneratedSocket socket; internal SocketSettings part; internal DepthActionSettings action; internal float value; internal string state; internal string fullState; }

        // A failed probe is never permission to change anything. The temporary component is
        // compared including references except the target float and two preserved radius leaves.
        // Radius is never written by this path; the draft comparison below requires its authoring values unchanged.
        private static NativeFloatTarget ProbeNativeFloat(Sps2SetupContext root)
        {
            if (root == null || root.asset == null || root.AuthoringContract != SpsAuthoringContract.Sps3Beta14 ||
                root.avatar == null || EditorUtility.IsPersistent(root.avatar) || root.settings?.parts == null) return null;
            var candidates = root.settings.parts.Where(p => p != null && p.included && p.AnimationEnabled && p.animationModelVersion == 1 &&
                p.animationGroups != null && p.animationGroups.Count(g => g != null && g.kind == SocketAnimationKind.Depth) == 1 &&
                p.animationGroups.Where(g => g != null && g.kind == SocketAnimationKind.Depth).All(g => g.actions != null && g.actions.Count == 1 &&
                    g.actions[0] != null && g.actions[0].kind == DepthActionKind.BlendShape && g.actions[0].blendShapeMode == BlendShapeMode.Normal &&
                    g.actions[0].renderer != null && !string.IsNullOrEmpty(g.actions[0].shape))).ToArray();
            if (candidates.Length != 1) return null;
            var part = candidates[0]; var group = part.animationGroups.Single(g => g != null && g.kind == SocketAnimationKind.Depth);
            var socket = root.sockets.Where(s => s != null && s.id == part.id).ToArray();
            if (socket.Length != 1 || socket[0].socket == null || EditorUtility.IsPersistent(socket[0].socket)) return null;
            GameObject temporary = null;
            try {
                Sps3NativeEditProtection.RequireNoMissingReferences(socket[0].socket);
                var value = VrcFuryCompatibility.UniqueNativeWeight(socket[0].socket);
                if (value == null) return null;
                temporary = new GameObject("SPS value comparison") { hideFlags = HideFlags.HideAndDontSave };
                using (SpsBackend.Use(root.settings.family)) using (SpsAuthoringCompatibility.Use(root.AuthoringContract)) {
                    VrcFuryApi.CreateSocket(temporary); var expected = VrcFuryCompatibility.FindSocket(temporary);
                    if (expected == null || expected.GetType() != socket[0].socket.GetType()) return null;
                    VrcFuryCompatibility.Configure(expected, part, root.settings, new List<string>(), true, false, false, false);
                    bool Ignore(string line) => line.StartsWith(VrcFuryCompatibility.NativeValuePath + ":", StringComparison.Ordinal) ||
                        (line.StartsWith("depthActions2.Array.data[0].plugRadiusForZeroWeight:", StringComparison.Ordinal) ||
                            line.StartsWith("depthActions2.Array.data[0].plugRadiusForFullWeight:", StringComparison.Ordinal));
                    string WithoutValue(string state) => string.Join("\n", state.Split('\n').Where(line => !Ignore(line)));
                    var actualState = SpsMigrationAdapter.State(socket[0].socket, "depthActions2");
                    if (WithoutValue(actualState) != WithoutValue(SpsMigrationAdapter.State(expected, "depthActions2"))) return null;
                    return new NativeFloatTarget { socket = socket[0], part = part, action = group.actions[0], value = value.floatValue, state = actualState, fullState = SpsMigrationAdapter.State(socket[0].socket) };
                }
            } catch { return null; }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }

        internal static void SynchronizeNativeFloat(Sps2SetupContext root)
        {
            var target = ProbeNativeFloat(root);
            if (target != null) target.action.weight = target.value;
        }

        private static bool CanEditSocketInstance(VRCAvatarDescriptor avatar) =>
            avatar != null && !EditorUtility.IsPersistent(avatar) && avatar.gameObject.scene.IsValid() &&
            avatar.gameObject.scene.isLoaded && PrefabStageUtility.GetPrefabStage(avatar.gameObject) == null &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        // Native enableAuto is the applied value on this instance. A pending window draft is
        // never replaced. Old schemas without explicit participant choices remain read-only.
        private static bool TryReadNativeAuto(Sps2SetupContext root, SocketSettings part, out bool value)
        {
            value = false;
            if (part == null || !part.included || root?.avatar == null || root.asset == null ||
                root.settings?.parts == null ||
                root.settings.parts.Count(p => p != null && p.id == part.id) != 1) return false;
            var slot = root.sockets.SingleOrDefault(x => x != null && x.id == part.id);
            var socket = slot?.socket;
            if (socket == null || slot.pose == null || slot.anchor == null || socket.transform != slot.pose ||
                slot.pose.parent != slot.anchor || slot.anchor.parent != root.transform || EditorUtility.IsPersistent(socket) ||
                socket.GetComponentInParent<VRCAvatarDescriptor>(true) != root.avatar ||
                !SpsBackend.PackageMatches(socket.GetType(), root.settings.family, out _) ||
                VrcFuryCompatibility.ReadIdentity(socket) != Sps2SetupStorage.Token(root, part.id)) return false;
            return VrcFuryCompatibility.TryReadAuto(socket, out value);
        }

        internal static void SynchronizeNativeAuto(Sps2SetupContext root, SetupSettings settings)
        {
            if (settings?.parts == null) return;
            if (root.AuthoringContract == SpsAuthoringContract.LegacySps2) {
                if (TryReadLegacyAuto(root, out var auto)) settings.autoMode = auto;
                return;
            }
            if (settings.sps3AutoEligibilityVersion != 1) return;
            foreach (var part in settings.parts)
                if (TryReadNativeAuto(root, part, out var value)) part.sps3AutoParticipant = value;
        }
        // The legacy UI has a single Auto switch. Mixed native values cannot be represented.
        private static bool TryReadLegacyAuto(Sps2SetupContext root, out bool enabled)
        {
            enabled = false;
            bool any = false;
            foreach (var part in root.settings.parts.Where(p => p != null && p.included && !p.IsFingerRing)) {
                if (!TryReadNativeAuto(root, part, out var value) || any && value != enabled) return false;
                enabled = value;
                any = true;
            }
            return any;
        }

        private static string DescribeDepthIssue(SocketSettings part, VRCAvatarDescriptor avatar)
        {
            foreach (var group in part.EffectiveAnimationGroups()) {
                if (group?.kind != SocketAnimationKind.Depth || group.actions == null) continue;
                var seen = new HashSet<(SkinnedMeshRenderer, string)>();
                foreach (var action in group.actions) {
                    if (action == null || action.kind != DepthActionKind.BlendShape || action.blendShapeMode != BlendShapeMode.Normal) continue;
                    var renderer = action.renderer;
                    if (renderer == null || renderer.sharedMesh == null || !renderer.transform.IsChildOf(avatar.transform) ||
                        string.IsNullOrEmpty(action.shape) || renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0)
                        return L("{0}の BlendShape はメッシュまたはシェイプを確認できないため反映できませんでした。");
                    if (!seen.Add((renderer, action.shape)))
                        return L("{0}の BlendShape は同じシェイプが重複しているため反映できませんでした。");
                    if (float.IsNaN(action.weight) || float.IsInfinity(action.weight) || action.weight < 0 || action.weight > 100)
                        return L("{0}の BlendShape は重みが 0～100 の範囲外のため反映できませんでした。");
                }
            }
            return null;
        }

        private static bool ApplyLegacySceneAuto(Sps2SetupContext root, SetupSettings draft, out string message, out bool changed)
        {
            changed = false;
            var projection = root.settings.Copy();
            var edits = new List<SocketLeafEdit>();
            bool known = TryReadLegacyAuto(root, out var current);
            if (known) {
                projection.autoMode = draft.autoMode;
                if (draft.autoMode != current) {
                    foreach (var part in projection.parts.Where(p => p != null && p.included && !p.IsFingerRing)) {
                        // Changing socket membership at the same time is a regeneration request.
                        if (!draft.parts.Any(p => p != null && p.included && p.id == part.id)) {
                            message = L("対象部位を外す変更は再生成してください。既存の Auto は保持しました。");
                            return false;
                        }
                        var socket = root.sockets.Single(slot => slot.id == part.id).socket;
                        edits.Add(new SocketLeafEdit { socket = socket, identity = Sps2SetupStorage.Token(root, part.id),
                            before = SpsMigrationAdapter.State(socket), auto = draft.autoMode });
                    }
                }
            }
            bool complete = SameOutputProjection(root, projection, draft);
            if (edits.Count != 0) {
                CommitSceneEdits(root, edits);
                changed = true;
            }
            var notes = new List<string>();
            if (!known) notes.Add(L("Auto は対象を特定できないか、部位ごとの値が異なるため反映できませんでした。"));
            if (!complete) notes.Add(L("Auto 以外の旧形式の設定を変更するには再生成が必要です。"));
            message = notes.Count == 0 ? (changed ? L("反映しました。シーンを保存してください。") : L("変更はありません。")) :
                string.Join("\n", notes);
            return complete && known;
        }

        private sealed class SocketLeafEdit
        {
            internal Component socket;
            internal string identity, before;
            internal bool? auto;
            internal float? weight;

            internal void Commit()
            {
                if (VrcFuryCompatibility.ReadIdentity(socket) != identity || SpsMigrationAdapter.State(socket) != before)
                    throw new InvalidOperationException("対象 Socket が確認後に変わりました。");
                Undo.RecordObject(socket, "Apply SPS Socket settings");
                VrcFuryCompatibility.ApplySocketLeaves(socket, auto, weight, before);
            }
        }

        // Rebuild the output projection from the selected scene instance on each explicit Apply.
        // No new authoring asset, identity token, Options model, menu or socket is allocated.
        internal static bool ApplySceneFields(Sps2SetupContext root, SetupSettings draft,
            out string message, out bool changed)
        {
            message = null;
            changed = false;
            if (root?.settings == null || draft?.parts == null || !CanEditSocketInstance(root.avatar) ||
                draft.family != root.settings.family)
                return false;
            ValidateOwnership(root, true);
            if (draft.parts.Any(p => p == null || string.IsNullOrEmpty(p.id)) ||
                draft.parts.GroupBy(p => p.id).Any(g => g.Count() != 1)) {
                message = L("部位の ID が重複または欠落しているため反映先を確認できません。");
                return false;
            }
            if (root.AuthoringContract == SpsAuthoringContract.LegacySps2)
                return ApplyLegacySceneAuto(root, draft, out message, out changed);
            if (root.AuthoringContract != SpsAuthoringContract.Sps3Beta14 || draft.family != SpsFamily.VrcFury)
                return false;
            var baseline = root.settings.Copy();
            SynchronizeNativeAuto(root, baseline);
            var weightTarget = ProbeNativeFloat(root);
            var projected = baseline.Copy();
            if (weightTarget != null) {
                var group = projected.parts.Single(p => p.id == weightTarget.part.id).animationGroups.Single(g => g.kind == SocketAnimationKind.Depth);
                group.actions[0].weight = weightTarget.value;
                baseline.parts.Single(p => p.id == weightTarget.part.id).animationGroups.Single(g => g.kind == SocketAnimationKind.Depth).actions[0].weight = weightTarget.value;
            }
            var pending = new List<string>();
            var requested = draft.parts.ToDictionary(p => p.id);
            if (!new HashSet<string>(projected.parts.Select(p => p.id)).SetEquals(requested.Keys))
                pending.Add(L("Socket を追加または削除するには再生成が必要です。"));
            bool autoEdit = false, weightEdit = false;
            var edits = new List<SocketLeafEdit>();
            foreach (var part in projected.parts) {
                if (!requested.TryGetValue(part.id, out var next)) continue;
                bool? newAuto = null;
                float? newWeight = null;
                if (part.included && next.included && root.settings.sps3AutoEligibilityVersion == 1 && draft.sps3AutoEligibilityVersion == 1 &&
                    TryReadNativeAuto(root, part, out var actualAuto)) {
                    bool selected = draft.Sps3IncludeInAuto(next);
                    if (selected != actualAuto) { newAuto = selected; autoEdit = true; }
                    part.sps3AutoParticipant = selected;
                }
                if (weightTarget != null && part.id == weightTarget.part.id && next.included && next.animationEnabled &&
                    next.animationModelVersion == 1 && next.animationGroups != null) {
                    var prior = part.animationGroups.Single(g => g.kind == SocketAnimationKind.Depth);
                    var groups = next.animationGroups.Where(g => g != null && g.kind == SocketAnimationKind.Depth).ToArray();
                    var action = groups.Length == 1 && groups[0].id == prior.id && groups[0].actions?.Count == 1 ?
                        groups[0].actions[0] : null;
                    if (action != null && action.kind == DepthActionKind.BlendShape && action.blendShapeMode == BlendShapeMode.Normal &&
                        action.renderer == weightTarget.action.renderer && action.shape == weightTarget.action.shape &&
                        action.weight >= 0 && action.weight <= 100) {
                        prior.actions[0].weight = action.weight;
                        if (action.weight != weightTarget.value) { newWeight = action.weight; weightEdit = true; }
                    }
                }
                if (newAuto.HasValue || newWeight.HasValue) {
                    var socket = root.sockets.Single(g => g.id == part.id).socket;
                    string token = Sps2SetupStorage.Token(root, part.id);
                    if (socket.GetComponentInParent<VRCAvatarDescriptor>(true) != root.avatar ||
                        VrcFuryCompatibility.ReadIdentity(socket) != token || EditorUtility.IsPersistent(socket) ||
                        !SpsBackend.PackageMatches(socket.GetType(), SpsFamily.VrcFury, out _))
                        throw new InvalidOperationException("Socket の所有参照を確認できません: " + part.id);
                    edits.Add(new SocketLeafEdit { socket = socket, before = SpsMigrationAdapter.State(socket),
                        identity = token, auto = newAuto, weight = newWeight });
                }
                if (JsonUtility.ToJson(part) != JsonUtility.ToJson(next)) {
                    string label = SocketDisplayNames.Resolve(next, draft.menuLanguage);
                    int before = pending.Count;
                    if (part.included != next.included || part.target != next.target || part.custom != next.custom)
                        pending.Add(string.Format(L("{0}の生成対象または追従先を変更するには再生成が必要です。"), label));
                    if (!VrcFuryCompatibility.SameDisplayName(part, projected, next, draft))
                        pending.Add(string.Format(L("{0}の表示名を変更するには再生成が必要です。"), label));
                    if (part.sps3AutoParticipant != next.sps3AutoParticipant)
                        pending.Add(string.Format(L("{0}の Auto は反映先を確認できませんでした。"), label));
                    bool animationDiff = next.AnimationEnabled != part.AnimationEnabled ||
                        !next.EffectiveAnimationGroups().Select(g => JsonUtility.ToJson(g)).SequenceEqual(
                            part.EffectiveAnimationGroups().Select(g => JsonUtility.ToJson(g)));
                    if (animationDiff || pending.Count == before)
                        pending.Add(string.Format(DescribeDepthIssue(next, root.avatar) ??
                            L("{0}の設定は現在の Socket に個別に書き込めないため反映できませんでした。"), label));
                }
            }
            var globalBefore = projected.Copy(); var globalAfter = draft.Copy();
            globalBefore.parts.Clear(); globalAfter.parts.Clear();
            if (JsonUtility.ToJson(globalBefore) != JsonUtility.ToJson(globalAfter)) pending.Add(L("全体設定・SPS Options の変更には再生成が必要です。"));
            if (pending.Count == 0 && !SameOutputProjection(root, projected, draft))
                pending.Add(L("保存済みの参照または部位順が一致せず反映できませんでした。"));
            if (!autoEdit && !weightEdit) {
                message = pending.Count == 0 ? L("変更はありません。") : string.Join("\n", pending);
                return pending.Count == 0;
            }
            if (weightEdit && !autoEdit && pending.Count == 0) {
                // Keep the previously verified weight-only transaction and its full readback.
                bool applied = TryApplyNativeFloat(root.avatar, draft, out message);
                changed = applied;
                if (!applied) message = L("BlendShape の保存先を再確認できず、重みは未反映です。");
                return applied;
            }
            CommitSceneEdits(root, edits);
            changed = true;
            message = pending.Count == 0 ? L("反映しました。シーンを保存してください。") : string.Join("\n", pending);
            return pending.Count == 0;
        }

        private static bool SameOutputProjection(Sps2SetupContext root, SetupSettings projected, SetupSettings requested)
        {
            // A draft can contain an unresolved reference. That leaves the complete
            // projection unverified, but does not invalidate an independently matched leaf.
            try { return ExistingSettingsState(root, projected) == ExistingSettingsState(root, requested); }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static void CommitSceneEdits(Sps2SetupContext root, List<SocketLeafEdit> edits)
        {
            Undo.IncrementCurrentGroup();
            int transaction = Undo.GetCurrentGroup();
            try {
                foreach (var edit in edits) edit.Commit();
                Undo.FlushUndoRecordObjects();
                EditorSceneManager.MarkSceneDirty(root.avatar.gameObject.scene);
                Undo.SetCurrentGroupName("Apply SPS Socket settings");
                Undo.CollapseUndoOperations(transaction);
            } catch { Undo.RevertAllDownToGroup(transaction); throw; }
        }

        // Returns false for non-matching requests so the ordinary protection path remains in force.
        internal static bool TryApplyNativeFloat(VRCAvatarDescriptor avatar, SetupSettings draft, out string message)
        {
            message = null; var root = Find(avatar); var target = ProbeNativeFloat(root);
            if (target == null || draft?.parts == null || root.asset == null) return false;
            if (!CanEditSocketInstance(avatar)) return false;
            var baseline = root.settings.Copy();
            SynchronizeNativeAuto(root, baseline);
            var saved = baseline.parts.Single(p => p.id == target.part.id).animationGroups.Single(g => g.kind == SocketAnimationKind.Depth).actions[0];
            saved.weight = target.value;
            var proposed = draft.Copy();
            var part = proposed.parts.SingleOrDefault(p => p != null && p.id == target.part.id);
            if (part?.animationGroups == null) return false;
            var groups = part.animationGroups.Where(g => g != null && g.kind == SocketAnimationKind.Depth).ToArray();
            if (groups.Length != 1 || groups[0].actions == null || groups[0].actions.Count != 1 || groups[0].actions[0] == null) return false;
            var next = groups[0].actions[0].weight;
            if (float.IsNaN(next) || float.IsInfinity(next) || next < 0 || next > 100) return false;
            groups[0].actions[0].weight = target.value;
            // This path never replaces or removes a GameObject, component, asset,
            // or reference. Extra children and external holders are not impacts of
            // changing this one verified float on the existing Socket. All other
            // draft settings must still match, and the full native state is checked
            // again immediately before and after the Undo-protected write.
            if (!NativeFloatDecision.Admit(1, ExistingSettingsState(root, baseline), ExistingSettingsState(root, proposed), false, next)) return false;
            if (next == target.value) { message = L("変更はありません。"); return true; }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Update SPS BlendShape value");
            try {
                var beforeAsset = root.asset.stateJson; var socket = target.socket.socket;
                Undo.RecordObject(socket, "Update SPS BlendShape value");
                var check = VrcFuryCompatibility.UpdateVerifiedWeight(socket, target.value, target.fullState, next);
                string WithoutValue(string state) => string.Join("\n", state.Split('\n').Where(line => !line.StartsWith(VrcFuryCompatibility.NativeValuePath + ":", StringComparison.Ordinal)));
                if (check == null || check.floatValue != next || root.asset.stateJson != beforeAsset ||
                    WithoutValue(SpsMigrationAdapter.State(socket)) != WithoutValue(target.fullState))
                    throw new InvalidOperationException("Socket readback did not match; change reverted.");
                EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene); Undo.CollapseUndoOperations(undo);
                message = L("反映しました。シーンを保存してください。"); return true;
            } catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
