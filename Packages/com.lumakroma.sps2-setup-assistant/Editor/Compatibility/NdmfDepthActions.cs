using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Shipped SPSNDMF 1.1430.0 has no PublicApi. Only this boundary constructs its
    // observed serialized models. No dependency builders or implementation are invoked/copied.
    internal sealed class NdmfDepthActions
    {
        private readonly SerializedObject socket;
        private const string Group = "depthActions2.Array.data[0].";
        private NdmfDepthActions(Component component) { socket = new SerializedObject(component); }
        private static SerializedProperty Require(SerializedObject data, string path, SerializedPropertyType kind) =>
            VrcFuryCompatibility.Require(data, path, kind);
        internal static Type ModelType(string name)
        {
            var type = VrcFuryApi.FindType("SpsNdmf.Model." + name);
            if (type == null || type.Assembly.GetName().Name != "SPSNDMF" ||
                !SpsBackend.PackageMatches(type, SpsFamily.SpsForNdmf, out var version) || version != SpsBackend.NdmfVersion)
                throw new InvalidOperationException(L("SPS for NDMFの設定schemaを確認できません。生成を中止します。"));
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
                throw new InvalidOperationException(L("SPS for NDMFの設定schemaを確認できません。生成を中止します。"));
            version.SetValue(result, latest.Invoke(result, Array.Empty<object>()));
            return result;
        }
        internal static NdmfDepthActions Create(Component component, Vector2 range, float smoothing, bool self)
        {
            if (SpsBackend.FamilyOf(component) != SpsFamily.SpsForNdmf) throw new InvalidOperationException("SPSNDMF Socket required");
            var result = new NdmfDepthActions(component);
            Require(result.socket, "depthActions2", SerializedPropertyType.Generic).arraySize = 1;
            Require(result.socket, Group + "range", SerializedPropertyType.Vector2).vector2Value = range;
            Require(result.socket, Group + "smoothingSeconds", SerializedPropertyType.Float).floatValue = smoothing;
            Require(result.socket, Group + "enableSelf", SerializedPropertyType.Boolean).boolValue = self;
            Require(result.socket, Group + "reverseClip", SerializedPropertyType.Boolean).boolValue = false;
            var units = Require(result.socket, Group + "units", SerializedPropertyType.Enum);
            if (Array.IndexOf(units.enumNames, "Meters") < 0 || Array.IndexOf(units.enumNames, "Plugs") < 0 || Array.IndexOf(units.enumNames, "Local") < 0)
                throw new InvalidOperationException(L("SPS for NDMFの設定schemaを確認できません。生成を中止します。"));
            units.enumValueIndex = Array.IndexOf(units.enumNames, "Meters");
            Require(result.socket, Group + "actionSet.actions", SerializedPropertyType.Generic).arraySize = 0;
            result.Save();
            return result;
        }
        private string Add(string name, object value = null)
        {
            var list = Require(socket, Group + "actionSet.actions", SerializedPropertyType.Generic);
            int index = list.arraySize; list.arraySize++;
            string path = Group + "actionSet.actions.Array.data[" + index + "]";
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
        }
        internal void AddTurnOn(GameObject obj)
        {
            var path = Add("StateAction.ObjectToggleAction");
            Require(socket, path + "obj", SerializedPropertyType.ObjectReference).objectReferenceValue = obj;
            var mode = Require(socket, path + "mode", SerializedPropertyType.Enum);
            int on = Array.IndexOf(mode.enumNames, "TurnOn");
            if (on < 0 || Array.IndexOf(mode.enumNames, "TurnOff") < 0) throw new InvalidOperationException("SPSNDMF Object action schema");
            mode.enumValueIndex = on;
            Save();
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
        }
        internal static bool Probe(Type socketType)
        {
            GameObject staging = null;
            try
            {
                staging = new GameObject("SPSNDMF depth schema probe") { hideFlags = HideFlags.HideAndDontSave };
                var actions = Create(staging.AddComponent(socketType), Vector2.zero, 0, true);
                actions.AddBlendshape("", 0, null); actions.AddTurnOn(null); actions.AddAnimationClip(null);
                return true;
            }
            catch { return false; }
            finally { if (staging != null) UnityEngine.Object.DestroyImmediate(staging); }
        }
    }
}
