using System;
using UnityEditor;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Stages the same managed-reference transfer used by the full native path.
    // The caller owns Undo, ApplyModifiedProperties, and prefab override recording.
    internal static class Sps3ActiveActionTransfer
    {
        internal static int Stage(SerializedObject destination, Sps3ActiveAnimationDraft draft)
        {
            if (destination == null || draft?.Native == null) throw new ArgumentNullException(nameof(destination));
            var from = new SerializedObject(draft.Native).FindProperty("activeActions.actions");
            var to = destination.FindProperty("activeActions.actions");
            if (from == null || to == null || !from.isArray || !to.isArray)
                throw new InvalidOperationException("Active action serialized array is unavailable.");
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++) {
                var source = from.GetArrayElementAtIndex(i); var target = to.GetArrayElementAtIndex(i);
                if (source.propertyType != SerializedPropertyType.ManagedReference || target.propertyType != SerializedPropertyType.ManagedReference)
                    throw new InvalidOperationException("Active action managed-reference schema changed.");
                target.managedReferenceValue = source.managedReferenceValue;
            }
            return from.arraySize;
        }
    }
}
