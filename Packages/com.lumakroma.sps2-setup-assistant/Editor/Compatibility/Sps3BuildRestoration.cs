using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Post-build, mixed-WD Disabled only. No native menu/parameter/WD rewrite.
    // beta.14 keeps ordinary socket ON-only direct branches; its Legacy false branch is empty.
    internal static class Sps3BuildRestoration
    {
        internal const string LegacyLabel = "<b>Legacy Support</b>\n<size=20>DPS/TPS/SPS1/SPS2\nOne socket at a time";
        internal static void Complete(VRCAvatarDescriptor avatar, Sps2SetupContext root)
        {
            if (root == null || root.AuthoringContract != SpsAuthoringContract.Sps3Beta14 ||
                root.settings.family != SpsFamily.VrcFury)
                throw new InvalidOperationException("SPS3 restoration requires explicit beta.14 provenance.");
            if (avatar == null || avatar.expressionsMenu == null || avatar.expressionParameters == null)
                throw new InvalidOperationException("Missing native SPS3 build output.");
            var entries = Controls(avatar.expressionsMenu);
            var parameters = new Dictionary<string, string>();
            foreach (var socket in root.sockets)
            {
                string suffix = "_" + Sps2SetupStorage.Token(root, socket.id);
                var matches = entries.Where(c => c.type == VRCExpressionsMenu.Control.ControlType.Toggle &&
                    c.parameter?.name != null && c.parameter.name.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Ambiguous owned SPS3 socket menu: " + socket.id);
                parameters.Add(socket.id, matches[0].parameter.name);
            }
            var legacy = entries.Where(c => c.name == LegacyLabel && c.type == VRCExpressionsMenu.Control.ControlType.Toggle).ToArray();
            if (legacy.Length > 1) throw new InvalidOperationException("Ambiguous native SPS3 Legacy Support control.");
            var layers = avatar.baseAnimationLayers;
            int index = Array.FindIndex(layers, l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (index < 0 || !(layers[index].animatorController is AnimatorController original))
                throw new InvalidOperationException("Unsupported native SPS3 FX controller.");
            // Assign only after all restoration succeeds; original animator assets are never edited.
            var fx = UnityEngine.Object.Instantiate(original); fx.name = original.name + " SPS3";
            Sps2BuildIntegration.StabilizeOwnedSocketToggles(avatar, fx, root, parameters);
            if (legacy.Length == 1) StabilizeOwnedLegacy(avatar, fx, root, legacy[0].parameter?.name);
            layers[index].animatorController = fx; avatar.baseAnimationLayers = layers;
        }

        private static List<VRCExpressionsMenu.Control> Controls(VRCExpressionsMenu menu)
        {
            var result = new List<VRCExpressionsMenu.Control>(); var seen = new HashSet<VRCExpressionsMenu>();
            var queue = new Queue<VRCExpressionsMenu>(); queue.Enqueue(menu);
            while (queue.Count != 0)
            {
                var current = queue.Dequeue(); if (current == null || !seen.Add(current)) continue;
                foreach (var control in current.controls)
                {
                    if (control == null) continue;
                    result.Add(control); if (control.subMenu != null) queue.Enqueue(control.subMenu);
                }
            }
            return result;
        }

        internal static void StabilizeOwnedLegacy(VRCAvatarDescriptor avatar, AnimatorController fx,
            Sps2SetupContext root, string parameter)
        {
            if (string.IsNullOrEmpty(parameter)) throw new InvalidOperationException("Missing SPS3 legacy parameter.");
            var paths = root.sockets.Select(s => s.pose == null ? null : AnimationUtility.CalculateTransformPath(s.pose, avatar.transform)).ToArray();
            if (paths.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("Missing owned SPS3 pose.");
            bool Owned(EditorCurveBinding binding) => paths.Any(path => binding.path == path ||
                binding.path.StartsWith(path + "/", StringComparison.Ordinal));
            var trees = new HashSet<BlendTree>();
            void Collect(Motion motion)
            {
                if (!(motion is BlendTree tree) || !trees.Add(tree)) return;
                foreach (var child in tree.children) Collect(child.motion);
            }
            void CollectMachine(AnimatorStateMachine machine)
            {
                foreach (var state in machine.states) Collect(state.state.motion);
                foreach (var child in machine.stateMachines) CollectMachine(child.stateMachine);
            }
            foreach (var layer in fx.layers) CollectMachine(layer.stateMachine);
            var matches = trees.Where(t => t.blendType == BlendTreeType.Simple1D && t.blendParameter == parameter).ToArray();
            if (matches.Length != 1 || fx.parameters.Count(p => p.name == parameter && p.type == AnimatorControllerParameterType.Float) != 1)
                throw new InvalidOperationException("Unsupported SPS3 Legacy Support graph.");
            var original = matches[0]; var children = original.children;
            if (children.Length != 2 || children[0].threshold != 0 || children[1].threshold != float.Epsilon ||
                !(children[0].motion is AnimationClip off) || !(children[1].motion is AnimationClip on) ||
                AnimationUtility.GetCurveBindings(off).Length != 0 || AnimationUtility.GetObjectReferenceCurveBindings(off).Length != 0 ||
                off.events.Length != 0)
                throw new InvalidOperationException("Unsupported SPS3 Legacy Support branch shape.");
            if (AnimationUtility.GetObjectReferenceCurveBindings(on).Any(Owned))
                throw new InvalidOperationException("Unexpected owned SPS3 Legacy object-reference curve.");
            var bindings = AnimationUtility.GetCurveBindings(on).Where(Owned).ToArray();
            if (bindings.Length == 0) return; // Native global control may only affect other people's components.
            var values = new List<float>();
            foreach (var binding in bindings)
            {
                bool active = binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive";
                bool gizmo = binding.type?.FullName == "VF.Component.VRCFurySocketGizmo" && binding.propertyName == "data.useLegacyLights";
                var curve = AnimationUtility.GetEditorCurve(on, binding);
                if ((!active && !gizmo) || curve == null || curve.length != 1 || curve.keys[0].time != 0 ||
                    (curve.keys[0].value != 0 && curve.keys[0].value != 1) ||
                    !TryGetOwnedBaseline(avatar.gameObject, binding, gizmo, out var baseline) || baseline != 0)
                    throw new InvalidOperationException("Unsupported owned SPS3 Legacy baseline.");
                values.Add(baseline);
            }
            var restoredOff = new AnimationClip(); EditorUtility.CopySerialized(off, restoredOff);
            restoredOff.name = "SPS3 Explicit Legacy Off";
            for (int i = 0; i < bindings.Length; i++) AnimationUtility.SetEditorCurve(restoredOff, bindings[i], AnimationCurve.Constant(0, 0, values[i]));
            var replacement = new BlendTree(); EditorUtility.CopySerialized(original, replacement);
            children[0].motion = restoredOff; replacement.children = children;
            ReplaceStaticTree(fx, original, replacement);
        }

        private static bool TryGetOwnedBaseline(GameObject avatar, EditorCurveBinding binding, bool gizmo, out float baseline)
        {
            if (AnimationUtility.GetFloatValue(avatar, binding, out baseline)) return true;
            if (!gizmo) return false;
            var target = avatar.transform.Find(binding.path);
            if (target == null) return false;
            var component = target.GetComponent(binding.type);
            if (component == null) return false;
            var property = new SerializedObject(component).FindProperty(binding.propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean) return false;
            baseline = property.boolValue ? 1f : 0f;
            return true;
        }

        private static void ReplaceStaticTree(AnimatorController fx, BlendTree original, BlendTree replacement)
        {
            bool Contains(Motion motion) => motion == original || motion is BlendTree tree && tree.children.Any(c => Contains(c.motion));
            Motion Copy(Motion motion)
            {
                if (motion == original) return replacement;
                if (!(motion is BlendTree tree) || !Contains(tree)) return motion;
                var copy = new BlendTree(); EditorUtility.CopySerialized(tree, copy);
                var children = copy.children;
                for (int i = 0; i < children.Length; i++) children[i].motion = Copy(children[i].motion);
                copy.children = children; return copy;
            }
            var layers = fx.layers; int count = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                var machine = layers[i].stateMachine;
                if (!machine.states.Any(s => Contains(s.state.motion))) continue;
                if (machine.states.Length != 1 || machine.stateMachines.Length != 0 || machine.anyStateTransitions.Length != 0 ||
                    machine.entryTransitions.Length != 0 || machine.states[0].state.transitions.Length != 0)
                    throw new InvalidOperationException("Unsupported stateful SPS3 Legacy graph.");
                var clone = new AnimatorStateMachine(); EditorUtility.CopySerialized(machine, clone);
                var state = new AnimatorState(); EditorUtility.CopySerialized(machine.states[0].state, state);
                state.motion = Copy(state.motion); var children = machine.states; children[0].state = state;
                clone.states = children; clone.defaultState = state; layers[i].stateMachine = clone; count++;
            }
            if (count != 1) throw new InvalidOperationException("Ambiguous SPS3 Legacy graph ownership.");
            fx.layers = layers;
        }
    }
}
