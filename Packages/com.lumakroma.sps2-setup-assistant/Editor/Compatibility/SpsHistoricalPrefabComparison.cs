using System;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Native-schema comparison boundary. Callers retain ownership and cleanup of temporary objects.
    internal static class SpsHistoricalPrefabComparison
    {
        internal static bool IsLegacyFuryArmatureLink(Component component) =>
            component != null && component.GetType().FullName == "VF.Model.VRCFury";
        internal static bool IsModularAvatarBoneProxy(Component component) =>
            component != null && component.GetType().FullName == "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy";

        // Raw 1.1403-era sockets can reach beta14 without the native Inspector/Builder Upgrade.
        // Normalize only a disposable serialized copy with the exact pinned native function.
        // Unmodeled fields are copied too, so full canonical comparison still rejects them.
        internal static Component PrepareHistoricalComparison(Sps2SetupContext root, GeneratedSocket generated, out GameObject owned)
        {
            owned = null;
            var source = new SerializedObject(generated.socket);
            if (root.AuthoringContract != SpsAuthoringContract.LegacySps2 ||
                VrcFuryCompatibility.Require(source, "offsetsInLocalUnits", SerializedPropertyType.Boolean).boolValue)
                return generated.socket;
            Sps3Beta14SocketAdapter.RequireObservedNativeContract();
            if (VrcFuryCompatibility.Require(source, "version", SerializedPropertyType.Integer).intValue != 8)
                throw new InvalidOperationException("Only the verified version-8 historical socket schema can be compared before native upgrade.");
            if (!VrcFuryCompatibility.Require(source, "legacyOffsetLocal", SerializedPropertyType.Vector3).vector3Value.Equals(Vector3.zero))
                throw new InvalidOperationException("Historical raw local offset conflicts with unupgraded data.");
            var stops = VrcFuryCompatibility.Require(source, "guidedPathStops", SerializedPropertyType.Generic);
            for (int i = 0; i < stops.arraySize; i++)
                foreach (var field in new[] { "tangentInLocal", "tangentOutLocal" })
                    if (!VrcFuryCompatibility.Require(source, "guidedPathStops.Array.data[" + i + "]." + field,
                        SerializedPropertyType.Vector3).vector3Value.Equals(Vector3.zero))
                        throw new InvalidOperationException("Historical tangent residue conflicts with unupgraded local data: " + field);
            string before = EditorJsonUtility.ToJson(generated.socket);
            owned = new GameObject("SPS historical comparison") { hideFlags = HideFlags.HideAndDontSave };
            owned.transform.SetPositionAndRotation(generated.pose.position, generated.pose.rotation);
            owned.transform.localScale = generated.pose.lossyScale;
            var copy = owned.AddComponent(generated.socket.GetType());
            EditorUtility.CopySerializedManagedFieldsOnly(generated.socket, copy);
            var copyData = new SerializedObject(copy);
            VrcFuryCompatibility.Require(copyData, "m_Enabled", SerializedPropertyType.Boolean).boolValue =
                VrcFuryCompatibility.Require(source, "m_Enabled", SerializedPropertyType.Boolean).boolValue;
            copyData.ApplyModifiedPropertiesWithoutUndo();
            // Runtime VRCFuryHapticSocket.Upgrade(8), not an Inspector upgrade of source assets.
            VrcFuryApi.Call(copy, "Upgrade", new[] { typeof(int) }, 8);
            if (!VrcFuryCompatibility.Require(new SerializedObject(copy), "offsetsInLocalUnits", SerializedPropertyType.Boolean).boolValue ||
                EditorJsonUtility.ToJson(generated.socket) != before)
                throw new InvalidOperationException("Historical comparison did not preserve source data during its native-copy upgrade.");
            return copy;
        }

        // Comparison-only normalization of the pinned beta14 Upgrade's retained obsolete data.
        // Never change source/native generation, and never excuse arbitrary custom offsets.
        internal static void CopyVerifiedLegacyPathUpgradeResidue(Sps2SetupContext root, GeneratedSocket generated, Component expected)
        {
            if (root.AuthoringContract != SpsAuthoringContract.LegacySps2 || generated.pathStops.Length == 0) return;
            if (expected == null || expected == generated.socket || EditorUtility.IsPersistent(expected))
                throw new InvalidOperationException("Historical comparison requires a separate non-persistent destination.");
            Sps3Beta14SocketAdapter.RequireObservedNativeContract();
            var source = new SerializedObject(generated.socket);
            var destination = new SerializedObject(expected);
            if (!VrcFuryCompatibility.Require(source, "offsetsInLocalUnits", SerializedPropertyType.Boolean).boolValue)
                throw new InvalidOperationException("Historical native path offsets have not been upgraded.");
            for (int i = 0; i < generated.pathStops.Length; i++)
            {
                foreach (bool entering in new[] { false, true })
                {
                    string field = "guidedPathStops.Array.data[" + i + "]." + (entering ? "tangentIn" : "tangentOut");
                    var obsolete = VrcFuryCompatibility.Require(source, field, SerializedPropertyType.Vector3).vector3Value;
                    if (obsolete.Equals(Vector3.zero)) continue; // Already the fresh component's default.
                    var frame = entering ? generated.pathStops[i] : i == 0 ? generated.pose : generated.pathStops[i - 1];
                    float scale = frame != null ? frame.lossyScale.x : 0;
                    if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0 ||
                        !VrcFuryCompatibility.Require(source, field + "Local", SerializedPropertyType.Vector3).vector3Value.Equals(obsolete / scale))
                        throw new InvalidOperationException("Historical tangent residue does not match the pinned native Upgrade formula: " + field);
                    // Native Upgrade converts meters to local units without clearing the old field.
                    // Admit only that proven duplicate in the temporary comparison component.
                    VrcFuryCompatibility.Require(destination, field, SerializedPropertyType.Vector3).vector3Value = obsolete;
                }
            }
            destination.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
