using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed class Sps2SetupAssistantWindow : EditorWindow
    {
        private readonly Dictionary<SocketPresetId, bool> enabledPresets = new Dictionary<SocketPresetId, bool>();
        private VRCAvatarDescriptor descriptor;
        private AttachmentBackend backend;
        private Vector2 scroll;
        private string lastMessage;
        private MessageType lastMessageType;

        [MenuItem("Tools/LumaKroma/SPS2 Setup Assistant")]
        public static void Open()
        {
            var window = GetWindow<Sps2SetupAssistantWindow>();
            window.titleContent = new GUIContent("SPS2 Setup");
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            foreach (var preset in SocketCatalog.Presets)
            {
                if (!enabledPresets.ContainsKey(preset.Id))
                {
                    enabledPresets.Add(preset.Id, true);
                }
            }

            TryAdoptSelection();
        }

        private void OnSelectionChange()
        {
            if (descriptor == null)
            {
                TryAdoptSelection();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EnsureSelectedBackendIsAvailable();

            EditorGUILayout.LabelField("SPS2 Setup Assistant", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Private 0.1.0 PoC. Creates one removable, tool-owned Socket setup root. " +
                "Initial transforms are editable first placements, not fit guarantees.",
                MessageType.Info);

            descriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
                "Avatar Descriptor",
                descriptor,
                typeof(VRCAvatarDescriptor),
                true);

            var availableBackends = AttachmentBackendRegistry.AvailableBackends;
            var backendIndex = availableBackends.ToList().IndexOf(backend);
            backendIndex = GUILayout.Toolbar(
                backendIndex,
                availableBackends.Select(AttachmentBackendRegistry.GetDisplayName).ToArray());
            backend = availableBackends[backendIndex];

            if (!AttachmentBackendRegistry.IsAvailable(AttachmentBackend.VrcFuryWithModularAvatar))
            {
                EditorGUILayout.HelpBox(
                    "Modular Avatar is not installed at a supported version. The VRCFury-only profile remains fully available.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Socket locations", EditorStyles.boldLabel);

            var snapshotIsValid = HumanoidSnapshot.TryCapture(descriptor, out var snapshot, out var validationError);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(210f));
            foreach (var preset in SocketCatalog.Presets)
            {
                var bone = HumanBodyBones.LastBone;
                var available = snapshotIsValid && snapshot.TryResolveBone(preset, out bone, out _);
                using (new EditorGUI.DisabledScope(!available))
                {
                    var label = available
                        ? $"{preset.DisplayName} ({bone})"
                        : $"{preset.DisplayName} — missing {string.Join(" / ", preset.BoneCandidates)}";
                    enabledPresets[preset.Id] = EditorGUILayout.ToggleLeft(label, enabledPresets[preset.Id]);
                }
            }
            EditorGUILayout.EndScrollView();

            if (!snapshotIsValid)
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            if (!string.IsNullOrWhiteSpace(lastMessage))
            {
                EditorGUILayout.HelpBox(lastMessage, lastMessageType);
            }

            using (new EditorGUI.DisabledScope(!snapshotIsValid))
            {
                if (GUILayout.Button("Set Up Sockets", GUILayout.Height(36f)))
                {
                    Generate();
                }
            }

            EditorGUILayout.HelpBox(
                "Advanced Radius Offset, tags, Guided Path, depth actions and legacy settings remain in the native VRCFury Inspector.",
                MessageType.None);
        }

        private void Generate()
        {
            var enabled = enabledPresets.Where(pair => pair.Value).Select(pair => pair.Key).ToArray();
            if (SocketSetupGenerator.TryGenerate(
                    descriptor,
                    backend,
                    enabled,
                    out var root,
                    out var unavailable,
                    out var error))
            {
                lastMessage = $"Created {root.transform.childCount} independent Socket locations.";
                if (unavailable.Count > 0)
                {
                    lastMessage += " Unavailable: " + string.Join(" ", unavailable);
                }

                lastMessageType = MessageType.Info;
            }
            else
            {
                lastMessage = error;
                lastMessageType = MessageType.Error;
            }
        }

        private void TryAdoptSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                descriptor = selected.GetComponentInParent<VRCAvatarDescriptor>();
            }
        }

        private void EnsureSelectedBackendIsAvailable()
        {
            if (!AttachmentBackendRegistry.IsAvailable(backend))
            {
                backend = AttachmentBackendRegistry.AvailableBackends[0];
            }
        }
    }
}
