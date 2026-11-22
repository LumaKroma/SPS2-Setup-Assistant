using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Prepared native operations only. No UI, initializer, build callback or existing generator calls this class.
    internal static partial class Sps3Beta14SocketAdapter
    {
        internal static bool NativeValidationEnabled => false;
        internal const string RootName = "SPS3 Candidate (Validation Only)";
        private const string SocketTypeName = "VF.Component.VRCFuryHapticSocket";
        private const string UndoName = "Create isolated SPS3 validation socket";

        // Identity is issued only by creating a new host in an empty, unsaved, active test scene.
        // Old authoring assets elsewhere in the same project are permitted and never read or modified.
        internal sealed partial class FixtureHost
        {
            internal GameObject Host { get; }
            internal int SceneHandle { get; }
            private FixtureHost(GameObject host, int sceneHandle) { Host = host; SceneHandle = sceneHandle; }
            internal static FixtureHost Create(Scene scene, string name)
            {
                if (!NativeValidationEnabled) throw new InvalidOperationException("Native fixture creation is disabled.");
                if (!Sps3FreshFixtureAdmission.Allows(scene.IsValid(), scene.isLoaded, scene == SceneManager.GetActiveScene(),
                    string.IsNullOrEmpty(scene.path), scene.IsValid() ? scene.GetRootGameObjects().Length : -1, name))
                    throw new InvalidOperationException("Create a separate empty, unsaved, active test scene first. Existing scenes are not converted.");
                return new FixtureHost(new GameObject(name), scene.handle);
            }
        }

        internal static GameObject CreateForValidation(FixtureHost fixture, Sps3SocketSpecification specification, AnimationClip depthClip)
        {
            if (!NativeValidationEnabled)
                throw new InvalidOperationException("Native SPS3 generation is disabled pending explicit fixture approval and Unity validation.");
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            var session = new Session(fixture, specification, depthClip);
            Sps3SocketTransaction.Execute(session);
            return session.Root;
        }

        private sealed partial class Session : ISps3SocketTransactionSession
        {
            private readonly GameObject host;
            private readonly FixtureHost fixture;
            private readonly Sps3SocketSpecification spec;
            private readonly AnimationClip depthClip;
            private Type socketType;
            private int[] originalComponents;
            private int originalChildCount;
            private GameObject root, pose;
            private Component socket;
            private object api;
            private readonly List<GameObject> owned = new List<GameObject>();
            private readonly List<Transform> stops = new List<Transform>();
            private string identity;
            private Sps3SerializedState preparedState, expectedState;
            private readonly List<ExistingState> originalState = new List<ExistingState>();
            internal GameObject Root => root;

            internal Session(FixtureHost fixture, Sps3SocketSpecification spec, AnimationClip depthClip)
            { this.fixture = fixture; host = fixture.Host; this.spec = spec; this.depthClip = depthClip; }

            public void ValidateBeforeMutation()
            {
                if (spec == null) throw new ArgumentNullException(nameof(spec));
                ValidateHost();
                ValidateResolvedDepth();
                if (resolver == null && spec.DepthEnabled && depthClip == null) throw new InvalidOperationException("An explicit test AnimationClip is required for depth.");
                socketType = RequireObservedNativeContract();
                if (resolver != null) RequireDepthActionApiShape();
                var contact = VrcFuryApi.FindType("VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver");
                var sdk = contact == null ? null : PackageInfo.FindForAssembly(contact.Assembly);
                if (sdk == null || sdk.name != "com.vrchat.base" || sdk.version != "3.10.6" ||
                    !Sps3MaterialContactProbe.HasRequiredPublicShape(sdk.version, VrcFuryApi.FindType))
                    throw new InvalidOperationException("The exact SDK3.10.6 material-contact contract is required for this fixture.");
                originalComponents = ComponentIds(host);
                originalChildCount = host.transform.childCount;
                foreach (var component in host.GetComponentsInChildren<Component>(true)) originalState.Add(new ExistingState(component));
                identity = "sps3-candidate-" + Guid.NewGuid().ToString("N");
            }

            private void ValidateHost()
            {
                fixture.SceneSnapshot?.RequireUnchanged(host, root);
                if (host == null || host.transform.parent != null || !host.scene.IsValid() || !host.scene.isLoaded ||
                    host.scene.handle != fixture.SceneHandle || !string.IsNullOrEmpty(host.scene.path) ||
                    SceneManager.GetActiveScene() != host.scene || EditorUtility.IsPersistent(host) ||
                    PrefabUtility.IsPartOfPrefabAsset(host) || PrefabUtility.IsPartOfPrefabInstance(host))
                    throw new InvalidOperationException("Only a dedicated, loaded scene fixture is allowed; no prefab assets or instances.");
                if (host.scene.GetRootGameObjects().Any(value => value != host && value != root && !fixture.IsPreservedSceneRoot(value)))
                    throw new InvalidOperationException("The fixture must be the sole preexisting root in a dedicated scene.");
                foreach (Transform child in host.transform)
                if (FullSetupGenerator.IsGeneratedRootName(child.name) || child.name == RootName) throw new InvalidOperationException("An existing setup will not be converted.");
                foreach (var component in host.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing scripts make fixture ownership uncertain.");
                    if (PrefabUtility.IsPartOfPrefabAsset(component.gameObject) || PrefabUtility.IsPartOfPrefabInstance(component.gameObject))
                        throw new InvalidOperationException("Populate the new fixture from synthetic data, not prefab instances.");
                    var name = component.GetType().FullName;
                    if (name.StartsWith("LumaKroma.Sps2SetupAssistant.", StringComparison.Ordinal) ||
                        name == SocketTypeName || name == "SpsNdmf.Component.VRCFuryHapticSocket" ||
                        name == "VRC.Core.PipelineManager")
                        throw new InvalidOperationException("Existing native/blueprint-bearing objects are outside this new-socket fixture scope.");
                }
            }

            public void Begin() { /* No global Undo scope is opened for staging. */ }
            public void CreateStaging()
            {
                root = NewOwned(RootName, null);
                root.SetActive(false); root.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(root, host.scene);
                pose = NewOwned("Socket", root.transform);
                api = InvokePublic(null, VrcFuryApi.FindType(Sps3CandidatePreflight.FactoryApi), "CreateSocket", new[] { typeof(GameObject) }, pose);
                socket = pose.GetComponents<Component>().Single(c => c != null && c.GetType() == socketType);
                var initial = new SerializedObject(socket);
                VerifyUnrequestedDefaults(initial);
                RequireEmpty(initial, "depthActions2"); RequireEmpty(initial, "guidedPathStops");
                preparedState = CaptureState(new SerializedObject(socket));
                GuardPublic(() => InvokePublic(api, api.GetType(), "SetName", new[] { typeof(string) }, spec.Name), new[] { "name" },
                    d => { if (P(d, "name", SerializedPropertyType.String).stringValue != spec.Name) throw new InvalidOperationException("Public name did not match request."); });
                var mode = api.GetType().GetNestedType("Mode", BindingFlags.Public);
                GuardPublic(() => InvokePublic(api, api.GetType(), "SetMode", new[] { mode }, Enum.Parse(mode, spec.Mode)), new[] { "addLight" },
                    d => { if (EnumName(P(d, "addLight", SerializedPropertyType.Enum)) != spec.Mode) throw new InvalidOperationException("Public mode did not match request."); });
                if (spec.UseRadiusOffset) GuardPublic(() => InvokePublic(api, api.GetType(), "UseRadiusOffset", Type.EmptyTypes), new[] { "useRadiusOffset" },
                    d => { if (!P(d, "useRadiusOffset", SerializedPropertyType.Boolean).boolValue) throw new InvalidOperationException("Public radius offset did not match request."); });
                if (spec.DepthEnabled)
                {
                    var actions = GuardPublic(() => InvokePublic(api, api.GetType(), "AddDepthActions", new[] { typeof(Vector2), typeof(float), typeof(bool) },
                        new Vector2(spec.RangeMin, spec.RangeMax), 0f, true), DepthCreationPaths(false), VerifyCreatedDepth);
                    if (resolver != null) AppendResolvedActions(actions);
                    else GuardPublic(() => InvokePublic(actions, actions.GetType(), "AddAnimationClip", new[] { typeof(AnimationClip) }, depthClip), DepthCreationPaths(true), VerifyClipPayload);
                }
                for (int i = 0; i < spec.PathCount; i++)
                {
                    var point = spec.PathPoint(i); var stop = NewOwned("Path " + (i + 1), root.transform);
                    stop.transform.localPosition = new Vector3(point.X, point.Y, point.Z); stops.Add(stop.transform);
                }
            }
            private GameObject NewOwned(string name, Transform parent)
            {
                var value = new GameObject(name); owned.Add(value); // Track before any operation that can fail.
                if (parent != null) value.transform.SetParent(parent, false);
                return value;
            }

            public void WriteOwnedSettings()
            {
                if (failAfterStagingForTest) throw new InvalidOperationException("Injected native depth staging failure.");
                RequireOwnedSocket();
                var data = new SerializedObject(socket);
                preparedState.RequireSame(CaptureState(data));
                VerifyUnrequestedDefaults(data);
                RequireEmpty(data, "guidedPathStops");
                if (spec.DepthEnabled) { if (resolver != null) VerifyResolvedPayload(data); else VerifyClipPayload(data); }
                // Resolve every required property/type before applying any serialized change.
                P(data, "name", SerializedPropertyType.String); P(data, "oscId", SerializedPropertyType.String);
                foreach (var field in new[] { "addMenuItem", "enableAuto", SharedChannelPath, "useRadiusOffset", "offsetsInLocalUnits" })
                    P(data, field, SerializedPropertyType.Boolean);
                if (!data.FindProperty("offsetsInLocalUnits").boolValue) throw new InvalidOperationException("Native Reset did not establish local offsets.");
                var path = P(data, "guidedPathStops", SerializedPropertyType.Generic);
                if (!path.isArray) throw new InvalidOperationException("Guided path is not an array.");
                path.arraySize = spec.PathCount;
                for (int i = 0; i < spec.PathCount; i++)
                {
                    string prefix = "guidedPathStops.Array.data[" + i + "].";
                    P(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue = stops[i];
                    P(data, prefix + "shrink", SerializedPropertyType.Boolean).boolValue = spec.CollapsePath;
                    P(data, prefix + "customizeTangentIn", SerializedPropertyType.Boolean).boolValue = false;
                    P(data, prefix + "customizeTangentOut", SerializedPropertyType.Boolean).boolValue = false;
                    foreach (var field in new[] { "tangentIn", "tangentOut", "tangentInLocal", "tangentOutLocal" })
                        if (!P(data, prefix + field, SerializedPropertyType.Vector3).vector3Value.Equals(Vector3.zero))
                            throw new InvalidOperationException("Unexpected new-path tangent: " + field);
                }
                if (spec.DepthEnabled)
                {
                    if (P(data, "depthActions2", SerializedPropertyType.Generic).arraySize != 1) throw new InvalidOperationException("Unexpected depth group count.");
                    const string prefix = "depthActions2.Array.data[0].";
                    P(data, prefix + "range", SerializedPropertyType.Vector2).vector2Value = new Vector2(spec.RangeMin, spec.RangeMax);
                    SetEnum(P(data, prefix + "units", SerializedPropertyType.Enum), spec.DepthUnits);
                    foreach (var field in new[] { "enableSelf", "enableOthers", "allowHole", "allowRing" }) P(data, prefix + field, SerializedPropertyType.Boolean).boolValue = true;
                    P(data, prefix + "smoothingSeconds", SerializedPropertyType.Float).floatValue = 0f;
                    P(data, prefix + "reverseClip", SerializedPropertyType.Boolean).boolValue = false;
                    P(data, prefix + "plugRadiusForFullWeight", SerializedPropertyType.Float).floatValue = 0f;
                    if (FuryNativeFeatureShapes.InspectInstalled().RadiusZero)
                        P(data, prefix + "plugRadiusForZeroWeight", SerializedPropertyType.Float).floatValue = 0f;
                }
                data.FindProperty("name").stringValue = spec.Name; data.FindProperty("oscId").stringValue = identity;
                data.FindProperty("addMenuItem").boolValue = true; data.FindProperty("enableAuto").boolValue = spec.IncludeInAuto;
                data.FindProperty(SharedChannelPath).boolValue = spec.PublicChannel; data.FindProperty("useRadiusOffset").boolValue = spec.UseRadiusOffset;
                // Capture the intended buffer before Apply can invoke native serialization callbacks.
                // Never Update this modified buffer first, and never replace this expectation after Apply.
                if (expectedState != null) throw new InvalidOperationException("Expected state was already sealed.");
                expectedState = CaptureState(data);
                preparedState.RequireOnlyChanges(expectedState, WritePaths());
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            public void VerifyStaging()
            {
                VerifyValues();
                if (root.transform.parent != null || root.activeSelf || root.hideFlags != HideFlags.HideAndDontSave || root.scene != host.scene)
                    throw new InvalidOperationException("Staging escaped isolation.");
            }
            public void Commit()
            {
                ValidateResolvedDepth();
                ValidateHost();
                if (host.transform.childCount != originalChildCount || !ComponentIds(host).SequenceEqual(originalComponents))
                    throw new InvalidOperationException("Host changed during preparation; nothing will be converted.");
                VerifyExistingState();
                root.hideFlags = HideFlags.None; root.transform.SetParent(host.transform, false);
                root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
                root.SetActive(true);
            }
            public void VerifyCommitted()
            {
                VerifyValues();
                if (root.transform.parent != host.transform || root.scene != host.scene || host.transform.childCount != originalChildCount + 1 ||
                    !root.activeSelf || root.hideFlags != HideFlags.None)
                    throw new InvalidOperationException("Commit ownership verification failed.");
                var remaining = host.GetComponentsInChildren<Component>(true).Where(c => c != null && !c.transform.IsChildOf(root.transform))
                    .Select(c => c.GetInstanceID()).OrderBy(i => i).ToArray();
                if (!remaining.SequenceEqual(originalComponents)) throw new InvalidOperationException("Existing host objects changed during commit.");
                VerifyExistingState();
            }
            public void Complete()
            {
                // One fully configured creation record. Never collapse/revert a global range of Undo operations.
                Undo.IncrementCurrentGroup(); Undo.RegisterCreatedObjectUndo(root, UndoName);
                VerifyCommitted();
            }
            public void Rollback()
            {
                foreach (var value in owned.Where(value => value != null))
                {
                    if (value.GetComponentsInChildren<Transform>(true).Any(t => !owned.Contains(t.gameObject)))
                        throw new InvalidOperationException("A foreign child entered owned staging. Preserve it; cleanup outcome is uncertain.");
                    if (value.GetComponents<Component>().Any(c => c == null || (c.GetType() != typeof(Transform) && (value != pose || c.GetType() != socketType))))
                        throw new InvalidOperationException("An unknown component entered owned staging. Preserve it; cleanup outcome is uncertain.");
                }
                // Only objects created and recorded by this session may be cleaned up. Never delete source assets.
                foreach (var value in owned.AsEnumerable().Reverse()) if (value != null)
                {
                    foreach (var component in value.GetComponents<Component>()) if (component != null) Undo.ClearUndo(component);
                    Undo.ClearUndo(value); Object.DestroyImmediate(value);
                }
                if (owned.Any(value => value != null)) throw new InvalidOperationException("Owned staging objects remain after rollback.");
                VerifyExistingState();
            }
            private void VerifyExistingState()
            {
                fixture.SceneSnapshot?.RequireUnchanged(host, root);
                if (host == null || originalState.Any(state => !state.Unchanged()))
                    throw new InvalidOperationException("Preexisting fixture state changed. Preserve that state; never auto-revert unrelated edits.");
                var remaining = host.GetComponentsInChildren<Component>(true).Where(c => c != null && (root == null || !c.transform.IsChildOf(root.transform)))
                    .Select(c => c.GetInstanceID()).OrderBy(i => i).ToArray();
                if (!remaining.SequenceEqual(originalComponents) || host.scene.GetRootGameObjects().Any(value => value != host && value != root && !fixture.IsPreservedSceneRoot(value)))
                    throw new InvalidOperationException("Foreign objects or components appeared. Preserve them; this transaction cannot claim clean rollback.");
            }
            private sealed class ExistingState
            {
                private readonly Component component;
                private readonly string json, name, tag;
                private readonly int layer, parent, sibling;
                private readonly bool active;
                private readonly HideFlags flags, componentFlags;
                private readonly Vector3 position, scale;
                private readonly Quaternion rotation;
                internal ExistingState(Component component)
                {
                    this.component = component; var obj = component.gameObject;
                    name = obj.name; tag = obj.tag; layer = obj.layer; active = obj.activeSelf; flags = obj.hideFlags; componentFlags = component.hideFlags;
                    if (component is Transform t)
                    { parent = t.parent == null ? 0 : t.parent.GetInstanceID(); sibling = t.GetSiblingIndex(); position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
                    else json = EditorJsonUtility.ToJson(component);
                }
                internal bool Unchanged()
                {
                    if (component == null) return false; var obj = component.gameObject;
                    if (obj.name != name || obj.tag != tag || obj.layer != layer || obj.activeSelf != active || obj.hideFlags != flags || component.hideFlags != componentFlags) return false;
                    if (component is Transform t) return (t.parent == null ? 0 : t.parent.GetInstanceID()) == parent && t.GetSiblingIndex() == sibling &&
                        t.localPosition.Equals(position) && t.localRotation.Equals(rotation) && t.localScale.Equals(scale);
                    return EditorJsonUtility.ToJson(component) == json;
                }
            }
            private object GuardPublic(Func<object> call, IEnumerable<string> allowed, Action<SerializedObject> postcondition = null)
            {
                preparedState.RequireSame(CaptureState(new SerializedObject(socket)));
                var result = call();
                ValidateResolvedDepth();
                var data = new SerializedObject(socket);
                VerifyUnrequestedDefaults(data);
                postcondition?.Invoke(data);
                var after = CaptureState(data);
                preparedState.RequireOnlyChanges(after, allowed);
                preparedState = after;
                return result;
            }
            private static void RequireEmpty(SerializedObject data, string field)
            {
                var list = P(data, field, SerializedPropertyType.Generic);
                if (!list.isArray || list.arraySize != 0) throw new InvalidOperationException("Expected a new empty list: " + field);
            }
            private void VerifyCreatedDepth(SerializedObject data)
            {
                var groups = P(data, "depthActions2", SerializedPropertyType.Generic);
                if (!groups.isArray || groups.arraySize != 1) throw new InvalidOperationException("Expected one new depth group.");
                const string p = "depthActions2.Array.data[0].";
                RequireEmpty(data, p + "actionSet.actions");
                if (!P(data, p + "range", SerializedPropertyType.Vector2).vector2Value.Equals(new Vector2(spec.RangeMin, spec.RangeMax)) ||
                    EnumName(P(data, p + "units", SerializedPropertyType.Enum)) != "Local" ||
                    P(data, p + "smoothingSeconds", SerializedPropertyType.Float).floatValue != 0f ||
                    P(data, p + "plugRadiusForFullWeight", SerializedPropertyType.Float).floatValue != 0f ||
                    (FuryNativeFeatureShapes.InspectInstalled().RadiusZero && P(data, p + "plugRadiusForZeroWeight", SerializedPropertyType.Float).floatValue != 0f) ||
                    P(data, p + "reverseClip", SerializedPropertyType.Boolean).boolValue)
                    throw new InvalidOperationException("Unexpected public API depth defaults.");
                foreach (var field in new[] { "enableSelf", "enableOthers", "allowHole", "allowRing" })
                    if (!P(data, p + field, SerializedPropertyType.Boolean).boolValue)
                        throw new InvalidOperationException("Unexpected public API depth condition: " + field);
            }
            private void VerifyClipPayload(SerializedObject data)
            {
                const string a = "depthActions2.Array.data[0].actionSet.actions";
                var actions = P(data, a, SerializedPropertyType.Generic);
                if (!actions.isArray || actions.arraySize != 1 ||
                    actions.GetArrayElementAtIndex(0).propertyType != SerializedPropertyType.ManagedReference ||
                    actions.GetArrayElementAtIndex(0).managedReferenceValue?.GetType().FullName != "VF.Model.StateAction.AnimationClipAction" ||
                    P(data, a + ".Array.data[0].clip.objRef", SerializedPropertyType.ObjectReference).objectReferenceValue != depthClip)
                    throw new InvalidOperationException("Unexpected public API depth payload.");
                foreach (var field in new[] { "desktopActive", "androidActive", "localOnly", "remoteOnly" })
                    if (P(data, a + ".Array.data[0]." + field, SerializedPropertyType.Boolean).boolValue)
                        throw new InvalidOperationException("Unrequested animation condition: " + field);
            }
            // Exact new-node paths from pinned beta.14, not a depthActions2 subtree exemption.
            // Unity Next(true) emits the separate .Array container token as well as .Array.size.
            private static IEnumerable<string> DepthCreationPaths(bool clip)
            {
                const string p = "depthActions2.Array.data[0].";
                if (!clip)
                {
                    foreach (var key in new[] { "depthActions2", "depthActions2.Array", "depthActions2.Array.size", "depthActions2.Array.data[0]" }) yield return key;
                    foreach (var key in new[] { "range", "units", "enableSelf", "enableOthers", "allowHole", "allowRing",
                        "smoothingSeconds", "reverseClip", "plugRadiusForFullWeight", "actionSet", "actionSet.actions", "actionSet.actions.Array", "actionSet.actions.Array.size" }) yield return p + key;
                    if (FuryNativeFeatureShapes.InspectInstalled().RadiusZero) yield return p + "plugRadiusForZeroWeight";
                }
                else
                {
                    yield return p + "actionSet.actions"; yield return p + "actionSet.actions.Array"; yield return p + "actionSet.actions.Array.size";
                    const string a = p + "actionSet.actions.Array.data[0]";
                    yield return a;
                    foreach (var key in new[] { "version", "desktopActive", "androidActive", "localOnly", "remoteOnly", "clip",
                        "clip.version", "clip.fileID", "clip.guid", "clip.id", "clip.objRef" }) yield return a + "." + key;
                }
            }
            private IEnumerable<string> WritePaths()
            {
                foreach (var key in new[] { "name", "oscId", "addMenuItem", "enableAuto", SharedChannelPath, "useRadiusOffset",
                    "guidedPathStops", "guidedPathStops.Array.size" }) yield return key;
                if (spec.PathCount > 0) yield return "guidedPathStops.Array";
                for (int i = 0; i < spec.PathCount; i++)
                {
                    string p = "guidedPathStops.Array.data[" + i + "]";
                    yield return p;
                    if (FuryNativeFeatureShapes.InspectInstalled().PointRadius) {
                        yield return p + ".useRadiusOffset"; yield return p + ".maxRadiusOffset";
                    }
                    foreach (var key in new[] { "transform", "shrink", "customizeTangentIn", "customizeTangentOut",
                        "tangentIn", "tangentOut", "tangentInLocal", "tangentOutLocal" }) yield return p + "." + key;
                }
                if (spec.DepthEnabled)
                    foreach (var key in new[] { "range", "units", "enableSelf", "enableOthers", "allowHole", "allowRing",
                        "smoothingSeconds", "reverseClip", "plugRadiusForFullWeight" }) yield return "depthActions2.Array.data[0]." + key;
                if (spec.DepthEnabled && FuryNativeFeatureShapes.InspectInstalled().RadiusZero)
                    yield return "depthActions2.Array.data[0].plugRadiusForZeroWeight";
            }
            private static void VerifyUnrequestedDefaults(SerializedObject data)
            {
                if (UsesChannels &&
                    !P(data, "tagsUpgradedToChannels", SerializedPropertyType.Boolean).boolValue)
                    throw new InvalidOperationException("Native channel migration has not completed; do not overwrite legacy channel settings.");
                // These defaults come from the pinned Socket model. Unexpected values are rejected, never cleared.
                foreach (var field in new[] { "plugEntryRadiusActions", "activeActions.actions", CustomChannelsPath, "depthActions" })
                {
                    var list = P(data, field, SerializedPropertyType.Generic);
                    if (!list.isArray || list.arraySize != 0) throw new InvalidOperationException("Unrequested list is not empty: " + field);
                }
                foreach (var field in new[] { "overrideLegacySocketType", "overrideLegacyOffset", "enablePlugLengthParameter",
                    "enablePlugWidthParameter", "enableDepthAnimations", "enableActiveAnimation" })
                    if (P(data, field, SerializedPropertyType.Boolean).boolValue) throw new InvalidOperationException("Unrequested option: " + field);
                foreach (var field in new[] { "position", "rotation", "legacyOffset", "legacyOffsetLocal" })
                    if (!P(data, field, SerializedPropertyType.Vector3).vector3Value.Equals(Vector3.zero))
                        throw new InvalidOperationException("Unrequested pose or Legacy offset: " + field);
                if (EnumName(P(data, "legacySocketType", SerializedPropertyType.Enum)) != "Hole")
                    throw new InvalidOperationException("Unexpected unused Legacy type.");
            }
            internal static Sps3SerializedState CaptureState(SerializedObject data)
            {
                var values = new List<KeyValuePair<string, string>>();
                var p = data.GetIterator();
                bool children = true;
                int count = 0;
                while (p.Next(children))
                {
                    if (++count > 4096) throw new InvalidOperationException("Serialized tree exceeded fixture bound.");
                    children = p.propertyType == SerializedPropertyType.Generic || p.propertyType == SerializedPropertyType.ManagedReference;
                    string value;
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.Generic: value = p.isArray ? "array:" + p.arraySize : "container"; break;
                        case SerializedPropertyType.ManagedReference: value = p.managedReferenceFullTypename; break;
                        case SerializedPropertyType.Integer: value = p.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                        case SerializedPropertyType.ArraySize: value = p.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                        case SerializedPropertyType.Boolean: value = p.boolValue ? "true" : "false"; break;
                        case SerializedPropertyType.Float: value = BitConverter.ToString(BitConverter.GetBytes(p.doubleValue)); break;
                        case SerializedPropertyType.String: value = p.stringValue; break;
                        case SerializedPropertyType.ObjectReference: value = p.objectReferenceInstanceIDValue.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                        case SerializedPropertyType.Enum: value = p.intValue + ":" + p.enumValueIndex + ":" + string.Join("|", p.enumNames); break;
                        case SerializedPropertyType.Vector2: var v2 = p.vector2Value; value = Bits(v2.x, v2.y); break;
                        case SerializedPropertyType.Vector3: var v3 = p.vector3Value; value = Bits(v3.x, v3.y, v3.z); break;
                        default: throw new InvalidOperationException("Unverified serialized type at " + p.propertyPath + ": " + p.propertyType);
                    }
                    values.Add(new KeyValuePair<string, string>(p.propertyPath, p.propertyType + ":" + p.type + ":" + value));
                }
                return new Sps3SerializedState(values);
            }
            private static string Bits(params float[] values) => string.Join("/", values.Select(v => BitConverter.ToString(BitConverter.GetBytes(v))));

            private void RequireOwnedSocket()
            {
                if (root == null || pose == null || socket == null || !owned.Contains(root) || !owned.Contains(pose) ||
                    pose.transform.parent != root.transform || socket.gameObject != pose || socket.GetType() != socketType)
                    throw new InvalidOperationException("Session does not own the staging socket.");
            }
            private void VerifyValues()
            {
                ValidateResolvedDepth();
                RequireOwnedSocket(); var data = new SerializedObject(socket); data.Update();
                if (expectedState == null) throw new InvalidOperationException("Expected state has not been sealed.");
                expectedState.RequireSame(CaptureState(data));
                VerifyUnrequestedDefaults(data);
                bool same = P(data, "name", SerializedPropertyType.String).stringValue == spec.Name &&
                    P(data, "oscId", SerializedPropertyType.String).stringValue == identity &&
                    P(data, "enableAuto", SerializedPropertyType.Boolean).boolValue == spec.IncludeInAuto &&
                    P(data, SharedChannelPath, SerializedPropertyType.Boolean).boolValue == spec.PublicChannel &&
                    P(data, "useRadiusOffset", SerializedPropertyType.Boolean).boolValue == spec.UseRadiusOffset &&
                    P(data, "addMenuItem", SerializedPropertyType.Boolean).boolValue &&
                    P(data, "offsetsInLocalUnits", SerializedPropertyType.Boolean).boolValue &&
                    EnumName(P(data, "addLight", SerializedPropertyType.Enum)) == spec.Mode &&
                    P(data, "guidedPathStops", SerializedPropertyType.Generic).arraySize == spec.PathCount &&
                    P(data, "depthActions2", SerializedPropertyType.Generic).arraySize == (spec.DepthEnabled ? 1 : 0);
                same &= root.transform.localPosition.Equals(Vector3.zero) && root.transform.localRotation.Equals(Quaternion.identity) && root.transform.localScale.Equals(Vector3.one) &&
                    root.transform.childCount == 1 + spec.PathCount && pose.transform.childCount == 0 && pose.activeSelf && pose.hideFlags == HideFlags.None &&
                    pose.transform.localPosition.Equals(Vector3.zero) && pose.transform.localRotation.Equals(Quaternion.identity) && pose.transform.localScale.Equals(Vector3.one) &&
                    pose.GetComponents<Component>().Length == 2 && root.GetComponents<Component>().Length == 1;
                for (int i = 0; i < spec.PathCount; i++)
                {
                    string p = "guidedPathStops.Array.data[" + i + "].";
                    var point = spec.PathPoint(i);
                    same &= stops[i] != null && stops[i].parent == root.transform &&
                        stops[i].localPosition.Equals(new Vector3(point.X, point.Y, point.Z)) &&
                        stops[i].localRotation.Equals(Quaternion.identity) && stops[i].localScale.Equals(Vector3.one) &&
                        stops[i].gameObject.activeSelf && stops[i].gameObject.hideFlags == HideFlags.None && stops[i].GetComponents<Component>().Length == 1 && stops[i].childCount == 0 &&
                        P(data, p + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue == stops[i] &&
                        P(data, p + "shrink", SerializedPropertyType.Boolean).boolValue == spec.CollapsePath &&
                        !P(data, p + "customizeTangentIn", SerializedPropertyType.Boolean).boolValue &&
                        !P(data, p + "customizeTangentOut", SerializedPropertyType.Boolean).boolValue &&
                        !stops[i].GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType() == socketType);
                }
                if (spec.DepthEnabled)
                {
                    const string p = "depthActions2.Array.data[0].";
                    same &= EnumName(P(data, p + "units", SerializedPropertyType.Enum)) == spec.DepthUnits &&
                        P(data, p + "range", SerializedPropertyType.Vector2).vector2Value == new Vector2(spec.RangeMin, spec.RangeMax) &&
                        P(data, p + "enableSelf", SerializedPropertyType.Boolean).boolValue && P(data, p + "enableOthers", SerializedPropertyType.Boolean).boolValue &&
                        P(data, p + "allowHole", SerializedPropertyType.Boolean).boolValue && P(data, p + "allowRing", SerializedPropertyType.Boolean).boolValue &&
                        P(data, p + "smoothingSeconds", SerializedPropertyType.Float).floatValue == 0f &&
                        !P(data, p + "reverseClip", SerializedPropertyType.Boolean).boolValue &&
                        P(data, p + "plugRadiusForFullWeight", SerializedPropertyType.Float).floatValue == 0f &&
                        (!FuryNativeFeatureShapes.InspectInstalled().RadiusZero || P(data, p + "plugRadiusForZeroWeight", SerializedPropertyType.Float).floatValue == 0f);
                    if (resolver != null) VerifyResolvedPayload(data);
                    else
                    {
                    var actions = P(data, p + "actionSet.actions", SerializedPropertyType.Generic);
                    same &= actions.isArray && actions.arraySize == 1;
                    if (same)
                    {
                        var action = actions.GetArrayElementAtIndex(0);
                        same &= action.propertyType == SerializedPropertyType.ManagedReference &&
                            action.managedReferenceValue?.GetType().FullName == "VF.Model.StateAction.AnimationClipAction" &&
                            P(data, p + "actionSet.actions.Array.data[0].clip.objRef", SerializedPropertyType.ObjectReference).objectReferenceValue == depthClip;
                    }
                    }
                }
                if (!same) throw new InvalidOperationException("Post-write verification failed; rollback is required.");
            }
        }

        internal static Sps3SerializedState CaptureCompatibilityState(Component component) => Session.CaptureState(new SerializedObject(component));

        internal static Type RequireObservedNativeContract()
        {
            var socketType = VrcFuryApi.FindType(SocketTypeName);
            var package = RequireExactPackage(socketType);
            // A compiled type is not enough: if Unity cannot bind its MonoScript
            // asset, a scene can save a transient script reference that becomes
            // Missing Script after reopening. Refuse before mutating the avatar.
            var scriptPath = "Packages/" + package.name + "/Runtime/Component/VRCFuryHapticSocket.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null || script.GetClass() != socketType)
                throw new InvalidOperationException("VRCFury Socket のスクリプト資産と実行中の型が一致しないため、保存後の再読込を保証できません。Unity のインポート状態を確認してください。既存データは変更していません。");
            if (!FuryMinimalSps3Probe.Available(out var reason))
                throw new InvalidOperationException("Basic SPS3 Socket unavailable: " + reason);
            return socketType;
        }

        private static int[] ComponentIds(GameObject host) => host.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()).OrderBy(i => i).ToArray();
        private static SerializedProperty P(SerializedObject data, string name, SerializedPropertyType type)
        { var value = data.FindProperty(name); if (value == null || value.propertyType != type) throw new InvalidOperationException("SPS3 schema mismatch: " + name); return value; }
        private static string EnumName(SerializedProperty value) => value.enumValueIndex >= 0 && value.enumValueIndex < value.enumNames.Length ? value.enumNames[value.enumValueIndex] : null;
        private static void SetEnum(SerializedProperty value, string name)
        { int index = Sps3CandidatePlan.ResolveEnumByName(value.enumNames, name); if (index < 0) throw new InvalidOperationException("SPS3 enum mismatch: " + name); value.enumValueIndex = index; }
        private static object InvokePublic(object target, Type type, string name, Type[] arguments, params object[] values)
        {
            var method = type?.GetMethod(name, BindingFlags.Public | BindingFlags.DeclaredOnly | (target == null ? BindingFlags.Static : BindingFlags.Instance), null, arguments, null);
            if (method == null || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments) || method.IsDefined(typeof(ObsoleteAttribute), false))
                throw new InvalidOperationException("Exact public method unavailable: " + name);
            try { return method.Invoke(target, values); } catch (TargetInvocationException e) { throw new InvalidOperationException("Native SPS3 call failed: " + name, e.InnerException ?? e); }
        }
        private static PackageInfo RequireExactPackage(Type type)
        {
            var package = type == null ? null : PackageInfo.FindForAssembly(type.Assembly);
            if (package == null || package.name != Sps3CandidatePreflight.PackageId)
                throw new InvalidOperationException("VRCFury SPS3 package identity is required.");
            return package;
        }
        private static bool UsesChannels => VrcFuryApi.FindType(SocketTypeName)?.GetField("channelSettings") != null;
        private static string SharedChannelPath => UsesChannels ? "channelSettings.useSharedTag" : "useSharedTag";
        private static string CustomChannelsPath => UsesChannels ? "channelSettings.channels" : "tags";
        private static void ValidateModelShape(Type socket)
        {
            if (socket == null || socket.FullName != SocketTypeName || !typeof(Component).IsAssignableFrom(socket)) throw new InvalidOperationException("Socket model mismatch.");
            void Field(Type owner, string name, Type expected)
            {
                var f = owner?.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f == null || f.FieldType != expected || f.IsInitOnly || f.IsDefined(typeof(ObsoleteAttribute), false)) throw new InvalidOperationException("Model field mismatch: " + name);
            }
            Field(socket, "name", typeof(string)); Field(socket, "oscId", typeof(string));
            var radius = socket.GetNestedType("PlugEntryRadiusAction", BindingFlags.Public);
            var state = socket.Assembly.GetType("VF.Model.State");
            if (radius == null || state == null) throw new InvalidOperationException("Unrequested-action schema is absent.");
            Field(socket, "plugEntryRadiusActions", typeof(List<>).MakeGenericType(radius)); Field(socket, "activeActions", state);
            if (UsesChannels)
            {
                var channels = socket.Assembly.GetType("VF.Model.SpsChannelSettings");
                var entry = channels?.GetNestedType("Channel", BindingFlags.Public);
                if (channels == null || entry == null) throw new InvalidOperationException("SPS3 channel schema missing.");
                Field(socket, "channelSettings", channels);
                Field(channels, "useSharedTag", typeof(bool)); Field(channels, "useHipAvoidance", typeof(bool));
                Field(channels, "channels", typeof(List<>).MakeGenericType(entry));
                Field(entry, "id", typeof(string)); Field(entry, "allowSelf", typeof(bool)); Field(entry, "allowOthers", typeof(bool));
            }
            else { Field(socket, "tags", typeof(List<string>)); Field(socket, "useSharedTag", typeof(bool)); }
            foreach (var name in new[] { "overrideLegacySocketType", "overrideLegacyOffset", "enablePlugLengthParameter", "enablePlugWidthParameter" }) Field(socket, name, typeof(bool));
            foreach (var name in new[] { "position", "rotation", "legacyOffsetLocal" }) Field(socket, name, typeof(Vector3));
            var legacy = socket.GetNestedType("LegacySocketType", BindingFlags.Public);
            if (legacy == null || !legacy.IsEnum || !Enum.GetNames(legacy).SequenceEqual(new[] { "Hole", "Ring" })) throw new InvalidOperationException("Legacy enum contract changed.");
            Field(socket, "legacySocketType", legacy);
            foreach (var name in new[] { "addMenuItem", "enableAuto", "useRadiusOffset" }) Field(socket, name, typeof(bool));
            var stop = socket.GetNestedType("GuidedPathStop", BindingFlags.Public); var depth = socket.GetNestedType("DepthActionNew", BindingFlags.Public);
            if (stop == null || depth == null) throw new InvalidOperationException("Required native nested models are absent.");
            Field(socket, "guidedPathStops", typeof(List<>).MakeGenericType(stop)); Field(socket, "depthActions2", typeof(List<>).MakeGenericType(depth));
            Field(stop, "transform", typeof(Transform));
            if (FuryNativeFeatureShapes.InspectInstalled().PointRadius) {
                Field(socket, "maxRadiusOffset", typeof(float));
                Field(stop, "useRadiusOffset", typeof(bool)); Field(stop, "maxRadiusOffset", typeof(float));
            }
            foreach (var name in new[] { "shrink", "customizeTangentIn", "customizeTangentOut" }) Field(stop, name, typeof(bool));
            Field(depth, "range", typeof(Vector2));
            foreach (var name in new[] { "enableSelf", "enableOthers", "allowHole", "allowRing", "reverseClip" }) Field(depth, name, typeof(bool));
            Field(depth, "smoothingSeconds", typeof(float)); Field(depth, "plugRadiusForFullWeight", typeof(float));
            if (FuryNativeFeatureShapes.InspectInstalled().RadiusZero) Field(depth, "plugRadiusForZeroWeight", typeof(float));
            var units = socket.GetNestedType("DepthActionUnits", BindingFlags.Public);
            if (units == null || !units.IsEnum || !Enum.GetNames(units).SequenceEqual(new[] { "Meters", "Plugs", "Local" })) throw new InvalidOperationException("Depth enum contract changed.");
            Field(depth, "units", units);
        }
    }
}
