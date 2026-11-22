using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal struct PreviewProbeResult
    {
        internal bool Available;
        internal string Reason;
    }
    // The memo never authorizes writes; callers of real generation use the live probe.
    internal sealed class PreviewProbeMemo
    {
        private readonly object gate = new object();
        private int epoch;
        private bool known, capturing;
        private PreviewProbeResult value;
        internal void Invalidate() { lock (gate) { unchecked { epoch++; } known = false; } }
        internal PreviewProbeResult Read(Func<PreviewProbeResult> probe)
        {
            int started;
            lock (gate) {
                if (known) return value;
                if (capturing) throw new InvalidOperationException("Reentrant preview probe.");
                capturing = true; started = epoch;
            }
            PreviewProbeResult result;
            try { result = probe(); }
            catch { lock (gate) { capturing = false; } throw; }
            lock (gate) {
                capturing = false;
                if (started == epoch) { value = result; known = true; }
            }
            return result;
        }
    }
    // Structural evidence only: successful inspection is not evidence of builder
    // semantics, Scene/Prefab round-trip, or permission to replace owned data.
    internal static class FuryNativeFeatureShapes
    {
        // Only known Unity native components can be skipped; unknown types fail closed.
        internal static int ReferenceInspectionDisposition(bool knownNative, bool monoBehaviour) =>
            knownNative ? 0 : monoBehaviour ? 1 : 2;
        internal static bool RecoveryNeedsRepaint(object before, object after) => !ReferenceEquals(before, after);
        internal static bool UiValueChanged(string oldText, int oldKind, string newText, int newKind) =>
            oldKind != newKind || !string.Equals(oldText, newText, StringComparison.Ordinal);
        internal static bool NotificationNeedsRefresh(bool hasBaseline, bool changed, bool inspectionFailed) =>
            !hasBaseline || changed || inspectionFailed;
        internal static bool RelevantPackage(string name) => name == "com.vrcfury.vrcfury" || name == "com.vrchat.avatars" ||
            name == "com.vrchat.base" || name == "nadena.dev.modular-avatar" || name == "nadena.dev.ndmf" ||
            name == "net.nrzk.spsndmf" || name == "com.lumakroma.sps2-setup-assistant";
        internal static bool DraftChanged(object oldSettings, object newSettings, object oldAvatar, object newAvatar, string oldJson, string newJson) =>
            !ReferenceEquals(oldSettings, newSettings) || !ReferenceEquals(oldAvatar, newAvatar) ||
            !string.Equals(oldJson, newJson, StringComparison.Ordinal);
        internal static bool Requested(bool included, bool enabled, bool rewrite) => included && enabled && rewrite;
        internal static bool WriteList(bool depthList, bool rewriteDepth, bool rewriteEntry) =>
            depthList ? rewriteDepth : rewriteEntry;
        internal static bool MissingReferenceInspectionRequired(bool remove, bool depth, bool entry, bool active) =>
            remove || depth || entry || active;
        internal static bool MissingReferenceCheckPasses(Func<bool> detect)
        {
            try { return detect != null && !detect(); }
            catch { return false; }
        }
        internal struct Snapshot {
            internal bool SocketRadius, PathStops, Collapse, PointRadius, DepthActions, DepthApi;
            internal bool DepthMeters, DepthPlugs, DepthLocal, DepthSelf, DepthOthers, DepthSmoothing;
            internal bool RadiusFull, RadiusZero;
        }
        private static FieldInfo Field(Type owner, string name) => owner?.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        private static bool Has(Type owner, string name, Type expected) {
            var field=Field(owner,name);
            return field != null && field.FieldType == expected && !field.IsInitOnly && !field.IsDefined(typeof(ObsoleteAttribute),false);
        }
        private static Type ListItem(Type owner, string name) {
            var field=Field(owner,name); var type=field?.FieldType;
            return field != null && !field.IsInitOnly && !field.IsDefined(typeof(ObsoleteAttribute),false) &&
                type.IsGenericType && type.GetGenericTypeDefinition()==typeof(List<>) ? type.GetGenericArguments()[0] : null;
        }
        internal static Snapshot Inspect(Type socket, Type state) {
            var result = new Snapshot();
            if (socket == null) return result;
            result.SocketRadius = Has(socket,"useRadiusOffset",typeof(bool));
            var stop = ListItem(socket,"guidedPathStops");
            result.PathStops = stop != null && Has(stop,"transform",typeof(Transform));
            result.Collapse = result.PathStops && Has(stop,"shrink",typeof(bool));
            result.PointRadius = result.PathStops && Has(stop,"useRadiusOffset",typeof(bool)) && Has(stop,"maxRadiusOffset",typeof(float));
            var depth = ListItem(socket,"depthActions2");
            result.DepthActions = depth != null && state != null && Has(depth,"range",typeof(Vector2)) && Has(depth,"actionSet",state);
            if (!result.DepthActions) return result;
            var units=Field(depth,"units")?.FieldType;
            bool HasUnit(string value) => units != null && units.IsEnum && Enum.GetNames(units).Contains(value);
            result.DepthMeters=HasUnit("Meters"); result.DepthPlugs=HasUnit("Plugs"); result.DepthLocal=HasUnit("Local");
            result.DepthSelf=Has(depth,"enableSelf",typeof(bool)); result.DepthOthers=Has(depth,"enableOthers",typeof(bool));
            result.DepthSmoothing=Has(depth,"smoothingSeconds",typeof(float));
            result.RadiusFull=Has(depth,"plugRadiusForFullWeight",typeof(float));
            result.RadiusZero=Has(depth,"plugRadiusForZeroWeight",typeof(float));
            return result;
        }
        // Type metadata is immutable for the lifetime of the loaded assembly. Domain
        // reload clears this cache; package ownership and live writes stay uncached.
        private static Type cachedSocketType, cachedStateType;
        private static bool shapeKnown;
        private static Snapshot cachedShape;
        private static Snapshot InspectLoadedShape(Type socket, Type state) {
            if (!shapeKnown || cachedSocketType != socket || cachedStateType != state) {
                var next = Inspect(socket, state);
                cachedSocketType = socket; cachedStateType = state;
                cachedShape = next; shapeKnown = true;
            }
            return cachedShape;
        }
        internal static Snapshot InspectInstalled() {
            try {
                var socket=VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury));
                if (!SpsBackend.PackageMatches(socket,SpsFamily.VrcFury,out _)) return default;
                var result=InspectLoadedShape(socket,VrcFuryApi.FindType("VF.Model.State"));
                var api=VrcFuryApi.FindType("com.vrcfury.api.Components.FurySocket");
                var actions=VrcFuryApi.FindType("com.vrcfury.api.Actions.FuryActionSet");
                var method=api?.GetMethod("AddDepthActions",new[]{typeof(Vector2),typeof(float),typeof(bool)});
                result.DepthApi=actions != null && method != null && method.ReturnType==actions &&
                    SpsBackend.PackageMatches(api,SpsFamily.VrcFury,out _) && SpsBackend.PackageMatches(actions,SpsFamily.VrcFury,out _);
                return result;
            } catch { return default; }
        }
    }
}
