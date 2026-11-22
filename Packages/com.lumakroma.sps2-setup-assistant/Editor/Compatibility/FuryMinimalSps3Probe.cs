using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // This confirms the saveable component and minimal builder schema only.
    // It does not certify unreviewed advanced actions or runtime behavior.
    internal static class FuryMinimalSps3Probe
    {
        // UI preview only. Mutation paths continue to call Available live.
        private static readonly PreviewProbeMemo previewMemo = new PreviewProbeMemo();
        static FuryMinimalSps3Probe()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, __) => InvalidatePreview();
            AssemblyReloadEvents.beforeAssemblyReload += InvalidatePreview;
            UnityEditor.PackageManager.Events.registeredPackages += change => {
                if (change.added.Concat(change.removed).Concat(change.changedFrom).Concat(change.changedTo)
                    .Any(p => p != null && FuryNativeFeatureShapes.RelevantPackage(p.name))) InvalidatePreview();
            };
        }
        internal static void InvalidatePreview() => previewMemo.Invalidate();
        internal static bool AvailableForPreview(out string reason)
        {
            var result = previewMemo.Read(() => {
                var available = Available(out var why);
                return new PreviewProbeResult { Available = available, Reason = why };
            });
            reason = result.Reason; return result.Available;
        }
        internal static bool Available(out string reason)
        {
            reason = null;
            var socket = VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury));
            if (!FuryPersistentScriptProbe.HasPersistentBinding(socket, SpsBackend.PackageId(SpsFamily.VrcFury), out reason)) return false;
            if (!SpsBackend.PackageMatches(VrcFuryApi.FindType("VF.Builder.VRCFuryBuilder"), SpsFamily.VrcFury, out _))
            { reason = "Fury Socket builder not found"; return false; }
            var channels = socket.GetField("channelSettings", BindingFlags.Public | BindingFlags.Instance);
            if (channels == null || channels.FieldType.FullName != "VF.Model.SpsChannelSettings")
            { reason = "SPS3 channel schema unavailable"; return false; }
            // A broken or absent optional Plug must not prevent a saveable Socket.
            // Check only the fields used by the basic Socket creation path here.
            GameObject staging = null;
            try {
                staging = new GameObject("SPS basic Socket schema probe") { hideFlags = HideFlags.HideAndDontSave };
                var component = staging.AddComponent(socket);
                var data = new SerializedObject(component);
                var name = data.FindProperty("name"); var mode = data.FindProperty("addLight");
                if (name == null || name.propertyType != SerializedPropertyType.String ||
                    mode == null || mode.propertyType != SerializedPropertyType.Enum ||
                    !mode.enumNames.Any(x => x == "Auto" || x == "Ring" || x == "Hole")) {
                    reason = "Basic Socket name or mode schema is unavailable"; return false;
                }
                return true;
            } catch (Exception e) { reason = "Basic Socket schema inspection failed: " + e.GetType().Name; return false; }
            finally { if (staging != null) UnityEngine.Object.DestroyImmediate(staging); }
        }
    }
}
