using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static partial class Sps3Beta14SocketAdapter
    {
        internal sealed partial class FixtureHost
        {
            internal Sps3NativeFixtureSceneSnapshot SceneSnapshot { get; private set; }
            internal bool IsPreservedSceneRoot(GameObject value) => SceneSnapshot != null && SceneSnapshot.IsPreservedRoot(value);

            // Explicit test-only admission. Production FixtureHost.Create still requires zero roots and stays disabled.
            internal static FixtureHost CreateForNativeDepthTest(Scene scene, string name)
            {
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A fixture name is required.");
                var snapshot = Sps3NativeFixtureSceneSnapshot.CaptureForTest(scene);
                return new FixtureHost(new GameObject(name), scene.handle) { SceneSnapshot = snapshot };
            }
        }

        internal static GameObject CreateForNativeDepthTest(FixtureHost fixture,
            Sps3SocketSpecification specification, Sps3DepthReferenceResolver resolver,
            Sps3DepthClipBindingPolicy clipPolicy, bool failAfterStagingForTest = false)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            var session = new Session(fixture, specification, null, resolver, clipPolicy, failAfterStagingForTest);
            Sps3SocketTransaction.Execute(session);
            return session.Root;
        }

        internal static void RequireDepthActionApiShape()
        {
            var type = VrcFuryApi.FindType(Sps3CandidatePreflight.ActionsApi);
            void Require(string name, params Type[] arguments)
            {
                var method = type?.GetMethod(name, System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly,
                    null, arguments, null);
                if (method == null || method.ReturnType != typeof(void) ||
                    method.IsDefined(typeof(ObsoleteAttribute), false))
                    throw new InvalidOperationException("Exact beta14 depth public API unavailable: " + name);
            }
            Require("AddBlendshape", typeof(string), typeof(float), typeof(Renderer));
            Require("AddAnimationClip", typeof(AnimationClip));
            Require("AddTurnOn", typeof(GameObject));
        }

        private sealed partial class Session
        {
            private readonly bool failAfterStagingForTest;
            private readonly Sps3DepthReferenceResolver resolver;
            private readonly Sps3DepthClipBindingPolicy clipPolicy;
            private Sps3DepthReferenceResult bound;
            private const string ActionsPath = "depthActions2.Array.data[0].actionSet.actions";

            internal Session(FixtureHost fixture, Sps3SocketSpecification spec, AnimationClip clip,
                Sps3DepthReferenceResolver resolver, Sps3DepthClipBindingPolicy clipPolicy, bool failAfterStagingForTest)
                : this(fixture, spec, clip)
            { this.resolver = resolver; this.clipPolicy = clipPolicy; this.failAfterStagingForTest = failAfterStagingForTest; }

            private void ValidateResolvedDepth()
            {
                if (resolver == null) return;
                var plan = resolver.Plan;
                if (spec == null || !spec.DepthEnabled || !plan.Prepared || plan.Units != spec.DepthUnits ||
                    plan.RangeMin != spec.RangeMin || plan.RangeMax != spec.RangeMax || plan.Commands.Count > 64)
                    throw new InvalidOperationException("Resolved depth plan and fixture specification disagree or exceed the 64-row fixture bound.");
                var current = resolver.Resolve(host == null ? null : host.GetComponent<VRCAvatarDescriptor>(), clipPolicy);
                if (!current.ReferencesVerified)
                    throw new InvalidOperationException("Depth references rejected: " + string.Join(",", current.Blockers));
                bound = current;
            }

            private void AppendResolvedActions(object actions)
            {
                for (int i = 0; i < bound.Commands.Count; i++)
                {
                    int index = i;
                    var row = bound.Commands[i];
                    GuardPublic(() => {
                        switch (row.Command.Kind)
                        {
                            case Sps3DepthOutputKind.BlendShape:
                                return InvokePublic(actions, actions.GetType(), "AddBlendshape",
                                    new[] { typeof(string), typeof(float), typeof(Renderer) },
                                    row.Command.Shape, row.Command.Weight.Value, row.Renderer);
                            case Sps3DepthOutputKind.AnimationClip:
                                return InvokePublic(actions, actions.GetType(), "AddAnimationClip",
                                    new[] { typeof(AnimationClip) }, row.Clip);
                            case Sps3DepthOutputKind.ObjectOn:
                            case Sps3DepthOutputKind.ObjectOff:
                                return InvokePublic(actions, actions.GetType(), "AddTurnOn",
                                    new[] { typeof(GameObject) }, row.Target);
                            default: throw new InvalidOperationException("Unknown depth command.");
                        }
                    }, ActionCreationPaths(i, row.Command.Kind), data => VerifyResolvedPayload(data, index + 1, index));
                    if (row.Command.Kind == Sps3DepthOutputKind.ObjectOff)
                    {
                        RequireOwnedSocket();
                        var data = new SerializedObject(socket);
                        preparedState.RequireSame(CaptureState(data));
                        string path = ActionPath(i) + ".mode";
                        SetEnum(P(data, path, SerializedPropertyType.Enum), "TurnOff");
                        var expected = CaptureState(data);
                        preparedState.RequireOnlyChanges(expected, new[] { path });
                        data.ApplyModifiedPropertiesWithoutUndo();
                        ValidateResolvedDepth();
                        expected.RequireSame(CaptureState(new SerializedObject(socket)));
                        preparedState = expected;
                        VerifyResolvedPayload(new SerializedObject(socket), index + 1);
                    }
                }
                VerifyResolvedPayload(new SerializedObject(socket));
            }

            private static string ActionPath(int i) => ActionsPath + ".Array.data[" + i + "]";
            private static IEnumerable<string> ActionCreationPaths(int index, Sps3DepthOutputKind kind)
            {
                yield return ActionsPath; yield return ActionsPath + ".Array"; yield return ActionsPath + ".Array.size";
                string path = ActionPath(index); yield return path;
                foreach (string key in new[] { "version", "desktopActive", "androidActive", "localOnly", "remoteOnly" })
                    yield return path + "." + key;
                string[] keys;
                switch (kind)
                {
                    case Sps3DepthOutputKind.BlendShape:
                        keys = new[] { "blendShape", "blendShapeValue", "renderer", "allRenderers" }; break;
                    case Sps3DepthOutputKind.AnimationClip:
                        keys = new[] { "clip", "clip.version", "clip.fileID", "clip.guid", "clip.id", "clip.objRef" }; break;
                    case Sps3DepthOutputKind.ObjectOn:
                    case Sps3DepthOutputKind.ObjectOff: keys = new[] { "obj", "mode" }; break;
                    default: throw new InvalidOperationException("Unknown depth kind.");
                }
                foreach (string key in keys) yield return path + "." + key;
            }

            private void VerifyResolvedPayload(SerializedObject data) => VerifyResolvedPayload(data, bound.Commands.Count);
            private void VerifyResolvedPayload(SerializedObject data, int count, int publicOnIndex = -1)
            {
                var list = P(data, ActionsPath, SerializedPropertyType.Generic);
                if (!list.isArray || list.arraySize != count) throw new InvalidOperationException("Depth action count mismatch.");
                for (int i = 0; i < count; i++)
                {
                    var row = bound.Commands[i]; string p = ActionPath(i);
                    string expectedType;
                    bool same;
                    switch (row.Command.Kind)
                    {
                        case Sps3DepthOutputKind.BlendShape:
                            expectedType = "BlendShapeAction";
                            same = P(data, p + ".renderer", SerializedPropertyType.ObjectReference).objectReferenceValue == row.Renderer &&
                                P(data, p + ".blendShape", SerializedPropertyType.String).stringValue == row.Command.Shape &&
                                P(data, p + ".blendShapeValue", SerializedPropertyType.Float).floatValue == row.Command.Weight &&
                                !P(data, p + ".allRenderers", SerializedPropertyType.Boolean).boolValue;
                            break;
                        case Sps3DepthOutputKind.AnimationClip:
                            expectedType = "AnimationClipAction";
                            same = P(data, p + ".clip.objRef", SerializedPropertyType.ObjectReference).objectReferenceValue == row.Clip;
                            break;
                        case Sps3DepthOutputKind.ObjectOn:
                        case Sps3DepthOutputKind.ObjectOff:
                            expectedType = "ObjectToggleAction";
                            same = P(data, p + ".obj", SerializedPropertyType.ObjectReference).objectReferenceValue == row.Target &&
                                EnumName(P(data, p + ".mode", SerializedPropertyType.Enum)) ==
                                (row.Command.Kind == Sps3DepthOutputKind.ObjectOff && i != publicOnIndex ? "TurnOff" : "TurnOn");
                            break;
                        default: throw new InvalidOperationException("Unknown depth kind.");
                    }
                    var element = list.GetArrayElementAtIndex(i);
                    same &= row.Command.SourceActionIndex == i && element.propertyType == SerializedPropertyType.ManagedReference &&
                        element.managedReferenceValue?.GetType().FullName == "VF.Model.StateAction." + expectedType;
                    foreach (string key in new[] { "desktopActive", "androidActive", "localOnly", "remoteOnly" })
                        same &= !P(data, p + "." + key, SerializedPropertyType.Boolean).boolValue;
                    if (!same) throw new InvalidOperationException("Depth payload mismatch at source index " + i);
                }
            }
        }
    }

    // Read-only admission/preservation proof for the one observed auto-created NDMF root.
    // This is test-only; it never destroys, modifies or creates the NDMF activator.
    internal sealed class Sps3NativeFixtureSceneSnapshot
    {
        private readonly Scene scene;
        private readonly string expectedScenePath;
        internal bool WasDirtyAtAdmission { get; }
        private readonly GameObject preserved;
        private readonly Component[] components;
        private readonly string objectJson;
        private readonly string[] componentJson;
        private readonly int sibling;
        private const string ActivatorName = "nadena.dev.ndmf__Activator";
        private const string ActivatorType = "nadena.dev.ndmf.runtime.ApplyOnPlayGlobalActivator";

        private Sps3NativeFixtureSceneSnapshot(Scene scene, GameObject preserved)
        {
            this.scene = scene; this.preserved = preserved; expectedScenePath = scene.path;
            WasDirtyAtAdmission = scene.isDirty;
            if (preserved == null) return;
            components = preserved.GetComponents<Component>();
            objectJson = EditorJsonUtility.ToJson(preserved);
            componentJson = components.Select(EditorJsonUtility.ToJson).ToArray();
            sibling = preserved.transform.GetSiblingIndex();
        }

        internal static Sps3NativeFixtureSceneSnapshot CaptureForTest(Scene scene, string ownedNamedScenePath = null)
        {
            bool ownedNamed = ownedNamedScenePath != null && scene.path == ownedNamedScenePath &&
                System.Text.RegularExpressions.Regex.IsMatch(ownedNamedScenePath, @"^Assets/SPS1470_TEST_[0-9a-f]{32}\.unity$");
            if (!scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene() ||
                (!string.IsNullOrEmpty(scene.path) && !ownedNamed) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Requires an active loaded unsaved EditMode test scene.");
            // Owned edits from prior tests may leave the runner scene dirty. Never clear or save that state.
            var roots = scene.GetRootGameObjects();
            if (roots.Length == 0) return new Sps3NativeFixtureSceneSnapshot(scene, null);
            if (roots.Length != 1) throw new InvalidOperationException("Only the exact automatic NDMF root may preexist.");
            var root = roots[0]; var components = root.GetComponents<Component>();
            if (root.name != ActivatorName || root.transform.parent != null || root.transform.childCount != 0 ||
                root.hideFlags != HideFlags.HideInHierarchy || !root.activeSelf || root.layer != 0 || root.tag != "Untagged" ||
                !root.transform.localPosition.Equals(Vector3.zero) || !root.transform.localRotation.Equals(Quaternion.identity) ||
                !root.transform.localScale.Equals(Vector3.one) || components.Length != 2 || components.Any(c => c == null) ||
                components[0].GetType() != typeof(Transform) || components[1].GetType().FullName != ActivatorType ||
                !(components[1] is Behaviour behaviour) || !behaviour.enabled || EditorUtility.IsPersistent(root) ||
                PrefabUtility.IsPartOfPrefabAsset(root) || PrefabUtility.IsPartOfPrefabInstance(root))
                throw new InvalidOperationException("Preexisting root is not the observed automatic NDMF activator.");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(components[1].GetType().Assembly);
            if (package == null || package.name != "nadena.dev.ndmf" || package.version != "1.14.3")
                throw new InvalidOperationException("Observed NDMF 1.14.3 activator identity is required.");
            using (var hash = SHA256.Create())
            {
                var bytes = File.ReadAllBytes(Path.Combine(package.resolvedPath, "Runtime/ApplyOnPlayGlobalActivator.cs"));
                var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (actual != "846c290f5106d84254c8bc1048d1528d941182c80a8507e8571baec15159eb7d")
                    throw new InvalidOperationException("Observed NDMF activator source pin mismatch.");
            }
            return new Sps3NativeFixtureSceneSnapshot(scene, root);
        }

        internal bool IsPreservedRoot(GameObject value) => preserved != null && value == preserved;

        internal void RequireUnchanged(params GameObject[] ownedRoots)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != expectedScenePath)
                throw new InvalidOperationException("Runner scene identity changed.");
            if (WasDirtyAtAdmission && !scene.isDirty)
                throw new InvalidOperationException("The runner scene dirty flag was cleared; preserve its original dirty state.");
            if (!ReferenceEquals(preserved, null))
            {
                if (preserved == null || preserved.scene != scene || preserved.transform.parent != null ||
                    preserved.transform.childCount != 0 || preserved.transform.GetSiblingIndex() != sibling ||
                    EditorJsonUtility.ToJson(preserved) != objectJson ||
                    !preserved.GetComponents<Component>().SequenceEqual(components))
                    throw new InvalidOperationException("Preserved NDMF root identity or state changed.");
                for (int i = 0; i < components.Length; i++)
                    if (components[i] == null || EditorJsonUtility.ToJson(components[i]) != componentJson[i])
                        throw new InvalidOperationException("Preserved NDMF component state changed.");
            }
            if (scene.GetRootGameObjects().Any(root => !IsPreservedRoot(root) && !ownedRoots.Any(owned => owned != null && root == owned)))
                throw new InvalidOperationException("Foreign scene roots appeared; preserve them and stop.");
        }
    }

}
