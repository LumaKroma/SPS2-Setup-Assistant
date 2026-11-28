using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Authoring only. Reads existing FX curves and mesh deltas; never edits expression controllers.
    internal static class BlendShapeInterference
    {
        private const string PropertyPrefix = "blendShape.";
        private const float FloorSquared = 0.0000000001f;
        private const int VertexLimit = 200000;
        private const string MergeTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator, nadena.dev.modular-avatar.core";

        private static IEnumerable<(Transform root, RuntimeAnimatorController controller)> ExpressionLayers(VRCAvatarDescriptor avatar)
        {
            if (avatar.baseAnimationLayers != null)
                foreach (var layer in avatar.baseAnimationLayers)
                    if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault && layer.animatorController != null)
                        yield return (avatar.transform, layer.animatorController);

            var mergeType = Type.GetType(MergeTypeName, false);
            if (mergeType == null || !mergeType.IsPublic || !typeof(Component).IsAssignableFrom(mergeType)) yield break;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public;
            var layerField = mergeType.GetField("layerType", fields);
            var controllerField = mergeType.GetField("animator", fields);
            var modeField = mergeType.GetField("pathMode", fields);
            var rootField = mergeType.GetField("relativePathRoot", fields);
            if (layerField == null || controllerField == null || modeField == null) yield break;

            foreach (var component in avatar.GetComponentsInChildren(mergeType, true).OfType<Component>())
            {
                if (component.GetComponentInParent<VRCAvatarDescriptor>(true) != avatar ||
                    !Equals(layerField.GetValue(component), VRCAvatarDescriptor.AnimLayerType.FX) ||
                    !(controllerField.GetValue(component) is RuntimeAnimatorController controller) || controller == null)
                    continue;

                Transform root = null;
                switch (modeField.GetValue(component)?.ToString())
                {
                    case "Absolute": root = avatar.transform; break;
                    case "Relative":
                        root = component.transform;
                        var reference = rootField?.GetValue(component);
                        var get = reference?.GetType().GetMethod("Get", fields, null, new[] { typeof(Transform) }, null);
                        if (get?.ReturnType == typeof(GameObject) &&
                            get.Invoke(reference, new object[] { avatar.transform }) is GameObject target && target != null)
                            root = target.transform;
                        break;
                }
                if (root != null && (root == avatar.transform || root.IsChildOf(avatar.transform)))
                    yield return (root, controller);
            }
        }

        private static IEnumerable<(SkinnedMeshRenderer renderer, int index)> Bindings(
            VRCAvatarDescriptor avatar, Transform root, AnimationClip clip, bool onlyAbsolute)
        {
            if (clip == null) yield break;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer) ||
                    !binding.propertyName.StartsWith(PropertyPrefix, StringComparison.Ordinal)) continue;
                string path = binding.path ?? "";
                bool absolute = path.StartsWith("/", StringComparison.Ordinal);
                if (!absolute && onlyAbsolute) continue;
                var basis = absolute ? avatar.transform : root;
                if (absolute) path = path.Substring(1);
                var target = path.Length == 0 ? basis : basis.Find(path);
                if (target == null || target.GetComponentInParent<VRCAvatarDescriptor>(true) != avatar) continue;
                var renderers = target.GetComponents<SkinnedMeshRenderer>();
                if (renderers.Length != 1 || renderers[0].sharedMesh == null) continue;
                int index = renderers[0].sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(PropertyPrefix.Length));
                if (index >= 0) yield return (renderers[0], index);
            }
        }

        private static IEnumerable<(SkinnedMeshRenderer renderer, int index)> Driven(
            VRCAvatarDescriptor avatar, SocketAnimationGroup group, bool onlyKnownPaths)
        {
            if (group?.actions == null) yield break;
            foreach (var action in group.actions)
            {
                if (action?.kind == DepthActionKind.BlendShape && action.renderer != null &&
                    action.renderer.sharedMesh != null && !string.IsNullOrEmpty(action.shape))
                {
                    int index = action.renderer.sharedMesh.GetBlendShapeIndex(action.shape);
                    if (index >= 0) yield return (action.renderer, index);
                }
                else if (action?.kind == DepthActionKind.AnimationClip && action.clip != null)
                    foreach (var binding in Bindings(avatar, avatar.transform, action.clip, onlyKnownPaths))
                        yield return binding;
            }
        }

        private static void ProtectSdkShapes(VRCAvatarDescriptor avatar, HashSet<(SkinnedMeshRenderer, int)> protectedShapes)
        {
            var voice = avatar.VisemeSkinnedMesh;
            if (voice != null && voice.sharedMesh != null)
            {
                foreach (var name in avatar.VisemeBlendShapes ?? Array.Empty<string>())
                    if (!string.IsNullOrEmpty(name)) protectedShapes.Add((voice, voice.sharedMesh.GetBlendShapeIndex(name)));
                if (!string.IsNullOrEmpty(avatar.MouthOpenBlendShapeName))
                    protectedShapes.Add((voice, voice.sharedMesh.GetBlendShapeIndex(avatar.MouthOpenBlendShapeName)));
            }
            var eyes = avatar.customEyeLookSettings;
            if (avatar.enableEyeLook && eyes.eyelidsSkinnedMesh != null)
                foreach (int index in eyes.eyelidsBlendshapes ?? Array.Empty<int>())
                    protectedShapes.Add((eyes.eyelidsSkinnedMesh, index));
        }

        private static float[] Motion(Mesh mesh, int index, Vector3[] buffer)
        {
            var energy = new float[buffer.Length];
            for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                mesh.GetBlendShapeFrameVertices(index, frame, buffer, null, null);
                for (int i = 0; i < buffer.Length; i++)
                    energy[i] = Mathf.Max(energy[i], buffer[i].sqrMagnitude);
            }
            return energy;
        }

        internal static bool SubstantialOverlap(bool[] mask, float[] energy)
        {
            if (mask == null || energy == null || energy.Length == 0 || mask.Length != energy.Length) return false;
            var threshold = Math.Max(FloorSquared, .0025f * energy.Max());
            double total = 0, shared = 0;
            int vertices = 0;
            for (int i = 0; i < energy.Length; i++)
            {
                if (!(energy[i] >= threshold) || float.IsInfinity(energy[i])) continue;
                total += energy[i];
                if (mask[i]) { shared += energy[i]; vertices++; }
            }
            return vertices >= 3 && total > 0 && shared >= .65 * total;
        }

        private static IEnumerable<DepthActionSettings> Candidates(VRCAvatarDescriptor avatar,
            SocketAnimationGroup group, ILookup<SkinnedMeshRenderer, int> expressions, HashSet<(SkinnedMeshRenderer, int)> excluded)
        {
            foreach (var bucket in Driven(avatar, group, true).Distinct().GroupBy(b => b.renderer))
            {
                var renderer = bucket.Key;
                var mesh = renderer.sharedMesh;
                if (!mesh.isReadable || mesh.vertexCount == 0 || mesh.vertexCount > VertexLimit ||
                    renderer.GetComponentInParent<VRCAvatarDescriptor>(true) != avatar) continue;
                var options = expressions[renderer].Where(i => !excluded.Contains((renderer, i))).Distinct().OrderBy(i => i).ToArray();
                if (options.Length == 0) continue;
                var buffer = new Vector3[mesh.vertexCount];
                var mask = new bool[buffer.Length];
                foreach (var (_, index) in bucket)
                {
                    var energy = Motion(mesh, index, buffer);
                    float threshold = Mathf.Max(FloorSquared, .0025f * energy.Max());
                    for (int i = 0; i < mask.Length; ++i) mask[i] |= energy[i] >= threshold && !float.IsInfinity(energy[i]);
                }
                foreach (int index in options)
                {
                    if (!SubstantialOverlap(mask, Motion(mesh, index, buffer))) continue;
                    float neutral = renderer.GetBlendShapeWeight(index);
                    if (float.IsNaN(neutral) || float.IsInfinity(neutral)) continue;
                    yield return new DepthActionSettings { renderer = renderer, shape = mesh.GetBlendShapeName(index), weight = neutral };
                }
            }
        }

        internal static void Prepare(VRCAvatarDescriptor avatar, SetupSettings setup, Generation.Sps2SetupAsset owner)
        {
            var parts = setup.parts.Where(p => p != null && p.included && p.AnimationEnabled).ToArray();
            var enabled = parts.Where(p => p.animationModelVersion == 1)
                .SelectMany(p => p.EffectiveAnimationGroups().Where(g => g.interferenceEnabled)
                    .Select(g => (part:p, group:g))).ToArray();
            if (enabled.Length == 0 || avatar == null || AnimationMode.InAnimationMode() || !CustomBlendShapeOutput.CanAuthorHold) return;

            ILookup<SkinnedMeshRenderer, int> expressions;
            try {
                expressions = ExpressionLayers(avatar).Distinct()
                    .SelectMany(layer => layer.controller.animationClips.Distinct()
                        .SelectMany(clip => Bindings(avatar, layer.root, clip, false)))
                    .Distinct().ToLookup(x => x.renderer, x => x.index);
            } catch (Exception error) when (OptionalFailure(error)) { return; }
            var excluded = new HashSet<(SkinnedMeshRenderer, int)>(parts.SelectMany(p => p.EffectiveAnimationGroups()).SelectMany(g => Driven(avatar, g, false)));
            ProtectSdkShapes(avatar, excluded);
            foreach (var p in parts.Where(p => p.animationModelVersion == 1))
                foreach (var g in p.EffectiveAnimationGroups().Where(g => g.mouthNeutralEnabled && g.mouthNeutral?.renderer != null))
                {
                    var r = g.mouthNeutral.renderer;
                    if (r.sharedMesh != null && !string.IsNullOrEmpty(g.mouthNeutral.shape))
                        excluded.Add((r, r.sharedMesh.GetBlendShapeIndex(g.mouthNeutral.shape)));
                }

            foreach (var (part, group) in enabled)
            {
                if (!Sps3AnimationGroupNative.CanWriteOptional(part, group.kind, out _) ||
                    group.kind == SocketAnimationKind.Active && !FuryActiveShapeProbe.Inspect().Clip) continue;
                AnimationClip clip = null;
                try
                {
                    foreach (var binding in Candidates(avatar, group, expressions, excluded))
                    {
                        if (!CustomBlendShapeOutput.TryHoldBinding(avatar, binding, out string path)) continue;
                        clip ??= new AnimationClip { name = "SPS BlendShape Interference " + group.id, frameRate = 60, wrapMode = WrapMode.ClampForever };
                        var curve = AnimationCurve.Constant(group.kind == SocketAnimationKind.Active ? 0f : 1f / 60f, 1f, binding.weight);
                        curve.preWrapMode = curve.postWrapMode = WrapMode.ClampForever;
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer),
                            PropertyPrefix + binding.shape), curve);
                    }
                    if (clip != null) {
                        AssetDatabase.AddObjectToAsset(clip, owner);
                        EditorUtility.SetDirty(clip);
                        group.interferenceClip = clip;
                        clip = null;
                    }
                }
                catch (Exception error) when (OptionalFailure(error)) { }
                finally { if (clip != null && !AssetDatabase.Contains(clip)) UnityEngine.Object.DestroyImmediate(clip); }
            }
        }

        private static bool OptionalFailure(Exception error) =>
            error is InvalidOperationException || error is ArgumentException ||
            error is TargetInvocationException || error is UnityException;
    }
}
