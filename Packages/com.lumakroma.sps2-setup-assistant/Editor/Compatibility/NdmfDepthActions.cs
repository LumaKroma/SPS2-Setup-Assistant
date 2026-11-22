using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Shipped SPSNDMF 1.1430.0 has no PublicApi. Only this boundary constructs its
    // observed serialized models. No dependency builders or implementation are invoked/copied.
    internal enum NdmfActionKind { BlendShape, Object, Clip }

    // Pure decision layer; independent bits are retained even when one is absent.
    internal readonly struct NdmfActionCapabilities
    {
        internal readonly bool BlendShape, Object, Clip;
        internal NdmfActionCapabilities(bool blendShape, bool objectAction, bool clip)
        { BlendShape = blendShape; Object = objectAction; Clip = clip; }
        internal bool Supports(NdmfActionKind kind) => kind == NdmfActionKind.BlendShape ? BlendShape :
            kind == NdmfActionKind.Object ? Object : kind == NdmfActionKind.Clip && Clip;
        internal bool Any => BlendShape || Object || Clip;
    }

    internal sealed class NdmfDepthActions
    {
        private readonly SerializedObject socket;
        private string Group;
        private string ActionList => Group == null ? "activeActions.actions" : Group + "actionSet.actions";
        private NdmfDepthActions(Component component) { socket = new SerializedObject(component); }
        private static SerializedProperty Require(SerializedObject data, string path, SerializedPropertyType kind) =>
            VrcFuryCompatibility.Require(data, path, kind);
        internal static Type ModelType(string name)
        {
            var type = VrcFuryApi.FindType("SpsNdmf.Model." + name);
            if (type == null ||
                !SpsBackend.PackageMatches(type, SpsFamily.SpsForNdmf, out _))
                throw new InvalidOperationException(L("このアニメーションの保存形式を確認できません。"));
            return type;
        }
        private static object Model(string name)
        {
            var type = ModelType(name);
            var result = Activator.CreateInstance(type);
            // Public inherited version contract avoids the dependency's legacy migration
            // changing a freshly authored TurnOn into Toggle after serialization/reload.
            var version = type.GetProperty("Version", BindingFlags.Public | BindingFlags.Instance);
            var latest = VrcFuryApi.Method(type, "GetLatestVersion");
            if (version?.PropertyType != typeof(int) || version.SetMethod == null || latest?.ReturnType != typeof(int))
                throw new InvalidOperationException(L("このアニメーションの保存形式を確認できません。"));
            version.SetValue(result, latest.Invoke(result, Array.Empty<object>()));
            return result;
        }
        internal static NdmfDepthActions Create(Component component, Vector2 range, float smoothing, bool self, string unit = "Meters")
        {
            if (SpsBackend.FamilyOf(component) != SpsFamily.SpsForNdmf) throw new InvalidOperationException("SPSNDMF Socket required");
            var result = new NdmfDepthActions(component);
            var field = component.GetType().GetField("depthActions2");
            var rows = field?.GetValue(component) as IList;
            if (rows == null || !field.FieldType.IsGenericType)
                throw new InvalidOperationException("Missing native depth list.");
            int index = rows.Count;
            rows.Add(Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
            result.socket.Update();
            result.Group = "depthActions2.Array.data[" + index + "].";
            string Group = result.Group;
            Require(result.socket, Group + "range", SerializedPropertyType.Vector2).vector2Value = range;
            Require(result.socket, Group + "smoothingSeconds", SerializedPropertyType.Float).floatValue = smoothing;
            Require(result.socket, Group + "enableSelf", SerializedPropertyType.Boolean).boolValue = self;
            Require(result.socket, Group + "reverseClip", SerializedPropertyType.Boolean).boolValue = false;
            var units = Require(result.socket, Group + "units", SerializedPropertyType.Enum);
            if (Array.IndexOf(units.enumNames, unit) < 0)
                throw new InvalidOperationException(L("このアニメーションの保存形式を確認できません。"));
            units.enumValueIndex = Array.IndexOf(units.enumNames, unit);
            Require(result.socket, Group + "actionSet.actions", SerializedPropertyType.Generic).arraySize = 0;
            result.Save();
            return result;
        }
        private string Add(string name, object value = null)
        {
            var list = Require(socket, ActionList, SerializedPropertyType.Generic);
            int index = list.arraySize; list.arraySize++;
            string path = ActionList + ".Array.data[" + index + "]";
            Require(socket, path, SerializedPropertyType.ManagedReference).managedReferenceValue = value ?? Model(name);
            Save();
            return path + ".";
        }
        private void Save() { socket.ApplyModifiedPropertiesWithoutUndo(); socket.Update(); }
        internal void AddBlendshape(string shape, float weight, Renderer renderer)
        {
            var path = Add("StateAction.BlendShapeAction");
            Require(socket, path + "blendShape", SerializedPropertyType.String).stringValue = shape;
            Require(socket, path + "blendShapeValue", SerializedPropertyType.Float).floatValue = weight;
            Require(socket, path + "renderer", SerializedPropertyType.ObjectReference).objectReferenceValue = renderer;
            Require(socket, path + "allRenderers", SerializedPropertyType.Boolean).boolValue = false;
            Save();
            if (Require(socket, path + "blendShape", SerializedPropertyType.String).stringValue != shape ||
                Require(socket, path + "blendShapeValue", SerializedPropertyType.Float).floatValue != weight ||
                Require(socket, path + "renderer", SerializedPropertyType.ObjectReference).objectReferenceValue != renderer ||
                Require(socket, path + "allRenderers", SerializedPropertyType.Boolean).boolValue)
                throw new InvalidOperationException("BlendShape の読み戻し結果が一致しません");
        }
        internal void AddTurnOn(GameObject obj, bool enabled = true)
        {
            var path = Add("StateAction.ObjectToggleAction");
            Require(socket, path + "obj", SerializedPropertyType.ObjectReference).objectReferenceValue = obj;
            var mode = Require(socket, path + "mode", SerializedPropertyType.Enum);
            int on = Array.IndexOf(mode.enumNames, enabled ? "TurnOn" : "TurnOff");
            if (on < 0) throw new InvalidOperationException("SPSNDMF Object action schema");
            mode.enumValueIndex = on;
            Save();
            var written = Require(socket, path + "mode", SerializedPropertyType.Enum);
            if (written.enumValueIndex != on || Require(socket, path + "obj", SerializedPropertyType.ObjectReference).objectReferenceValue != obj)
                throw new InvalidOperationException("Object Action の読み戻し結果が一致しません");
        }
        internal void AddAnimationClip(AnimationClip clip)
        {
            var action = Model("StateAction.AnimationClipAction");
            var wrapper = Model("GuidAnimationClip");
            var field = action.GetType().GetField("clip", BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != wrapper.GetType()) throw new InvalidOperationException("SPSNDMF Clip action schema");
            field.SetValue(action, wrapper);
            var path = Add("StateAction.AnimationClipAction", action);
            Require(socket, path + "clip.objRef", SerializedPropertyType.ObjectReference).objectReferenceValue = clip;
            string id = "";
            if (clip != null)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long fileId))
                    throw new InvalidOperationException(L("Animation Clip は保存済みアセットを指定してください。"));
                id = guid + ":" + fileId;
            }
            Require(socket, path + "clip.id", SerializedPropertyType.String).stringValue = id;
            Save();
            if (Require(socket, path + "clip.id", SerializedPropertyType.String).stringValue != id ||
                Require(socket, path + "clip.objRef", SerializedPropertyType.ObjectReference).objectReferenceValue != clip)
                throw new InvalidOperationException("Clip の読み戻し結果が一致しません");
        }
        // All objects passed here are fresh staging components. Discard unsupported rows
        // on staging. The caller writes to the real socket only after the plan is complete.
        internal static bool Stage(Component staging, SocketSettings part, List<string> warnings, bool active, string displayName = null)
        {
            bool complete = true;
            void Omit(string detail) {
                complete = false;
                warnings?.Add(string.Format(L("{0}：{1}"), displayName ?? part.name, detail));
            }
            if (!part.AnimationEnabled) return true;
            foreach (var group in part.EffectiveAnimationGroups()) {
                if ((group?.kind == SocketAnimationKind.Active) != active) continue;
                if (active) {
                    var activeRow = new NdmfDepthActions(staging);
                    var listProperty = activeRow.socket.FindProperty(activeRow.ActionList);
                    if (listProperty == null || !listProperty.isArray || group.actions == null) {
                        Omit(L("Active の保存先またはアクションがありません。"));
                        continue;
                    }
                    foreach (var action in group.actions) {
                        int before = activeRow.socket.FindProperty(activeRow.ActionList).arraySize;
                        try { activeRow.WriteAction(action); }
                        catch (Exception e) {
                            TrimStaged(staging, activeRow.ActionList, before);
                            activeRow.socket.Update();
                            Omit(ActionIssue(action, e));
                        }
                    }
                    continue;
                }
                if (group?.kind != SocketAnimationKind.Depth || group.depthSettings == null || group.actions == null) {
                    Omit(L("このアニメーションの保存形式を確認できません。"));
                    continue;
                }
                var values = group.depthSettings;
                var list = new SerializedObject(staging).FindProperty("depthActions2");
                if (list == null || !list.isArray) { Omit(L("Depth の保存先がありません。")); continue; }
                int index = list.arraySize;
                NdmfDepthActions row;
                try {
                    if (!Finite(values.range.x) || !Finite(values.range.y) || values.range.x > values.range.y ||
                        !Finite(values.smoothingSeconds) || values.smoothingSeconds < 0)
                        throw new InvalidOperationException("深度範囲または平滑化時間が無効です");
                    row = Create(staging, values.range, values.smoothingSeconds, values.enableSelf, values.units.ToString());
                } catch (Exception e) {
                    TrimStaged(staging, "depthActions2", index);
                    Omit(SetupFeedback.Error(e, "深度の単位または保存先に対応していません。", optional: true));
                    continue;
                }
                var others = row.socket.FindProperty(row.Group + "enableOthers");
                if (others?.propertyType == SerializedPropertyType.Boolean) others.boolValue = values.enableOthers;
                else if (!values.enableOthers) Omit(L("他者入力の無効化に対応していません。"));
                var low = row.socket.FindProperty(row.Group + "plugRadiusForZeroWeight");
                var high = row.socket.FindProperty(row.Group + "plugRadiusForFullWeight");
                if (low?.propertyType == SerializedPropertyType.Float && high?.propertyType == SerializedPropertyType.Float) {
                    if (!values.radiusWeightEnabled) { low.floatValue = 0; high.floatValue = 0; }
                    else if (!values.radiusAutoCorrection && !values.radiusKeepNative && Finite(values.radiusZero) && Finite(values.radiusFull) &&
                        values.radiusZero >= 0 && values.radiusFull > 0 && values.radiusZero <= values.radiusFull) {
                        low.floatValue = values.radiusZero;
                        high.floatValue = values.radiusFull;
                    } else Omit(L("半径はネイティブの初期値を使用します。"));
                } else if (values.radiusWeightEnabled) Omit(L("半径重みの保存先がありません。"));
                row.Save();
                int count = 0;
                foreach (var action in group.actions) {
                    try {
                        row.WriteAction(action);
                        count++;
                    } catch (Exception e) {
                        TrimStaged(staging, row.Group + "actionSet.actions", count);
                        row.socket.Update();
                        Omit(ActionIssue(action, e));
                    }
                }
                if (count == 0) TrimStaged(staging, "depthActions2", index);
            }
            return complete;
        }
        private static string ActionIssue(DepthActionSettings action, Exception error)
        {
            string field = action == null ? L("アクション") : action.kind == DepthActionKind.Object ? L("オブジェクト") :
                action.kind == DepthActionKind.AnimationClip ? "Animation Clip" : "BlendShape";
            return string.Format(L("{0}：{1}"), field, SetupFeedback.Error(error, "このアクションの保存先に対応していません。", optional: true));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void TrimStaged(Component staging, string path, int count)
        {
            var data = new SerializedObject(staging);
            var list = Require(data, path, SerializedPropertyType.Generic);
            if (list.arraySize < count) throw new InvalidOperationException("Staged rows changed unexpectedly.");
            list.arraySize = count;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private void WriteAction(DepthActionSettings action)
        {
            if (action == null) throw new InvalidOperationException("未設定です");
            switch (action.kind) {
                case DepthActionKind.BlendShape:
                    if (action.blendShapeMode != BlendShapeMode.Normal || action.renderer == null ||
                        action.renderer.sharedMesh == null || string.IsNullOrEmpty(action.shape) ||
                        action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0 || !Finite(action.weight))
                        throw new InvalidOperationException("通常の BlendShape と有効な Renderer が必要です");
                    AddBlendshape(action.shape, Mathf.Clamp(action.weight, 0, 100), action.renderer);
                    break;
                case DepthActionKind.Object:
                    if (action.target == null) throw new InvalidOperationException("対象が未設定です");
                    AddTurnOn(action.target, action.objectOn);
                    break;
                case DepthActionKind.AnimationClip:
                    if (action.clip == null) throw new InvalidOperationException("Clip が未設定です");
                    AddAnimationClip(action.clip);
                    break;
                default: throw new InvalidOperationException("不明な Action です");
            }
        }

        // Probe independent feature shapes on separate transient components so a failure
        // in a clip model cannot be mistaken for a missing BlendShape or object model.
        internal static bool ProbeAction(Type socketType, NdmfActionKind kind)
        {
            GameObject staging = null;
            try
            {
                staging = new GameObject("SPSNDMF action schema probe") { hideFlags = HideFlags.HideAndDontSave };
                var component = staging.AddComponent(socketType);
                var field = component.GetType().GetField("depthActions2");
                var unit = field?.FieldType.IsGenericType == true ?
                    field.FieldType.GetGenericArguments()[0].GetField("units")?.FieldType : null;
                if (unit == null || !unit.IsEnum) return false;
                string supported = Array.Find(Enum.GetNames(unit), n => n == "Meters" || n == "Local" || n == "Plugs");
                if (supported == null) return false;
                var actions = Create(component, Vector2.zero, 0, true, supported);
                switch (kind) {
                    case NdmfActionKind.BlendShape: actions.AddBlendshape("Setup-probe", 37, null); break;
                    case NdmfActionKind.Object: actions.AddTurnOn(staging); break;
                    case NdmfActionKind.Clip: actions.AddAnimationClip(null); break;
                    default: return false;
                }
                return true;
            }
            catch { return false; }
            finally { if (staging != null) UnityEngine.Object.DestroyImmediate(staging); }
        }
        internal static NdmfActionCapabilities InspectActions(Type socketType) => new NdmfActionCapabilities(
            ProbeAction(socketType, NdmfActionKind.BlendShape),
            ProbeAction(socketType, NdmfActionKind.Object),
            ProbeAction(socketType, NdmfActionKind.Clip));


    }
}
