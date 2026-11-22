using System;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Shape evidence only. These flags do not certify the meaning of an unknown Fury implementation.
    internal static class FuryBasicOptionalCapabilities
    {
        internal struct Snapshot { internal bool SocketRadius, PathStops, Collapse, PointRadius, Depth; }
        internal static bool WantsSocketRadius(string id) => id == "chest" || id == "handLeft" || id == "handRight" ||
            id == "hands" || id == "footLeft" || id == "footRight" || id == "feet";
        internal static Snapshot Inspect(Component component)
        {
            var result = new Snapshot();
            if (component == null) return result;
            try {
                var data = new SerializedObject(component);
                bool Is(string path, SerializedPropertyType type) => data.FindProperty(path)?.propertyType == type;
                result.SocketRadius = Is("useRadiusOffset", SerializedPropertyType.Boolean);
                var stops = data.FindProperty("guidedPathStops");
                if (stops != null && stops.isArray) {
                    // Staged SerializedObject changes are discarded; the component is not modified.
                    stops.arraySize = Math.Max(1, stops.arraySize);
                    const string p = "guidedPathStops.Array.data[0].";
                    result.PathStops = Is(p + "transform", SerializedPropertyType.ObjectReference);
                    result.Collapse = result.PathStops && Is(p + "shrink", SerializedPropertyType.Boolean);
                    result.PointRadius = result.PathStops && Is(p + "useRadiusOffset", SerializedPropertyType.Boolean) &&
                        Is(p + "maxRadiusOffset", SerializedPropertyType.Float);
                    data.Update();
                }
                var depth = data.FindProperty("depthActions2");
                if (depth != null && depth.isArray) {
                    depth.arraySize = Math.Max(1, depth.arraySize);
                    const string p = "depthActions2.Array.data[0].";
                    result.Depth = Is(p + "range", SerializedPropertyType.Vector2) && Is(p + "actionSet", SerializedPropertyType.Generic);
                    data.Update();
                }
            } catch { return default; }
            return result;
        }
        internal static bool TrySetSocketRadius(Component socket, out string reason)
        {
            reason = null;
            bool original = false, wrote = false;
            try {
                if (socket == null || EditorUtility.IsPersistent(socket)) throw new InvalidOperationException("Expected a new staging Socket.");
                var data = new SerializedObject(socket); var property = data.FindProperty("useRadiusOffset");
                if (property == null || property.propertyType != SerializedPropertyType.Boolean) {
                    reason = "Socket radius offset is unavailable; native default retained."; return false;
                }
                original = property.boolValue; property.boolValue = true;
                wrote = true; data.ApplyModifiedPropertiesWithoutUndo();
                var after = new SerializedObject(socket).FindProperty("useRadiusOffset");
                if (after == null || after.propertyType != SerializedPropertyType.Boolean || !after.boolValue)
                    throw new InvalidOperationException("Socket radius offset did not retain its value.");
                return true;
            } catch (Exception e) {
                if (wrote) {
                    var rollback = new SerializedObject(socket); var value = rollback.FindProperty("useRadiusOffset");
                    if (value == null || value.propertyType != SerializedPropertyType.Boolean)
                        throw new InvalidOperationException("Cannot restore staging Socket radius state.", e);
                    value.boolValue = original; rollback.ApplyModifiedPropertiesWithoutUndo();
                    var verify = new SerializedObject(socket).FindProperty("useRadiusOffset");
                    if (verify == null || verify.boolValue != original)
                        throw new InvalidOperationException("Staging Socket radius rollback failed.", e);
                }
                reason = "Socket radius offset not confirmed: " + e.Message; return false;
            }
        }
    }
}
