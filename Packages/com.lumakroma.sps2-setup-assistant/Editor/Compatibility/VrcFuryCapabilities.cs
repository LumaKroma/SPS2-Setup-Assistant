using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
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
        public static string UpdateGuide => L("VRCFury の導入状態と Setup の確認結果を確認してください。既存の設定と生成物はそのまま保持します。");
        public const string GuideUrl = "https://vrcfury.com/download/";
        private static readonly Dictionary<SpsFamily, VrcFuryCapabilities> Cache = new Dictionary<SpsFamily, VrcFuryCapabilities>();
        public SpsFamily Family { get; private set; }
        private bool IsNativeContract { get; set; }
        public static VrcFuryCapabilities Current => ForAuthoring(SpsBackend.ActiveFamily, SpsAuthoringCompatibility.ActiveContract);
        public static VrcFuryCapabilities For(SpsFamily family)
        {
            SpsBackend.ValidateFamily(family);
            if (Cache.TryGetValue(family, out var result)) return result;
            using (SpsBackend.Use(family)) result = Inspect();
            Cache[family] = result;
            return result;
        }
        internal static VrcFuryCapabilities ForAuthoring(SpsFamily family, Generation.SpsAuthoringContract contract, bool preview = false)
        {
            SpsAuthoringCompatibility.ValidateContract(contract);
            if (contract == Generation.SpsAuthoringContract.LegacySps2) return For(family);
            var shape = family == SpsFamily.VrcFury ? FuryNativeFeatureShapes.InspectInstalled() : default;
            bool available = family == SpsFamily.VrcFury && (preview ? FuryMinimalSps3Probe.AvailableForPreview(out _) : SpsAuthoringCompatibility.ReviewedSps3Installed);
            return new VrcFuryCapabilities {
                Family = family, Version = InstalledSps3Version(), IsNativeContract = true,
                BlockReason = available ? null :
                    L("既存の SPS3 設定をこの Fury 構成で安全に再生成できるか確認できません。既存の設定と生成物は保持します。"),
                HasSps = true, Sps2 = true, PathStops = shape.PathStops, AutoMode = true, AutoSocketMode = true, Depth = shape.DepthActions && shape.DepthApi, RadiusOffset = shape.SocketRadius, Legacy = true,
                TestPlug = available && SupportsSps3TestPlug(),
                Path = shape.PathStops,
                Collapse = shape.Collapse,
                PublicAttachment = false
            };
        }

        private static bool SupportsSps3TestPlug()
        {
            var type = VrcFuryApi.FindType(SpsBackend.PlugType(SpsFamily.VrcFury));
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type)) return false;
            return type.GetField("enableSps")?.FieldType == typeof(bool) &&
                type.GetField("autoRenderer")?.FieldType == typeof(bool) &&
                type.GetField("configureTpsMesh")?.FieldType == typeof(List<Renderer>) &&
                type.GetField("name")?.FieldType == typeof(string);
        }

        private static string InstalledSps3Version()
        {
            SpsBackend.PackageMatches(VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)), SpsFamily.VrcFury, out var version);
            return version;
        }

        public static void Refresh() { Cache.Clear(); }
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
        public bool AutoSocketMode { get; private set; }
        public bool Legacy { get; private set; }
        public bool LocalOnly { get; private set; }
        internal NdmfActionCapabilities NativeActions { get; private set; }
        public bool Depth { get; private set; }
        public bool RadiusOffset { get; private set; }
        public bool TestPlug { get; private set; }
        private bool nativeBuildIntegration;
        public bool PublicAttachment { get; private set; }
        public bool CanGenerate => string.IsNullOrEmpty(BlockReason);

        private static bool Has(SerializedObject data, string path, SerializedPropertyType type) =>
            data?.FindProperty(path)?.propertyType == type;

        private static VrcFuryCapabilities Inspect()
        {
            var family = SpsBackend.ActiveFamily;
            var socket = VrcFuryApi.FindType(SpsBackend.SocketType(family));
            var plug = VrcFuryApi.FindType(SpsBackend.PlugType(family));
            SpsBackend.PackageMatches(socket, family, out var installedVersion);
            var result = InspectVersion(family, installedVersion, () => Inspect(socket, plug, family == SpsFamily.VrcFury));
            result.Family = family;
            if (!SpsBackend.PackageMatches(socket, family, out var version))
                result.BlockReason = string.Format(L("{0}本体のpackage IDを確認できません。Stubのみでは生成できません。"), SpsBackend.Name(family));
            else if (family == SpsFamily.VrcFury && !SpsBackend.PackageMatches(
                VrcFuryApi.FindType("VF.Builder.VRCFuryBuilder"), family, out _))
                result.BlockReason = string.Format(L("{0}のビルド機能が見つかりません。"), SpsBackend.Name(family));
            else if (family == SpsFamily.SpsForNdmf)
            {
                result.nativeBuildIntegration = SpsBackend.HasNativeNdmfBuildIntegration;
                if (result.CanGenerate)
                {
                    result.NativeActions = NdmfDepthActions.InspectActions(socket);
                    result.Depth = result.NativeActions.Any;
                    if (!ProbeNativeSocketSchema(socket))
                        result.BlockReason = L("SPS for NDMF の基本 Socket 設定または保存用のスクリプト参照を確認できません。");
                }
            }
            return result;
        }

        internal static VrcFuryCapabilities InspectVersion(SpsFamily family, string version, Func<VrcFuryCapabilities> probe)
        {
            var result = probe(); // Version is displayed only; the probe determines actual capability.
            result.Family = family; result.Version = version;
            return result;
        }

        private static bool ProbeNativeSocketSchema(Type socketType)
        {
            GameObject staging = null;
            try
            {
                staging = new GameObject("SPSNDMF socket schema probe") { hideFlags = HideFlags.HideAndDontSave };
                var component = staging.AddComponent(socketType) as MonoBehaviour;
                if (component == null) return false;
                var script = MonoScript.FromMonoBehaviour(component);
                if (script == null || script.GetClass() != socketType ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId) ||
                    string.IsNullOrEmpty(guid) || fileId == 0) return false;
                return ProbeBasicFields(component);
            }
            catch { return false; }
            finally { if (staging != null) UnityEngine.Object.DestroyImmediate(staging); }
        }

        internal static bool ProbeBasicFields(Component component)
        {
            var data = new SerializedObject(component);
            if (!Has(data, "oscId", SerializedPropertyType.String) ||
                !Has(data, "name", SerializedPropertyType.String) ||
                !Has(data, "addMenuItem", SerializedPropertyType.Boolean) ||
                !Has(data, "addLight", SerializedPropertyType.Enum)) return false;
            var mode = data.FindProperty("addLight");
            int index = Array.FindIndex(mode.enumNames, n => n == "Ring" || n == "Hole");
            if (index < 0) return false;
            bool menu = !data.FindProperty("addMenuItem").boolValue;
            mode.enumValueIndex = index;
            data.FindProperty("name").stringValue = "Setup probe";
            data.FindProperty("oscId").stringValue = "setup-probe";
            data.FindProperty("addMenuItem").boolValue = menu;
            data.ApplyModifiedPropertiesWithoutUndo();
            data.Update();
            return data.FindProperty("name").stringValue == "Setup probe" &&
                data.FindProperty("oscId").stringValue == "setup-probe" &&
                data.FindProperty("addMenuItem").boolValue == menu &&
                data.FindProperty("addLight").enumValueIndex == index;
        }

        // Also accepts independently supplied component schemas for contract tests. No test override of Current.
        internal static VrcFuryCapabilities Inspect(Type socketType, Type plugType, bool publicApi = true)
        {
            var result = new VrcFuryCapabilities { Version = L("未導入 / 不明") };
            var assembly = socketType?.Assembly ?? VrcFuryApi.FindType("com.vrcfury.api.FuryComponents")?.Assembly;
            var package = assembly == null ? null : UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (package != null) result.Version = package.version;
            GameObject temporary = null;
            try
            {
                if (socketType == null)
                { result.BlockReason = L("SPS が見つかりません。プレハブは生成できません。"); return result; }
                temporary = new GameObject("SPS compatibility probe") { hideFlags = HideFlags.HideAndDontSave };
                result.HasSps = true; // Socket generation is independent of the test Plug.
                try {
                    var plug = plugType == null ? null : new SerializedObject(temporary.AddComponent(plugType));
                    result.TestPlug = Has(plug, "autoRenderer", SerializedPropertyType.Boolean) &&
                        Has(plug, "configureTpsMesh", SerializedPropertyType.Generic) && Has(plug, "name", SerializedPropertyType.String);
                } catch { result.TestPlug = false; }
                var socket = new SerializedObject(temporary.AddComponent(socketType));
                result.Sps2 = Has(socket, "useSharedTag", SerializedPropertyType.Boolean);
                if (!Has(socket, "name", SerializedPropertyType.String) || !Has(socket, "addMenuItem", SerializedPropertyType.Boolean) ||
                    !Has(socket, "addLight", SerializedPropertyType.Enum) || !socket.FindProperty("addLight").enumNames.Any(n => n == "Ring" || n == "Hole"))
                { result.BlockReason = L("SPS の基本設定構造に対応できません。既存設定を守るため生成を停止します。"); return result; }
                result.AutoSocketMode = socket.FindProperty("addLight").enumNames.Contains("Auto");
                result.AutoMode = Has(socket, "enableAuto", SerializedPropertyType.Boolean);
                // Both SPS1 and SPS2 have native Stealth; absence is also checked on actual build output.
                result.LocalOnly = result.AutoMode;
                result.RadiusOffset = Has(socket, "useRadiusOffset", SerializedPropertyType.Boolean);
                result.PathStops = Has(socket, "guidedPathStops", SerializedPropertyType.Generic);
                result.Path = result.PathStops || Has(socket, "guidedPath", SerializedPropertyType.Generic);
                result.Legacy = (result.PathStops || !publicApi) && Has(socket, "useLights", SerializedPropertyType.Boolean);
                if (result.PathStops && socket.FindProperty("guidedPathStops").isArray)
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
                if (!publicApi) {
                    var stops = socket.FindProperty("guidedPathStops");
                    result.PathStops = stops != null && stops.isArray &&
                        Has(socket, "guidedPathStops.Array.data[0].transform", SerializedPropertyType.ObjectReference);
                    var oldPath = socket.FindProperty("guidedPath");
                    bool oldValid = oldPath != null && oldPath.isArray && oldPath.propertyType == SerializedPropertyType.Generic;
                    if (oldValid) {
                        oldPath.arraySize = 1;
                        oldValid = oldPath.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ObjectReference;
                    }
                    result.Path = result.PathStops || oldValid;
                    result.Collapse &= result.PathStops;
                    result.Tangents &= result.PathStops;
                    result.LocalTangents &= result.PathStops;
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
            catch (Exception e) { result.BlockReason = L("VRCFury の互換性を確認できません: ") + e.Message; return result; }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }

        internal static string BasicSocketNotice() => L(FuryBasicDepthProbe.Available()
            ? "基本 Socket と対応する Active・深度を生成します。貫通と半径による重み付けは省略し、入力は下書きに残します。"
            : "基本 Socket と対応する Active を生成します。深度・貫通・半径による重み付けは省略し、入力は下書きに残します。");

        public List<string> Notices(bool basicSps3 = false)
        {
            var notes = new List<string>();
            if (basicSps3) {
                notes.Add(BasicSocketNotice());
                return notes;
            }
            if (!CanGenerate) { notes.Add(BlockReason); return notes; }
            if (Family == SpsFamily.SpsForNdmf && !nativeBuildIntegration)
                notes.Add(L("SPS for NDMF のビルド連携を確認できません。"));
            if (IsNativeContract) {
                if (!Path) notes.Add(L("貫通経路を保存できません。入力は保持されます。"));
                if (!Depth) notes.Add(L("深度アクションを保存できません。入力は保持されます。"));
                return notes;
            }
            if (!Sps2) notes.Add(L("SPS無印（SPS1）を検出しました。対応する基本ソケットは生成できますが、SPS2 への更新を推奨します。"));
            void Missing(bool available, string feature) { if (!available) notes.Add(feature + L(" はこの版では利用できません（生成対象外）。")); }
            Missing(Path, L("貫通"));
            if (Path) { Missing(Collapse, L("首付近で太さを0にする")); Missing(Tangents, L("貫通経路の曲線調整（標準の経路補間を使用）")); }
            Missing(AutoMode, L("Auto Mode")); Missing(Legacy, L("後方互換性の切り替え"));
            Missing(LocalOnly, L("Local Only")); Missing(Depth, L("深度アクションの自動設定")); Missing(TestPlug, L("テストプラグ"));
            if (!RadiusOffset) notes.Add(L("Radius Offset 非対応のため、標準のソケット配置を使用します。"));
            if (!AutoSocketMode) notes.Add(L("Socket の Auto に対応していないため、Ring または Hole を使用します。"));
            if (!PublicAttachment && Family != SpsFamily.SpsForNdmf) notes.Add(L("追従の公開 API がないため Unity の Parent Constraint を使用します。"));
            return notes;
        }

        public SetupSettings Effective(SetupSettings requested, List<string> warnings = null)
        {
            if (!CanGenerate) throw new InvalidOperationException(BlockReason + "\n" + SpsBackend.Guide(Family));
            if (IsNativeContract && SpsAuthoringCompatibility.ActiveContract == Generation.SpsAuthoringContract.Sps3Beta14)
            {
                // This obsolete flag requested an old menu entry, not a runtime privacy state.
                // Normalize only the new SPS3 output copy; loading and old backend paths stay unchanged.
                var current = requested.Copy();
                current.localOnly = false;
                return current;
            }
            var result = requested.Copy();
            result.penetration &= Path;
            if (!Collapse) result.showInternalThickness = true;
            if (Family == SpsFamily.SpsForNdmf) result.modularAvatar = true;
            result.autoMode &= AutoMode; result.legacy &= Legacy; result.localOnly &= LocalOnly;
            if (!Depth && Family != SpsFamily.SpsForNdmf) foreach (var part in result.parts) part.depth = false;
            if (warnings != null && Family == SpsFamily.SpsForNdmf) {
                void Missing(bool requestedField, bool supported, string label) {
                    if (requestedField && !supported) warnings.Add(string.Format(L("{0}はこの構成では使用できません。"), L(label)));
                }
                Missing(requested.penetration, Path, "貫通");
                Missing(requested.penetration && !requested.showInternalThickness, Collapse, "首付近で太さを0にする");
                Missing(requested.autoMode, AutoMode, "Auto Mode");
                Missing(requested.legacy, Legacy, "後方互換性の切り替え");
                Missing(requested.localOnly, LocalOnly, "Local Only");
                if (!nativeBuildIntegration) warnings.Add(L("SPS for NDMF のビルド連携を確認できません。"));
            } else if (warnings != null) warnings.AddRange(Notices());
            return result;
        }
    }
}
