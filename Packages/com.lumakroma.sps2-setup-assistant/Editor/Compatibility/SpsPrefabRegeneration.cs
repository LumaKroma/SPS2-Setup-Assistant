using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Replaces generated scene output only. A source-owned root becomes a removed-object
    // override; the replacement is an added-object override in the same Undo transaction.
    // No prefab asset writes, Apply/Revert overrides, unpacking or native SPS changes.
    internal static class SpsPrefabRegeneration
    {
        internal static bool IsSourceOwned(Sps2SetupContext root) => root?.gameObject != null &&
            PrefabUtility.IsPartOfPrefabInstance(root.gameObject) &&
            !PrefabUtility.IsAddedGameObjectOverride(root.gameObject);

        private static InvalidOperationException Unsafe(string detail) => new InvalidOperationException(
            "Prefab setup regeneration cannot safely replace this generated output: " + detail +
            ". Existing scene objects and saved assets are unchanged.");

        internal static void ValidateSourceHierarchy(VRCAvatarDescriptor avatar, Sps2SetupContext root, bool allowAdditionalObjects = false)
        {
            if (!IsSourceOwned(root)) return;
            if (avatar == null || root.avatar != avatar || root.transform.parent != avatar.transform ||
                EditorUtility.IsPersistent(root.gameObject) || !root.gameObject.scene.IsValid() ||
                !root.gameObject.scene.isLoaded || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(root.gameObject) != null ||
                PrefabUtility.GetPrefabInstanceStatus(root.gameObject) != PrefabInstanceStatus.Connected)
                throw Unsafe("a connected generated child of a loaded scene avatar is required");
            var instance = PrefabUtility.GetNearestPrefabInstanceRoot(avatar.gameObject);
            if (instance == null || PrefabUtility.GetNearestPrefabInstanceRoot(root.gameObject) != instance)
                throw Unsafe("the generated root belongs to a separate nested prefab");

            var allowed = new HashSet<Transform> { root.transform };
            var anchors = new HashSet<Transform>();
            var poses = new Dictionary<Transform, Component>();
            var pathAnchors = new HashSet<Transform>();
            var pathRoot = root.transform.Find("Guided Paths");
            foreach (var socket in root.sockets)
            {
                if (socket == null || socket.anchor == null || socket.pose == null || socket.socket == null ||
                    socket.anchor.parent != root.transform || socket.pose.parent != socket.anchor ||
                    socket.socket.transform != socket.pose || !anchors.Add(socket.anchor) || poses.ContainsKey(socket.pose))
                    throw Unsafe("socket ownership is incomplete or ambiguous");
                allowed.Add(socket.anchor); allowed.Add(socket.pose); poses.Add(socket.pose, socket.socket);
                foreach (var stop in socket.pathStops)
                {
                    if (pathRoot == null || stop == null || stop.parent == null || stop.parent.parent != pathRoot)
                        throw Unsafe("guided-path ownership is incomplete");
                    allowed.Add(pathRoot); allowed.Add(stop); allowed.Add(stop.parent); pathAnchors.Add(stop.parent);
                }
            }
            // The generator records all real stops; the optional opposite endpoint is reached
            // from an exit stop's constraint and has this exact generated two-level shape.
            foreach (var anchor in pathAnchors.ToArray())
            {
                var constraint = anchor.GetComponent<ParentConstraint>();
                var vrcConstraint = anchor.GetComponents<Component>().SingleOrDefault(VrcConstraintBridge.IsVrcParent);
                if ((constraint == null) == (vrcConstraint == null)) throw Unsafe("a guided-path attachment is missing or ambiguous");
                var sourceTransforms = constraint != null ? Enumerable.Range(0, constraint.sourceCount).Select(i => constraint.GetSource(i).sourceTransform).ToArray() : VrcConstraintBridge.Sources(vrcConstraint);
                foreach (var target in sourceTransforms)
                {
                    if (target == null || !target.IsChildOf(root.transform) || allowed.Contains(target)) continue;
                    string id = target.name == "Virtual mouth Pose" ? "mouth" : target.name == "Virtual anus Pose" ? "anus" : null;
                    if (id == null || target.parent == null || target.parent.parent != pathRoot ||
                        target.parent.name != "Virtual " + id + " Follow" || root.sockets.Any(s => s.id == id))
                        throw Unsafe("a guided path refers to an unrecognized generated child");
                    allowed.Add(target); allowed.Add(target.parent); pathAnchors.Add(target.parent);
                }
            }

            foreach (var node in root.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (!allowed.Contains(node)) {
                    if (allowAdditionalObjects) continue;
                    throw Unsafe("unowned child '" + node.name + "' is inside the generated root");
                }
                if (PrefabUtility.GetNearestPrefabInstanceRoot(node.gameObject) != instance ||
                    PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject) == null)
                    throw Unsafe("an added or nested object is inside the source-owned generated root");
                var components = node.GetComponents<Component>();
                if (components.Any(c => c == null)) throw Unsafe("a missing script prevents component ownership checks");
                var extra = components.Where(c => !(c is Transform)).ToArray();
                if (extra.Any(c => PrefabUtility.GetCorrespondingObjectFromSource(c) == null)) {
                    if (!allowAdditionalObjects) throw Unsafe("an added component is inside the source-owned generated root");
                    extra = extra.Where(c => PrefabUtility.GetCorrespondingObjectFromSource(c) != null).ToArray();
                }
                if (node == root.transform)
                {
                    if (root.AuthoringContract == SpsAuthoringContract.Sps3Beta14)
                    {
                        Sps3OwnedGlobalOptions.VerifyGeneratedRoot(root, extra);
                    }
                    else if (extra.Length != 0) throw Unsafe("the legacy generated root has unowned components");
                }
                else if (poses.TryGetValue(node, out var socket))
                {
                    if (extra.Length != 1 || extra[0] != socket) throw Unsafe("a socket pose has unowned components");
                }
                else if (anchors.Contains(node))
                {
                    if (extra.Length != 1 || !(extra[0] is ParentConstraint) && !VrcConstraintBridge.IsVrcParent(extra[0]) &&
                        extra[0].GetType().FullName != "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy" &&
                        !IsFeature(extra[0], "VF.Model.Feature.ArmatureLink"))
                        throw Unsafe("a socket attachment has unowned components");
                }
                else if (pathAnchors.Contains(node))
                {
                    if (extra.Length != 1 || !(extra[0] is ParentConstraint) && !VrcConstraintBridge.IsVrcParent(extra[0]))
                        throw Unsafe("a guided-path attachment has unowned components");
                }
                else if (extra.Length != 0) throw Unsafe("a guided-path object has unowned components");
            }
        }

        private static bool IsFeature(Component component, string model)
        {
            if (component.GetType().FullName != "VF.Model.VRCFury") return false;
            var data = new SerializedObject(component);
            var content = data.FindProperty("content");
            var features = data.FindProperty("config.features");
            return content != null && content.propertyType == SerializedPropertyType.ManagedReference &&
                content.managedReferenceValue?.GetType().FullName == model &&
                features != null && features.isArray && features.arraySize == 0;
        }

        internal static void ValidateReplacementReferences(Sps2SetupContext root, SetupSettings settings, bool customAvatarFrame = false,
            bool allowAdditionalObjects = false, bool allowExternalReferences = false, bool allowNativeEdits = false)
        {
            // Validate again at the mutation boundary, not just when drawing the selection UI.
            ValidateSourceHierarchy(root.avatar, root, allowAdditionalObjects);
            // Dormant custom-target fields are persisted too, even for excluded parts.
            if (settings.parts.Any(p => p != null && p.target != null && p.target.IsChildOf(root.transform)))
                throw Unsafe("a saved custom target refers to the generated hierarchy being replaced");
            var removed = new HashSet<Object>();
            foreach (var node in root.gameObject.GetComponentsInChildren<Transform>(true))
            {
                removed.Add(node.gameObject);
                foreach (var component in node.GetComponents<Component>()) removed.Add(component);
            }
            var objects = Resources.FindObjectsOfTypeAll<Component>().Cast<Object>()
                .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>());
            foreach (var obj in objects.Distinct())
            {
                if (obj == null || removed.Contains(obj) || SpsMigrationAdapter.IsTransientAnimationSelectionCache(obj)) continue;
                var property = new SerializedObject(obj).GetIterator();
                while (property.Next(true))
                {
                    // Parent-to-child bookkeeping is changed by Unity's remove override itself.
                    // All other Transform/GameObject/component references remain real inputs.
                    if (obj is Transform && property.propertyPath.StartsWith("m_Children.", StringComparison.Ordinal)) continue;
                    if (!allowExternalReferences && property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null && removed.Contains(property.objectReferenceValue))
                        throw Unsafe("external reference from '" + obj.name + "' / " + property.propertyPath);
                }
            }
            FullSetupGenerator.ValidatePrefabGeneratedPayload(root, customAvatarFrame, allowAdditionalObjects, allowNativeEdits);
        }

        internal static void RemoveGeneratedRoot(Sps2SetupContext root)
        {
            if (EditorUtility.IsPersistent(root.gameObject)) throw Unsafe("persistent assets are never removed");
            bool sourceOwned = IsSourceOwned(root);
            var source = sourceOwned ? PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject) : null;
            var instance = sourceOwned ? PrefabUtility.GetNearestPrefabInstanceRoot(root.gameObject) : null;
            var parent = root.transform.parent.gameObject;
            Undo.DestroyObjectImmediate(root.gameObject);
            if (!sourceOwned) return;
            if (source == null || instance == null || PrefabUtility.GetPrefabInstanceStatus(instance) != PrefabInstanceStatus.Connected ||
                !PrefabUtility.GetRemovedGameObjects(instance).Any(r => r.assetGameObject == source && r.parentOfRemovedGameObjectInInstance == parent))
                throw new InvalidOperationException("Unity did not retain the expected removed-object override. Regeneration will be rolled back.");
        }
    }
}
