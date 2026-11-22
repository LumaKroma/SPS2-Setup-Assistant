using System;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // The generation preflight establishes ownership before copying
    // this state into an existing Socket. Building this draft never edits the target avatar.
    internal sealed class Sps3ActiveAnimationDraft : IDisposable
    {
        private GameObject root;
        internal Component Native { get; private set; }
        internal static Sps3ActiveAnimationDraft Create(IEnumerable<DepthActionSettings> input, List<string> warnings)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var actions = input.Select(a=>a?.Copy()).ToArray();
            foreach(var a in actions) if(a!=null) a.blendShapeMode=BlendShapeMode.Normal; // inactive curve bank is retained in saved inputs
            var type = VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury));
            if (SpsBackend.ActiveFamily != SpsFamily.VrcFury ||
                !SpsBackend.PackageMatches(type, SpsFamily.VrcFury, out _) ||
                !FuryPersistentScriptProbe.HasPersistentBinding(type, SpsBackend.PackageId(SpsFamily.VrcFury), out _))
                throw new InvalidOperationException("Active animation requires a saveable Fury Socket.");
            foreach (var action in actions)
            {
                if (action == null) throw new InvalidOperationException("Missing Active animation action.");
                if (action.kind != DepthActionKind.BlendShape && action.kind != DepthActionKind.Object && action.kind != DepthActionKind.AnimationClip)
                    throw new InvalidOperationException("Unknown Active animation action.");
                if (action.kind == DepthActionKind.BlendShape && (float.IsNaN(action.weight) || float.IsInfinity(action.weight)))
                    throw new InvalidOperationException("Active BlendShape weight must be finite.");
            }
            var caps = FuryActiveShapeProbe.Inspect();
            if (!caps.State) throw new InvalidOperationException("Active animation state schema is unavailable.");
            actions = actions.Where(a => {
                if (GenerationWarningPolicy.Unassigned(a) || caps.Supports(a)) return true;
                warnings?.Add("Unavailable Active operation skipped; the saved input is retained: " + a.kind);
                return false;
            }).ToArray();
            var draft = new Sps3ActiveAnimationDraft();
            try
            {
                draft.root = new GameObject("Owned SPS Active draft") { hideFlags = HideFlags.HideAndDontSave };
                var wrapper = VrcFuryApi.CreateSocket(draft.root);
                draft.Native = draft.root.GetComponent(type);
                if (draft.Native == null) throw new InvalidOperationException("Native Socket creation failed.");
                var output = wrapper.GetActiveActions();
                var off = new List<int>(); int index = 0;
                var bindings = new HashSet<string>();
                if (actions.Length > 1 && actions.Any(a=>a.kind==DepthActionKind.AnimationClip))
                    warnings?.Add(L("Active Animation のClipと他のアクションが同じ対象を書き換えないか確認してください。"));
                foreach (var action in actions)
                {
                    if (GenerationWarningPolicy.Unassigned(action)) { warnings?.Add("Assign a target for the Active action; this empty action was skipped."); continue; }
                    string binding = action.kind==DepthActionKind.Object ? "object:"+action.target.GetInstanceID() :
                        action.kind==DepthActionKind.BlendShape ? "shape:"+action.renderer.GetInstanceID()+":"+action.shape : null;
                    if(binding!=null&&!bindings.Add(binding))warnings?.Add(L("Active Animation で同じ対象が重複しています。値が競合しないよう整理してください。"));
                    switch (action.kind)
                    {
                        case DepthActionKind.BlendShape: output.AddBlendshape(action.shape, Mathf.Clamp(action.weight, 0, 100), action.renderer); break;
                        case DepthActionKind.AnimationClip: output.AddAnimationClip(action.clip); break;
                        case DepthActionKind.Object: output.AddTurnOn(action.target); if (!action.objectOn) off.Add(index); break;
                    }
                    index++;
                }
                // Public API only exposes TurnOn. Change only actions in this owned draft.
                var data = new SerializedObject(draft.Native);
                foreach (int i in off)
                {
                    var mode = data.FindProperty($"activeActions.actions.Array.data[{i}].mode");
                    if (mode == null || mode.propertyType != SerializedPropertyType.Enum)
                        throw new InvalidOperationException("Active Object action schema changed.");
                    int value = Array.IndexOf(mode.enumNames, "TurnOff");
                    if (value < 0) throw new InvalidOperationException("Native TurnOff is unavailable.");
                    mode.enumValueIndex = value;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                return draft;
            }
            catch { draft.Dispose(); throw; }
        }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; Native = null;
        }
    }
}
