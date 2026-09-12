using System;
using System.Collections.Generic;
using System.Linq;


using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;

using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Issue 121's explicit exception. All dependency-private serialization stays here.
    public static class VrcFuryCompatibility
    {
        public const string SupportedVersion = "1.1403.0";
        internal const string SocketType = "VF.Component.VRCFuryHapticSocket";
        internal const string PlugType = "VF.Component.VRCFuryHapticPlug";

        public static void RequireVersion()
        {
            var capabilities = VrcFuryCapabilities.Current;
            if (!capabilities.CanGenerate)
                throw new InvalidOperationException(capabilities.BlockReason + "\n" + VrcFuryCapabilities.UpdateGuide);
        }

        internal static SerializedProperty Require(SerializedObject data, string path, SerializedPropertyType type)
        {
            var p = data.FindProperty(path);
            if (p == null || p.propertyType != type)
                throw new InvalidOperationException("VRCFury の設定構造が一致しません: " + path);
            return p;
        }

        private static void SetEnum(SerializedProperty property, string name)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("VRCFury enum schema mismatch.");
            int index = Array.IndexOf(property.enumNames, name);
            if (index < 0) throw new InvalidOperationException("VRCFury enum value missing: " + name);
            property.enumValueIndex = index;
        }

        public static Component FindSocket(GameObject obj) => obj.GetComponents<Component>()
            .SingleOrDefault(c => c != null && c.GetType().FullName == SocketType);

        internal static Component[] AllSockets(GameObject avatar) => avatar.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && c.GetType().FullName == SocketType).ToArray();

        private static SerializedObject SocketData(Component component)
        {
            if (component == null || component.GetType().FullName != SocketType)
                throw new InvalidOperationException("所有 Socket の参照が失われています。");
            var data = new SerializedObject(component);
            Require(data, "name", SerializedPropertyType.String);
            Require(data, "addMenuItem", SerializedPropertyType.Boolean);
            return data;
        }

        public static Component CreateSocket(GameObject pose, SocketSettings part, SetupSettings setup, List<string> warnings)
        {
            RequireVersion();
            var wrapper = UndoComponentRegistration.Invoke(pose, "SPS2 セットアップ", () => VrcFuryApi.CreateSocket(pose));
            wrapper.SetName(part.name);
            wrapper.SetMode(part.id == "mouth" || part.id == "anus" ? "Ring" : "Auto");
            if (part.id == "chest" || part.id == "handLeft" || part.id == "handRight" || part.id == "hands" ||
                part.id == "footLeft" || part.id == "footRight" || part.id == "feet") wrapper.UseRadiusOffset();
            var component = FindSocket(pose);
            Configure(component, part, setup, warnings);
            return component;
        }

        public static void Configure(Component component, SocketSettings part, SetupSettings setup, List<string> warnings)
        {
            RequireVersion();
            var target = SocketData(component);
            if (target.FindProperty("depthActions2")?.arraySize > 1)
                throw new InvalidOperationException(part.name + ": 手動で追加された深度グループがあります。設定を保持するため、変更を中止しました。");
            var temporary = new GameObject("SPS2 configuration staging") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var wrapper = VrcFuryApi.CreateSocket(temporary);
                var offIndices = new List<int>();
                if (part.depth)
                {
                    var actions = wrapper.AddDepthActions(part.range, 0, true);
                    int count = 0;
                    foreach (var action in part.actions)
                    {
                        switch (action.kind)
                        {
                            case DepthActionKind.BlendShape:
                                if (action.renderer == null || action.renderer.sharedMesh == null ||
                                    string.IsNullOrEmpty(action.shape) || action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0)
                                { warnings.Add(part.name + ": BlendShape が未設定のため、このアクションを省きました。"); continue; }
                                actions.AddBlendshape(action.shape, Mathf.Clamp(action.weight, 0, 100), action.renderer);
                                break;
                            case DepthActionKind.AnimationClip:
                                if (action.clip == null) { warnings.Add(part.name + ": Clip が未設定です。"); continue; }
                                actions.AddAnimationClip(action.clip);
                                break;
                            case DepthActionKind.Object:
                                if (action.target == null) { warnings.Add(part.name + ": オブジェクトが未設定です。"); continue; }
                                actions.AddTurnOn(action.target);
                                if (!action.objectOn) offIndices.Add(count);
                                break;
                            default: throw new InvalidOperationException("Unknown depth action.");
                        }
                        count++;
                    }
                }
                var source = SocketData(FindSocket(temporary));
                if (part.depth)
                {
                    SetEnum(Require(source, "depthActions2.Array.data[0].units", SerializedPropertyType.Enum), part.units.ToString());
                    Require(source, "depthActions2.Array.data[0].enableSelf", SerializedPropertyType.Boolean).boolValue = true;
                    Require(source, "depthActions2.Array.data[0].smoothingSeconds", SerializedPropertyType.Float).floatValue = 0;
                    Require(source, "depthActions2.Array.data[0].reverseClip", SerializedPropertyType.Boolean).boolValue = false;
                    foreach (int index in offIndices)
                        SetEnum(Require(source, $"depthActions2.Array.data[0].actionSet.actions.Array.data[{index}].mode", SerializedPropertyType.Enum), "TurnOff");
                    source.ApplyModifiedPropertiesWithoutUndo();
                    source.Update();
                }
                Undo.RecordObject(component, "SPS2 設定反映");
                target.FindProperty("name").stringValue = part.name;
                SetOptionalBool(target, "enableAuto", setup.autoMode);
                if (VrcFuryCapabilities.Current.Legacy) SetOptionalBool(target, "useLights", setup.legacy);
                target.FindProperty("addMenuItem").boolValue = true;
                if (VrcFuryCapabilities.Current.Depth) CopyDepthActions(source, target);
                target.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static void CopyDepthActions(SerializedObject source, SerializedObject target)
        {
            var from = Require(source, "depthActions2", SerializedPropertyType.Generic);
            var to = Require(target, "depthActions2", SerializedPropertyType.Generic);
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                string path = $"depthActions2.Array.data[{i}].";
                Require(target, path + "range", SerializedPropertyType.Vector2).vector2Value = Require(source, path + "range", SerializedPropertyType.Vector2).vector2Value;
                Require(target, path + "units", SerializedPropertyType.Enum).enumValueIndex = Require(source, path + "units", SerializedPropertyType.Enum).enumValueIndex;
                foreach (string field in new[] { "enableSelf", "reverseClip" })
                    Require(target, path + field, SerializedPropertyType.Boolean).boolValue = Require(source, path + field, SerializedPropertyType.Boolean).boolValue;
                Require(target, path + "smoothingSeconds", SerializedPropertyType.Float).floatValue = Require(source, path + "smoothingSeconds", SerializedPropertyType.Float).floatValue;
                var actions = Require(source, path + "actionSet.actions", SerializedPropertyType.Generic);
                var destination = Require(target, path + "actionSet.actions", SerializedPropertyType.Generic);
                destination.arraySize = actions.arraySize;
                for (int n = 0; n < actions.arraySize; n++)
                {
                    string actionPath = path + $"actionSet.actions.Array.data[{n}]";
                    // Generic array copying cannot initialize polymorphic SerializeReference entries.
                    Require(target, actionPath, SerializedPropertyType.ManagedReference).managedReferenceValue =
                        Require(source, actionPath, SerializedPropertyType.ManagedReference).managedReferenceValue;
                }
            }
        }

        public static void ConfigureCommon(Component component, SetupSettings setup)
        {
            var data = SocketData(component);
            Undo.RecordObject(component, "SPS2 共通設定");
            SetOptionalBool(data, "enableAuto", setup.autoMode);
            if (VrcFuryCapabilities.Current.Legacy) SetOptionalBool(data, "useLights", setup.legacy);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static void SetOptionalBool(SerializedObject data, string path, bool value)
        {
            if (data.FindProperty(path) != null) Require(data, path, SerializedPropertyType.Boolean).boolValue = value;
        }

        public static void SetPath(Component socket, Transform[] stops, Transform avatar, bool collapseInternal = false)
        {
            if (stops.Length > 3) throw new InvalidOperationException("Guided Path は最大3通過点です。");
            foreach (var stop in stops)
                if (stop == null || !stop.IsChildOf(avatar) || AllSockets(stop.gameObject).Length != 0)
                    throw new InvalidOperationException("Guided Path の参照が不正です。");
            var data = SocketData(socket);
            var caps = VrcFuryCapabilities.Current;
            var array = data.FindProperty(caps.PathStops ? "guidedPathStops" : "guidedPath");
            if (array == null)
            {
                if (stops.Length != 0) throw new InvalidOperationException("この版では貫通経路を利用できません。");
                return;
            }
            Undo.RecordObject(socket, "SPS2 貫通設定");
            array.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
            {
                if (!caps.PathStops) { array.GetArrayElementAtIndex(i).objectReferenceValue = stops[i]; continue; }
                var prefix = $"guidedPathStops.Array.data[{i}].";
                Require(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue = stops[i];
                SetOptionalBool(data, prefix + "shrink", collapseInternal);
                if (caps.Tangents)
                {
                    SetOptionalBool(data, prefix + "customizeTangentIn", false);
                    SetOptionalBool(data, prefix + "customizeTangentOut", false);
                    Require(data, prefix + (caps.LocalTangents ? "tangentInLocal" : "tangentIn"), SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
                    Require(data, prefix + (caps.LocalTangents ? "tangentOutLocal" : "tangentOut"), SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
                }
            }
            if (caps.LocalTangents) SetOptionalBool(data, "offsetsInLocalUnits", true);
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        public static void SetPathCollapse(Component socket, bool collapse, int uncollapsedSegment = -1)
        {
            if (!VrcFuryCapabilities.Current.Collapse) return;
            RequireVersion(); var data = SocketData(socket);
            Undo.RecordObject(socket, "SPS2 体内の太さ");
            var count = data.FindProperty("guidedPathStops").arraySize;
            for (int i = 0; i < count; i++)
                Require(data, $"guidedPathStops.Array.data[{i}].shrink", SerializedPropertyType.Boolean).boolValue = collapse && i != uncollapsedSegment;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }
        public static void SetPathTangents(Component socket, int segment, Vector3 exit, Vector3 enter)
        {
            var caps = VrcFuryCapabilities.Current;
            if (!caps.Tangents) return;
            RequireVersion(); var data = SocketData(socket);
            if (segment < 0 || segment >= data.FindProperty("guidedPathStops").arraySize)
                throw new InvalidOperationException("貫通経路の区間がありません。");
            Undo.RecordObject(socket, "SPS2 貫通経路の接線");
            string prefix = $"guidedPathStops.Array.data[{segment}].";
            Require(data, prefix + "customizeTangentOut", SerializedPropertyType.Boolean).boolValue = true;
            Require(data, prefix + "customizeTangentIn", SerializedPropertyType.Boolean).boolValue = true;
            if (caps.LocalTangents)
            {
                var stop = Require(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue as Transform;
                var previous = segment == 0 ? socket.transform :
                    Require(data, $"guidedPathStops.Array.data[{segment - 1}].transform", SerializedPropertyType.ObjectReference).objectReferenceValue as Transform;
                if (stop == null || previous == null || Mathf.Abs(stop.lossyScale.x) < 1e-6f || Mathf.Abs(previous.lossyScale.x) < 1e-6f)
                    throw new InvalidOperationException("経路の参照またはスケールが不正です。");
                exit /= previous.lossyScale.x; enter /= stop.lossyScale.x;
                SetOptionalBool(data, "offsetsInLocalUnits", true);
            }
            Require(data, prefix + (caps.LocalTangents ? "tangentOutLocal" : "tangentOut"), SerializedPropertyType.Vector3).vector3Value = exit;
            Require(data, prefix + (caps.LocalTangents ? "tangentInLocal" : "tangentIn"), SerializedPropertyType.Vector3).vector3Value = enter;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static string ReadIdentity(Component socket)
        {
            var data = new SerializedObject(socket);
            var property = data.FindProperty("oscId");
            if (property != null && property.propertyType == SerializedPropertyType.String &&
                Sps2SetupStorage.TryToken(property.stringValue, out _, out _)) return property.stringValue;
            var name = data.FindProperty("name");
            return name != null && name.propertyType == SerializedPropertyType.String &&
                Sps2SetupStorage.TryToken(name.stringValue, out _, out _) ? name.stringValue : null;
        }
        internal static void SetAuthoringIdentity(Component socket, string identity)
        {
            var data = SocketData(socket);
            var property = data.FindProperty("oscId") ?? data.FindProperty("name");
            if (property.stringValue == identity) return;
            Undo.RecordObject(socket, "SPS2 設定識別子");
            property.stringValue = identity;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static void SetBuildToken(Component socket, string token)
        {
            var data = SocketData(socket);
            data.FindProperty("name").stringValue = token;
            var identity = data.FindProperty("oscId");
            if (identity != null) identity.stringValue = token;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ValidateBuildTokens(GameObject avatar, Sps2SetupContext root)
        {
            var tokens = new HashSet<string>(root.sockets.Select(s => "__SPS2_" + root.identity + "_" + s.id));
            if (tokens.Count != root.sockets.Count) throw new InvalidOperationException("SPS2 の部位識別子が重複しています。");
            foreach (var socket in AllSockets(avatar))
            {
                if (root.sockets.Any(s => s.socket == socket)) continue;
                var data = SocketData(socket);
                if (tokens.Contains(data.FindProperty("name").stringValue) || tokens.Contains(data.FindProperty("oscId")?.stringValue))
                    throw new InvalidOperationException("SPS2 のビルド識別子と既存 Socket が衝突しています。");
            }
            foreach (var plug in avatar.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().FullName == PlugType))
                if (tokens.Contains(new SerializedObject(plug).FindProperty("name")?.stringValue))
                    throw new InvalidOperationException("SPS2 のビルド識別子と Plug 名が衝突しています。");
        }

        internal static int AutoSocketCount(GameObject avatar) => AllSockets(avatar).Count(s =>
        {
            var data = SocketData(s);
            return data.FindProperty("addMenuItem").boolValue && data.FindProperty("enableAuto")?.boolValue == true;
        });

        public static Component CreateTestPlug(GameObject obj, Renderer[] renderers, string label = "SPS2 テストプラグ")
        {
            RequireVersion();
            if (!VrcFuryCapabilities.Current.TestPlug) throw new InvalidOperationException("この版ではテストプラグを生成できません。" + VrcFuryCapabilities.UpdateGuide);
            var type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().SingleOrDefault(t => t.FullName == PlugType);
            if (type == null) throw new InvalidOperationException("対応する VRCFury Plug が見つかりません。");
            var plug = Undo.AddComponent(obj, type);
            var data = new SerializedObject(plug);
            Require(data, data.FindProperty("enableSps") != null ? "enableSps" : "configureSps", SerializedPropertyType.Boolean).boolValue = true;
            Require(data, "autoRenderer", SerializedPropertyType.Boolean).boolValue = false;
            var list = Require(data, "configureTpsMesh", SerializedPropertyType.Generic);
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            Require(data, "name", SerializedPropertyType.String).stringValue = label;
            data.ApplyModifiedProperties();
            return plug;
        }
    }
}
