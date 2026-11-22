using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Prototype: only operates on the newly instantiated scene object, never on the Prefab asset.
    internal static class Sps3BasicActiveWriter
    {

        internal static void TryWrite(Component socket, VRCAvatarDescriptor avatar, SocketSettings part, List<string> warnings)
        {
            if (socket == null || avatar == null || part == null || !part.AnimationEnabled) return;
            var actions = Sps3AnimationGroupNative.Ordered(part).Where(g => g.kind == SocketAnimationKind.Active)
                .SelectMany(g => g.actions ?? new List<DepthActionSettings>()).ToArray();
            if (actions.Length == 0) return;
            int undoGroup = -1;
            try {
                if (EditorUtility.IsPersistent(socket) || !PrefabUtility.IsPartOfPrefabInstance(socket))
                    throw new InvalidOperationException("Active actions require a scene Prefab instance.");
                foreach (var action in actions) {
                    if (action == null || GenerationWarningPolicy.Unassigned(action)) continue;
                    Transform reference = action.kind == DepthActionKind.Object ? action.target?.transform :
                        action.kind == DepthActionKind.BlendShape ? action.renderer?.transform : null;
                    if (reference != null && reference != avatar.transform && !reference.IsChildOf(avatar.transform))
                        throw new InvalidOperationException("Active target must belong to the selected avatar.");
                    if (action.kind == DepthActionKind.AnimationClip && action.clip != null && !EditorUtility.IsPersistent(action.clip))
                        throw new InvalidOperationException("Active animation clip must be a saved asset.");
                }
                var current = new SerializedObject(socket); var menu = current.FindProperty("addMenuItem");
                if (menu == null || menu.propertyType != SerializedPropertyType.Boolean || !menu.boolValue)
                    throw new InvalidOperationException("The Socket menu needed for Active actions is unavailable or disabled.");
                var existing = current.FindProperty("activeActions.actions");
                if (existing == null || !existing.isArray || existing.arraySize != 0)
                    throw new InvalidOperationException("Existing Active actions cannot be safely replaced.");
                using (SpsBackend.Use(SpsFamily.VrcFury)) using (var draft = Sps3ActiveAnimationDraft.Create(actions, warnings)) {
                    Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Set basic Socket Active actions"); Undo.RecordObject(socket, "Set basic Socket Active actions");
                    int count = Sps3ActiveActionTransfer.Stage(current, draft);
                    current.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);

                    var check = new SerializedObject(socket).FindProperty("activeActions.actions");
                    if (check == null || !check.isArray || check.arraySize != count)
                        throw new InvalidOperationException("Active action write could not be verified.");
                    var found = new HashSet<UnityEngine.Object>();
                    var cursor = check.Copy(); var end = cursor.GetEndProperty();
                    while (cursor.Next(true) && !SerializedProperty.EqualContents(cursor, end))
                        if (cursor.propertyType == SerializedPropertyType.ObjectReference && cursor.objectReferenceValue != null)
                            found.Add(cursor.objectReferenceValue);
                    var caps = FuryActiveShapeProbe.Inspect();
                    foreach (var action in actions.Where(a => a != null && !GenerationWarningPolicy.Unassigned(a) && caps.Supports(a))) {
                        UnityEngine.Object expected = action.kind == DepthActionKind.Object ? action.target :
                            action.kind == DepthActionKind.BlendShape ? (UnityEngine.Object)action.renderer : action.clip;
                        if (expected != null && !found.Contains(expected))
                            throw new InvalidOperationException("An Active action reference did not survive the scene-instance write.");
                    }
                    Undo.CollapseUndoOperations(undoGroup); undoGroup = -1;
                }
            } catch (Exception e) {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                warnings?.Add("Active action was not applied; its input was retained: " + e.Message);
            }
        }
    }
}
