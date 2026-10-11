using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor.PackageManager;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // One synchronous operation owns the selected family. Nested operations restore it even on failure.
    // UI inspection uses For(family); never changes a persisted setup or picks a family automatically.
    internal static class SpsBackend
    {
        internal const string NdmfVersion = "1.1430.0";
        internal static SpsFamily ActiveFamily { get; private set; }
        internal static string Prefix(SpsFamily family) => family == SpsFamily.SpsForNdmf ? "SpsNdmf" : "VF";
        internal static string PackageId(SpsFamily family) => family == SpsFamily.SpsForNdmf ? "net.nrzk.spsndmf" : "com.vrcfury.vrcfury";
        internal static string Name(SpsFamily family) => family == SpsFamily.SpsForNdmf ? "SPS for NDMF" : "VRCFury";
        internal static string SocketType(SpsFamily family) => Prefix(family) + ".Component.VRCFuryHapticSocket";
        internal static string PlugType(SpsFamily family) => Prefix(family) + ".Component.VRCFuryHapticPlug";
        internal static string GuideUrl(SpsFamily family) => family == SpsFamily.SpsForNdmf
            ? "https://spsndmf.nrzk.net/spsndmf/overview/" : VrcFuryCapabilities.GuideUrl;
        internal static string Guide(SpsFamily family) => family == SpsFamily.SpsForNdmf
            ? L("SPS for NDMF、Modular Avatar、NDMFを導入してください。対応版と導入方法は案内で確認できます。")
            : VrcFuryCapabilities.UpdateGuide;

        internal static void ValidateFamily(SpsFamily family)
        {
            if (family != SpsFamily.VrcFury && family != SpsFamily.SpsForNdmf)
                throw new InvalidOperationException(L("SPS系列の設定が不明です。生成を中止します。"));
        }
        internal static IDisposable Use(SpsFamily family) { ValidateFamily(family); return new Selection(family); }
        private sealed class Selection : IDisposable
        {
            private readonly SpsFamily previous;
            internal Selection(SpsFamily family) { previous = ActiveFamily; ActiveFamily = family; }
            public void Dispose() { ActiveFamily = previous; }
        }
        internal static bool PackageIdMatches(string packageId, SpsFamily family)
        { ValidateFamily(family); return packageId == PackageId(family); }
        internal static bool PackageMatches(Type type, SpsFamily family, out string version)
        {
            var package = type == null ? null : PackageInfo.FindForAssembly(type.Assembly);
            version = package?.version;
            return package != null && PackageIdMatches(package.name, family);
        }
        internal static SpsFamily FamilyOf(Component component)
        {
            foreach (SpsFamily family in Enum.GetValues(typeof(SpsFamily)))
                if (component != null && (component.GetType().FullName == SocketType(family) || component.GetType().FullName == PlugType(family))) return family;
            throw new InvalidOperationException(L("SPS系列の設定が不明です。生成を中止します。"));
        }
        internal static Component[] AllSockets(GameObject avatar) => avatar.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && (c.GetType().FullName == SocketType(SpsFamily.VrcFury) || c.GetType().FullName == SocketType(SpsFamily.SpsForNdmf))).ToArray();
        internal static void ValidateExisting(Sps2SetupContext existing, SpsFamily requested)
        {
            ValidateFamily(requested);
            if (existing != null && existing.settings.family != requested)
                throw new InvalidOperationException(L("既存セットアップの系列は変更できません。別の新規セットアップで選択してください。"));
            if (existing != null && existing.sockets.Any(s => s.socket != null && FamilyOf(s.socket) != requested))
                throw new InvalidOperationException(L("設定とSocketのSPS系列が一致しません。変更を中止します。"));
        }
        internal static string MissingSourceGuidance => L("元の基盤または所有情報を確認できません。既存生成プレハブを手動で削除して新規生成するか、元の依存を再導入して移行してください。自動変更は行いません。");
        internal static bool HasNdmfBuildIntegration => VrcFuryApi.FindType(
            "LumaKroma.Sps2SetupAssistant.Editor.Compatibility.Ndmf.Sps2NdmfPlugin") != null;
    }
}
