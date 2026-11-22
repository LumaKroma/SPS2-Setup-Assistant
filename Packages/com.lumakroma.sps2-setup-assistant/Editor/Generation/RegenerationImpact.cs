using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Read-only impact inventory. It does not grant permission to replace anything.
    internal static class RegenerationImpact
    {
        internal sealed class Report
        {
            internal readonly List<string> AdditionalObjects = new List<string>();
            internal readonly List<string> AdditionalComponents = new List<string>();
            internal readonly List<string> ExternalReferences = new List<string>();
            internal readonly List<string> NativeEdits = new List<string>();
            internal readonly List<Object> ExternalHolders = new List<Object>();
            internal readonly List<string> Fingerprint = new List<string>();
            internal bool HasImpact => AdditionalObjects.Count != 0 || AdditionalComponents.Count != 0 || ExternalReferences.Count != 0 || NativeEdits.Count != 0;
            internal bool SameAs(Report other) => other != null && Fingerprint.OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(other.Fingerprint.OrderBy(x => x, StringComparer.Ordinal));
        }

        internal static Report Inspect(Sps2SetupContext root, SetupSettings requested = null, bool additionsOnly = false)
        {
            var report = new Report();
            if (root == null || root.gameObject == null) return report;
            if (requested != null) report.Fingerprint.Add("draft:" + JsonUtility.ToJson(requested));
            var known = new HashSet<Transform> { root.transform };
            foreach (var socket in root.sockets ?? new List<GeneratedSocket>())
            {
                if (socket == null) continue;
                if (socket.anchor != null) known.Add(socket.anchor);
                if (socket.pose != null) known.Add(socket.pose);
                foreach (var stop in socket.pathStops ?? Array.Empty<Transform>())
                    for (var t = stop; t != null && t != root.transform && t.IsChildOf(root.transform); t = t.parent) known.Add(t);
            }
            // Guided-path helper nodes are owned only when the entire recorded
            // hierarchy still matches its saved proof. Unknown or edited trees
            // remain visible in the impact report rather than being assumed safe.
            var guided = root.transform.Find("Guided Paths");
            if (guided != null && root.oralBoundaryPath && !string.IsNullOrEmpty(root.nativePathProof)) {
                try {
                    if (Sps3OwnedPath.ProofEquals(Sps3OwnedPath.CaptureHierarchy(root), root.nativePathProof))
                        foreach (var t in guided.GetComponentsInChildren<Transform>(true)) known.Add(t);
                } catch (InvalidOperationException) { /* report unverified nodes conservatively */ }
            }
            var removed = new HashSet<Object>();
            foreach (var node in root.gameObject.GetComponentsInChildren<Transform>(true))
            {
                removed.Add(node.gameObject);
                foreach (var component in node.GetComponents<Component>()) if (component != null) removed.Add(component);
                if (!known.Contains(node)) {
                    report.AdditionalObjects.Add(Path(node, root.transform));
                    report.Fingerprint.Add("object:" + node.GetInstanceID() + ":" + EditorJsonUtility.ToJson(node.gameObject));
                    foreach (var extra in node.GetComponents<Component>())
                        report.Fingerprint.Add(extra == null ? "missing:" + node.GetInstanceID() :
                            "child-component:" + extra.GetInstanceID() + ":" + EditorJsonUtility.ToJson(extra));
                }
                if (!known.Contains(node)) continue;
                bool recognizedAttachment = false;
                bool recognizedOptions = false;
                foreach (var component in node.GetComponents<Component>())
                {
                    if (component == null) { report.AdditionalComponents.Add(Path(node, root.transform) + ": Missing Script"); report.Fingerprint.Add("missing:" + node.GetInstanceID()); continue; }
                    if (component is Transform) continue;
                    if (node == root.transform && component is Sps3OptionsOwnership) continue;
                    if (root.sockets != null && root.sockets.Any(s => s != null && s.socket == component)) continue;
                    var type = component.GetType().FullName ?? "";
                    if (node == root.transform && !recognizedOptions && SpsMigrationAdapter.IsSingleOptions(component)) {
                        recognizedOptions = true;
                        continue;
                    }
                    if (node != root.transform && !recognizedAttachment &&
                        (type == "UnityEngine.Animations.ParentConstraint" || VrcConstraintBridge.IsVrcParent(component) || type == "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy" ||
                        SpsMigrationAdapter.IsSingleArmatureLink(component))) {
                        recognizedAttachment = true;
                        continue;
                    }
                    report.AdditionalComponents.Add(Path(node, root.transform) + ": " + type);
                    report.Fingerprint.Add("component:" + component.GetInstanceID() + ":" + EditorJsonUtility.ToJson(component));
                }
            }
            // Replacement confirmation is about generated-root additions only.
            // This optional inventory deliberately does not inspect external holders or
            // compare requested/native values. It cannot authorize replacement by itself.
            if (additionsOnly) return report;
            var holders = Resources.FindObjectsOfTypeAll<Component>().Cast<Object>()
                .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>()).Distinct();
            foreach (var holder in holders)
            {
                if (holder == null || removed.Contains(holder)) continue;
                // Unity's transient animation-window selection object is an editor
                // cache, not a user-authored reference. The migration inventory uses
                // the same exact type/assembly/hideFlags/path check; no other editor
                // object or ScriptableObject is excluded by this condition.
                if (SpsMigrationAdapter.IsTransientAnimationSelectionCache(holder)) continue;
                if (holder is Component component && !EditorUtility.IsPersistent(component) &&
                    (!component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded)) continue;
                SpsMigrationAdapter.VisitRemovedReferences(holder, removed, (path, reference) => {
                    report.ExternalReferences.Add(holder.name + " (" + holder.GetType().Name + ") / " + path);
                    report.Fingerprint.Add("reference:" + holder.GetInstanceID() + ":" + path + ":" + reference.GetInstanceID() + ":" + EditorJsonUtility.ToJson(holder));
                    if (!report.ExternalHolders.Contains(holder)) report.ExternalHolders.Add(holder);
                });
            }
            if (requested != null && root.AuthoringContract == SpsAuthoringContract.Sps3Beta14) {
                using var backend = SpsBackend.Use(root.settings.family);
                using var contract = SpsAuthoringCompatibility.Use(root.AuthoringContract);
                if (!string.IsNullOrEmpty(root.nativePathProof)) {
                    var currentPath = Sps3OwnedPath.CaptureHierarchy(root);
                    if (!Sps3OwnedPath.ProofEquals(currentPath, root.nativePathProof)) {
                        report.NativeEdits.Add("Generated guided-path settings or transforms changed");
                        report.Fingerprint.Add("guided-path:" + currentPath);
                    }
                }
                if (VrcConstraintBridge.HasOwned(root) || root.asset != null && !string.IsNullOrEmpty(root.asset.vrcConstraintEvidence)) {
                    var current = VrcConstraintBridge.CaptureOwned(root);
                    if (root.asset == null || !VrcConstraintBridge.EvidenceEquals(current, root.asset.vrcConstraintEvidence)) {
                        report.NativeEdits.Add("VRC Constraint settings or references changed");
                        report.Fingerprint.Add("vrc-constraints:" + current);
                    }
                }
                Sps3NativeEditProtection.RequireBeforeMutation(root, requested, true,
                    requested.parts.Where(p => p != null && p.included).Select(p => p.id), nativeConflicts: report.NativeEdits);
                if (report.NativeEdits.Count != 0) {
                    foreach (var socket in root.sockets)
                        if (socket?.socket != null) report.Fingerprint.Add("native:" + socket.socket.GetInstanceID() + ":" + EditorJsonUtility.ToJson(socket.socket));
                }
            }
            return report;
        }

        internal static void RecordExternalUndo(Report report)
        {
            if (report == null) return;
            foreach (var holder in report.ExternalHolders)
                if (holder != null) Undo.RegisterCompleteObjectUndo(holder, "SPS regeneration references");
        }

        private static string Path(Transform node, Transform root)
        {
            var parts = new List<string>();
            for (var current = node; current != null; current = current.parent)
            {
                parts.Add(current.name);
                if (current == root) break;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
