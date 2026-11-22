using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Native scene edits are not imported into SetupSettings. Preserve them on unrelated
    // Apply and reject an operation that would overwrite/remove them before any mutation.
    internal static class Sps3NativeEditProtection
    {
        internal const string Conflict = "Sps3NativeEditConflict";

        // A null managedReferenceValue may still have recoverable serialized data.
        // Inspect only a component that is about to be rewritten/removed and its
        // corresponding source component; no prefab asset is applied or modified.
        internal static void RequireNoMissingReferences(Component component)
        {
            if (component == null) throw new InvalidOperationException("Cannot inspect a missing component; existing data is unchanged.");
            // Unity's managed-reference utility rejects built-in native components.
            // These known attachment/transform types do not host C# SerializeReference fields.
            // Do not call the API on them; unknown native component types fail closed.
            int disposition = FuryNativeFeatureShapes.ReferenceInspectionDisposition(
                component is Transform || component is UnityEngine.Animations.ParentConstraint, component is MonoBehaviour);
            if (disposition == 0) return;
            if (disposition == 2)
                throw new InvalidOperationException("Cannot inspect managed references on component type " + component.GetType().FullName + "; existing data is unchanged.");
            try
            {
                if (!FuryNativeFeatureShapes.MissingReferenceCheckPasses(() => SerializationUtility.HasManagedReferencesWithMissingTypes(component)))
                    throw new InvalidOperationException("The component contains managed references with missing types.");
                var visited = new HashSet<Component> { component };
                var source = PrefabUtility.GetCorrespondingObjectFromSource(component);
                while (source != null)
                {
                    if (!visited.Add(source)) throw new InvalidOperationException("Prefab source chain is cyclic.");
                    if (!FuryNativeFeatureShapes.MissingReferenceCheckPasses(() => SerializationUtility.HasManagedReferencesWithMissingTypes(source)))
                        throw new InvalidOperationException("A corresponding Prefab component contains managed references with missing types.");
                    source = PrefabUtility.GetCorrespondingObjectFromSource(source);
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Cannot safely change or remove " + component.name +
                    ": missing managed-reference data or inspection failure. Existing data is unchanged.", e);
            }
        }

        internal static void RequireDiscardSafe(GameObject root)
        {
            if (root == null) throw new InvalidOperationException("Cannot inspect the objects being removed; existing data is unchanged.");
            foreach (var component in root.GetComponentsInChildren<Component>(true)) RequireNoMissingReferences(component);
        }

        internal static bool SameDepth(SocketSettings a, SocketSettings b)
        {
            return Sps3AnimationGroupNative.SameOutput(a, b);
        }

        internal static void RequireBeforeMutation(Sps2SetupContext existing, SetupSettings requested,
            bool regenerate, IEnumerable<string> placementIds, bool allowConfirmedNativeEdits = false,
            ICollection<string> nativeConflicts = null, bool allowConfirmedExternalReferences = false)
        {
            if (existing == null || existing.AuthoringContract != SpsAuthoringContract.Sps3Beta14) return;
            if (requested?.parts == null || existing.settings?.parts == null)
                throw new InvalidOperationException(Conflict + ": Saved settings are incomplete; existing data is unchanged.");
            var placements = new HashSet<string>(placementIds, StringComparer.Ordinal);
            var oldIds = new HashSet<string>(existing.sockets.Select(s => s.id), StringComparer.Ordinal);
            bool pathRebuild = regenerate || placements.Any(id => !oldIds.Contains(id)) ||
                existing.sockets.Any(s => !placements.Contains(s.id)) ||
                existing.settings.penetration != requested.penetration ||
                existing.settings.parts.Any(p => (p.id == "mouth" || p.id == "anus") &&
                    p.included != requested.parts.Find(n => n.id == p.id)?.included);
            bool pathRewrite = pathRebuild || existing.settings.showInternalThickness != requested.showInternalThickness;
            bool allowKnownPathEdits = regenerate && (allowConfirmedNativeEdits || nativeConflicts != null);
            if (pathRewrite) Sps3OwnedPath.RequireHierarchyUnchanged(existing, allowKnownPathEdits);
            if (pathRebuild) Sps3OwnedPath.RequireNoExternalReferences(existing,
                regenerate && (allowConfirmedExternalReferences || nativeConflicts != null));
            foreach (var generated in existing.sockets)
            {
                var saved = existing.settings.parts.SingleOrDefault(p => p.id == generated.id);
                var next = requested.parts.SingleOrDefault(p => p.id == generated.id);
                if (saved == null || generated.socket == null)
                    throw new InvalidOperationException(Conflict + ": Cannot verify saved native socket " + generated.id);
                bool remove = regenerate || next == null || !placements.Contains(generated.id);
                bool nameRewrite = !remove && !VrcFuryCompatibility.SameDisplayName(saved, existing.settings, next, requested);
                bool autoRewrite = !remove && existing.settings.Sps3IncludeInAuto(saved) != requested.Sps3IncludeInAuto(next);
                bool legacyRewrite = !remove && existing.settings.legacy != requested.legacy;
                bool modeRewrite = !remove && existing.settings.penetration != requested.penetration;
                bool depthRewrite = !remove && !Sps3AnimationGroupNative.SameOutput(saved,next,3);
                bool entryRewrite = !remove && !Sps3AnimationGroupNative.SameOutput(saved,next,4);
                bool activeRewrite = !remove && !Sps3AnimationGroupNative.SameOutput(saved,next,2);
                if (FuryNativeFeatureShapes.MissingReferenceInspectionRequired(remove, depthRewrite, entryRewrite, activeRewrite))
                {
                    if (remove) RequireDiscardSafe(generated.anchor != null ? generated.anchor.gameObject : null);
                    else RequireNoMissingReferences(generated.socket);
                }
                bool pathSocket = saved.id == "mouth" || saved.id == "anus";
                try { VerifySavedPayload(existing, generated, saved, remove, depthRewrite, entryRewrite, activeRewrite, nameRewrite, autoRewrite, legacyRewrite, modeRewrite, pathSocket, pathRewrite, pathRebuild, allowKnownPathEdits); }
                catch (InvalidOperationException e) when (regenerate && e.Message.StartsWith(Conflict + ":", StringComparison.Ordinal) &&
                    e.InnerException != null && e.InnerException.Message.StartsWith("Native settings differ:", StringComparison.Ordinal) &&
                    (allowConfirmedNativeEdits || nativeConflicts != null)) {
                    nativeConflicts?.Add(generated.id + ": " + e.InnerException.Message);
                }
            }
        }

        private static void VerifySavedPayload(Sps2SetupContext existing, GeneratedSocket generated, SocketSettings saved,
            bool remove, bool depthRewrite, bool entryRewrite, bool activeRewrite, bool nameRewrite, bool autoRewrite, bool legacyRewrite, bool modeRewrite, bool pathSocket, bool pathRewrite, bool pathRebuild, bool allowKnownPathEdits)
        {
            GameObject owned = null;
            try
            {
                owned = new GameObject("SPS3 native preservation comparison") { hideFlags = HideFlags.HideAndDontSave };
                var wrapper = VrcFuryApi.CreateSocket(owned);
                wrapper.SetMode(existing.settings.penetration && pathSocket ? "Ring" : "Auto");
                if (saved.id == "chest" || saved.id == "handLeft" || saved.id == "handRight" || saved.id == "hands" ||
                    saved.id == "footLeft" || saved.id == "footRight" || saved.id == "feet") wrapper.UseRadiusOffset();
                var expected = VrcFuryCompatibility.FindSocket(owned);
                VrcFuryCompatibility.Configure(expected, saved, existing.settings, new List<string>(),
                    remove || depthRewrite, true, remove || activeRewrite, remove || entryRewrite);
                VrcFuryCompatibility.SetAuthoringIdentity(expected, Sps2SetupStorage.Token(existing, generated.id));
                if ((remove || pathRewrite) && pathSocket && generated.pathStops != null && generated.pathStops.Length != 0)
                    Sps3OwnedPath.PopulateExpected(existing, generated, expected, allowKnownPathEdits);
                if (remove)
                {
                    SpsMigrationAdapter.ValidateEquivalent(generated.socket, expected);
                    return;
                }
                // The asset identity is renewed by every Apply; all other fields are guarded
                // only when the corresponding saved Setup control changed. No native menu/action write.
                RequireSubtree(generated.socket, expected, "oscId");
                if (nameRewrite) RequireSubtree(generated.socket, expected, "name");
                if (autoRewrite) RequireSubtree(generated.socket, expected, "enableAuto");
                if (legacyRewrite) RequireSubtree(generated.socket, expected, "useLights");
                if (activeRewrite) RequireSubtree(generated.socket,expected,"activeActions");
                if (depthRewrite) {
                    RequireSubtree(generated.socket, expected, "depthActions2");
                }
                if (entryRewrite) {
                    if (new SerializedObject(generated.socket).FindProperty("plugEntryRadiusActions") != null ||
                        new SerializedObject(expected).FindProperty("plugEntryRadiusActions") != null)
                        RequireSubtree(generated.socket, expected, "plugEntryRadiusActions");
                }
                if (pathSocket)
                {
                    if (modeRewrite) RequireSubtree(generated.socket, expected, "addLight");
                    if (pathRewrite) RequireSubtree(generated.socket, expected, "guidedPathStops");
                    if (pathRebuild) RequireSubtree(generated.socket, expected, "offsetsInLocalUnits");
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(Conflict + ": Native SPS edits are not represented in the saved Setup settings. " +
                    "This operation would overwrite or remove them; existing data is unchanged. Socket " + generated.id + ". " + e.Message, e);
            }
            finally
            {
                if (owned != null)
                {
                    // Only the newly created comparison object/components own these Undo entries.
                    foreach (var component in owned.GetComponents<Component>()) if (component != null) Undo.ClearUndo(component);
                    Undo.ClearUndo(owned);
                    Object.DestroyImmediate(owned);
                }
            }
        }

        private static void RequireSubtree(Component actual, Component expected, string root)
        {
            var a = new SerializedObject(actual).FindProperty(root);
            var b = new SerializedObject(expected).FindProperty(root);
            if (a == null || b == null || a.propertyType != b.propertyType ||
                SpsMigrationAdapter.State(actual, root) != SpsMigrationAdapter.State(expected, root))
                throw new InvalidOperationException("Native settings differ: " + root);
        }
    }
}
