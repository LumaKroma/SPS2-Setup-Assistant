using System;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Linq;
using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        private sealed class AutoDisplaySnapshot
        {
            internal bool ControlsVisible;
            internal AutoCapacityEstimate Capacity;
        }
        [NonSerialized] private AutoDisplayCache<AutoDisplaySnapshot> autoDisplayCache;
        private sealed class GenerationPreview
        {
            internal string Blocker;
            internal string Warning;
            internal Sps3NewSetupPreflight Readiness;
            internal FuryNormalOutputRouter.Route Route;
        }
        [NonSerialized] private AutoDisplayCache<GenerationPreview> generationPreviewCache;
        private sealed class FamilyDisplay {
            internal VrcFuryCapabilities Fury, Ndmf;
            internal SpsFamily? ExistingFamily;
            internal string Error;
        }
        [NonSerialized] private AutoDisplayCache<FamilyDisplay> familyDisplayCache;
        private FamilyDisplay ReadFamilyDisplay(EventType? eventType) {
            FamilyDisplay Capture() {
                var value = new FamilyDisplay();
                try {
                    var root = FullSetupGenerator.Find(descriptor);
                    value.ExistingFamily = root?.settings?.family;
                    value.Fury = VrcFuryCapabilities.ForAuthoring(SpsFamily.VrcFury,
                        SpsAuthoringCompatibility.SelectContract(SpsFamily.VrcFury, root, true, true), true);
                } catch (Exception e) { value.Error = TryUserReason(e.Message, out var reason) ? reason : L("設定を読み取れませんでした。選択したアバターと導入状態を確認してください。"); value.Fury = VrcFuryCapabilities.For(SpsFamily.VrcFury); }
                value.Ndmf = VrcFuryCapabilities.For(SpsFamily.SpsForNdmf);
                return value;
            }
            if (!eventType.HasValue || !PreviewUsesMemo(eventType.Value)) return Capture();
            if (familyDisplayCache == null) familyDisplayCache = new AutoDisplayCache<FamilyDisplay>();
            return familyDisplayCache.Get(EditorApplication.timeSinceStartup, Capture, () => {
                var avatar=descriptor; var draft=settings; var family=draft.family;
                int revision=shapeSelectionRevision, contextRevision=autoContextRevision;
                return () => this != null && ReferenceEquals(descriptor,avatar) &&
                    (ReferenceEquals(avatar,null) || avatar != null) && ReferenceEquals(settings,draft) &&
                    settings != null && settings.family==family && revision==shapeSelectionRevision && contextRevision==autoContextRevision;
            }, eventType.Value == EventType.Layout);
        }
        [NonSerialized] private bool autoDisplayHooksEnabled;
        [NonSerialized] private int autoContextRevision;
        [NonSerialized] private TransformWatch externalWatch;
        [NonSerialized] private Dictionary<string, Hash128> assetWatch;
        [NonSerialized] private HashSet<string> assetDependencies;
        [NonSerialized] private string externalDraftJson;
        private sealed class TransformWatch
        {
            internal VRCAvatarDescriptor avatar;
            internal Transform[] nodes, parents;
            internal int[] childCounts;
            internal string[] names;
            internal bool[] active;
            internal Vector3[] positions, scales;
            internal Quaternion[] rotations;
            internal SkinnedMeshRenderer[] renderers;
            internal Mesh[] meshes;
            internal float[][] weights;
            internal bool Changed(VRCAvatarDescriptor current)
            {
                if (!ReferenceEquals(avatar, current) || (avatar != null && current == null)) return true;
                for (int i = 0; i < nodes.Length; i++) {
                    var t = nodes[i];
                    if (t == null || t.parent != parents[i] || t.childCount != childCounts[i] ||
                        t.name != names[i] || t.gameObject.activeSelf != active[i]) return true;
                }
                return false;
            }
            internal bool VisualChanged()
            {
                for (int i = 0; i < nodes.Length; i++) {
                    var t = nodes[i];
                    if (t == null || t.localPosition != positions[i] || t.localRotation != rotations[i] || t.localScale != scales[i]) return true;
                }
                for (int i = 0; i < renderers.Length; i++) {
                    var renderer = renderers[i];
                    if (renderer == null || renderer.sharedMesh != meshes[i]) return true;
                    int count = meshes[i] == null ? 0 : meshes[i].blendShapeCount;
                    if (weights[i].Length != count) return true;
                    for (int n = 0; n < count; n++) if (renderer.GetBlendShapeWeight(n) != weights[i][n]) return true;
                }
                return false;
            }
        }
        private void CaptureTransformWatch()
        {
            var items = new HashSet<Transform>();
            var renderers = Array.Empty<SkinnedMeshRenderer>();
            void Add(Transform t) { if (t != null) items.Add(t); }
            if (descriptor != null) {
                Add(descriptor.transform); Add(descriptor.transform.parent);
                var animator = descriptor.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) Add(animator.GetBoneTransform((HumanBodyBones)i));
                renderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var renderer in renderers) Add(renderer.transform);
                Sps2SetupContext generated = null;
                try { generated = FullSetupGenerator.Find(descriptor); } catch { /* Other guards report ambiguous ownership. */ }
                if (generated?.gameObject != null) foreach (var t in generated.gameObject.GetComponentsInChildren<Transform>(true)) Add(t);
            }
            if (settings?.parts != null) foreach (var p in settings.parts) if (p != null) Add(p.target);
            var nodes = items.ToArray();
            var meshes = renderers.Select(r => r.sharedMesh).ToArray();
            externalWatch = new TransformWatch { avatar = descriptor, nodes = nodes,
                parents = nodes.Select(t => t.parent).ToArray(), childCounts = nodes.Select(t => t.childCount).ToArray(),
                names = nodes.Select(t => t.name).ToArray(), active = nodes.Select(t => t.gameObject.activeSelf).ToArray(),
                positions = nodes.Select(t => t.localPosition).ToArray(), rotations = nodes.Select(t => t.localRotation).ToArray(), scales = nodes.Select(t => t.localScale).ToArray(),
                renderers = renderers, meshes = meshes,
                weights = renderers.Select((r, i) => Enumerable.Range(0, meshes[i] == null ? 0 : meshes[i].blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()).ToArray() };
            externalDraftJson = settings == null ? null : JsonUtility.ToJson(settings);
        }
        private void CaptureAssetWatch()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            void Add(UnityEngine.Object o) { if (o == null) return; var path = AssetDatabase.GetAssetPath(o); if (!string.IsNullOrEmpty(path)) paths.Add(path); }
            if (descriptor != null) {
                Add(PrefabUtility.GetCorrespondingObjectFromSource(descriptor.gameObject));
                var animator = descriptor.GetComponent<Animator>(); if (animator != null) Add(animator.runtimeAnimatorController);
                foreach (var renderer in descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    Add(renderer.sharedMesh); foreach (var material in renderer.sharedMaterials) Add(material);
                }
                try { Add(FullSetupGenerator.Find(descriptor)?.asset); } catch { /* No ownership decision is made here. */ }
            }
            if (settings?.parts != null) foreach (var part in settings.parts.Where(p => p != null))
                foreach (var action in Sps3AnimationGroupNative.AllSavedActions(part).Where(a => a != null)) {
                    Add(action.clip); Add(action.generatedBlendShapeClip); if (action.renderer != null) Add(action.renderer.sharedMesh);
                }
            var socketType = VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury));
            var package = socketType == null ? null : UnityEditor.PackageManager.PackageInfo.FindForAssembly(socketType.Assembly);
            if (package != null && package.name == SpsBackend.PackageId(SpsFamily.VrcFury))
                paths.Add(package.assetPath + "/Runtime/Component/" + socketType.Name + ".cs");
            assetDependencies = new HashSet<string>(paths, StringComparer.Ordinal);
            if (paths.Count != 0) foreach (var path in AssetDatabase.GetDependencies(paths.ToArray(), true)) assetDependencies.Add(path);
            assetWatch = paths.ToDictionary(p => p, p => AssetDatabase.GetAssetDependencyHash(p), StringComparer.Ordinal);
        }
        private void AutoProjectChanged()
        {
            try {
                bool changed = assetWatch != null && !assetWatch.All(pair => AssetDatabase.GetAssetDependencyHash(pair.Key) == pair.Value);
                if (!FuryNativeFeatureShapes.NotificationNeedsRefresh(assetWatch != null, changed, false)) return;
            } catch { /* Failure must not silently suppress refresh. */ }
            FuryMinimalSps3Probe.InvalidatePreview();
            FuryPreviewEnvironmentCache.Invalidate();
            AutoExternalContextChanged();
        }
        private void AutoPackagesChanged(UnityEditor.PackageManager.PackageRegistrationEventArgs change)
        {
            if (change.added.Concat(change.removed).Concat(change.changedFrom).Concat(change.changedTo).Any(p => p != null && FuryNativeFeatureShapes.RelevantPackage(p.name)))
                AutoExternalContextChanged();
        }
        private void AutoHierarchyChanged()
        {
            try {
                bool changed = externalWatch != null && externalWatch.Changed(descriptor);
                if (!FuryNativeFeatureShapes.NotificationNeedsRefresh(externalWatch != null, changed, false)) return;
            }
            catch { /* Unknown hierarchy state must not suppress an update. */ }
            externalWatch = null;
            RecoverAvatar();
            AutoExternalContextChanged();
        }
        private void AutoUndoChanged()
        {
            // ObjectChangeEvents handles target component/asset Undo. This catches the
            // EditorWindow's own serialized draft without refreshing for unrelated Undo.
            var now = settings == null ? null : JsonUtility.ToJson(settings);
            if (string.Equals(now, externalDraftJson, StringComparison.Ordinal)) return;
            InvalidateShapeSelection();
            AutoExternalContextChanged();
        }
        private void RefreshUnnotifiedChangesOnFocus()
        {
            try {
                if (externalWatch != null && (externalWatch.Changed(descriptor) || externalWatch.VisualChanged())) {
                    AutoExternalContextChanged(); return;
                }
                AutoProjectChanged();
            } catch { AutoExternalContextChanged(); }
        }
        private void InvalidateAutoDisplayCache() { autoDisplayCache?.Invalidate(); depthDisplayCache?.Invalidate(); generationPreviewCache?.Invalidate(); generationPreviewPass?.Invalidate(); familyDisplayCache?.Invalidate(); }
        private void AutoExternalContextChanged()
        {
            EndBlendPreview();
            unchecked { autoContextRevision++; }
            externalWatch = null;
            assetWatch = null; assetDependencies = null;
            InvalidateAutoDisplayCache();
            RefreshLegacySetupNotice();
            // No binding, draft mutation, discovery, or expensive work in event callbacks.
            if (this != null) Repaint();
        }
        private void EnableAutoDisplayCache()
        {
            if (autoDisplayHooksEnabled) return;
            autoDisplayHooksEnabled = true;
            EditorApplication.hierarchyChanged += AutoHierarchyChanged;
            EditorApplication.projectChanged += AutoProjectChanged;
            UnityEditor.PackageManager.Events.registeredPackages += AutoPackagesChanged;
            ObjectChangeEvents.changesPublished += AutoObjectChanges;
            Undo.undoRedoPerformed += AutoUndoChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += AutoSceneClosing;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += AutoSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += AutoSceneClosed;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += AutoActiveSceneChanged;
            InvalidateAutoDisplayCache();
        }
        private void DisableAutoDisplayCache()
        {
            if (autoDisplayHooksEnabled) {
                EditorApplication.hierarchyChanged -= AutoHierarchyChanged;
                EditorApplication.projectChanged -= AutoProjectChanged;
                UnityEditor.PackageManager.Events.registeredPackages -= AutoPackagesChanged;
                ObjectChangeEvents.changesPublished -= AutoObjectChanges;
                Undo.undoRedoPerformed -= AutoUndoChanged;
                UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= AutoSceneClosing;
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= AutoSceneOpened;
                UnityEditor.SceneManagement.EditorSceneManager.sceneClosed -= AutoSceneClosed;
                UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= AutoActiveSceneChanged;
            }
            autoDisplayHooksEnabled = false;
            externalWatch = null;
            assetWatch = null; assetDependencies = null;
            unchecked { autoContextRevision++; }
            InvalidateAutoDisplayCache();
        }
        private void AutoSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            if (descriptor != null && descriptor.gameObject.scene == scene) { RememberAvatar(); SaveWindowDraft(); }
        }
        private void AutoSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            if (descriptor == null && !string.IsNullOrEmpty(avatarId) && GlobalObjectId.TryParse(avatarId, out var id)) {
                var recovered = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
                if (recovered != null && recovered.gameObject.scene == scene) BindAvatar(recovered);
            }
            AutoExternalContextChanged();
        }
        private void AutoSceneClosed(UnityEngine.SceneManagement.Scene scene) => AutoExternalContextChanged();
        private void AutoActiveSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene) => AutoExternalContextChanged();
        private void AutoObjectChanges(ref ObjectChangeEventStream changes)
        {
            // Undoable Inspector/property changes do not necessarily change hierarchy.
            // ObjectChangeEvents covers undoable changes only; unknown asset events are
            // conservatively invalidated until dependency-specific tracking is proven.
            for (int i = 0; i < changes.length; i++)
            {
                var kind = changes.GetEventType(i);
                if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var data);
                    var obj = EditorUtility.InstanceIDToObject(data.instanceId);
                    var transform = obj is GameObject go ? go.transform : (obj as Component)?.transform;
                    if (AffectsAvatar(transform))
                    { AutoExternalContextChanged(); return; }
                }
                else if (kind == ObjectChangeKind.ChangeAssetObjectProperties) {
                    changes.GetChangeAssetObjectPropertiesEvent(i, out var data);
                    var obj = EditorUtility.InstanceIDToObject(data.instanceId);
                    var path = obj == null ? null : AssetDatabase.GetAssetPath(obj);
                    if (assetDependencies == null || (path != null && assetDependencies.Contains(path))) { AutoExternalContextChanged(); return; }
                }
                else if (kind == ObjectChangeKind.UpdatePrefabInstances) {
                    changes.GetUpdatePrefabInstancesEvent(i, out var data);
                    foreach (var id in data.instanceIds) {
                        var obj = EditorUtility.InstanceIDToObject(id);
                        var transform = obj is GameObject go ? go.transform : (obj as Component)?.transform;
                        if (obj == null || AffectsAvatar(transform)) { AutoExternalContextChanged(); return; }
                    }
                }
            }
        }
        private bool AffectsAvatar(Transform value) => descriptor != null && value != null &&
            (value == descriptor.transform || value.IsChildOf(descriptor.transform) || descriptor.transform.IsChildOf(value));
        private bool TryReadSps3ControlsFresh(out Sps2SetupContext root)
        {
            root = null;
            if (settings == null || settings.family != SpsFamily.VrcFury || !FuryMinimalSps3Probe.AvailableForPreview(out _)) return false;
            try {
                root = FullSetupGenerator.Find(descriptor);
                return root == null || root.AuthoringContract == SpsAuthoringContract.Sps3Beta14;
            } catch { return false; }
        }
        private AutoDisplaySnapshot GetAutoDisplaySnapshot()
        {
            if (autoDisplayCache == null) autoDisplayCache = new AutoDisplayCache<AutoDisplaySnapshot>();
            return autoDisplayCache.Get(EditorApplication.timeSinceStartup,
                () => {
                    bool visible = TryReadSps3ControlsFresh(out var root);
                    int count = settings?.parts?.Count(p => p != null && p.included && settings.Sps3IncludeInAuto(p)) ?? 0;
                    return new AutoDisplaySnapshot {
                        ControlsVisible = visible,
                        Capacity = visible && settings?.parts != null
                            ? AutoCapacityEstimate.InspectKnownRoot(descriptor, settings, root)
                            : new AutoCapacityEstimate(count, null, null)
                    };
                },
                () => {
                    var avatar = descriptor; var stamp = new AutoDraftStamp(settings);
                    int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
                    return () => this != null && ReferenceEquals(descriptor, avatar) &&
                        (ReferenceEquals(avatar, null) || avatar != null) &&
                        revision == shapeSelectionRevision && contextRevision == autoContextRevision && stamp.Matches(settings);
                }, Event.current == null || Event.current.type != EventType.Repaint);
        }
        // Layout/Repaint cannot commit edits. Input events deliberately retain fresh
        // guards, including animation drawing branches that can edit the draft.
        internal static bool AutoDisplayUsesMemo(EventType type) => type == EventType.Layout || type == EventType.Repaint;
        // Pointer movement/release can be caused by moving the window and should not
        // itself trigger a full analysis. Context and draft changes still invalidate
        // the display snapshot; generation validates again before any mutation.
        internal static bool PreviewUsesMemo(EventType type) => true;
        private GenerationPreview ReadGenerationPreview(EventType? eventType)
        {
            if (generationPreviewPass == null) return ReadGenerationPreviewCached(eventType);
            return generationPreviewPass.Get(() => ReadGenerationPreviewCached(eventType), () => {
                var avatar = descriptor; var draft = settings;
                int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
                return () => this != null && ReferenceEquals(descriptor, avatar) &&
                    (ReferenceEquals(avatar, null) || avatar != null) && ReferenceEquals(settings, draft) &&
                    revision == shapeSelectionRevision && contextRevision == autoContextRevision;
            });
        }
        private GenerationPreview ReadGenerationPreviewCached(EventType? eventType)
        {
            GenerationPreview Capture()
            {
                var outputSettings=DraftForOutput();
                var route = FuryNormalOutputRouter.ResolvePreview(descriptor, outputSettings);
                string warning = null;
                var blocker = (route.Mode == FuryFeatureOutputPlan.Mode.PlacementOnly || route.Mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3) ? null : SpsAuthoringCompatibility.SelectionBlocker(descriptor, outputSettings, out warning, route.Validated);
                return new GenerationPreview { Blocker = blocker, Warning = warning, Route = route, Readiness = InspectSps3Selection(descriptor,outputSettings) };
            }
            if (!eventType.HasValue || !PreviewUsesMemo(eventType.Value)) return Capture();
            if (generationPreviewCache == null) generationPreviewCache = new AutoDisplayCache<GenerationPreview>();
            return generationPreviewCache.Get(EditorApplication.timeSinceStartup, Capture, () => {
                var avatar = descriptor; var draft = settings;
                int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
                return () => this != null && ReferenceEquals(descriptor, avatar) &&
                    (ReferenceEquals(avatar, null) || avatar != null) && ReferenceEquals(settings, draft) &&
                    revision == shapeSelectionRevision && contextRevision == autoContextRevision;
            }, eventType.Value == EventType.Layout);
        }
        private bool Sps3ControlsForDrawing => Event.current != null && PreviewUsesMemo(Event.current.type)
            ? GetAutoDisplaySnapshot().ControlsVisible : Sps3ControlsVisible;

        private AutoParticipantSelection OpenAutoParticipantSelection()
        {
            if (!TryReadSps3ControlsFresh(out var root)) return null;
            var draft = settings; var avatar = descriptor;
            var owner = root?.asset; var rootObject = root?.gameObject;
            string identity = root?.identity, stateJson = owner != null ? owner.stateJson : null;
            int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
            // Native JSON is inspected only at popup open and Apply, never in idle polling.
            var native = AutoNativeSnapshot.Capture(avatar != null ? SpsBackend.AllSockets(avatar.gameObject) : Array.Empty<Component>());
            return new AutoParticipantSelection(draft,
                p => Model.SocketDisplayNames.Resolve(p, draft.menuLanguage),
                () => this != null && autoDisplayHooksEnabled && ReferenceEquals(descriptor, avatar) &&
                    (ReferenceEquals(avatar, null) || avatar != null) && ReferenceEquals(settings, draft) &&
                    revision == shapeSelectionRevision && contextRevision == autoContextRevision &&
                    (ReferenceEquals(owner, null) || owner != null) && (ReferenceEquals(rootObject, null) || rootObject != null),
                apply => Change(apply),
                () => {
                    if (!TryReadSps3ControlsFresh(out var now)) return false;
                    return now?.asset == owner && now?.gameObject == rootObject && now?.identity == identity &&
                        (owner != null ? owner.stateJson : null) == stateJson &&
                        native.Matches(avatar != null ? SpsBackend.AllSockets(avatar.gameObject) : Array.Empty<Component>());
                });
        }
    }

    internal sealed class AutoNativeSnapshot
    {
        private readonly Component[] sockets;
        private readonly string[] json;
        private AutoNativeSnapshot(Component[] sockets)
        { this.sockets = sockets.ToArray(); json = sockets.Select(EditorJsonUtility.ToJson).ToArray(); }
        internal static AutoNativeSnapshot Capture(Component[] sockets) => new AutoNativeSnapshot(sockets);
        internal bool Matches(Component[] current)
        {
            if (current == null || current.Length != sockets.Length) return false;
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i] == null || current[i] != sockets[i] || EditorJsonUtility.ToJson(current[i]) != json[i]) return false;
            return true;
        }
    }
}
