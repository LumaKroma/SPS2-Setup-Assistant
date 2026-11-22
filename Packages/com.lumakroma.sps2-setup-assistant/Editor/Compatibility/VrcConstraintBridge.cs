using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Text;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // This helper is deliberately not called by the product generator until its
    // ownership, prefab-comparison and guided-path readers support VRC constraints.
    internal static class VrcConstraintBridge
    {
        private const string NativeType = "VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint";
        internal static Action conversionCheckpointForTests;
        private static MethodInfo Converter()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("VRC.SDK3.Avatars.AvatarDynamicsSetup", false))
                .FirstOrDefault(x => x != null);
            return t?.GetMethod("DoConvertUnityConstraints", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(IConstraint[]), typeof(VRCAvatarDescriptor), typeof(bool) }, null);
        }
        internal static bool Available => Converter() != null && VrcFuryApi.FindType(NativeType) != null;
        internal static bool IsVrcParent(Component value) => value != null && value.GetType().FullName == NativeType;
        [Serializable] private sealed class Evidence { public int version=1; public List<Entry> entries=new List<Entry>(); }
        [Serializable] private sealed class Entry { public string path, payload; }
        internal static bool HasOwned(Sps2SetupContext root)
        {
            if(root == null || root.gameObject == null) return false;
            if(root.sockets != null && root.sockets.Any(s=>s?.anchor!=null && s.anchor.GetComponents<Component>().Any(IsVrcParent))) return true;
            var guided=root.transform.Find("Guided Paths");
            return guided!=null && guided.GetComponentsInChildren<Component>(true).Any(IsVrcParent);
        }
        // Execution-group indices are SDK scheduler caches, not authored constraint settings.
        // Normalize old evidence too, so previously generated instances remain comparable.
        internal static string NormalizePayload(string payload)
        {
            if (payload == null) return null;
            return string.Concat(payload.Split(new[] { '\n' }, StringSplitOptions.None)
                .Where(line => !line.StartsWith("25:cachedExecutionGroupIndex:", StringComparison.Ordinal) &&
                               !line.StartsWith("30:latestValidExecutionGroupIndex:", StringComparison.Ordinal))
                .Select(line => line + "\n")).TrimEnd('\n') + (payload.EndsWith("\n", StringComparison.Ordinal) ? "\n" : "");
        }
        internal static bool EvidenceEquals(string a, string b)
        {
            if (a == b) return true;
            try {
                var left = JsonUtility.FromJson<Evidence>(a); var right = JsonUtility.FromJson<Evidence>(b);
                if (left == null || right == null || left.version != right.version || left.entries == null || right.entries == null || left.entries.Count != right.entries.Count) return false;
                for (int i = 0; i < left.entries.Count; i++)
                    if (left.entries[i] == null || right.entries[i] == null || left.entries[i].path != right.entries[i].path ||
                        NormalizePayload(left.entries[i].payload) != NormalizePayload(right.entries[i].payload)) return false;
                return true;
            } catch (Exception) { return false; }
        }
        // Only known socket anchors and the generated guided-path tree participate.
        // Retained plugs and unrelated avatar objects are never claimed.
        internal static string CaptureOwned(Sps2SetupContext root)
        {
            if(root == null || root.gameObject == null || root.avatar == null) throw new InvalidOperationException("Missing setup owner.");
            string Path(Transform t) {
                if(t==null || !t.IsChildOf(root.transform)) throw new InvalidOperationException("Attachment is outside the generated root.");
                var names=new List<string>();
                while(t!=root.transform) {
                    if(t.name.Contains("/") || t.parent.Cast<Transform>().Count(x=>x.name==t.name)!=1)
                        throw new InvalidOperationException("Ambiguous generated attachment path.");
                    names.Add(t.name); t=t.parent;
                }
                names.Reverse(); return string.Join("/",names);
            }
            var nodes=(root.sockets ?? new List<GeneratedSocket>()).Where(s=>s?.anchor!=null).Select(s=>s.anchor).ToList();
            var guided=root.transform.Find("Guided Paths");
            if(guided!=null) nodes.AddRange(guided.GetComponentsInChildren<Transform>(true));
            var evidence=new Evidence();
            foreach(var node in nodes.Distinct()) {
                var values=node.GetComponents<Component>().Where(IsVrcParent).ToArray();
                if(values.Length>1) throw new InvalidOperationException("Ambiguous VRC attachment.");
                if(values.Length==1) evidence.entries.Add(new Entry {path=Path(node),payload=Capture(values[0],root.avatar.transform)});
            }
            evidence.entries=evidence.entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToList();
            return JsonUtility.ToJson(evidence);
        }
        internal static Transform[] Sources(Component constraint)
        {
            if (!IsVrcParent(constraint)) throw new InvalidOperationException("Expected a VRC Parent Constraint.");
            var t=constraint.GetType(); var count=(int)t.GetProperty("SourceCount").GetValue(constraint);
            var get=t.GetMethod("GetSource", new[] { typeof(int) });
            var result=new List<Transform>();
            for(int i=0;i<count;i++) {
                var source=get.Invoke(constraint,new object[] {i}); var st=source.GetType();
                var target=(st.GetProperty("SourceTransform")?.GetValue(source) ?? st.GetField("SourceTransform")?.GetValue(source)) as Transform;
                if(target==null) throw new InvalidOperationException("VRC Parent Constraint has a missing source.");
                result.Add(target);
            }
            return result.ToArray();
        }
        internal sealed class SingleSourceData
        {
            internal Transform source;
            internal Vector3 positionRest, rotationRest, positionOffset, rotationOffset;
        }
        internal static SingleSourceData ReadSingleSource(Component constraint)
        {
            if (!IsVrcParent(constraint)) throw new InvalidOperationException("Expected a VRC Parent Constraint.");
            var t=constraint.GetType();
            if ((int)t.GetProperty("SourceCount").GetValue(constraint)!=1) throw new InvalidOperationException("One VRC constraint source is required.");
            var source=t.GetMethod("GetSource",new[] {typeof(int)}).Invoke(constraint,new object[] {0});
            object Read(object obj,string name) {
                var type=obj.GetType(); var prop=type.GetProperty(name); if(prop!=null) return prop.GetValue(obj);
                var field=type.GetField(name); if(field!=null) return field.GetValue(obj);
                throw new InvalidOperationException("Unsupported VRC constraint field: "+name);
            }
            var target=Read(source,"SourceTransform") as Transform;
            if(target==null) throw new InvalidOperationException("VRC constraint source is missing.");
            return new SingleSourceData { source=target, positionRest=(Vector3)Read(constraint,"PositionAtRest"),
                rotationRest=(Vector3)Read(constraint,"RotationAtRest"), positionOffset=(Vector3)Read(source,"ParentPositionOffset"),
                rotationOffset=(Vector3)Read(source,"ParentRotationOffset") };
        }
        // Only a disposable comparison object can use this overload; it never scans
        // the avatar or converts animation clips.
        internal static Component ConvertComparison(ParentConstraint source)
        {
            var method=Converter(); var native=VrcFuryApi.FindType(NativeType);
            if(method==null || native==null || source==null || (source.gameObject.hideFlags & HideFlags.HideAndDontSave)!=HideFlags.HideAndDontSave ||
                source.GetComponent(native)!=null) throw new InvalidOperationException("VRC comparison requires a disposable, isolated constraint.");
            var host=source.gameObject;
            if((bool)method.Invoke(null,new object[] {new IConstraint[] {source},null,false}) || host.GetComponent<ParentConstraint>()!=null || host.GetComponents(native).Length!=1)
                throw new InvalidOperationException("Could not construct a disposable VRC comparison constraint.");
            return host.GetComponent(native);
        }
        // Stable across scene save/reload: scene object references are stored as
        // unambiguous avatar-relative paths; asset references use GUID and local ID.
        // Refuse unsupported serialized types rather than silently losing proof.
        internal static string Capture(Component constraint, Transform avatar)
        {
            if (!IsVrcParent(constraint) || avatar == null) throw new InvalidOperationException("Expected a VRC Parent Constraint.");
            string ScenePath(Transform value) {
                if (value == null || !(value == avatar || value.IsChildOf(avatar))) throw new InvalidOperationException("Constraint reference leaves the avatar.");
                var pieces = new List<string>();
                while (value != avatar) {
                    if (value.name.Contains("/") || value.parent.Cast<Transform>().Count(t => t.name == value.name) != 1)
                        throw new InvalidOperationException("Ambiguous constraint reference path.");
                    pieces.Add(value.name); value = value.parent;
                }
                pieces.Reverse(); return string.Join("/", pieces);
            }
            string Ref(UnityEngine.Object o, string propertyPath) {
                if (o == null) return "null";
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id)) return "asset:" + guid + ":" + id;
                var t = o is GameObject go ? go.transform : (o as Component)?.transform;
                if (t == null) throw new InvalidOperationException("Unresolved VRC constraint reference at " + propertyPath + ": " + o.GetType().FullName);
                return "scene:" + ScenePath(t) + ":" + (o is Component c ? c.GetType().FullName : "GameObject");
            }
            var text = new StringBuilder(); var p = new SerializedObject(constraint).GetIterator(); bool enter = true;
            while (p.Next(enter)) {
                enter = true;
                // Unity prefab bookkeeping is not a constraint setting; it can point to
                // transient PrefabInstance objects and differs between an instance and scene.
                if (p.propertyPath == "cachedExecutionGroupIndex" || p.propertyPath == "latestValidExecutionGroupIndex") { enter = false; continue; }
                if (p.propertyPath == "m_CorrespondingSourceObject" ||
                    p.propertyPath == "m_PrefabInstance" || p.propertyPath == "m_PrefabAsset" ||
                    p.propertyPath == "m_GameObject" || p.propertyPath == "m_Script") { enter = false; continue; }
                string value;
                switch (p.propertyType) {
                    case SerializedPropertyType.Generic: continue;
                    case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character: case SerializedPropertyType.LayerMask: value = p.longValue.ToString(CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Boolean: value = p.boolValue ? "1" : "0"; break;
                    case SerializedPropertyType.Float: value = p.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Vector3:
                        var v = p.vector3Value;
                        value = v.x.ToString("R", CultureInfo.InvariantCulture) + "," + v.y.ToString("R", CultureInfo.InvariantCulture) + "," + v.z.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Quaternion:
                        var q = p.quaternionValue;
                        value = q.x.ToString("R", CultureInfo.InvariantCulture) + "," + q.y.ToString("R", CultureInfo.InvariantCulture) + "," + q.z.ToString("R", CultureInfo.InvariantCulture) + "," + q.w.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.String: value = p.stringValue ?? ""; break;
                    // Do not descend into Unity's transient m_FileID/m_PathID details.
                    case SerializedPropertyType.ObjectReference: value = Ref(p.objectReferenceValue,p.propertyPath); enter = false; break;
                    default: throw new InvalidOperationException("Unsupported VRC constraint field: " + p.propertyPath + " (" + p.propertyType + ")");
                }
                text.Append(p.propertyPath.Length).Append(':').Append(p.propertyPath).Append(':')
                    .Append((int)p.propertyType).Append(':').Append(value.Length).Append(':').Append(value).Append('\n');
            }
            return text.ToString();
        }
        // Caller owns the Undo group and must revert it if this throws. Never scans
        // or converts other avatar components, nor rewrites animation clips.
        internal static void ConvertNewOwned(Transform generatedRoot, VRCAvatarDescriptor avatar,
            IReadOnlyList<ParentConstraint> attachments)
        {
            var method = Converter();
            var native = VrcFuryApi.FindType(NativeType);
            if (method == null || native == null) throw new InvalidOperationException("VRC Parent Constraint conversion is unavailable.");
            if (generatedRoot == null || avatar == null || EditorUtility.IsPersistent(generatedRoot) ||
                !generatedRoot.gameObject.scene.IsValid() || !generatedRoot.gameObject.scene.isLoaded ||
                !generatedRoot.IsChildOf(avatar.transform) || attachments == null || attachments.Count == 0 ||
                attachments.Any(c => c == null || !c.transform.IsChildOf(generatedRoot) || c.GetComponent(native) != null) ||
                attachments.Distinct().Count() != attachments.Count)
                throw new InvalidOperationException("VRC conversion requires unique new attachments inside the generated scene hierarchy.");
            var anchors = attachments.Select(c => c.gameObject).ToArray();
            var issues = (bool)method.Invoke(null, new object[] { attachments.Cast<IConstraint>().ToArray(), avatar, false });
            conversionCheckpointForTests?.Invoke();
            if (issues || anchors.Any(a => a == null || a.GetComponent<ParentConstraint>() != null || a.GetComponents(native).Length != 1))
                throw new InvalidOperationException("VRC constraint conversion was incomplete; revert the enclosing Undo group.");
        }

        internal static void RetargetComparison(Component staged, Transform temporaryAnchor, Transform actualAnchor)
        {
            var serialized = new SerializedObject(staged);
            var target = serialized.FindProperty("TargetTransform");
            if (target != null && target.objectReferenceValue == temporaryAnchor) {
                target.objectReferenceValue = actualAnchor;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
