using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Read/compare native data only inside the existing compatibility boundary.
    internal static class SpsMigrationAdapter
    {
        internal static void ValidateReplacementReferences(VRCAvatarDescriptor avatar, HashSet<Component> replaced)
        {
            // Cross-scene and loaded ScriptableObject references are also live inputs.
            var objects = Resources.FindObjectsOfTypeAll<Component>().Cast<UnityEngine.Object>()
                .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>());
            var assets = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath);
            foreach (var obj in objects.Concat(assets).Distinct())
            {
                if (obj == null || (obj is Component c && replaced.Contains(c))) continue;
                var p = new SerializedObject(obj).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is Component reference && replaced.Contains(reference))
                        throw new InvalidOperationException(L("置換対象への外部参照があるため、基盤切り替えを中止します。") + " " + obj.name + " / " + p.propertyPath);
            }
        }
        private static readonly HashSet<string> UnityFields = new HashSet<string> {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Script" };
        internal static string State(Component component)
        {
            var data = new SerializedObject(component);
            var p = data.GetIterator(); var values = new List<string>();
            while (p.Next(true))
            {
                if (UnityFields.Any(field => p.propertyPath == field || p.propertyPath.StartsWith(field + ".", StringComparison.Ordinal))) continue;
                string value;
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Generic: continue;
                    case SerializedPropertyType.Integer: case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character: value = p.longValue.ToString(); break;
                    case SerializedPropertyType.Boolean: value = p.boolValue.ToString(); break;
                    case SerializedPropertyType.Float: value = p.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.String: value = p.stringValue; break;
                    case SerializedPropertyType.Enum: value = p.enumValueIndex.ToString(); break;
                    case SerializedPropertyType.ObjectReference: value = p.objectReferenceInstanceIDValue.ToString(); break;
                    case SerializedPropertyType.ManagedReference: value = p.managedReferenceFullTypename; break;
                    case SerializedPropertyType.Vector2: value = p.vector2Value.ToString("R"); break;
                    case SerializedPropertyType.Vector3: value = p.vector3Value.ToString("R"); break;
                    case SerializedPropertyType.Vector4: value = p.vector4Value.ToString("R"); break;
                    case SerializedPropertyType.Quaternion: value = p.quaternionValue.ToString("R"); break;
                    case SerializedPropertyType.Color: value = p.colorValue.ToString("R"); break;
                    default: throw new InvalidOperationException(L("手動変更または未対応の基盤固有設定があります。既存の生成物は変更しません。") + p.propertyPath);
                }
                values.Add(p.propertyPath + ":" + p.propertyType + ":" + value);
            }
            return string.Join("\n", values);
        }
        internal static void ValidateEquivalent(Component actual, Component expected)
        {
            if (actual == null || actual.GetType() != expected.GetType())
                throw new InvalidOperationException("Native component type differs: " + actual?.name);
            // Fury's public CreateArmatureLink factory owns this self-reference.
            // Compare the same source anchor identity, rather than the disposable stage anchor.
            var expectedData = new SerializedObject(expected);
            var propBone = expectedData.FindProperty("content.propBone");
            if (propBone != null && propBone.objectReferenceValue == expected.gameObject)
            {
                propBone.objectReferenceValue = actual.gameObject;
                expectedData.ApplyModifiedPropertiesWithoutUndo();
            }
            var actualState = State(actual); var expectedState = State(expected);
            if (actualState != expectedState)
            {
                var actualFields = actualState.Split('\n'); var expectedFields = expectedState.Split('\n');
                var difference = actualFields.Except(expectedFields).FirstOrDefault() ?? "missing field";
                var key = difference.Split(':')[0];
                var expectedValue = expectedFields.FirstOrDefault(v => v.StartsWith(key + ":")) ?? "absent";
                throw new InvalidOperationException("Native settings differ: " + actual.name + " / actual " + difference + " / expected " + expectedValue);
            }
        }
        internal static void CopyKnownPath(Component source, Component destination, Transform[] stops)
        {
            var from = new SerializedObject(source); var to = new SerializedObject(destination);
            var sourceStops = from.FindProperty("guidedPathStops");
            if (sourceStops == null)
            {
                if (stops.Length != 0) throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。"));
                return;
            }
            if (sourceStops.arraySize != stops.Length) throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。"));
            var targetStops = to.FindProperty("guidedPathStops");
            if (targetStops == null)
            {
                if (stops.Length != 0) throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。"));
                return;
            }
            var sourceOffsets = from.FindProperty("offsetsInLocalUnits");
            if (stops.Length > 0 && sourceOffsets != null && !sourceOffsets.boolValue)
                throw new InvalidOperationException("Native offsets have not been upgraded; migration cannot safely preserve the path.");
            targetStops.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
            {
                string prefix = "guidedPathStops.Array.data[" + i + "].";
                if (from.FindProperty(prefix + "transform")?.objectReferenceValue != stops[i])
                    throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。"));
                foreach (string field in new[] { "transform", "shrink", "customizeTangentIn", "customizeTangentOut" })
                {
                    var a = from.FindProperty(prefix + field); var b = to.FindProperty(prefix + field);
                    if (a == null && b == null) continue;
                    if (a == null || b == null || a.propertyType != b.propertyType) throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。") + field);
                    switch (a.propertyType)
                    {
                        case SerializedPropertyType.Boolean: b.boolValue = a.boolValue; break;
                        case SerializedPropertyType.Vector3: b.vector3Value = a.vector3Value; break;
                        case SerializedPropertyType.ObjectReference: b.objectReferenceValue = a.objectReferenceValue; break;
                        default: throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。") + field);
                    }
                }
                Transform previous = i == 0 ? source.transform : stops[i - 1];
                foreach (bool entering in new[] { false, true })
                {
                    string field = entering ? "tangentIn" : "tangentOut";
                    Transform frame = entering ? stops[i] : previous;
                    var a = from.FindProperty(prefix + field + "Local") ?? from.FindProperty(prefix + field);
                    var b = to.FindProperty(prefix + field + "Local") ?? to.FindProperty(prefix + field);
                    if (a == null && b == null) continue;
                    if (a == null || b == null || frame == null) throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。"));
                    // Old Fury offsets use rotation and meters; new local offsets use the full TRS.
                    bool sourceLocal = from.FindProperty(prefix + field + "Local") != null;
                    bool targetLocal = to.FindProperty(prefix + field + "Local") != null;
                    if (sourceLocal == targetLocal) b.vector3Value = a.vector3Value;
                    else b.vector3Value = ConvertTangent(a.vector3Value, frame, sourceLocal, targetLocal);
                }
            }
            var targetOffsets = to.FindProperty("offsetsInLocalUnits");
            if (targetOffsets != null)
                targetOffsets.boolValue = sourceOffsets == null || sourceOffsets.boolValue;
            Undo.RecordObject(destination, "SPS2 migration path");
            to.ApplyModifiedPropertiesWithoutUndo();
        }
        internal static Vector3 ConvertTangent(Vector3 value, Transform frame, bool sourceLocal, bool targetLocal)
        {
            if (sourceLocal == targetLocal) return value;
            var scale = frame.lossyScale;
            if (float.IsNaN(scale.x) || float.IsInfinity(scale.x) || float.IsNaN(scale.y) || float.IsInfinity(scale.y) || float.IsNaN(scale.z) || float.IsInfinity(scale.z) || Mathf.Abs(scale.x * scale.y * scale.z) < 1e-10f)
                throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。"));
            Vector3 world = sourceLocal ? frame.TransformVector(value) : frame.rotation * value;
            return targetLocal ? frame.InverseTransformVector(world) : Quaternion.Inverse(frame.rotation) * world;
        }
        internal static string PlugLabel(Component plug) => new SerializedObject(plug).FindProperty("name")?.stringValue ?? "SPS2 Test Plug";
    }
}
