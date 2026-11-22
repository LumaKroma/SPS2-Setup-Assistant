using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Explicit review policy, not inferred from a clip or its filename. No rebasing or asset writes.
    internal enum Sps3DepthClipBindingPolicy { Unresolved, AvatarRootActiveAndBlendShapeCurves }

    internal sealed class Sps3DepthBoundCommand
    {
        internal Sps3DepthOutputCommand Command { get; }
        internal SkinnedMeshRenderer Renderer { get; }
        internal GameObject Target { get; }
        internal AnimationClip Clip { get; }
        internal Sps3DepthBoundCommand(Sps3DepthOutputCommand command, DepthActionSettings action)
        {
            Command = command;
            if (command.Kind == Sps3DepthOutputKind.BlendShape) Renderer = action.renderer;
            else if (command.Kind == Sps3DepthOutputKind.AnimationClip) Clip = action.clip;
            else Target = action.target;
        }
    }

    internal sealed class Sps3DepthReferenceResult
    {
        internal IReadOnlyList<Sps3DepthBoundCommand> Commands { get; }
        internal IReadOnlyList<string> Blockers { get; }
        internal bool ReferencesVerified => Blockers.Count == 0;
        internal bool CanApply => false;
        internal Sps3DepthReferenceResult(List<Sps3DepthBoundCommand> commands, List<string> errors)
        {
            if (errors.Count != 0) commands.Clear();
            Commands = commands.AsReadOnly(); Blockers = errors.AsReadOnly();
        }
    }

    // Capture and resolve on Unity's editor main thread. A result is a point-in-time observation,
    // not durable authorization: resolve again immediately before and after future native callbacks.
    // Action identities/fields are frozen. Mesh and clip CONTENTS are inspected afresh, not frozen.
    // This bounded clip policy is not Fury owner-relative/path/scale parity; production stays disconnected.
    // Old pure plans cannot prove reference identity; only the plan compiled at this capture is used.
    internal sealed class Sps3DepthReferenceResolver
    {
        private readonly SocketSettings source;
        private readonly List<DepthActionSettings> list;
        private readonly DepthActionSettings[] identities;
        private readonly DepthActionSettings[] values;
        private readonly string id;
        private readonly bool included, depth;
        private readonly DepthUnits units;
        private readonly Vector2 range;
        internal Sps3DepthOutputPlan Plan { get; }

        private Sps3DepthReferenceResolver(SocketSettings part, SetupSettings setup)
        {
            source = part;
            if (part == null || setup == null) { Plan = Sps3DepthOutputPlan.Compile(null); return; }
            id = part.id; included = part.included; depth = part.depth; units = part.units; range = part.range;
            list = part.actions;
            identities = list == null ? new DepthActionSettings[0] : list.ToArray();
            values = new DepthActionSettings[identities.Length];
            for (int i = 0; i < values.Length; i++) values[i] = identities[i]?.Copy();
            Plan = new Sps3SettingsPartReadPlan(part, setup).DepthOutput;
        }

        internal static Sps3DepthReferenceResolver Capture(SocketSettings part, SetupSettings setup) =>
            new Sps3DepthReferenceResolver(part, setup);

        internal Sps3DepthReferenceResult Resolve(VRCAvatarDescriptor avatar,
            Sps3DepthClipBindingPolicy clipPolicy = Sps3DepthClipBindingPolicy.Unresolved)
        {
            var errors = new List<string>();
            var commands = new List<Sps3DepthBoundCommand>();
            if (!Plan.Prepared) errors.Add("DepthPlanNotPrepared");
            if (avatar == null) errors.Add("MissingLiveAvatar");
            if (source == null || identities == null || source.id != id || source.included != included ||
                source.depth != depth || source.units != units || !source.range.x.Equals(range.x) ||
                !source.range.y.Equals(range.y) || !ReferenceEquals(source.actions, list) ||
                list == null || list.Count != identities.Length) errors.Add("SourceChangedSinceCapture");
            if (errors.Count != 0) return new Sps3DepthReferenceResult(commands, errors);
            if (Plan.Commands.Count != identities.Length) errors.Add("PlanSourceCountMismatch");
            for (int i = 0; i < identities.Length; i++)
            {
                var action = list[i]; var old = values[i];
                if (!ReferenceEquals(action, identities[i]) || !Same(action, old))
                { errors.Add("SourceActionChanged:" + i); continue; }
                if (i >= Plan.Commands.Count) continue;
                var command = Plan.Commands[i];
                if (command.SourceActionIndex != i || !Matches(command, action))
                { errors.Add("PlanSourceMismatch:" + i); continue; }
                string issue = null;
                switch (command.Kind)
                {
                    case Sps3DepthOutputKind.BlendShape:
                        if (action.renderer == null) issue = "MissingLiveRenderer";
                        else if (!Belongs(action.renderer.transform, avatar)) issue = "RendererOutsideAvatar";
                        else if (action.renderer.sharedMesh == null) issue = "MissingLiveMesh";
                        else if (action.renderer.sharedMesh.GetBlendShapeIndex(command.Shape) < 0) issue = "MissingBlendShape";
                        break;
                    case Sps3DepthOutputKind.AnimationClip:
                        issue = ValidateClip(action.clip, avatar, clipPolicy);
                        break;
                    default:
                        if (action.target == null) issue = "MissingLiveObject";
                        else if (!Belongs(action.target.transform, avatar)) issue = "ObjectOutsideAvatar";
                        break;
                }
                if (issue != null) errors.Add(issue + ":" + i);
                else commands.Add(new Sps3DepthBoundCommand(command, action));
            }
            return new Sps3DepthReferenceResult(commands, errors);
        }

        private static bool Same(DepthActionSettings a, DepthActionSettings b) => a != null && b != null &&
            a.kind == b.kind && a.shape == b.shape && a.weight.Equals(b.weight) && a.objectOn == b.objectOn &&
            ReferenceEquals(a.renderer, b.renderer) && ReferenceEquals(a.target, b.target) && ReferenceEquals(a.clip, b.clip);

        private static bool Matches(Sps3DepthOutputCommand c, DepthActionSettings a)
        {
            if (a.kind == DepthActionKind.BlendShape)
                return c.Kind == Sps3DepthOutputKind.BlendShape && c.Shape == a.shape && c.Weight == Mathf.Clamp(a.weight, 0, 100);
            if (a.kind == DepthActionKind.AnimationClip) return c.Kind == Sps3DepthOutputKind.AnimationClip;
            return a.kind == DepthActionKind.Object && c.Kind == (a.objectOn ? Sps3DepthOutputKind.ObjectOn : Sps3DepthOutputKind.ObjectOff);
        }

        private static bool Belongs(Transform target, VRCAvatarDescriptor avatar)
        {
            for (var current = target; current != null; current = current.parent)
            {
                // Reject a nested avatar, even if it is a descendant of the supplied root.
                var owner = current.GetComponent<VRCAvatarDescriptor>();
                if (owner != null) return owner == avatar;
                if (current == avatar.transform) return true;
            }
            return false;
        }

        private static string ValidateClip(AnimationClip clip, VRCAvatarDescriptor avatar, Sps3DepthClipBindingPolicy policy)
        {
            if (clip == null) return "MissingLiveClip";
            if (policy != Sps3DepthClipBindingPolicy.AvatarRootActiveAndBlendShapeCurves) return "ClipBindingPolicyUnresolved";
            if (clip.legacy || clip.humanMotion) return "UnsupportedClipMotion";
            if (AnimationUtility.GetAnimationEvents(clip).Length != 0) return "ClipEventsUnsupported";
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0) return "ObjectReferenceCurvesUnsupported";
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0) return "EmptyClipUnsupported";
            foreach (var binding in bindings)
            {
                var target = UniquePath(avatar.transform, binding.path);
                if (target == null || !Belongs(target, avatar)) return "ClipTargetMissingAmbiguousOrOutsideAvatar";
                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive") { }
                else if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName != null &&
                    binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                {
                    var renderers = target.GetComponents<SkinnedMeshRenderer>();
                    if (renderers.Length != 1 || renderers[0].sharedMesh == null ||
                        renderers[0].sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11)) < 0)
                        return "ClipBlendShapeMissingOrAmbiguous";
                }
                else return "ClipBindingUnsupported";
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0) return "ClipCurveMissing";
                foreach (var key in curve.keys)
                    if (!Finite(key.time) || !Finite(key.value) || float.IsNaN(key.inTangent) ||
                        float.IsNaN(key.outTangent) || !Finite(key.inWeight) || !Finite(key.outWeight))
                        return "ClipCurveNonfinite"; // Infinite tangents are valid stepped curves.
            }
            return null;
        }

        private static Transform UniquePath(Transform root, string path)
        {
            if (path == null) return null;
            if (path.Length == 0) return root;
            var current = root;
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..") return null;
                Transform found = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name != segment) continue;
                    if (found != null) return null;
                    found = child;
                }
                if (found == null) return null;
                current = found;
            }
            return current;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
