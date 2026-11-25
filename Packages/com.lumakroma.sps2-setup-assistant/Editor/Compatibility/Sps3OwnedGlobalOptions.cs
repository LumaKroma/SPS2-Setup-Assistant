using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Exact beta14 authoring boundary. Only directly linked Setup-created avatar-root Options are editable.
    internal static class Sps3OwnedGlobalOptions
    {
        private const string ComponentType = "VF.Model.VRCFury";
        private const string ModelType = "VF.Model.Feature.SpsOptions";
        private static readonly string[] Controlled = { "legacySocketCompatibility", "legacyModeAvailable", "legacyModeEnabledOnAvatarLoad" };
        private static readonly string[] PolicyFields = { "autoModeAvailable", "autoModeDefaultOn", "autoModeSaved", "saveSockets",
            "legacyModeAvailable", "legacyModeEnabledOnAvatarLoad", "legacyModeSaved",
            "legacySocketCompatibility", "tpsDepthAnimationCompatibility", "addDpsTipLight" };
        private static readonly Lazy<bool> MenuPathAvailable = new Lazy<bool>(() => VrcFuryApi.FindType(ModelType)?.GetField("menuPath")?.FieldType == typeof(string));
        internal static bool SupportsMenuPath => MenuPathAvailable.Value;
        private static int RequestedPolicy(SetupSettings s) => (s.sps3AutoAvailable ? 1 : 0) | (s.sps3AutoDefaultOn ? 2 : 0) |
            (s.sps3AutoSaved ? 4 : 0) | (s.sps3SaveSockets ? 8 : 0) | (s.sps3PolicyVersion == 2 ? s.LegacyTreeMask() :
            (s.sps3LegacyAvailable ? 16 : 0) | (s.sps3LegacyDefaultOn ? 32 : 0) | (s.sps3LegacySaved ? 64 : 0));
        private static int NativePolicy(Component component, int version = 1)
        {
            var data = new SerializedObject(component); int value = 0;
            for (int i = 0; i < (version == 2 ? PolicyFields.Length : 7); i++)
                if (VrcFuryCompatibility.Require(data, "content." + PolicyFields[i], SerializedPropertyType.Boolean).boolValue) value |= 1 << i;
            return value;
        }
        private static int HistoricalPolicy(bool legacy) => 1 | (legacy ? 48 : 0);
        private static void RequirePolicy(Component component, int expected, int version = 1)
        {
            if (NativePolicy(component, version) != expected)
                throw new InvalidOperationException(L("Fury側のAuto・保存・Legacy設定を確認してください。Setupの記録と異なるため、既存値を変更せず停止しました。"));
        }
        internal static void ValidatePolicyRequest(VRCAvatarDescriptor avatar, Sps2SetupContext existing, SetupSettings settings)
        {
            if (settings.sps3PolicyVersion < 0 || settings.sps3PolicyVersion > 2)
                throw new InvalidOperationException("Unknown SPS3 settings policy version.");
            var options = AllOptions(avatar);
            if (options.Length == 0) return;
            if (options.Length != 1) throw new InvalidOperationException("Ambiguous SPS Options.");
            var marker = existing == null ? null : Marker(existing);
            if (settings.sps3PolicyVersion == 2 && (marker?.globalPolicyVersion ?? 0) < 2 && marker != null)
                RequireLegacyAnimationAdoption(options[0], settings);
            if (marker != null && marker.globalPolicyVersion >= 1) {
                RequirePolicy(options[0], marker.globalPolicyBaseline, marker.globalPolicyVersion);
                if (settings.sps3PolicyVersion < marker.globalPolicyVersion) throw new InvalidOperationException("Explicit SPS3 global policy cannot be downgraded implicitly.");
            }
            else if (settings.sps3PolicyVersion >= 1) {
                // Existing schema-1 ownership did not own Auto/save fields. Acquire only an exact historical baseline.
                if (marker != null || existing != null && options[0].gameObject == existing.gameObject)
                    RequirePolicy(options[0], HistoricalPolicy(existing.settings.legacy));
                else RequirePolicy(options[0], RequestedPolicy(settings), settings.sps3PolicyVersion); // Borrowed: read only, never adopted.
            }
        }
        private static int ReadLegacyAnimationMask(Component component)
        {
            ValidateEditableOptions(component);
            var data = new SerializedObject(component);
            var paths = new System.Collections.Generic.List<string>();
            if (IsModel(data.FindProperty("content"))) paths.Add("content");
            var features = data.FindProperty("config.features");
            if (features != null && features.isArray)
                for (int i = 0; i < features.arraySize; i++)
                    if (IsModel(features.GetArrayElementAtIndex(i))) paths.Add("config.features.Array.data[" + i + "]");
            if (paths.Count != 1) throw new InvalidOperationException("SPS Options is ambiguous.");
            var prefix = paths[0] + ".";
            return (VrcFuryCompatibility.Require(data, prefix + "tpsDepthAnimationCompatibility", SerializedPropertyType.Boolean).boolValue ? 1 : 0) |
                (VrcFuryCompatibility.Require(data, prefix + "addDpsTipLight", SerializedPropertyType.Boolean).boolValue ? 2 : 0);
        }
        // Read-only observation. Ownership/adoption is still checked at generation, never granted by this read.
        internal static int ObserveLegacyAnimationMask(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return 0;
            var options=AllOptions(avatar);
            if (options.Length==0) return 0;
            if (options.Length!=1) throw new InvalidOperationException("Ambiguous SPS Options.");
            return ReadLegacyAnimationMask(options[0]);
        }
        private static void RequireLegacyAnimationAdoption(Component component, SetupSettings requested)
        {
            if (!requested.LegacyAdoptionMatches(ReadLegacyAnimationMask(component)))
                throw new InvalidOperationException(L("Legacy設定が編集開始後に変更されています。設定を読み直して再編集してください。既存値は変更していません。"));
        }
        internal static void RequirePinnedSchema()
        {
            var type = VrcFuryApi.FindType(ComponentType);
            var package = type == null ? null : UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            var model = VrcFuryApi.FindType(ModelType);
            if (package == null || package.name != Sps3CandidatePreflight.PackageId || model?.Assembly != type.Assembly)
                throw new InvalidOperationException("SPS3 global-options type or package identity is unavailable.");
            // Specific options are checked at the point of read/write; an absent
            // unrelated option must not reject a basic Socket.
        }
        private static bool IsModel(SerializedProperty p) => p != null && p.propertyType == SerializedPropertyType.ManagedReference &&
            p.managedReferenceValue?.GetType().FullName == ModelType;
        private static bool HasOptions(Component component)
        {
            if (component == null || component.GetType().FullName != ComponentType) return false;
            var data = new SerializedObject(component);
            if (IsModel(data.FindProperty("content"))) return true;
            var old = data.FindProperty("config.features");
            if (old != null && old.isArray)
                for (int i = 0; i < old.arraySize; i++) if (IsModel(old.GetArrayElementAtIndex(i))) return true;
            return false;
        }
        internal static Component[] AllOptions(VRCAvatarDescriptor avatar) =>
            avatar.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetComponentInParent<VRCAvatarDescriptor>(true) == avatar && HasOptions(c)).ToArray();
        internal static bool PreserveGlobalOptionsPolicy(bool hasExisting, bool unmanaged, bool hasOptions) =>
            hasExisting ? unmanaged : hasOptions;
        internal static bool PreserveGlobalOptions(VRCAvatarDescriptor avatar, Sps2SetupContext existing)
        {
            if (avatar == null) return false;
            if (existing != null) {
                if (existing.asset == null) return false;
                // Borrowed or absent options were never owned by this setup. Keep them
                // unchanged even when the requested global policy differs.
                bool unmanaged = existing.asset.globalOptionsUnmanaged || Marker(existing) == null;
                return PreserveGlobalOptionsPolicy(true, unmanaged, false);
            }
            bool hasOptions = AllOptions(avatar).Length != 0;
            return PreserveGlobalOptionsPolicy(false, false, hasOptions);
        }
        // A synchronous read-only snapshot of each Options model slot. It is kept
        // only during one explicit UI operation; never persisted on a shared asset.
        internal sealed class ConflictSnapshot {
            internal VRCAvatarDescriptor avatar;
            internal Component[] components;
            internal string[] signatures;
            internal string[] paths;
            internal int slots;
            internal bool onlyOwned;
            internal bool expectingRemoval;
            internal Transform expectedRemovedRoot;
            internal string[] afterRemovalPaths;
            internal bool Inside(Transform root) => root != null && components.Any(c => c != null && (c.transform == root || c.transform.IsChildOf(root)));
            internal bool StillMatches() {
                if (avatar == null) return false;
                var current = CaptureConflict(avatar, null);
                var expectedPaths = expectingRemoval && expectedRemovedRoot == null ? afterRemovalPaths : paths;
                return slots == current.slots && components.SequenceEqual(current.components) && signatures.SequenceEqual(current.signatures) && expectedPaths != null && expectedPaths.SequenceEqual(current.paths);
            }
        }
        private static string SnapshotComponent(Component component) {
            var data = new SerializedObject(component);
            var references = new System.Text.StringBuilder();
            var iterator = data.GetIterator();
            while (iterator.Next(true)) {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    references.Append(iterator.propertyPath).Append('=').Append(iterator.objectReferenceInstanceIDValue).Append(';');
            }
            return EditorJsonUtility.ToJson(component) + "|" + references;
        }
        private static string ScenePath(Transform transform, Transform avatar, Transform removedRoot = null) {
            var nodes = new System.Collections.Generic.List<string>();
            for (var t=transform; t!=null; t=t.parent) {
                int index=t.GetSiblingIndex();
                if (removedRoot != null && t.parent == removedRoot.parent && index > removedRoot.GetSiblingIndex()) index--;
                nodes.Add(t.name + " [" + index + "]");
                if (t==avatar) break;
            }
            nodes.Reverse(); return string.Join("/", nodes);
        }
        internal static ConflictSnapshot CaptureConflict(VRCAvatarDescriptor avatar, Sps2SetupContext existing) {
            if (avatar == null) throw new InvalidOperationException("アバターが指定されていません。");
            var components = AllOptions(avatar);
            var paths = new System.Collections.Generic.List<string>();
            foreach (var component in components) {
                ValidateEditableOptions(component);
                var data = new SerializedObject(component);
                if (IsModel(data.FindProperty("content"))) paths.Add(ScenePath(component.transform, avatar.transform) + " / content");
                var features = data.FindProperty("config.features");
                if (features != null && features.isArray)
                    for (int i=0; i<features.arraySize; i++)
                        if (IsModel(features.GetArrayElementAtIndex(i))) paths.Add(ScenePath(component.transform, avatar.transform) + " / features[" + i + "]");
            }
            bool owned = false;
            if (paths.Count == 1 && existing != null) {
                try { var marker = Marker(existing); owned = marker != null && marker.options == components[0]; }
                catch (Exception) { owned = false; }
                if (!owned) owned = FindProvenOptionsHost(avatar, existing.asset, components) != null;
            }
            return new ConflictSnapshot { avatar=avatar, components=components, paths=paths.ToArray(), slots=paths.Count,
                signatures=components.Select(SnapshotComponent).ToArray(), onlyOwned=owned };
        }
        private static ConflictSnapshot activeKeep;
        private static bool activeReplace;
        private sealed class KeepScope : IDisposable {
            public void Dispose() { activeKeep = null; activeReplace = false; }
        }
        internal static IDisposable Keep(ConflictSnapshot snapshot, bool replace = false) {
            if (activeKeep != null || snapshot == null || !snapshot.StillMatches())
                throw new InvalidOperationException("SPS Options が変更されたため、処理を中止しました。");
            activeKeep=snapshot; activeReplace=replace; return new KeepScope();
        }
        internal static void PrepareKeepRemoval(VRCAvatarDescriptor avatar, Transform removedRoot) {
            if (activeKeep == null) return;
            ValidateKeep(avatar, removedRoot);
            if (removedRoot == null || activeKeep.expectingRemoval) throw new InvalidOperationException("Invalid SPS replacement sequence.");
            var expected = new System.Collections.Generic.List<string>();
            foreach (var component in activeKeep.components) {
                var data=new SerializedObject(component);
                if (IsModel(data.FindProperty("content"))) expected.Add(ScenePath(component.transform, avatar.transform, removedRoot) + " / content");
                var features=data.FindProperty("config.features");
                if (features != null && features.isArray)
                    for (int i=0; i<features.arraySize; i++)
                        if (IsModel(features.GetArrayElementAtIndex(i))) expected.Add(ScenePath(component.transform, avatar.transform, removedRoot) + " / features[" + i + "]");
            }
            activeKeep.afterRemovalPaths=expected.ToArray();
            activeKeep.expectedRemovedRoot=removedRoot;
            activeKeep.expectingRemoval=true;
        }
        internal static void ValidateKeep(VRCAvatarDescriptor avatar, Transform removedRoot = null) {
            if (activeKeep == null) return;
            if (activeKeep.avatar != avatar || !activeKeep.StillMatches())
                throw new InvalidOperationException("SPS Options が確認後に変更されたため、処理を中止しました。");
            if (activeKeep.Inside(removedRoot))
                throw new InvalidOperationException("対象の SPS Options が再生成するルート内にあります。元の場所での保持も安全な置換もできないため、再生成を開始しませんでした。");
        }
        // Preflight and commit operate only on the native SPS Options model.
        internal sealed class ReplacementPlan {
            internal bool enabled;
            internal bool keepExisting;
            internal bool keepDuplicates;
            internal bool replaceExisting;
            internal SetupSettings settings;
        }
        private static bool RequestedOptionsEnabled(SetupSettings s) {
            // A real Options model is needed to express both-off: Fury defaults
            // to enabled when no model exists. Preserve old defaults for policy 0.
            return true;
        }
        private static void WriteOptionAt(SerializedObject data, string path, SetupSettings settings) {
            var model = VrcFuryApi.FindType(ModelType);
            var slot = VrcFuryCompatibility.Require(data, path, SerializedPropertyType.ManagedReference);
            if (slot.managedReferenceValue?.GetType() != model) {
                slot.managedReferenceValue = Activator.CreateInstance(model, true);
                VrcFuryCompatibility.Require(data, path + ".version", SerializedPropertyType.Integer).intValue = 0;
            }
            var destination = data.FindProperty(path + ".menuPath");
            if (destination != null && destination.propertyType == SerializedPropertyType.String)
                destination.stringValue = settings.sps3MenuPath ?? "";
            foreach (var field in Controlled.Where(f => settings.sps3PolicyVersion != 2))
                VrcFuryCompatibility.Require(data, path + "." + field, SerializedPropertyType.Boolean).boolValue = settings.legacy;
            if (settings.sps3PolicyVersion >= 1)
                for (int i = 0; i < (settings.sps3PolicyVersion == 2 ? PolicyFields.Length : 7); i++)
                    VrcFuryCompatibility.Require(data, path + "." + PolicyFields[i], SerializedPropertyType.Boolean).boolValue = (RequestedPolicy(settings) & (1 << i)) != 0;
        }
        private static void WriteFreshOptions(Component component, SetupSettings settings, bool allowTransient = false) {
            var data = new SerializedObject(component);
            WriteOptionAt(data, "content", settings);
            data.ApplyModifiedPropertiesWithoutUndo();
            RequireContainer(component, allowTransient);
            RequireControlled(component, settings.legacy, settings.sps3PolicyVersion, allowTransient);
            if (settings.sps3PolicyVersion >= 1) RequirePolicy(component, RequestedPolicy(settings), settings.sps3PolicyVersion);
        }
        private static void ValidateEditableOptions(Component component) {
            if (component == null || component.GetType().FullName != ComponentType ||
                UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(component))
                throw new InvalidOperationException("SPS Options の保存データを安全に読み取れません。");
            var data = new SerializedObject(component);
            var content = data.FindProperty("content");
            if (content == null || content.propertyType != SerializedPropertyType.ManagedReference)
                throw new InvalidOperationException("SPS Options の保存形式に対応していません。");
            var features = data.FindProperty("config.features");
            if (features != null && !features.isArray)
                throw new InvalidOperationException("Fury の機能リストを変更できません。");
            if (!HasOptions(component)) throw new InvalidOperationException("SPS Options が見つかりません。");
        }
        internal static bool TryPrepareReplacement(VRCAvatarDescriptor avatar, SetupSettings settings, out ReplacementPlan plan, out string reason) {
            plan = null; reason = null; GameObject temporary = null;
            try {
                if (activeKeep != null) {
                    ValidateKeep(avatar);
                    if (!activeReplace) {
                        plan = new ReplacementPlan { keepExisting = true, keepDuplicates = activeKeep.slots > 1, settings = settings.Copy() };
                        return true;
                    }
                    // Exercise the same staged model capability validation as new output.
                    temporary = new GameObject("SPS Options validation") { hideFlags = HideFlags.HideAndDontSave };
                    var candidateType = VrcFuryApi.FindType(ComponentType);
                    if (candidateType == null || !typeof(Component).IsAssignableFrom(candidateType)) throw new InvalidOperationException("SPS Options type unavailable.");
                    WriteFreshOptions(temporary.AddComponent(candidateType), settings, allowTransient: true);
                    plan = new ReplacementPlan { replaceExisting = true, settings = settings.Copy() };
                    return true;
                }
                RequirePinnedSchema();
                foreach (var old in AllOptions(avatar)) {
                    ValidateEditableOptions(old);
                }
                var enabled = RequestedOptionsEnabled(settings);
                if (enabled) {
                    temporary = new GameObject("SPS Options validation") { hideFlags = HideFlags.HideAndDontSave };
                    var type = VrcFuryApi.FindType(ComponentType);
                    if (type == null || !typeof(Component).IsAssignableFrom(type)) throw new InvalidOperationException("SPS Options の型を利用できません");
                    var staged = temporary.AddComponent(type);
                    WriteFreshOptions(staged, settings, allowTransient: true);
                }
                plan = new ReplacementPlan { enabled = enabled, settings = settings.Copy() };
                return true;
            } catch (Exception e) { reason = e.Message; return false; }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }
        // Removing the old root would also remove this Options host. Never move,
        // duplicate, or silently destroy it while regenerating the Socket hierarchy.
        internal static void ValidateOptionsInsideRemovedRoot(VRCAvatarDescriptor avatar, Sps2SetupContext old) {
            if (old == null) return;
            if (AllOptions(avatar).Any(c => c.transform == old.transform || c.transform.IsChildOf(old.transform)))
                throw new InvalidOperationException("旧生成ルート内の SPS Options を同じオブジェクトに保持できないため、再生成を中止しました。既存のデータは変更していません。");
        }
        private const string SettingsHostName = "SPS Settings";

        internal static GameObject CreateNativeHost(Transform parent, string label) {
            // Duplicate labels are allowed in Unity. Keep existing siblings and the object
            // that an existing hierarchy path resolves to in their original order.
            var prior = parent.Find(label);
            var host = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(host, "Create " + label);
            Undo.SetTransformParent(host.transform, parent, "Create " + label);
            host.transform.SetAsLastSibling();
            if (prior != null && parent.Find(label) != prior)
                throw new InvalidOperationException("Cannot preserve the existing hierarchy path.");
            return host;
        }

        private static GameObject FindProvenOptionsHost(VRCAvatarDescriptor avatar, Sps2SetupAsset previous, Component[] selected) {
            if (previous == null || selected.Length != 1 || string.IsNullOrEmpty(previous.managedOptionsModelJson)) return null;
            var options = selected[0];
            if (options == null || options.transform.parent != avatar.transform) return null;
            try {
                var content = new SerializedObject(options).FindProperty("content");
                if (!IsModel(content) || JsonUtility.ToJson(content.managedReferenceValue) != previous.managedOptionsModelJson) return null;
                // A standalone manual Options model with the same settings still needs consent.
                return OwnedNativeMenuAuthoring.HasRecordedRulesBelow(avatar, options.transform, previous)
                    ? options.gameObject : null;
            } catch (Exception) {
                // Ambiguous evidence goes to the ordinary Options choice before any changes.
                return null;
            }
        }

        private static void RecordOptionsHost(Sps2SetupAsset asset, GameObject host, Component options) {
            if (asset == null || host == null || options == null || options.gameObject != host)
                throw new InvalidOperationException("Cannot record SPS Settings configuration.");
            var content = new SerializedObject(options).FindProperty("content");
            if (!IsModel(content)) throw new InvalidOperationException("Cannot verify SPS Settings Options.");
            Undo.RecordObject(asset, "Record SPS Settings configuration");
            asset.managedOptionsModelJson = JsonUtility.ToJson(content.managedReferenceValue);
            EditorUtility.SetDirty(asset);
        }
        internal static void CommitReplacement(VRCAvatarDescriptor avatar, Sps2SetupContext root, ReplacementPlan plan, Sps2SetupAsset previous, out GameObject settingsHost) {
            settingsHost = null;
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.keepExisting) {
                ValidateKeep(avatar);
                Undo.RecordObject(root.asset, "Retain SPS Options");
                root.asset.globalOptionsUnmanaged = true;
                root.asset.optionsOwnershipSchema = 0;
                EditorUtility.SetDirty(root.asset);
                return;
            }
            if (plan.replaceExisting) {
                ValidateKeep(avatar);
                var oldItems = activeKeep.components.ToArray();
                // New native model must exist and round-trip before any old slot is touched.
                var host = FindProvenOptionsHost(avatar, previous, oldItems);
                if (host == null) {
                    host = CreateNativeHost(avatar.transform, SettingsHostName);
                }
                settingsHost = host;
                var fresh = Undo.AddComponent(host, VrcFuryApi.FindType(ComponentType));
                WriteFreshOptions(fresh, plan.settings);
                foreach (var item in oldItems) {
                    ValidateEditableOptions(item);
                    Undo.RecordObject(item, "Replace SPS Options");
                    var data = new SerializedObject(item);
                    var content = data.FindProperty("content");
                    var features = data.FindProperty("config.features");
                    // Capture the ordered native managed-reference IDs of non-Options
                    // entries; deleting Options must not change their identity/order.
                    var retained = new System.Collections.Generic.List<long>();
                    if (features != null && features.isArray)
                        for (int i=0; i<features.arraySize; i++) {
                            var entry=features.GetArrayElementAtIndex(i);
                            if (!IsModel(entry)) retained.Add(entry.managedReferenceId);
                        }
                    if (IsModel(content)) content.managedReferenceValue = null;
                    if (features != null && features.isArray)
                        for (int i=features.arraySize-1; i>=0; i--)
                            if (IsModel(features.GetArrayElementAtIndex(i))) features.DeleteArrayElementAtIndex(i);
                    data.ApplyModifiedProperties(); data.Update();
                    features=data.FindProperty("config.features"); content=data.FindProperty("content");
                    var after = new System.Collections.Generic.List<long>();
                    if (features != null && features.isArray)
                        for (int i=0; i<features.arraySize; i++) after.Add(features.GetArrayElementAtIndex(i).managedReferenceId);
                    if (!retained.SequenceEqual(after)) throw new InvalidOperationException("Non-Options feature verification failed; rolling back.");
                    bool empty = content != null && content.managedReferenceValue == null && (features == null || features.arraySize == 0);
                    if (empty) Undo.DestroyObjectImmediate(item);
                    else PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
                var afterOptions=AllOptions(avatar);
                if (afterOptions.Length != 1 || afterOptions[0] != fresh) throw new InvalidOperationException("SPS Options replacement readback failed; rolling back.");
                RequireControlled(fresh, plan.settings.legacy, plan.settings.sps3PolicyVersion);
                if (plan.settings.sps3PolicyVersion >= 1) RequirePolicy(fresh, RequestedPolicy(plan.settings), plan.settings.sps3PolicyVersion);
                RecordOptionsHost(root.asset, host, fresh);
                Undo.RecordObject(root.asset, "SPS Options replacement");
                root.asset.globalOptionsUnmanaged=true; root.asset.optionsOwnershipSchema=0; EditorUtility.SetDirty(root.asset);
                return;
            }
            var old = AllOptions(avatar);
            foreach (var item in old) ValidateEditableOptions(item);
            var provenHost = FindProvenOptionsHost(avatar, previous, old);
            // Existing Options stay in their native host and slot, even if the host
            // is currently inactive. The author controls component activation.
            var destination = plan.enabled ? old.FirstOrDefault() : null;
            bool written = false;
            foreach (var item in old) {
                Undo.RecordObject(item, "SPS Options replacement");
                var data = new SerializedObject(item);
                var content = data.FindProperty("content");
                var features = data.FindProperty("config.features");
                if (IsModel(content)) {
                    if (item == destination && !written) { WriteOptionAt(data, "content", plan.settings); written = true; }
                    else content.managedReferenceValue = null;
                }
                // Delete from the end so every non-Options element retains relative order.
                if (features != null && features.isArray) {
                    int keep = -1;
                    if (item == destination && !written)
                        for (int i = 0; i < features.arraySize; i++)
                            if (IsModel(features.GetArrayElementAtIndex(i))) { keep = i; break; }
                    for (int i = features.arraySize - 1; i >= 0; i--) {
                        var entry = features.GetArrayElementAtIndex(i);
                        if (!IsModel(entry)) continue;
                        if (i == keep) { WriteOptionAt(data, "config.features.Array.data[" + i + "]", plan.settings); written = true; }
                        else features.DeleteArrayElementAtIndex(i);
                    }
                }
                data.ApplyModifiedProperties();
                data.Update();
                content = data.FindProperty("content"); features = data.FindProperty("config.features");
                bool empty = content.managedReferenceValue == null && (features == null || features.arraySize == 0);
                if (empty) Undo.DestroyObjectImmediate(item);
                else PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            if (plan.enabled && !written) {
                settingsHost = CreateNativeHost(avatar.transform, SettingsHostName);
                var fresh = Undo.AddComponent(settingsHost, VrcFuryApi.FindType(ComponentType));
                WriteFreshOptions(fresh, plan.settings);
                RecordOptionsHost(root.asset, settingsHost, fresh);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fresh);
            }
            if (written && destination != null && provenHost != null && destination.gameObject == provenHost) {
                settingsHost = provenHost;
                RecordOptionsHost(root.asset, provenHost, destination);
            }
            var actual = AllOptions(avatar);
            int count = 0;
            foreach (var item in actual) {
                var data = new SerializedObject(item);
                if (IsModel(data.FindProperty("content"))) count++;
                var entries = data.FindProperty("config.features");
                if (entries != null && entries.isArray)
                    for (int i = 0; i < entries.arraySize; i++) if (IsModel(entries.GetArrayElementAtIndex(i))) count++;
            }
            if (count != (plan.enabled ? 1 : 0))
                throw new InvalidOperationException("SPS Options replacement verification failed; reverting generation.");
            Undo.RecordObject(root.asset, "SPS Options replacement");
            root.asset.globalOptionsUnmanaged = true;
            root.asset.optionsOwnershipSchema = 0;
            EditorUtility.SetDirty(root.asset);
        }
        internal static void ValidateSocketOwnership(VRCAvatarDescriptor avatar, Sps2SetupContext existing)
        {
            foreach (var socket in SpsBackend.AllSockets(avatar.gameObject))
                if (existing == null || !existing.sockets.Any(s => s.socket == socket))
                    throw new InvalidOperationException("Foreign native sockets must be preserved.");
        }
        private static void RequireContainer(Component component, bool allowTransient = false)
        {
            if (component == null || component.GetType().FullName != ComponentType ||
                !(component is Behaviour behaviour) || !behaviour.enabled || (!allowTransient && (component.hideFlags & HideFlags.DontSave) != 0))
                throw new InvalidOperationException("An enabled native SPS Options component is required.");
            var data = new SerializedObject(component);
            var old = data.FindProperty("config.features");
            if (!IsModel(data.FindProperty("content")) || old == null || !old.isArray || old.arraySize != 0 ||
                VrcFuryCompatibility.Require(data, "content.version", SerializedPropertyType.Integer).intValue != 0 ||
                VrcFuryCompatibility.Require(data, "somethingIsBroken", SerializedPropertyType.Boolean).boolValue)
                throw new InvalidOperationException("Mixed, legacy or invalid SPS Options containers are not changed or adopted.");
        }
        private static void RequireControlled(Component component, bool legacy, int policyVersion = 0, bool allowTransient = false)
        {
            RequireContainer(component, allowTransient);
            var data = new SerializedObject(component);
            foreach (var field in Controlled.Where(f => policyVersion == 0 || policyVersion == 1 && f == "legacySocketCompatibility"))
                if (VrcFuryCompatibility.Require(data, "content." + field, SerializedPropertyType.Boolean).boolValue != legacy)
                    throw new InvalidOperationException("SPS Options conflict with the requested Legacy setting: " + field +
                        ". Existing native settings are preserved.");
        }
        internal sealed class OwnershipView
        {
            internal Sps3OptionsOwnership legacy;
            internal Component options;
            internal int globalPolicyVersion, globalPolicyBaseline, menuLayoutVersion;
            internal string menuSettingsLabel;
        }
        internal static OwnershipView Marker(Sps2SetupContext root)
        {
            var markers = root.avatar.GetComponentsInChildren<Sps3OptionsOwnership>(true);
            if (markers.Length == 0) {
                var asset = root.asset;
                if (asset == null || asset.optionsOwnershipSchema == 0) return null;
                if (asset.optionsOwnershipSchema != 1 || asset.optionsPolicyVersion < 0 || asset.optionsPolicyVersion > 2 ||
                    asset.optionsMenuLayoutVersion < 0 || asset.optionsMenuLayoutVersion > 1 ||
                    !Guid.TryParseExact(root.identity, "N", out _) || Sps2SetupStorage.Identity(asset) != root.identity)
                    throw new InvalidOperationException("Invalid saved SPS Options ownership evidence.");
                var candidates = AllOptions(root.avatar);
                if (candidates.Length != 1 || candidates[0].gameObject != root.avatar.gameObject)
                    throw new InvalidOperationException("Owned SPS Options must remain unique on the avatar root.");
                RequireContainer(candidates[0]);
                return new OwnershipView { options = candidates[0], globalPolicyVersion = asset.optionsPolicyVersion,
                    globalPolicyBaseline = asset.optionsPolicyBaseline, menuLayoutVersion = asset.optionsMenuLayoutVersion,
                    menuSettingsLabel = asset.optionsMenuSettingsLabel };
            }
            if (markers.Length != 1 || markers[0].gameObject != root.gameObject || !markers[0].enabled ||
                (markers[0].hideFlags & HideFlags.DontSave) != 0 ||
                markers[0].schema != 1 || markers[0].globalPolicyVersion < 0 || markers[0].globalPolicyVersion > 2 || !Guid.TryParseExact(root.identity, "N", out _) ||
                markers[0].setupIdentity != root.identity || markers[0].options == null ||
                markers[0].options.gameObject != root.avatar.gameObject)
                throw new InvalidOperationException(L("Setupの生成物とSPS Optionsの参照を確認してください。所有情報を確認できないため、既存データを変更せず停止しました。"));
            var options = AllOptions(root.avatar);
            if (options.Length != 1 || options[0] != markers[0].options)
                throw new InvalidOperationException("The owned SPS Options reference no longer identifies the unique avatar-root Options.");
            RequireContainer(options[0]);
            var m = markers[0];
            return new OwnershipView { legacy = m, options = m.options, globalPolicyVersion = m.globalPolicyVersion,
                globalPolicyBaseline = m.globalPolicyBaseline, menuLayoutVersion = m.menuLayoutVersion,
                menuSettingsLabel = m.menuSettingsLabel };
        }
        internal static void ValidateOwnership(VRCAvatarDescriptor avatar, Sps2SetupContext existing,
            bool migratingLegacy = false, bool? requestedLegacy = null, SetupSettings requestedSettings = null)
        {
            ValidateSocketOwnership(avatar, existing);
            if (migratingLegacy && (existing == null || existing.AuthoringContract != SpsAuthoringContract.LegacySps2))
                throw new InvalidOperationException("Legacy migration requires an existing legacy setup.");
            if (!migratingLegacy && existing != null && existing.AuthoringContract != SpsAuthoringContract.Sps3Beta14)
                throw new InvalidOperationException("An explicit SPS3 setup is required.");
            var options = AllOptions(avatar);
            var markers = avatar.GetComponentsInChildren<Sps3OptionsOwnership>(true);
            if (options.Length > 1) throw new InvalidOperationException("Duplicate SPS Options are preserved; resolve them before authoring.");
            if (options.Length == 0)
            {
                if (markers.Length != 0 || existing != null && !migratingLegacy)
                    throw new InvalidOperationException("Previously authored SPS Options or their ownership link are missing.");
                return;
            }
            var component = options[0];
            if (component.gameObject == avatar.gameObject)
            {
                if (markers.Length != 0 || existing != null && Marker(existing) != null)
                {
                    if (existing == null || migratingLegacy) throw new InvalidOperationException("Foreign SPS Options ownership markers are preserved.");
                    var marker = Marker(existing);
                    RequireControlled(marker.options, existing.settings.legacy, marker.globalPolicyVersion);
                    if (marker.globalPolicyVersion >= 1) RequirePolicy(marker.options, marker.globalPolicyBaseline, marker.globalPolicyVersion);
                }
                else
                {
                    // Deliberately borrowed, never claimed: all uncontrolled native customization remains authoritative.
                    RequireControlled(component, requestedLegacy ?? existing?.settings.legacy ?? false, (requestedSettings ?? existing?.settings)?.sps3PolicyVersion ?? 0);
                }
                return;
            }
            if (existing == null || migratingLegacy || markers.Length != 0 || component.gameObject != existing.gameObject)
                throw new InvalidOperationException("Preexisting non-root SPS Options are preserved; only verified old Setup output can be relocated.");
            Verify(component, existing.settings.legacy);
            SpsMigrationAdapter.ValidateReplacementReferences(avatar, new System.Collections.Generic.HashSet<Component> { component });
        }

        // Issued only after existing ownership was verified, before root removal or settings identity rotation.
        internal sealed class OwnershipTransfer
        {
            internal VRCAvatarDescriptor avatar;
            internal Component component;
            internal Sps3OptionsOwnership marker;
            internal string state;
            internal int policyVersion, policyBaseline, menuLayoutVersion;
            internal string menuSettingsLabel;
        }
        internal static OwnershipTransfer PrepareForAuthoring(Sps2SetupContext existing, bool legacy, bool regenerate)
        {
            if (existing == null || existing.AuthoringContract != SpsAuthoringContract.Sps3Beta14) return null;
            RequirePinnedSchema();
            if (EditorUtility.IsPersistent(existing.avatar) || EditorUtility.IsPersistent(existing.gameObject) ||
                !existing.avatar.gameObject.scene.IsValid() || !existing.avatar.gameObject.scene.isLoaded ||
                existing.transform.parent != existing.avatar.transform ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(existing.avatar.gameObject) != null)
                throw new InvalidOperationException("SPS Options relocation requires a loaded scene avatar, never a saved prefab or Prefab Mode.");
            ValidateOwnership(existing.avatar, existing, requestedLegacy: legacy);
            var marker = Marker(existing);
            var component = AllOptions(existing.avatar).Single();
            if (marker == null && component.gameObject == existing.avatar.gameObject) return null;
            if (marker == null)
            {
                // Old generated-child ownership: preserve the entire verified native payload, not a reconstructed default.
                var copy = Undo.AddComponent(existing.avatar.gameObject, VrcFuryApi.FindType(ComponentType));
                Undo.RecordObject(copy, "Relocate SPS Options");
                EditorUtility.CopySerializedManagedFieldsOnly(component, copy);
                ((Behaviour)copy).enabled = ((Behaviour)component).enabled;
                copy.hideFlags = component.hideFlags;
                SpsMigrationAdapter.ValidateEquivalent(component, copy);
                PrefabUtility.RecordPrefabInstancePropertyModifications(copy);
                if (!regenerate) Undo.DestroyObjectImmediate(component);
                component = copy;
            }
            return new OwnershipTransfer { avatar = existing.avatar, component = component, marker = marker?.legacy,
                menuLayoutVersion = marker == null ? 0 : marker.menuLayoutVersion, menuSettingsLabel = marker == null ? null : marker.menuSettingsLabel,
                state = SpsMigrationAdapter.State(component), policyVersion = marker == null ? 0 : marker.globalPolicyVersion, policyBaseline = marker != null && marker.globalPolicyVersion >= 1 ? marker.globalPolicyBaseline : HistoricalPolicy(existing.settings.legacy) };
        }
        internal static void Apply(Sps2SetupContext root, bool legacy, OwnershipTransfer transfer = null, SetupSettings requested = null, bool adoptMenuLayout = false, Action<string> menuWarning = null)
        {
            RequirePinnedSchema();
            void WarnMenu(string message) { if(menuWarning!=null)menuWarning(message); else Debug.LogWarning(message,root.gameObject); }
            requested = requested ?? root.settings;
            if (requested.sps3PolicyVersion < 0 || requested.sps3PolicyVersion > 2) throw new InvalidOperationException("Unknown SPS3 policy.");
            if (root.AuthoringContract != SpsAuthoringContract.Sps3Beta14 || root.gameObject == null ||
                root.transform.parent != root.avatar.transform || !Guid.TryParseExact(root.identity, "N", out _) ||
                EditorUtility.IsPersistent(root.avatar) || !root.avatar.gameObject.scene.IsValid() ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(root.avatar.gameObject) != null)
                throw new InvalidOperationException("Global options require the explicit identified SPS3 setup root.");
            var matches = AllOptions(root.avatar);
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate SPS Options appeared during authoring.");
            var markers = root.avatar.GetComponentsInChildren<Sps3OptionsOwnership>(true);
            Component component;
            bool owned;
            bool created = false;
            int policyVersion = transfer?.policyVersion ?? 0;
            if (transfer != null)
            {
                if (transfer.avatar != root.avatar || transfer.component == null || matches.Length != 1 || matches[0] != transfer.component ||
                    transfer.component.gameObject != root.avatar.gameObject || SpsMigrationAdapter.State(transfer.component) != transfer.state ||
                    markers.Any(m => m != transfer.marker || m.gameObject != root.gameObject))
                    throw new InvalidOperationException("SPS Options ownership changed during authoring; rolling back.");
                component = transfer.component; owned = true;
            }
            else if (matches.Length == 0)
            {
                if (markers.Length != 0) throw new InvalidOperationException("Missing linked SPS Options are never replaced by inference.");
                component = Undo.AddComponent(root.avatar.gameObject, VrcFuryApi.FindType(ComponentType));
                var data = new SerializedObject(component);
                VrcFuryCompatibility.Require(data, "content", SerializedPropertyType.ManagedReference).managedReferenceValue =
                    Activator.CreateInstance(VrcFuryApi.FindType(ModelType), true);
                VrcFuryCompatibility.Require(data, "content.version", SerializedPropertyType.Integer).intValue = 0;
                foreach (var field in Controlled) VrcFuryCompatibility.Require(data, "content." + field, SerializedPropertyType.Boolean).boolValue = legacy;
                data.ApplyModifiedProperties();
                component = data.targetObject as Component; owned = true; created = true;
            }
            else
            {
                component = matches[0];
                if (component.gameObject != root.avatar.gameObject) throw new InvalidOperationException("Non-root SPS Options were not safely relocated.");
                var marker = Marker(root);
                owned = marker != null;
                policyVersion = marker?.globalPolicyVersion ?? 0;
                RequireControlled(component, owned ? root.settings.legacy : legacy, owned ? policyVersion : requested.sps3PolicyVersion);
                if (policyVersion >= 1) RequirePolicy(component, marker.globalPolicyBaseline, marker.globalPolicyVersion);
            }
            RequireContainer(component);
            if (!owned) {
                if (requested.sps3PolicyVersion >= 1) RequirePolicy(component, RequestedPolicy(requested), requested.sps3PolicyVersion);
                if (adoptMenuLayout) WarnMenu("Avatar-root SPS Options remain unchanged. Menu arrangement is attempted on the build copy when its safety checks pass.");
                return; // Foreign root Options and every native field remain untouched.
            }
            if (policyVersion > requested.sps3PolicyVersion) throw new InvalidOperationException("SPS3 policy downgrade is not supported.");
            if (requested.sps3PolicyVersion >= 1 && !created && policyVersion == 0)
                RequirePolicy(component, transfer?.policyBaseline ?? HistoricalPolicy(root.settings.legacy));
            if (requested.sps3PolicyVersion == 2 && policyVersion < 2 && !created) {
                RequireLegacyAnimationAdoption(component, requested);
            }
            var before = Sps3Beta14SocketAdapter.CaptureCompatibilityState(component);
            Undo.RecordObject(component, "SPS3 global options");
            var target = new SerializedObject(component);
            foreach (var field in Controlled.Where(f => requested.sps3PolicyVersion != 2)) VrcFuryCompatibility.Require(target, "content." + field, SerializedPropertyType.Boolean).boolValue = legacy;
            if (requested.sps3PolicyVersion >= 1)
                for (int i = 0; i < (requested.sps3PolicyVersion == 2 ? PolicyFields.Length : 7); i++)
                    VrcFuryCompatibility.Require(target, "content." + PolicyFields[i], SerializedPropertyType.Boolean).boolValue = (RequestedPolicy(requested) & (1 << i)) != 0;
            target.ApplyModifiedProperties();
            before.RequireOnlyChanges(Sps3Beta14SocketAdapter.CaptureCompatibilityState(component), (requested.sps3PolicyVersion >= 1 ? Controlled.Concat(PolicyFields.Take(requested.sps3PolicyVersion == 2 ? PolicyFields.Length : 7)).Distinct() : Controlled).Select(f => "content." + f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            var asset = root.asset;
            if (asset == null || Sps2SetupStorage.Identity(asset) != root.identity)
                throw new InvalidOperationException("A unique settings asset is required for SPS Options ownership.");
            Undo.RecordObject(asset, "SPS3 Options ownership");
            asset.optionsOwnershipSchema = 1;
            if (transfer != null) { asset.optionsMenuLayoutVersion = transfer.menuLayoutVersion; asset.optionsMenuSettingsLabel = transfer.menuSettingsLabel; }
            if (adoptMenuLayout && asset.optionsMenuLayoutVersion == 0) {
                var label=Sps3MenuLayoutBuild.SettingsLabel(requested.menuLanguage);
                var warning=Sps3MenuLayoutBuild.AdoptionWarning(root,component,label);
                if (warning==null) {
                    asset.optionsMenuLayoutVersion = 1;
                    asset.optionsMenuSettingsLabel = label;
                } else WarnMenu(warning);
            }
            else if (adoptMenuLayout && asset.optionsMenuLayoutVersion == 1) {
                asset.optionsMenuSettingsLabel = Sps3MenuLayoutBuild.SettingsLabel(requested.menuLanguage);
                var warning=Sps3MenuLayoutBuild.AdoptionWarning(root,component,asset.optionsMenuSettingsLabel);
                if(warning!=null)WarnMenu(warning);
            }
            asset.optionsPolicyVersion = requested.sps3PolicyVersion;
            asset.optionsPolicyBaseline = requested.sps3PolicyVersion >= 1 ? RequestedPolicy(requested) : 0;
            EditorUtility.SetDirty(asset);
            // The old marker has already been verified by PrepareForAuthoring. Removing
            // only that marker is undoable and occurs after the new evidence is recorded.
            if (transfer?.marker != null && transfer.marker.gameObject == root.gameObject)
                Undo.DestroyObjectImmediate(transfer.marker);
            RequireControlled(component, legacy, requested.sps3PolicyVersion);
            if (requested.sps3PolicyVersion >= 1) RequirePolicy(component, RequestedPolicy(requested), requested.sps3PolicyVersion);
            Marker(root);
        }
        internal static void VerifyGeneratedRoot(Sps2SetupContext root, Component[] extra)
        {
            if (extra.Length == 1 && HasOptions(extra[0])) { Verify(extra[0], root.settings.legacy); return; }
            if (extra.Length > 1 || extra.Length == 1 && !(extra[0] is Sps3OptionsOwnership))
                throw new InvalidOperationException("The generated root has unowned components.");
            if (extra.Length == 0)
            {
                // Borrowed Options are compared against the current request by the outer preflight,
                // not against stale saved settings when the user changed native settings directly.
                var options = AllOptions(root.avatar);
                if (root.asset != null && root.asset.globalOptionsUnmanaged && options.Length == 0) return;
                if (root.avatar.GetComponentsInChildren<Sps3OptionsOwnership>(true).Length != 0 ||
                    options.Length != 1 || options[0].gameObject != root.avatar.gameObject)
                    throw new InvalidOperationException("The markerless setup has no unique borrowed avatar-root Options.");
                RequireContainer(options[0]);
                return;
            }
            ValidateOwnership(root.avatar, root);
            if (extra.Length == 1 && Marker(root)?.legacy != extra[0])
                throw new InvalidOperationException("The generated Options ownership link is invalid.");
        }
        internal static void Verify(Component component, bool legacy)
        {
            RequireContainer(component);
            var data = new SerializedObject(component);
            if (!IsModel(data.FindProperty("content"))) throw new InvalidOperationException("Owned SPS3 feature schema changed.");
            var old = data.FindProperty("config.features");
            if (old == null || !old.isArray || old.arraySize != 0) throw new InvalidOperationException("Unexpected legacy global feature configuration.");
            if (!string.IsNullOrEmpty(VrcFuryCompatibility.Require(data, "content.menuPath", SerializedPropertyType.String).stringValue))
                throw new InvalidOperationException("Owned global menu path was independently customized; preserve it.");
            foreach (var field in new[] { "saveSockets", "addDpsTipLight", "tpsDepthAnimationCompatibility", "autoModeDefaultOn", "autoModeSaved", "legacyModeSaved" })
                if (VrcFuryCompatibility.Require(data, "content." + field, SerializedPropertyType.Boolean).boolValue)
                    throw new InvalidOperationException("Owned global option was independently customized: " + field);
            if (!VrcFuryCompatibility.Require(data, "content.autoModeAvailable", SerializedPropertyType.Boolean).boolValue)
                throw new InvalidOperationException("Owned global Auto availability was independently customized.");
            var icon = data.FindProperty("content.menuIcon.objRef");
            var iconGuid = data.FindProperty("content.menuIcon.guid");
            if ((icon != null && icon.objectReferenceValue != null) || (iconGuid != null && !string.IsNullOrEmpty(iconGuid.stringValue)))
                throw new InvalidOperationException("Owned global menu icon was independently customized; preserve it.");
            foreach (var field in Controlled)
                if (VrcFuryCompatibility.Require(data, "content." + field, SerializedPropertyType.Boolean).boolValue != legacy)
                    throw new InvalidOperationException("Owned global option was independently changed: " + field);
        }
    }
}
