using System;
using System.Collections;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static class MouthShapeHoldEdit
    {
        private const string ListName = Sps3AnimationGroupNative.DepthList;
        private const string AssetFolder = "Assets/SPS2Settings";
        private static string Prefix(Sps2SetupContext root, string groupId) =>
            AssetFolder + "/MouthHold_" + root.identity + "_" + groupId + "_";

        private static bool Locate(Sps2SetupContext root, out SocketAnimationGroup group, out Component socket,
            out AnimationClip clip)
        {
            group = null; socket = null; clip = null;
            if (root?.AuthoringContract != SpsAuthoringContract.Sps3Beta14 || root.settings.family != SpsFamily.VrcFury) return false;
            var part = root.settings.parts.Find(x => x.id == "mouth" && !x.custom && x.included && x.AnimationEnabled);
            var groups = part?.animationGroups?.Where(g => g != null && g.kind == SocketAnimationKind.Depth).ToArray();
            if (groups == null || groups.Length != 1 || groups[0].actions?.Count != 1) return false;
            group = groups[0];
            var opening = group.actions[0];
            if (opening?.kind != DepthActionKind.BlendShape || opening.blendShapeMode != BlendShapeMode.Normal ||
                opening.renderer == null || string.IsNullOrEmpty(opening.shape)) return false;
            var slot = root.sockets.Find(s => s.id == "mouth");
            socket = slot?.socket;
            if (socket == null || socket.transform != slot.pose || EditorUtility.IsPersistent(socket) ||
                VrcFuryCompatibility.ReadIdentity(socket) != Sps2SetupStorage.Token(root,"mouth")) return false;
            var list = new SerializedObject(socket).FindProperty(ListName);
            if (list == null || !list.isArray || list.arraySize < 1 || list.arraySize > 2) return false;
            var primary = list.GetArrayElementAtIndex(0);
            var actions = primary.FindPropertyRelative("actionSet.actions");
            if (actions?.arraySize != 1) return false;
            var open = actions.GetArrayElementAtIndex(0);
            if (open.FindPropertyRelative("renderer")?.objectReferenceValue != opening.renderer ||
                open.FindPropertyRelative("blendShape")?.stringValue != opening.shape) return false;
            if (list.arraySize == 1) return true;
            var hold = list.GetArrayElementAtIndex(1);
            var holds = hold.FindPropertyRelative("actionSet.actions");
            if (holds?.arraySize != 1) return false;
            var found = holds.GetArrayElementAtIndex(0).FindPropertyRelative("clip.objRef")?.objectReferenceValue as AnimationClip;
            if (found == null) return false;
            string path = AssetDatabase.GetAssetPath(found);
            bool recorded = found == group.mouthNeutral?.clip && path == AssetDatabase.GetAssetPath(root.asset);
            if (!Decode(root,found,out var binding)) return false;
            if (recorded) {
                if (found.name != "SPS Mouth Hold " + group.id ||
                    binding.renderer != group.mouthNeutral.renderer || binding.shape != group.mouthNeutral.shape ||
                    binding.weight != group.mouthNeutral.weight) return false;
                var expected = CustomBlendShapeOutput.MakeHoldClip(root.avatar,binding,group.id);
                try { if (CustomBlendShapeOutput.Signature(expected) != CustomBlendShapeOutput.Signature(found)) return false; }
                finally { Object.DestroyImmediate(expected); }
            } else if (!AssetDatabase.IsMainAsset(found) ||
                path != Prefix(root,group.id) + CustomBlendShapeOutput.Signature(found) + ".anim") return false;
            var nativeAction = holds.GetArrayElementAtIndex(0);
            if (nativeAction.managedReferenceValue?.GetType() != VrcFuryApi.FindType("VF.Model.StateAction.AnimationClipAction")) return false;
            foreach (string flag in new[]{"desktopActive","androidActive","localOnly","remoteOnly"}) {
                var field = nativeAction.FindPropertyRelative(flag);
                if (field != null && (field.propertyType == SerializedPropertyType.Boolean ? field.boolValue : field.intValue != 0))
                    return false;
            }
            foreach (string key in new[] {"range","units","smoothingSeconds","enableSelf","enableOthers","reverseClip","allowHole","allowRing"}) {
                var a = primary.FindPropertyRelative(key);
                var b = hold.FindPropertyRelative(key);
                if (a == null && b == null) continue;
                if (a == null || b == null || !SerializedProperty.DataEquals(a,b)) return false;
            }
            foreach (string key in new[] {"plugRadiusForFullWeight","plugRadiusForZeroWeight"})
                if ((hold.FindPropertyRelative(key)?.floatValue ?? 0) != 0) return false;
            clip = found;
            return true;
        }

        private static bool Decode(Sps2SetupContext root, AnimationClip clip, out DepthActionSettings result)
        {
            result = null;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length != 1 || AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0 ||
                AnimationUtility.GetAnimationEvents(clip).Length != 0 || bindings[0].type != typeof(SkinnedMeshRenderer) ||
                !bindings[0].propertyName.StartsWith("blendShape.",StringComparison.Ordinal)) return false;
            string path = bindings[0].path;
            if (!path.StartsWith("/",StringComparison.Ordinal)) return false;
            var renderer = (path.Length == 1 ? root.avatar.transform : root.avatar.transform.Find(path.Substring(1)))?.GetComponent<SkinnedMeshRenderer>();
            var keys = AnimationUtility.GetEditorCurve(clip,bindings[0]).keys;
            if (renderer == null || keys.Length != 2 || keys[0].time != 1f/60f || keys[1].time != 1 ||
                keys[0].value != keys[1].value || !(keys[0].value >= 0 && keys[0].value <= 100)) return false;
            var binding = new DepthActionSettings { renderer=renderer, shape=bindings[0].propertyName.Substring(11), weight=keys[0].value, clip=clip };
            if (!CustomBlendShapeOutput.TryHoldBinding(root.avatar,binding,out var resolved) || resolved != path) return false;
            result = binding;
            return true;
        }

        // Project a verified scene override onto the loaded snapshot only; shared assets stay immutable.
        internal static void Observe(Sps2SetupContext root)
        {
            if (!Locate(root,out var group,out _,out var clip)) return;
            if (clip != null && Decode(root,clip,out var binding)) {
                group.mouthNeutralEnabled = true;
                group.mouthNeutral = binding;
            } else if (clip == null && group.mouthNeutral?.clip != null) {
                group.mouthNeutralEnabled = false;
                group.mouthNeutral.clip = null;
            }
        }

        // Generation measures the mesh on an output copy. An untouched window draft can
        // still carry the pre-measurement values. Compare that cache separately from input.
        private static SocketAnimationSettings ForHoldComparison(SocketAnimationSettings saved, SocketAnimationSettings draft)
        {
            if (saved == null || draft == null || !saved.radiusAutoCorrection || !draft.radiusAutoCorrection ||
                !saved.radiusAutoHasResult || draft.radiusAutoHasResult ||
                saved.radiusZero != saved.radiusAutoZero || saved.radiusFull != saved.radiusAutoFull ||
                draft.radiusZero != draft.radiusManualZero || draft.radiusFull != draft.radiusManualFull)
                return saved;
            var copy = saved.Copy();
            copy.radiusZero = draft.radiusZero;
            copy.radiusFull = draft.radiusFull;
            copy.radiusAutoHasResult = draft.radiusAutoHasResult;
            copy.radiusAutoZero = draft.radiusAutoZero;
            copy.radiusAutoFull = draft.radiusAutoFull;
            copy.radiusAutoUsedStandard = draft.radiusAutoUsedStandard;
            copy.radiusAutoMessage = draft.radiusAutoMessage;
            return copy;
        }

        internal static bool TryApply(Sps2SetupContext root, SetupSettings draft, SetupSettings projection,
            Func<bool> matches, out bool success, out bool changed, out string message)
        {
            success = changed = false; message = null;
            var part = draft.parts.Find(p => p.id=="mouth");
            var previous = projection.parts.Find(p => p.id=="mouth");
            var targets = part?.animationGroups?.Where(g=>g.kind==SocketAnimationKind.Depth).ToArray();
            if (targets == null || targets.Length != 1 || previous?.animationGroups == null) return false;
            var desired=targets[0];
            var before=previous.animationGroups.Find(g=>g.id==desired.id && g.kind==SocketAnimationKind.Depth);
            if (before==null || (!before.mouthNeutralEnabled && !desired.mouthNeutralEnabled)) return false;
            var binding=desired.mouthNeutral;
            if (before.mouthNeutralEnabled==desired.mouthNeutralEnabled && binding?.renderer==before.mouthNeutral?.renderer &&
                binding?.shape==before.mouthNeutral?.shape && binding?.weight==before.mouthNeutral?.weight) return false;
            // Limit the fast path to an isolated hold edit. Other window edits retain their normal status and safeguards.
            var original=before.mouthNeutral; bool enabled=before.mouthNeutralEnabled;
            var depth = before.depthSettings;
            before.mouthNeutral=binding; before.mouthNeutralEnabled=desired.mouthNeutralEnabled;
            before.depthSettings = ForHoldComparison(depth, desired.depthSettings);
            bool only;
            try { only=matches(); } finally {
                before.mouthNeutral=original; before.mouthNeutralEnabled=enabled;
                before.depthSettings=depth;
            }
            if (!only) return false;
            if (!CustomBlendShapeOutput.CanAuthorHold || !Locate(root,out _,out var socket,out var prior)) {
                message=Localization.Sps2Localization.L("口形状の固定先を一意に特定できません。"); return true;
            }
            string problem=desired.mouthNeutralEnabled ? Sps3AnimationGroupNative.MouthNeutralProblem(draft,part,desired) : null;
            if (problem==null && desired.mouthNeutralEnabled && !CustomBlendShapeOutput.TryHoldBinding(root.avatar,binding,out _))
                problem="口形状の固定先を一意に特定できません。";
            if (problem!=null && prior==null) { message=Localization.Sps2Localization.L(problem);return true; }
            var list = socket.GetType().GetField(ListName)?.GetValue(socket) as IList;
            if (list==null || list.Count!=(prior==null?1:2)) return false;
            var staging=new GameObject("SPS mouth hold staging") { hideFlags=HideFlags.HideAndDontSave };
            string newPath=null;
            Undo.IncrementCurrentGroup();int transaction=Undo.GetCurrentGroup();
            try {
                object row=null;
                if (desired.mouthNeutralEnabled && problem==null) {
                    var created=CustomBlendShapeOutput.MakeHoldClip(root.avatar,binding,desired.id);
                    string path=Prefix(root,desired.id)+CustomBlendShapeOutput.Signature(created)+".anim";
                    var persistent=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (persistent==null && !System.IO.File.Exists(path)) {
                        AssetDatabase.CreateAsset(created,path); newPath=path; persistent=created;
                    } else {
                        Object.DestroyImmediate(created);
                        if (persistent==null || !AssetDatabase.IsMainAsset(persistent) ||
                            path!=Prefix(root,desired.id)+CustomBlendShapeOutput.Signature(persistent)+".anim")
                            throw new InvalidOperationException("Mouth hold clip asset differs.");
                    }
                    var primary=list[0];var type=primary.GetType();
                    var wrapper=VrcFuryApi.CreateSocket(staging);
                    wrapper.AddDepthActions((Vector2)type.GetField("range").GetValue(primary),
                        (float)type.GetField("smoothingSeconds").GetValue(primary),
                        (bool)type.GetField("enableSelf").GetValue(primary)).AddAnimationClip(persistent);
                    row=((IList)socket.GetType().GetField(ListName).GetValue(VrcFuryCompatibility.FindSocket(staging)))[0];
                    foreach(string key in new[]{"units","enableOthers","reverseClip","allowHole","allowRing"}) {
                        var field=type.GetField(key);if(field!=null)field.SetValue(row,field.GetValue(primary));
                    }
                    foreach(string key in new[]{"plugRadiusForZeroWeight","plugRadiusForFullWeight"}) type.GetField(key)?.SetValue(row,0f);
                }
                Undo.RegisterCompleteObjectUndo(socket,"Apply mouth shape hold");
                if(prior!=null)list.RemoveAt(1);
                if(row!=null)list.Add(row);
                EditorUtility.SetDirty(socket);
                PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
                if(!Locate(root,out _,out _,out var committed) || (row!=null)!=(committed!=null))
                    throw new InvalidOperationException("Mouth hold readback failed.");
                EditorSceneManager.MarkSceneDirty(root.avatar.gameObject.scene);
                Undo.CollapseUndoOperations(transaction);
                changed=true;success=problem==null;
                message=Localization.Sps2Localization.L(problem ?? "反映しました。シーンを保存してください。");
            } catch {
                Undo.RevertAllDownToGroup(transaction);
                if(newPath!=null)AssetDatabase.DeleteAsset(newPath);
                throw;
            } finally { Object.DestroyImmediate(staging); }
            return true;
        }
    }
}
