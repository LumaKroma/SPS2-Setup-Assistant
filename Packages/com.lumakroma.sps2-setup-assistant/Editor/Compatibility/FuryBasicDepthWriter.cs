using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Only writes a new prefab instance; the persistent input asset is never edited.
    internal static class FuryBasicDepthWriter
    {
        internal static void TryWrite(Component socket, VRCAvatarDescriptor avatar, SocketSettings part, List<string> warnings, Action failAfterWriteForTest = null)
        {
            if (socket == null || avatar == null || part == null || !part.AnimationEnabled) return;
            var groups = part.EffectiveAnimationGroups().Where(g => g != null && g.kind == SocketAnimationKind.Depth).ToArray();
            if (groups.Length == 0) return;
            GameObject draft = null; int undo = -1;
            try {
                if (!FuryBasicDepthProbe.Available()) throw new InvalidOperationException("Depth API/schema unavailable");
                if (groups.Length != 1) throw new InvalidOperationException("Only one Depth group is supported");
                var g = groups[0]; var s = g.depthSettings;
                if (s == null || s.units != DepthUnits.Local || s.radiusWeightEnabled || !s.enableOthers ||
                    float.IsNaN(s.range.x) || float.IsNaN(s.range.y) || float.IsInfinity(s.range.x) ||
                    float.IsInfinity(s.range.y) || s.range.x >= s.range.y || float.IsNaN(s.smoothingSeconds) ||
                    float.IsInfinity(s.smoothingSeconds) || s.smoothingSeconds < 0)
                    throw new InvalidOperationException("Only Local units, enabled Others, and unweighted valid range are supported");
                if (g.actions == null || g.actions.Any(a => a == null || GenerationWarningPolicy.Unassigned(a)))
                    throw new InvalidOperationException("Depth action is missing or unassigned");
                var actions = g.actions.ToArray();
                if (actions.Length != 1 || actions[0].kind != DepthActionKind.Object || !actions[0].objectOn || actions[0].target == null)
                    throw new InvalidOperationException("Only one assigned Object ON action is supported");
                var target = actions[0].target.transform;
                if (target != avatar.transform && !target.IsChildOf(avatar.transform))
                    throw new InvalidOperationException("Depth target must be inside the avatar");
                if (EditorUtility.IsPersistent(socket) || !PrefabUtility.IsPartOfPrefabInstance(socket))
                    throw new InvalidOperationException("Depth requires a scene prefab instance");
                var dst = new SerializedObject(socket); var dstList = dst.FindProperty("depthActions2");
                if (dstList == null || !dstList.isArray || dstList.arraySize != 0)
                    throw new InvalidOperationException("Existing Depth settings cannot be replaced");
                draft = new GameObject("SPS Depth draft") { hideFlags = HideFlags.HideAndDontSave };
                using (SpsBackend.Use(SpsFamily.VrcFury)) {
                    var wrapper = VrcFuryApi.CreateSocket(draft);
                    var output = wrapper.AddDepthActions(s.range, s.smoothingSeconds, s.enableSelf);
                    output.AddTurnOn(actions[0].target);
                }
                var native = draft.GetComponent(socket.GetType());
                var source = native == null ? null : new SerializedObject(native).FindProperty("depthActions2");
                if (source == null || !source.isArray || source.arraySize != 1)
                    throw new InvalidOperationException("Depth draft was not created");
                undo = Undo.GetCurrentGroup(); Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Set basic Socket Depth action"); Undo.RecordObject(socket, "Set basic Socket Depth action");
                dst.CopyFromSerializedProperty(source); dst.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
                failAfterWriteForTest?.Invoke();
                var check = new SerializedObject(socket); var array = check.FindProperty("depthActions2");
                var item = array != null && array.isArray && array.arraySize == 1 ? array.GetArrayElementAtIndex(0) : null;
                var units = item?.FindPropertyRelative("units"); var range = item?.FindPropertyRelative("range");
                var self = item?.FindPropertyRelative("enableSelf"); var other = item?.FindPropertyRelative("enableOthers");
                var weight = item?.FindPropertyRelative("plugRadiusForFullWeight");
                var smoothing = item?.FindPropertyRelative("smoothingSeconds");
                var reverse = item?.FindPropertyRelative("reverseClip");
                var writtenActions = item?.FindPropertyRelative("actionSet.actions");
                var writtenAction = writtenActions != null && writtenActions.isArray && writtenActions.arraySize == 1
                    ? writtenActions.GetArrayElementAtIndex(0) : null;
                var mode = writtenAction?.FindPropertyRelative("mode");
                if (units == null || units.enumValueIndex < 0 || units.enumNames[units.enumValueIndex] != "Local" ||
                    range == null || range.vector2Value != s.range || self == null || self.boolValue != s.enableSelf ||
                    other == null || !other.boolValue || (weight != null && weight.floatValue != 0) ||
                    smoothing == null || smoothing.floatValue != s.smoothingSeconds || reverse == null || reverse.boolValue ||
                    writtenAction == null || writtenAction.propertyType != SerializedPropertyType.ManagedReference ||
                    writtenAction.managedReferenceValue?.GetType().FullName != "VF.Model.StateAction.ObjectToggleAction" ||
                    mode == null || mode.enumValueIndex < 0 || mode.enumNames[mode.enumValueIndex] != "TurnOn")
                    throw new InvalidOperationException("Depth write did not preserve required settings");
                bool found = false; var cursor = item.Copy(); var end = cursor.GetEndProperty();
                while (cursor.Next(true) && !SerializedProperty.EqualContents(cursor, end))
                    if (cursor.propertyType == SerializedPropertyType.ObjectReference && cursor.objectReferenceValue == actions[0].target) found = true;
                if (!found) throw new InvalidOperationException("Depth target reference was not preserved");
                Undo.CollapseUndoOperations(undo); undo = -1;
            } catch (Exception e) {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                warnings?.Add("Depth was not applied; all input was retained: " + e.Message);
            } finally { if (draft != null) UnityEngine.Object.DestroyImmediate(draft); }
        }
    }
}
