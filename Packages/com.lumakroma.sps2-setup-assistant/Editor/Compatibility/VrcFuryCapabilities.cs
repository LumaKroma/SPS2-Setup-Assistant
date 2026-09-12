using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    public sealed class VrcFuryCapabilities
    {
        private VrcFuryCapabilities() { }
        public const string UpdateGuide = "VCC の Manage Project で VRCFury を最新の安定版に更新してください。未導入の場合は VRCFury の公式導入案内をご確認ください。";
        public const string GuideUrl = "https://vrcfury.com/download/";
        private static VrcFuryCapabilities current;
        public static VrcFuryCapabilities Current => current ?? (current = Inspect());
        public static void Refresh() { current = null; }
        public string Version { get; private set; }
        public string BlockReason { get; private set; }
        public bool HasSps { get; private set; }
        public bool Sps2 { get; private set; }
        public bool Path { get; private set; }
        public bool PathStops { get; private set; }
        public bool Tangents { get; private set; }
        public bool LocalTangents { get; private set; }
        public bool Collapse { get; private set; }
        public bool AutoMode { get; private set; }
        public bool Legacy { get; private set; }
        public bool Instant => Legacy;
        public bool LocalOnly { get; private set; }
        public bool Depth { get; private set; }
        public bool RadiusOffset { get; private set; }
        public bool TestPlug { get; private set; }
        public bool PublicAttachment { get; private set; }
        public bool CanGenerate => string.IsNullOrEmpty(BlockReason);

        private static bool Has(SerializedObject data, string path, SerializedPropertyType type) =>
            data?.FindProperty(path)?.propertyType == type;

        private static VrcFuryCapabilities Inspect() => Inspect(
            VrcFuryApi.FindType(VrcFuryCompatibility.SocketType), VrcFuryApi.FindType(VrcFuryCompatibility.PlugType));

        // Also accepts independently supplied component schemas for contract tests. No test override of Current.
        internal static VrcFuryCapabilities Inspect(Type socketType, Type plugType, bool publicApi = true)
        {
            var result = new VrcFuryCapabilities { Version = "未導入 / 不明" };
            var assembly = socketType?.Assembly ?? VrcFuryApi.FindType("com.vrcfury.api.FuryComponents")?.Assembly;
            var package = assembly == null ? null : UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (package != null) result.Version = package.version;
            GameObject temporary = null;
            try
            {
                if (socketType == null || plugType == null)
                { result.BlockReason = "SPS が見つかりません。プレハブは生成できません。"; return result; }
                temporary = new GameObject("SPS compatibility probe") { hideFlags = HideFlags.HideAndDontSave };
                var plug = new SerializedObject(temporary.AddComponent(plugType));
                result.HasSps = Has(plug, "enableSps", SerializedPropertyType.Boolean) || Has(plug, "configureSps", SerializedPropertyType.Boolean);
                if (!result.HasSps)
                { result.BlockReason = "この VRCFury には SPS が搭載されていません。プレハブは生成できません。"; return result; }
                result.TestPlug = Has(plug, "autoRenderer", SerializedPropertyType.Boolean) &&
                    Has(plug, "configureTpsMesh", SerializedPropertyType.Generic) && Has(plug, "name", SerializedPropertyType.String);
                var socket = new SerializedObject(temporary.AddComponent(socketType));
                result.Sps2 = Has(socket, "useSharedTag", SerializedPropertyType.Boolean);
                if (!Has(socket, "name", SerializedPropertyType.String) || !Has(socket, "addMenuItem", SerializedPropertyType.Boolean) ||
                    !Has(socket, "addLight", SerializedPropertyType.Enum) || !socket.FindProperty("addLight").enumNames.Contains("Ring"))
                { result.BlockReason = "SPS の基本設定構造に対応できません。既存設定を守るため生成を停止します。"; return result; }
                result.AutoMode = Has(socket, "enableAuto", SerializedPropertyType.Boolean);
                // Both SPS1 and SPS2 have native Stealth; absence is also checked on actual build output.
                result.LocalOnly = result.AutoMode;
                result.RadiusOffset = Has(socket, "useRadiusOffset", SerializedPropertyType.Boolean);
                result.PathStops = Has(socket, "guidedPathStops", SerializedPropertyType.Generic);
                result.Path = result.PathStops || Has(socket, "guidedPath", SerializedPropertyType.Generic);
                result.Legacy = result.PathStops && Has(socket, "useLights", SerializedPropertyType.Boolean);
                if (result.PathStops)
                {
                    socket.FindProperty("guidedPathStops").arraySize = 1;
                    result.Collapse = Has(socket, "guidedPathStops.Array.data[0].shrink", SerializedPropertyType.Boolean);
                    const string prefix = "guidedPathStops.Array.data[0].";
                    result.LocalTangents = Has(socket, prefix + "tangentInLocal", SerializedPropertyType.Vector3) &&
                        Has(socket, prefix + "tangentOutLocal", SerializedPropertyType.Vector3);
                    result.Tangents = Has(socket, prefix + "customizeTangentIn", SerializedPropertyType.Boolean) &&
                        Has(socket, prefix + "customizeTangentOut", SerializedPropertyType.Boolean) &&
                        (result.LocalTangents || (Has(socket, prefix + "tangentIn", SerializedPropertyType.Vector3) &&
                        Has(socket, prefix + "tangentOut", SerializedPropertyType.Vector3)));
                }
                var socketApi = VrcFuryApi.FindType("com.vrcfury.api.Components.FurySocket");
                var actionApi = VrcFuryApi.FindType("com.vrcfury.api.Actions.FuryActionSet");
                result.Depth = publicApi && Has(socket, "depthActions2", SerializedPropertyType.Generic) &&
                    socketApi != null && actionApi != null &&
                    VrcFuryApi.Method(VrcFuryApi.FindType("com.vrcfury.api.FuryComponents"), "CreateSocket", typeof(GameObject))?.ReturnType == socketApi &&
                    VrcFuryApi.Method(socketApi, "AddDepthActions", typeof(Vector2), typeof(float), typeof(bool))?.ReturnType == actionApi &&
                    VrcFuryApi.Method(actionApi, "AddBlendshape", typeof(string), typeof(float), typeof(Renderer)) != null &&
                    VrcFuryApi.Method(actionApi, "AddAnimationClip", typeof(AnimationClip)) != null &&
                    VrcFuryApi.Method(actionApi, "AddTurnOn", typeof(GameObject)) != null;
                if (result.Depth)
                {
                    socket.FindProperty("depthActions2").arraySize = 1;
                    const string d = "depthActions2.Array.data[0].";
                    result.Depth = Has(socket, d + "range", SerializedPropertyType.Vector2) &&
                        Has(socket, d + "units", SerializedPropertyType.Enum) &&
                        Has(socket, d + "enableSelf", SerializedPropertyType.Boolean) &&
                        Has(socket, d + "reverseClip", SerializedPropertyType.Boolean) &&
                        Has(socket, d + "smoothingSeconds", SerializedPropertyType.Float);
                }
                var attachmentApi = VrcFuryApi.FindType("com.vrcfury.api.Components.FuryArmatureLink");
                result.PublicAttachment = publicApi && attachmentApi != null && VrcFuryApi.Method(VrcFuryApi.FindType("com.vrcfury.api.FuryComponents"),
                    "CreateArmatureLink", typeof(GameObject))?.ReturnType == attachmentApi &&
                    VrcFuryApi.Method(attachmentApi, "LinkTo", typeof(HumanBodyBones), typeof(string)) != null &&
                    VrcFuryApi.Method(attachmentApi, "SetAlign", typeof(bool)) != null;
                return result;
            }
            catch (Exception e) { result.BlockReason = "VRCFury の互換性を確認できません: " + e.Message; return result; }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }

        public List<string> Notices()
        {
            var notes = new List<string>();
            if (!CanGenerate) { notes.Add(BlockReason); return notes; }
            if (!Sps2) notes.Add("SPS無印（SPS1）を検出しました。対応する基本ソケットは生成できますが、SPS2 への更新を推奨します。");
            void Missing(bool available, string feature) { if (!available) notes.Add(feature + " はこの版では利用できません（生成対象外）。"); }
            Missing(Path, "貫通");
            if (Path) { Missing(Collapse, "首付近で太さを0にする"); Missing(Tangents, "貫通経路の曲線調整（標準の経路補間を使用）"); }
            Missing(AutoMode, "Auto Mode"); Missing(Legacy, "後方互換性の切り替え"); Missing(Instant, "インスタント起動");
            Missing(LocalOnly, "Local Only"); Missing(Depth, "深度アクションの自動設定"); Missing(TestPlug, "テストプラグ");
            if (!RadiusOffset) notes.Add("Radius Offset 非対応のため、標準のソケット配置を使用します。");
            if (!PublicAttachment) notes.Add("追従の公開 API がないため Unity の Parent Constraint を使用します。");
            return notes;
        }

        public SetupSettings Effective(SetupSettings requested, List<string> warnings = null)
        {
            if (!CanGenerate) throw new InvalidOperationException(BlockReason + "\n" + UpdateGuide);
            var result = requested.Copy();
            result.penetration &= Path;
            if (!Collapse) result.showInternalThickness = true;
            result.autoMode &= AutoMode; result.legacy &= Legacy; result.instant &= Instant; result.localOnly &= LocalOnly;
            if (!Depth) foreach (var part in result.parts) part.depth = false;
            if (warnings != null) warnings.AddRange(Notices());
            return result;
        }
    }
}
