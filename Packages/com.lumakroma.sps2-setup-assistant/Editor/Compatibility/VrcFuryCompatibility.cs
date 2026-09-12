using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.api;
using com.vrcfury.api.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Issue 121's explicit exception. All dependency-private serialization stays here.
    public static class VrcFuryCompatibility
    {
        public const string SupportedVersion = "1.1403.0";
        internal const string SocketType = "VF.Component.VRCFuryHapticSocket";
        private const string PlugType = "VF.Component.VRCFuryHapticPlug";

        public static void RequireVersion()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FuryComponents).Assembly);
            if (package == null || package.name != "com.vrcfury.vrcfury" || package.version != SupportedVersion)
                throw new InvalidOperationException($"この構成は VRCFury {SupportedVersion} 用です。現在の版では変更を行いません。");
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
            Require(data, "oscId", SerializedPropertyType.String);
            Require(data, "enableAuto", SerializedPropertyType.Boolean);
            Require(data, "addMenuItem", SerializedPropertyType.Boolean);
            Require(data, "useLights", SerializedPropertyType.Boolean);
            Require(data, "guidedPathStops", SerializedPropertyType.Generic);
            Require(data, "depthActions2", SerializedPropertyType.Generic);
            return data;
        }

        public static Component CreateSocket(GameObject pose, SocketSettings part, SetupSettings setup, List<string> warnings)
        {
            RequireVersion();
            var wrapper = UndoComponentRegistration.Invoke(pose, "SPS2 セットアップ", () => FuryComponents.CreateSocket(pose));
            wrapper.SetName(part.name);
            wrapper.SetMode(part.id == "mouth" || part.id == "anus" ? FurySocket.Mode.Ring : FurySocket.Mode.Auto);
            var component = FindSocket(pose);
            Configure(component, part, setup, warnings);
            return component;
        }

        public static void Configure(Component component, SocketSettings part, SetupSettings setup, List<string> warnings)
        {
            RequireVersion();
            var target = SocketData(component);
            if (target.FindProperty("depthActions2").arraySize > 1)
                throw new InvalidOperationException(part.name + ": 手動で追加された深度グループがあります。設定を保持するため、変更を中止しました。");
            var temporary = new GameObject("SPS2 configuration staging") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var wrapper = FuryComponents.CreateSocket(temporary);
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
                target.FindProperty("enableAuto").boolValue = setup.autoMode;
                target.FindProperty("useLights").boolValue = setup.legacy;
                target.FindProperty("addMenuItem").boolValue = true;
                target.CopyFromSerializedProperty(source.FindProperty("depthActions2"));
                target.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        public static void ConfigureCommon(Component component, SetupSettings setup)
        {
            var data = SocketData(component);
            Undo.RecordObject(component, "SPS2 共通設定");
            data.FindProperty("enableAuto").boolValue = setup.autoMode;
            data.FindProperty("useLights").boolValue = setup.legacy;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        public static void SetPath(Component socket, Transform[] stops, Transform avatar)
        {
            if (stops.Length > 3) throw new InvalidOperationException("Guided Path は最大3通過点です。");
            foreach (var stop in stops)
                if (stop == null || !stop.IsChildOf(avatar) || AllSockets(stop.gameObject).Length != 0)
                    throw new InvalidOperationException("Guided Path の参照が不正です。");
            var data = SocketData(socket);
            var array = data.FindProperty("guidedPathStops");
            Undo.RecordObject(socket, "SPS2 貫通設定");
            array.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
            {
                var prefix = $"guidedPathStops.Array.data[{i}].";
                Require(data, prefix + "transform", SerializedPropertyType.ObjectReference).objectReferenceValue = stops[i];
                Require(data, prefix + "shrink", SerializedPropertyType.Boolean).boolValue = false;
                Require(data, prefix + "customizeTangentIn", SerializedPropertyType.Boolean).boolValue = false;
                Require(data, prefix + "customizeTangentOut", SerializedPropertyType.Boolean).boolValue = false;
                Require(data, prefix + "tangentIn", SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
                Require(data, prefix + "tangentOut", SerializedPropertyType.Vector3).vector3Value = Vector3.zero;
            }
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static string ReadIdentity(Component socket)
        {
            var property = new SerializedObject(socket).FindProperty("oscId");
            return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue : null;
        }
        internal static void SetAuthoringIdentity(Component socket, string identity)
        {
            var data = SocketData(socket);
            if (data.FindProperty("oscId").stringValue == identity) return;
            Undo.RecordObject(socket, "SPS2 設定識別子");
            data.FindProperty("oscId").stringValue = identity;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }

        internal static void SetBuildToken(Component socket, string token)
        {
            var data = SocketData(socket);
            data.FindProperty("name").stringValue = token;
            data.FindProperty("oscId").stringValue = token;
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
                if (tokens.Contains(data.FindProperty("name").stringValue) || tokens.Contains(data.FindProperty("oscId").stringValue))
                    throw new InvalidOperationException("SPS2 のビルド識別子と既存 Socket が衝突しています。");
            }
        }

        internal static int AutoSocketCount(GameObject avatar) => AllSockets(avatar).Count(s =>
        {
            var data = SocketData(s);
            return data.FindProperty("addMenuItem").boolValue && data.FindProperty("enableAuto").boolValue;
        });

        public static Component CreateTestPlug(GameObject obj, Renderer[] renderers)
        {
            RequireVersion();
            var type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().SingleOrDefault(t => t.FullName == PlugType);
            if (type == null) throw new InvalidOperationException("対応する VRCFury Plug が見つかりません。");
            var plug = Undo.AddComponent(obj, type);
            var data = new SerializedObject(plug);
            Require(data, "enableSps", SerializedPropertyType.Boolean).boolValue = true;
            Require(data, "autoRenderer", SerializedPropertyType.Boolean).boolValue = false;
            var list = Require(data, "configureTpsMesh", SerializedPropertyType.Generic);
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            Require(data, "name", SerializedPropertyType.String).stringValue = "SPS2 テストプラグ";
            data.ApplyModifiedProperties();
            return plug;
        }
    }
}
