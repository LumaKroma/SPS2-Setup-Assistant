using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase.Editor.BuildPipeline;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Surround the observed VRCFury -10000 callback; NDMF optimization follows.
    public sealed class Sps2BeforeVrcFury : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -10001;
        public bool OnPreprocessAvatar(GameObject avatar) => Sps2BuildIntegration.Run(avatar, true);
    }
    public sealed class Sps2AfterVrcFury : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -9999;
        public bool OnPreprocessAvatar(GameObject avatar) => Sps2BuildIntegration.Run(avatar, false);
    }

    public static class Sps2BuildIntegration
    {
        private const string AutoLabel = "<b>Auto Mode</b>\n<size=20>Activates hole nearest to a VRCFury plug";
        private const string LegacyLabel = "<b>Legacy Compatibility</b>\n<size=20>DPS / TPS / SPS1\nOne socket at a time";
        private const string LocalOnlyLabel = "<b>Stealth Mode</b>\n<size=20>Only local haptics,\nInvisible to others";

        private static readonly Dictionary<int, Sps2SetupContext> Pending = new Dictionary<int, Sps2SetupContext>();
        internal static bool Run(GameObject avatar, bool before)
        {

            try
            {

                Sps2SetupContext root;
                if (before) { Pending.Remove(avatar.GetInstanceID()); root = Sps2SetupStorage.Find(avatar.GetComponent<VRCAvatarDescriptor>(), true); }
                else { Pending.TryGetValue(avatar.GetInstanceID(), out root); Pending.Remove(avatar.GetInstanceID()); }
                if (root == null) return true;
                VrcFuryCompatibility.RequireVersion();
                if (root.schema != 1 || string.IsNullOrEmpty(root.identity))
                    throw new InvalidOperationException(L("SPS2 の所有ルートが不正です。"));

                if (before)
                {
                    Pending.Add(avatar.GetInstanceID(), root);
                    VrcFuryCompatibility.ValidateBuildTokens(avatar, root);
                    root.settings = VrcFuryCapabilities.Current.Effective(root.settings);
                    if (VrcFuryCapabilities.Current.Legacy && VrcFuryCompatibility.AutoSocketCount(avatar) > 16) throw new InvalidOperationException(L("Auto Mode の対象が16個を超えています。"));
                    var seenSockets = new HashSet<Component>();
                    foreach (var socket in root.sockets)
                    {
                        // NDMF/MA may already have moved anchors beneath their target bones.
                        if (socket.socket == null || !socket.socket.transform.IsChildOf(avatar.transform) || !seenSockets.Add(socket.socket))
                            throw new InvalidOperationException(L("SPS2 Socket の所有参照が不正です。"));
                        socket.buildToken = "__SPS2_" + root.identity + "_" + socket.id;
                        VrcFuryCompatibility.SetBuildToken(socket.socket, socket.buildToken);
                    }
                }
                else
                {
                    Complete(avatar.GetComponent<VRCAvatarDescriptor>(), root);
                }
                return true;
            }
            catch (Exception e) { Pending.Remove(avatar.GetInstanceID()); Debug.LogError(L("SPS2 ビルドを中止しました: ") + e.Message, avatar); return false; }
        }

        private sealed class MenuEntry
        {
            public VRCExpressionsMenu parent;
            public VRCExpressionsMenu.Control control;
        }

        private static VRCExpressionsMenu CloneMenu(VRCExpressionsMenu source, Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> seen)
        {
            if (source == null) return null;
            if (seen.TryGetValue(source, out var existing)) return existing;
            var copy = UnityEngine.Object.Instantiate(source); seen.Add(source, copy);
            foreach (var control in copy.controls) control.subMenu = CloneMenu(control.subMenu, seen);
            return copy;
        }
        private static List<MenuEntry> Entries(VRCExpressionsMenu menu)
        {
            var result = new List<MenuEntry>();
            var queue = new Queue<VRCExpressionsMenu>(); var seen = new HashSet<VRCExpressionsMenu>(); queue.Enqueue(menu);
            while (queue.Count != 0)
            {
                var current = queue.Dequeue(); if (current == null || !seen.Add(current)) continue;
                foreach (var control in current.controls)
                {
                    result.Add(new MenuEntry { parent = current, control = control });
                    if (control.subMenu != null) queue.Enqueue(control.subMenu);
                }
            }
            return result;
        }
        private static MenuEntry Unique(List<MenuEntry> entries, string label, bool required)
        {
            var matching = entries.Where(e => e.control.name == label && e.control.type == VRCExpressionsMenu.Control.ControlType.Toggle).ToArray();
            if (matching.Length > 1 || (required && matching.Length != 1)) throw new InvalidOperationException(L("SPS メニューの対応が不明です: ") + label);
            return matching.SingleOrDefault();
        }
        private static VRCExpressionsMenu Menu(string name)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.name = name;
            menu.controls = new List<VRCExpressionsMenu.Control>(); return menu;
        }
        private static VRCExpressionsMenu.Control Submenu(string name, VRCExpressionsMenu menu) => new VRCExpressionsMenu.Control
        { name = name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu };

        private static void Complete(VRCAvatarDescriptor avatar, Sps2SetupContext root)
        {
            if (avatar == null || avatar.expressionsMenu == null || avatar.expressionParameters == null)
                throw new InvalidOperationException(L("VRCFury のビルド結果がありません。"));
            var layers = avatar.baseAnimationLayers;
            int fxIndex = Array.FindIndex(layers, l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxIndex < 0 || !(layers[fxIndex].animatorController is AnimatorController original)) throw new InvalidOperationException(L("FX Controller の形式が一致しません。"));
            // Instantiate controller before changing its parameters/layer array. Existing state machines stay untouched.
            var fx = UnityEngine.Object.Instantiate(original); fx.name = original.name + " SPS2";
            layers[fxIndex].animatorController = fx; avatar.baseAnimationLayers = layers;
            avatar.expressionParameters = UnityEngine.Object.Instantiate(avatar.expressionParameters);
            avatar.expressionsMenu = CloneMenu(avatar.expressionsMenu, new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>());
            var entries = Entries(avatar.expressionsMenu);
            var owned = new Dictionary<string, MenuEntry>();
            foreach (var socket in root.sockets)
            {
                if (string.IsNullOrEmpty(socket.buildToken)) throw new InvalidOperationException(L("SPS2 のビルド前処理がありません。"));
                var entry = Unique(entries, socket.buildToken, true);
                SetPersistence(avatar, fx, entry, false, 0);
                owned.Add(socket.id, entry);
            }
            StabilizeOwnedSocketToggles(avatar, fx, root, owned.ToDictionary(p => p.Key, p => p.Value.control.parameter.name));
            var auto = Unique(entries, AutoLabel, false);
            var legacy = Unique(entries, LegacyLabel, false);
            var localOnly = Unique(entries, LocalOnlyLabel, root.settings.localOnly);
            if (localOnly != null) SetPersistence(avatar, fx, localOnly, false, 0);
            if (root.settings.legacy && legacy == null) throw new InvalidOperationException(L("後方互換性メニューがありません。"));
            if (auto != null) SetPersistence(avatar, fx, auto, true, 0);
            if (legacy != null) SetPersistence(avatar, fx, legacy, true, 1);
            StabilizeOwnedGlobalControls(avatar, fx, root, localOnly?.control.parameter.name, legacy?.control.parameter.name);
            var requiredControls = owned.Values.Select(e => e.control).ToList();
            if (auto != null) requiredControls.Add(auto.control);
            if (legacy != null) requiredControls.Add(legacy.control);
            if (localOnly != null) requiredControls.Add(localOnly.control);
            var nativeContainer = entries.Where(e => e.control.subMenu != null)
                .Select(e => new { entry = e, children = Entries(e.control.subMenu) })
                .Where(e => requiredControls.All(c => e.children.Any(child => child.control == c)))
                .OrderBy(e => e.children.Count).Select(e => e.entry).FirstOrDefault();
            var nativeMenu = nativeContainer?.control.subMenu;
            var menu = Menu("SPS2");
            var language = root.settings.menuLanguage;
            var settingsMenu = Menu(L("設定", language));
            menu.controls.Add(Submenu(L("設定", language), settingsMenu));
            // Move the native controls themselves: one UI control per shared parameter.
            if (root.settings.autoMode && auto != null)
            { auto.parent.controls.Remove(auto.control); auto.control.name = L("Auto Mode", language); settingsMenu.controls.Add(auto.control); }
            if (root.settings.legacy && legacy != null)
            { legacy.parent.controls.Remove(legacy.control); legacy.control.name = L("後方互換性", language); settingsMenu.controls.Add(legacy.control); }
            if (localOnly != null) localOnly.parent.controls.Remove(localOnly.control);
            if (root.settings.localOnly && localOnly != null)
            { localOnly.control.name = L("Local Only", language); settingsMenu.controls.Add(localOnly.control); }
            var direct = new[] { "mouth", "chest", "vagina", "anus", "handRight", "handLeft", "hands" };
            void MoveSocket(string id, VRCExpressionsMenu destination)
            {
                if (!owned.TryGetValue(id, out var entry)) return;
                entry.parent.controls.Remove(entry.control);
                entry.control.name = SocketDisplayNames.Resolve(root.settings.parts.Single(p => p.id == id), language);
                destination.controls.Add(entry.control);
            }
            foreach (var id in direct) MoveSocket(id, menu);

            foreach (var part in root.settings.parts.Where(p => !direct.Contains(p.id))) MoveSocket(part.id, menu);

            // Preserve native options and unrelated Socket controls inside the same entry.
            // Empty native pagination pages disappear after moving the owned controls.
            if (nativeMenu != null)
            {
                PruneEmptyMenus(nativeMenu, new HashSet<VRCExpressionsMenu>());
                if (nativeMenu.controls.Count != 0)
                    settingsMenu.controls.Add(Submenu(L("標準設定・既存Socket", language), nativeMenu));
                nativeContainer.control.name = "SPS2";
                nativeContainer.control.subMenu = menu;
            }
            else
            {
                PruneEmptyMenus(avatar.expressionsMenu, new HashSet<VRCExpressionsMenu>());
                avatar.expressionsMenu.controls.Add(Submenu("SPS2", menu));
                Paginate(avatar.expressionsMenu, language);
            }
            Paginate(menu, language);
            Paginate(settingsMenu, language);
            // Final SDK validation runs after native parameter compression; do not reject its pre-compression cost here.
        }

        // Native Direct Blend Trees otherwise rely on Write Defaults to restore
        // zero-weight socket clips. Mixed avatar WD can retain their last ON value.
        // Keep native menu/Auto/exclusivity parameters and clips; give each owned
        // native toggle branch an explicit baseline at parameter zero.
        internal static void StabilizeOwnedSocketToggles(VRCAvatarDescriptor avatar,
            AnimatorController fx, Sps2SetupContext root, IReadOnlyDictionary<string, string> parameters)
        {
            string one = "__SPS2_" + root.identity + "_ExplicitToggleOne";
            if (fx.parameters.Any(p => p.name == one))
                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            var byParameter = parameters.ToDictionary(p => p.Value, p => p.Key);
            var counts = parameters.Keys.ToDictionary(id => id, id => 0);
            var layers = fx.layers;
            bool ContainsOwned(Motion motion)
            {
                if (!(motion is BlendTree tree)) return false;
                return tree.children.Any(child =>
                    (tree.blendType == BlendTreeType.Direct && byParameter.ContainsKey(child.directBlendParameter)) ||
                    ContainsOwned(child.motion));
            }
            Motion CloneMotion(Motion motion)
            {
                if (!(motion is BlendTree original)) return motion;
                // Instantiate can assert on Unity's native strong references in
                // generated animator sub-assets. Copy serialized data into a new
                // object, then replace only the owned motion references below.
                var tree = new BlendTree();
                EditorUtility.CopySerialized(original, tree);
                var children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (tree.blendType == BlendTreeType.Direct && byParameter.TryGetValue(child.directBlendParameter, out var id))
                    {
                        var parameter = fx.parameters.Single(p => p.name == child.directBlendParameter);
                        var socket = root.sockets.Single(s => s.id == id);
                        if (parameter.type != AnimatorControllerParameterType.Float || socket.pose == null ||
                            !(child.motion is AnimationClip on))
                            throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                        string prefix = AnimationUtility.CalculateTransformPath(socket.pose, avatar.transform) + "/";
                        var bindings = AnimationUtility.GetCurveBindings(on);
                        var activation = bindings.Where(b => b.type == typeof(GameObject) && b.propertyName == "m_IsActive" &&
                            b.path.StartsWith(prefix, StringComparison.Ordinal) && b.path.EndsWith("/BakedSpsSocket", StringComparison.Ordinal)).ToArray();
                        if (activation.Length != 1 || avatar.transform.Find(activation[0].path) == null ||
                            AnimationUtility.GetObjectReferenceCurveBindings(on).Length != 0 || ++counts[id] != 1)
                            throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                        var off = new AnimationClip { name = "SPS2 Explicit Off " + id };
                        foreach (var binding in bindings)
                        {
                            float value;
                            if (binding.type == typeof(Animator) && binding.path == "")
                            {
                                var baseline = fx.parameters.SingleOrDefault(p => p.name == binding.propertyName);
                                if (baseline == null || baseline.type != AnimatorControllerParameterType.Float)
                                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                                value = baseline.defaultFloat;
                            }
                            else if (!AnimationUtility.GetFloatValue(avatar.gameObject, binding, out value))
                                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                            if (binding.Equals(activation[0]) && value != 0)
                                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                            AnimationUtility.SetEditorCurve(off, binding, AnimationCurve.Constant(0, 0, value));
                        }
                        var toggle = new BlendTree { name = "SPS2 Explicit Toggle " + id,
                            blendType = BlendTreeType.Simple1D, blendParameter = child.directBlendParameter,
                            useAutomaticThresholds = false };
                        toggle.children = new[] {
                            new ChildMotion { motion = off, threshold = 0, timeScale = 1 },
                            new ChildMotion { motion = on, threshold = 1, timeScale = 1 }
                        };
                        child.motion = toggle;
                        child.directBlendParameter = one;
                    }
                    else child.motion = CloneMotion(child.motion);
                    children[i] = child;
                }
                tree.children = children;
                return tree;
            }
            for (int i = 0; i < layers.Length; i++)
            {
                var original = layers[i].stateMachine;
                if (!original.states.Any(s => ContainsOwned(s.state.motion))) continue;
                // The observed native optimizer puts toggle branches in a single
                // static state. Refuse unknown stateful graphs instead of changing
                // shared transition targets or silently guessing a new contract.
                if (original.states.Length != 1 || original.stateMachines.Length != 0 ||
                    original.anyStateTransitions.Length != 0 || original.entryTransitions.Length != 0 ||
                    original.states[0].state.transitions.Length != 0)
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                var machine = new AnimatorStateMachine();
                EditorUtility.CopySerialized(original, machine);
                var state = new AnimatorState();
                EditorUtility.CopySerialized(original.states[0].state, state);
                state.motion = CloneMotion(state.motion);
                var child = original.states[0]; child.state = state;
                machine.states = new[] { child }; machine.defaultState = state;
                layers[i].stateMachine = machine;
            }
            if (counts.Values.Any(count => count != 1))
                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            fx.AddParameter(new AnimatorControllerParameter { name = one, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            fx.layers = layers;
        }

        // Native global empty branches also retain values under mixed avatar WD.
        // Preserve native thresholds/layers/WD and add only owned missing baselines.
        internal static void StabilizeOwnedGlobalControls(VRCAvatarDescriptor avatar,
            AnimatorController fx, Sps2SetupContext root, string stealthParameter, string legacyParameter)
        {
            if (string.IsNullOrEmpty(stealthParameter)) return;
            var prefixes = root.sockets.Select(s =>
                AnimationUtility.CalculateTransformPath(s.pose, avatar.transform) + "/BakedSpsSocket/").ToArray();
            var allTrees = new HashSet<BlendTree>();
            void Collect(Motion motion)
            {
                if (!(motion is BlendTree tree) || !allTrees.Add(tree)) return;
                foreach (var child in tree.children) Collect(child.motion);
            }
            void CollectMachine(AnimatorStateMachine machine)
            {
                foreach (var state in machine.states) Collect(state.state.motion);
                foreach (var child in machine.stateMachines) CollectMachine(child.stateMachine);
            }
            foreach (var layer in fx.layers) CollectMachine(layer.stateMachine);
            BlendTree Find(string parameter)
            {
                var found = allTrees.Where(t => t.blendParameter == parameter &&
                    t.blendType == BlendTreeType.Simple1D).ToArray();
                if (found.Length != 1 || fx.parameters.Count(p => p.name == parameter &&
                    p.type == AnimatorControllerParameterType.Float) != 1)
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                var c = found[0].children;
                if (c.Length != 2 || c[0].threshold != 0 || c[1].threshold != float.Epsilon ||
                    !(c[0].motion is AnimationClip) || !(c[1].motion is AnimationClip))
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                return found[0];
            }
            void RequireEmpty(AnimationClip clip)
            {
                if (AnimationUtility.GetCurveBindings(clip).Length != 0 ||
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0 || clip.events.Length != 0)
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            }
            EditorCurveBinding[] OwnedDisabledBindings(AnimationClip clip)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip).Where(b =>
                    prefixes.Any(prefix => b.path.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
                foreach (var b in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (b.type != typeof(GameObject) || b.propertyName != "m_IsActive" ||
                        avatar.transform.Find(b.path) == null || curve == null || curve.keys.Length != 1 ||
                        curve.keys[0].value != 0 || curve.keys[0].time != 0)
                        throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                }
                if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b =>
                    prefixes.Any(prefix => b.path.StartsWith(prefix, StringComparison.Ordinal))))
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                return bindings;
            }
            var stealth = Find(stealthParameter);
            var stealthEmpty = (AnimationClip)stealth.children[0].motion;
            RequireEmpty(stealthEmpty);
            var stealthBindings = OwnedDisabledBindings((AnimationClip)stealth.children[1].motion);
            BlendTree legacy = string.IsNullOrEmpty(legacyParameter) ? null : Find(legacyParameter);
            var lightBindings = legacy == null ? new EditorCurveBinding[0] :
                OwnedDisabledBindings((AnimationClip)legacy.children[0].motion);
            if (legacy != null) RequireEmpty((AnimationClip)legacy.children[1].motion);
            if (lightBindings.Any(b => !stealthBindings.Contains(b)))
                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            if (stealthBindings.Length == 0) return;
            var restoreBindings = stealthBindings.Except(lightBindings).ToArray();
            AnimationClip Values(AnimationClip template, IEnumerable<EditorCurveBinding> bindings,
                bool restore, string name)
            {
                var clip = new AnimationClip(); EditorUtility.CopySerialized(template, clip); clip.name = name;
                foreach (var binding in bindings)
                {
                    float baseline;
                    if (!AnimationUtility.GetFloatValue(avatar.gameObject, binding, out baseline) ||
                        (baseline != 0 && baseline != 1))
                        throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                    AnimationUtility.SetEditorCurve(clip, binding,
                        AnimationCurve.Constant(0, 0, restore ? baseline : 0));
                }
                return clip;
            }
            var replacements = new Dictionary<BlendTree, BlendTree>();
            var newStealth = new BlendTree(); EditorUtility.CopySerialized(stealth, newStealth);
            var sc = newStealth.children;
            sc[0].motion = Values(stealthEmpty, restoreBindings, true, "SPS2 Restore Stealth Outputs");
            newStealth.children = sc; replacements.Add(stealth, newStealth);
            if (legacy != null && lightBindings.Length != 0)
            {
                var empty = (AnimationClip)legacy.children[1].motion;
                var whenLegacy = new BlendTree(); EditorUtility.CopySerialized(stealth, whenLegacy);
                whenLegacy.name = "SPS2 Legacy Lights Respect Stealth";
                var lc = whenLegacy.children;
                lc[0].motion = Values(empty, lightBindings, true, "SPS2 Restore Legacy Lights");
                lc[1].motion = Values(empty, lightBindings, false, "SPS2 Keep Stealth Lights Off");
                whenLegacy.children = lc;
                var newLegacy = new BlendTree(); EditorUtility.CopySerialized(legacy, newLegacy);
                var c = newLegacy.children; c[1].motion = whenLegacy; newLegacy.children = c;
                replacements.Add(legacy, newLegacy);
            }
            bool ContainsChange(Motion motion)
            {
                if (!(motion is BlendTree tree)) return false;
                return replacements.ContainsKey(tree) || tree.children.Any(c => ContainsChange(c.motion));
            }
            var applied = new HashSet<BlendTree>();
            Motion Copy(Motion motion)
            {
                if (!(motion is BlendTree tree) || !ContainsChange(tree)) return motion;
                if (replacements.TryGetValue(tree, out var replacement))
                { applied.Add(tree); return replacement; }
                var clone = new BlendTree(); EditorUtility.CopySerialized(tree, clone);
                var c = clone.children;
                for (int i = 0; i < c.Length; i++) c[i].motion = Copy(c[i].motion);
                clone.children = c; return clone;
            }
            var layers = fx.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                var original = layers[i].stateMachine;
                if (!original.states.Any(s => ContainsChange(s.state.motion))) continue;
                if (original.states.Length != 1 || original.stateMachines.Length != 0 ||
                    original.anyStateTransitions.Length != 0 || original.entryTransitions.Length != 0 ||
                    original.states[0].state.transitions.Length != 0)
                    throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
                var machine = new AnimatorStateMachine(); EditorUtility.CopySerialized(original, machine);
                var state = new AnimatorState(); EditorUtility.CopySerialized(original.states[0].state, state);
                state.motion = Copy(state.motion);
                var c = original.states[0]; c.state = state; machine.states = new[] { c }; machine.defaultState = state;
                layers[i].stateMachine = machine;
            }
            if (replacements.Keys.Any(tree => !applied.Contains(tree)))
                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            fx.layers = layers;
        }

        private static void PruneEmptyMenus(VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> seen)
        {
            if (menu == null || !seen.Add(menu)) return;
            foreach (var control in menu.controls.ToArray())
            {
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu || control.subMenu == null) continue;
                PruneEmptyMenus(control.subMenu, seen);
                if (control.subMenu.controls.Count == 0) menu.controls.Remove(control);
            }
        }

        internal static void Paginate(VRCExpressionsMenu menu, DisplayLanguage language)
        {
            if (menu.controls.Count <= 8) return;
            var next = Menu(menu.name + L(" 続き", language)); next.controls.AddRange(menu.controls.Skip(7));
            menu.controls.RemoveRange(7, menu.controls.Count - 7); menu.controls.Add(Submenu(L("次へ", language), next)); Paginate(next, language);
        }
        private static void SetPersistence(VRCAvatarDescriptor avatar, AnimatorController fx, MenuEntry entry, bool saved, float initial)
        {
            string name = entry.control.parameter?.name;
            var matches = avatar.expressionParameters.parameters.Where(p => p.name == name).ToArray();
            var parameters = fx.parameters; var animatorMatches = parameters.Where(p => p.name == name).ToArray();
            if (string.IsNullOrEmpty(name) || matches.Length != 1 || matches[0].valueType != VRCExpressionParameters.ValueType.Bool || animatorMatches.Length != 1 ||
                (animatorMatches[0].type != AnimatorControllerParameterType.Float && animatorMatches[0].type != AnimatorControllerParameterType.Bool))
                throw new InvalidOperationException(L("SPS パラメーターの対応が不明です。"));
            matches[0].saved = saved; matches[0].defaultValue = initial;
            animatorMatches[0].defaultFloat = initial; animatorMatches[0].defaultBool = initial > .5f; fx.parameters = parameters;
        }

    }
}
