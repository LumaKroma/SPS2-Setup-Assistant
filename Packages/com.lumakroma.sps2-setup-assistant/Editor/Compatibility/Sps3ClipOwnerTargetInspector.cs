using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Read-only current-hierarchy inspection. NOT a native load/build result or clip admission policy.
    // 1.1403.0 and 1.1442.1-beta.14 share owner-first ancestor fallback and slash-absolute semantics.
    // Fury also uses historical hierarchy snapshots during build; this inspector cannot reproduce them.
    internal static class Sps3ClipOwnerTargetInspector
    {
        internal static Transform Resolve(VRCAvatarDescriptor avatar, Transform socketOwner,
            string path, Type bindingType, out string blocker)
        {
            blocker = null;
            if (avatar == null || socketOwner == null) { blocker = "MissingLiveAvatarOrOwner"; return null; }
            if (!Owned(socketOwner, avatar)) { blocker = "OwnerOutsideAvatar"; return null; }
            if (path == null || bindingType == null) { blocker = "MissingBindingPathOrType"; return null; }
            // VFClip handles Animator bindings before object resolution, including full-body conversion.
            if (bindingType == typeof(Animator)) { blocker = "AnimatorBindingNeedsNativeInterpretation"; return null; }
            if (bindingType != typeof(GameObject) && !typeof(Component).IsAssignableFrom(bindingType))
            { blocker = "UnsupportedBindingType"; return null; }
            bool absolute = path.StartsWith("/", StringComparison.Ordinal);
            for (Transform from = absolute ? avatar.transform : socketOwner; from != null; from = from.parent)
            {
                var target = FindUnique(avatar.transform, from, path, out bool ambiguous);
                if (ambiguous) { blocker = "AmbiguousSiblingPath"; return null; }
                if (target != null)
                {
                    if (!Owned(target, avatar)) { blocker = "TargetInsideDifferentAvatar"; return null; }
                    if (bindingType == typeof(GameObject)) return target;
                    var components = target.GetComponents(bindingType);
                    if (components.Length > 1) { blocker = "AmbiguousBindingComponent"; return null; }
                    if (components.Length == 1 && components[0] != null) return target;
                    // Fury also has special Station/constraint-type conversion cases. Do not guess them.
                }
                if (absolute || from == avatar.transform) break;
            }
            blocker = "NoCurrentHierarchyTarget"; return null;
        }

        private static Transform FindUnique(Transform avatar, Transform from, string path, out bool ambiguous)
        {
            ambiguous = false;
            var current = path.StartsWith("/", StringComparison.Ordinal) ? avatar : from;
            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == ".") continue;
                if (segment == "..")
                {
                    if (current == avatar) return null;
                    current = current.parent;
                    if (current == null) return null;
                    continue;
                }
                Transform match = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name != segment) continue;
                    if (match != null) { ambiguous = true; return null; }
                    match = child;
                }
                if (match == null) return null;
                current = match;
            }
            return current;
        }

        private static bool Owned(Transform target, VRCAvatarDescriptor avatar)
        {
            for (var current = target; current != null; current = current.parent)
            {
                var owner = current.GetComponent<VRCAvatarDescriptor>();
                if (owner != null) return owner == avatar;
                if (current == avatar.transform) return true;
            }
            return false;
        }
    }
}
