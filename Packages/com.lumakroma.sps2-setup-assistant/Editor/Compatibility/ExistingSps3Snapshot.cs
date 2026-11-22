using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Read-only source observation. This does not by itself prove that a staged
    // replacement preserves the data or authorize destroying the source.
    internal sealed class ExistingSps3Snapshot
    {
        private sealed class ComponentRecord
        {
            internal Component Target;
            internal string Type, Json;
            internal readonly List<ReferenceRecord> References = new List<ReferenceRecord>();
        }
        private struct ReferenceRecord { internal string Path; internal int Id; }
        private struct NodeRecord { internal Transform Target; internal string Name, Tag; internal bool Active; internal int Layer, Sibling; }
        private readonly GameObject root;
        private readonly int rootId;
        private readonly List<NodeRecord> nodes = new List<NodeRecord>();
        private readonly List<ComponentRecord> components = new List<ComponentRecord>();
        private ExistingSps3Snapshot(GameObject value) { root = value; rootId = value.GetInstanceID(); }

        internal static ExistingSps3Snapshot Capture(GameObject value)
        {
            if (value == null || EditorUtility.IsPersistent(value) || !value.scene.IsValid() || !value.scene.isLoaded)
                throw new InvalidOperationException("A loaded scene instance is required; existing data was not changed.");
            var snapshot = new ExistingSps3Snapshot(value);
            foreach (var node in value.GetComponentsInChildren<Transform>(true))
                snapshot.nodes.Add(new NodeRecord { Target = node, Name = node.name, Tag = node.tag,
                    Active = node.gameObject.activeSelf, Layer = node.gameObject.layer, Sibling = node.GetSiblingIndex() });
            foreach (var component in value.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing component: existing data cannot be verified.");
                var record = new ComponentRecord { Target = component, Type = component.GetType().AssemblyQualifiedName,
                    Json = EditorJsonUtility.ToJson(component) };
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    int id = property.objectReferenceInstanceIDValue;
                    if (id != 0 && property.objectReferenceValue == null)
                        throw new InvalidOperationException("Unresolved reference: " + property.propertyPath);
                    record.References.Add(new ReferenceRecord { Path = property.propertyPath, Id = id });
                }
                snapshot.components.Add(record);
            }
            return snapshot;
        }

        // Recheck original identity, hierarchy order, values and references immediately
        // before any future commit; a mismatch fails closed.
        internal bool StillMatches() => StillMatchesExcept(null);
        internal bool StillMatchesExcept(Transform allowedPose)
        {
            if (root == null || root.GetInstanceID() != rootId) return false;
            try
            {
                var current = Capture(root);
                if (current.nodes.Count != nodes.Count) return false;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var a = nodes[i]; var b = current.nodes[i];
                    if (a.Target != b.Target || a.Name != b.Name || a.Tag != b.Tag || a.Active != b.Active ||
                        a.Layer != b.Layer || a.Sibling != b.Sibling) return false;
                }
                if (current.components.Count != components.Count) return false;
                for (int i = 0; i < components.Count; i++)
                {
                    var a = components[i]; var b = current.components[i];
                    if (a.Target != b.Target || a.Type != b.Type || (a.Target != allowedPose && a.Json != b.Json) || a.References.Count != b.References.Count) return false;
                    for (int j = 0; j < a.References.Count; j++)
                        if (a.References[j].Path != b.References[j].Path || a.References[j].Id != b.References[j].Id) return false;
                }
                return true;
            }
            catch { return false; }
        }
    }
}
