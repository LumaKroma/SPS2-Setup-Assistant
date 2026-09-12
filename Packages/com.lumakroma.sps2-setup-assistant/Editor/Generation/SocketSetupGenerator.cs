using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;

using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static class SocketSetupGenerator
    {
        public const string OwnedRootName = "SPS2 Socket Setup [com.lumakroma.sps2-setup-assistant]";
        public const string SocketPoseName = "Socket Pose";

        public static bool TryGenerate(
            VRCAvatarDescriptor descriptor,
            AttachmentBackend backend,
            IEnumerable<SocketPresetId> enabledPresetIds,
            out GameObject generatedRoot,
            out IReadOnlyList<string> unavailableReasons,
            out string error)
        {
            generatedRoot = null;
            unavailableReasons = Array.Empty<string>();

            if (!VrcFuryCapabilities.Current.CanGenerate)
            {
                error = VrcFuryCapabilities.Current.BlockReason + "\n" + VrcFuryCapabilities.UpdateGuide;
                return false;
            }

            if (!AttachmentBackendRegistry.IsAvailable(backend))
            {
                error = $"The {AttachmentBackendRegistry.GetDisplayName(backend)} attachment backend is unavailable.";
                return false;
            }

            if (!HumanoidSnapshot.TryCapture(descriptor, out var snapshot, out error))
            {
                return false;
            }

            if (!SocketPlacementPlanner.TryCreatePlan(
                    snapshot,
                    enabledPresetIds,
                    out var plan,
                    out unavailableReasons,
                    out error))
            {
                return false;
            }

            if (!TryFindOwnedRoot(descriptor.transform, out var oldRoot, out error))
            {
                return false;
            }

            var animator = descriptor.GetComponent<Animator>();
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set Up SPS2 Sockets");

            try
            {
                if (oldRoot != null)
                {
                    Undo.DestroyObjectImmediate(oldRoot.gameObject);
                }

                generatedRoot = CreateGameObject(OwnedRootName, descriptor.transform);
                ResetLocalTransform(generatedRoot.transform);

                foreach (var placement in plan.Placements)
                {
                    var bone = animator.GetBoneTransform(placement.AnchorBone);
                    if (bone == null)
                    {
                        throw new InvalidOperationException(
                            $"Humanoid bone {placement.AnchorBone} disappeared while generating {placement.Preset.DisplayName}.");
                    }

                    var anchor = CreateGameObject(placement.Preset.NodeName, generatedRoot.transform);
                    anchor.transform.SetPositionAndRotation(bone.position, bone.rotation);
                    ApplyAttachment(anchor, placement.AnchorBone, backend);

                    var socketPose = CreateGameObject(SocketPoseName, anchor.transform);
                    socketPose.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);

                    var socket = UndoComponentRegistration.Invoke(
                        socketPose,
                        "Set Up SPS2 Sockets",
                        () => VrcFuryApi.CreateSocket(socketPose));
                    socket.SetName(placement.Preset.DisplayName);
                    socket.SetMode("Auto");
                }

                Selection.activeGameObject = generatedRoot;
                if (generatedRoot.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(generatedRoot.scene);
                }

                Undo.CollapseUndoOperations(undoGroup);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                generatedRoot = null;
                error = $"Setup was reverted: {exception.Message}";
                return false;
            }
        }

        public static bool TryFindOwnedRoot(Transform avatarRoot, out Transform ownedRoot, out string error)
        {
            ownedRoot = null;
            var matches = new List<Transform>();
            for (var i = 0; i < avatarRoot.childCount; i++)
            {
                var child = avatarRoot.GetChild(i);
                if (child.name == OwnedRootName)
                {
                    matches.Add(child);
                }
            }

            if (matches.Count > 1)
            {
                error = $"Found {matches.Count} exact tool-owned roots. Resolve the duplicate manually before re-running.";
                return false;
            }

            if (matches.Count == 1 && !HasOwnedCatalogSignature(matches[0]))
            {
                error = "The exact-name setup root has an unexpected hierarchy. It will not be replaced automatically.";
                return false;
            }

            ownedRoot = matches.SingleOrDefault();
            error = null;
            return true;
        }

        public static bool HasOwnedCatalogSignature(Transform root)
        {
            if (root == null || root.name != OwnedRootName || root.childCount == 0)
            {
                return false;
            }

            var allowedNames = new HashSet<string>(SocketCatalog.Presets.Select(preset => preset.NodeName));
            var seenNames = new HashSet<string>();
            for (var i = 0; i < root.childCount; i++)
            {
                var anchor = root.GetChild(i);
                if (!allowedNames.Contains(anchor.name) || !seenNames.Add(anchor.name))
                {
                    return false;
                }

                if (anchor.childCount != 1 || anchor.GetChild(0).name != SocketPoseName)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplyAttachment(
            GameObject anchor,
            HumanBodyBones bone,
            AttachmentBackend backend)
        {
            if (!AttachmentBackendRegistry.TryApply(backend, anchor, bone, out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Set Up SPS2 Sockets");
            Undo.SetTransformParent(gameObject.transform, parent, "Set Up SPS2 Sockets");
            return gameObject;
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
