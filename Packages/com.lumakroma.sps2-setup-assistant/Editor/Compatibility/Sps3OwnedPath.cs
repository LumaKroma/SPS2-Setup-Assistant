using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Only generated hierarchy is recorded, never the actual native payload as expected data.
    internal static class Sps3OwnedPath
    {
        internal static bool Supported => SpsBackend.PackageMatches(
            VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)), SpsFamily.VrcFury, out _) &&
            FuryNativeFeatureShapes.InspectInstalled().PathStops;
        [Serializable] private sealed class Proof { public int version=1; public List<Node> nodes=new List<Node>(); }
        [Serializable] private sealed class ProofVrc { public int version=2; public List<NodeVrc> nodes=new List<NodeVrc>(); }
        [Serializable] private sealed class NodeVrc { public Node core; public string vrcPayload; }
        [Serializable] private sealed class Node {
            public string path; public Vector3 position,scale; public Quaternion rotation;
            public bool active,controlled; public Constraint constraint;
        }
        [Serializable] private sealed class Constraint {
            public bool active,locked,enabled; public float weight; public int translationAxes,rotationAxes;
            public Vector3 translationRest,rotationRest; public List<Source> sources=new List<Source>();
        }
        [Serializable] private sealed class Source { public string path; public float weight; public Vector3 translation,rotation; }
        private static string Path(Transform t, Transform avatar) {
            if(t==null || !(t==avatar || t.IsChildOf(avatar))) throw new InvalidOperationException("Path reference left the avatar.");
            var parts=new List<string>();
            while(t!=avatar) {
                if(t.name.Contains("/") || t.parent.Cast<Transform>().Count(x=>x.name==t.name)!=1)
                    throw new InvalidOperationException("Ambiguous owned path name.");
                parts.Add(t.name); t=t.parent;
            }
            parts.Reverse(); return string.Join("/",parts);
        }
        internal static string CaptureHierarchy(Sps2SetupContext root) {
            var path=root.transform.Find("Guided Paths");
            if(path==null) throw new InvalidOperationException("Owned path hierarchy is missing.");
            var proof=new Proof();
            var proofVrc=new ProofVrc(); bool hasVrc=false;
            foreach(var t in path.GetComponentsInChildren<Transform>(true).OrderBy(t=>Path(t,root.transform),StringComparer.Ordinal)) {
                var components=t.GetComponents<Component>();
                if(components.Any(c=>c==null || !(c is Transform || c is ParentConstraint || VrcConstraintBridge.IsVrcParent(c))) ||
                    components.Count(c=>c is ParentConstraint || VrcConstraintBridge.IsVrcParent(c))>1)
                    throw new InvalidOperationException("Unmodeled component on owned path.");
                var c=t.GetComponent<ParentConstraint>();
                var native=components.SingleOrDefault(VrcConstraintBridge.IsVrcParent);
                var n=new Node { path=Path(t,root.transform),scale=t.localScale,active=t.gameObject.activeSelf,controlled=c!=null || native!=null };
                if(c==null && native==null) { n.position=t.localPosition; n.rotation=t.localRotation; }
                else {
                    if(c!=null) n.constraint=new Constraint {active=c.constraintActive,locked=c.locked,enabled=c.enabled,weight=c.weight,
                        translationAxes=(int)c.translationAxis,rotationAxes=(int)c.rotationAxis,translationRest=c.translationAtRest,rotationRest=c.rotationAtRest};
                    for(int i=0;c!=null && i<c.sourceCount;i++) {
                        var s=c.GetSource(i);
                        n.constraint.sources.Add(new Source {path=Path(s.sourceTransform,root.avatar.transform),weight=s.weight,
                            translation=c.GetTranslationOffset(i),rotation=c.GetRotationOffset(i)});
                    }
                }
                proof.nodes.Add(n);
                hasVrc |= native != null;
                proofVrc.nodes.Add(new NodeVrc { core=n, vrcPayload=native==null?null:VrcConstraintBridge.Capture(native,root.avatar.transform) });
            }
            return hasVrc ? JsonUtility.ToJson(proofVrc) : JsonUtility.ToJson(proof);
        }
        internal static bool ProofEquals(string a, string b) {
            if (a == b) return true;
            try {
                var left=JsonUtility.FromJson<ProofVrc>(a); var right=JsonUtility.FromJson<ProofVrc>(b);
                if(left==null || right==null || left.version!=2 || right.version!=2 || left.nodes==null || right.nodes==null || left.nodes.Count!=right.nodes.Count) return false;
                foreach(var n in left.nodes) if(n!=null) n.vrcPayload=VrcConstraintBridge.NormalizePayload(n.vrcPayload);
                foreach(var n in right.nodes) if(n!=null) n.vrcPayload=VrcConstraintBridge.NormalizePayload(n.vrcPayload);
                return JsonUtility.ToJson(left)==JsonUtility.ToJson(right);
            } catch(Exception) { return false; }
        }
        internal static void RequireHierarchyUnchanged(Sps2SetupContext root, bool allowKnownChanges = false) {
            bool hasStops=root.sockets.Any(s=>s.pathStops!=null && s.pathStops.Length!=0);
            bool hasTree=root.transform.Find("Guided Paths")!=null;
            if(!hasStops && !hasTree && string.IsNullOrEmpty(root.nativePathProof)) return;
            if(!Supported || !root.oralBoundaryPath || string.IsNullOrEmpty(root.nativePathProof))
                throw new InvalidOperationException("Sps3NativeEditConflict: Owned path has no saved ownership proof; retain existing data.");
            var current=CaptureHierarchy(root); // Unknown structures still fail closed.
            if(!allowKnownChanges && !ProofEquals(root.nativePathProof,current))
                throw new InvalidOperationException("Sps3NativeEditConflict: Owned path was edited or has no saved ownership proof; retain existing data.");
        }
        internal static void PopulateExpected(Sps2SetupContext root, GeneratedSocket generated, Component expected, bool allowKnownChanges = false) {
            RequireHierarchyUnchanged(root, allowKnownChanges);
            if(!root.settings.penetration || generated.pathStops.Length!=3 || (generated.id!="mouth" && generated.id!="anus"))
                throw new InvalidOperationException("Unmodeled guided path ownership must be retained.");
            VrcFuryCompatibility.SetPath(expected,generated.pathStops,root.avatar.transform);
            VrcFuryCompatibility.SetPathCollapse(expected,!root.settings.showInternalThickness,generated.id=="mouth"?0:2);
        }
        internal static void RequireNoExternalReferences(Sps2SetupContext root, bool allowConfirmed = false) {
            var tree=root.transform.Find("Guided Paths"); if(tree==null) return;
            foreach(var sceneRoot in root.avatar.gameObject.scene.GetRootGameObjects())
            foreach(var component in sceneRoot.GetComponentsInChildren<Component>(true)) {
                if(component==null || component is Transform || component.transform.IsChildOf(tree)) continue;
                var p=new SerializedObject(component).GetIterator();
                while(p.Next(true)) {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference) continue;
                    var reference=p.objectReferenceValue;
                    var t=reference is GameObject go?go.transform:(reference as Component)?.transform;
                    if(t==null || !(t==tree||t.IsChildOf(tree))) continue;
                    bool ownNative=root.sockets.Any(s=>s.socket==component) && p.propertyPath.StartsWith("guidedPathStops.Array.data[",StringComparison.Ordinal);
                    if(!ownNative && !allowConfirmed) throw new InvalidOperationException("Sps3NativeEditConflict: External reference to owned path; existing data is unchanged.");
                }
            }
        }
    }
}
