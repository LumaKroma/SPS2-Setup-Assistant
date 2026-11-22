using System;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static class FuryNormalOutputRouter
    {

        internal struct Route { internal FuryFeatureOutputPlan.Mode Mode; internal string Reason; internal bool Validated; }
        internal static bool UseUnreviewedBasic(bool reviewed, bool minimumAvailable) => !reviewed && minimumAvailable;
        // Test-only override; never assigned in product code.
        internal static Func<bool> ReviewedOverrideForTests;
        internal static bool CanUseBasicAfterContractFailure(bool confirmedNoExisting, bool minimumAvailable) => confirmedNoExisting && minimumAvailable;
        internal static Route Resolve(VRCAvatarDescriptor avatar, SetupSettings settings) => ResolveCore(avatar, settings, false);
        internal static Route ResolvePreview(VRCAvatarDescriptor avatar, SetupSettings settings) => ResolveCore(avatar, settings, true);
        private static Route ResolveCore(VRCAvatarDescriptor avatar, SetupSettings settings, bool preview)
        {
            if (settings == null || settings.family != SpsFamily.VrcFury)
                return new Route { Mode = FuryFeatureOutputPlan.Mode.LegacySps2 };
            bool confirmedNoExisting = false;
            string basicIssue = null;
            try {
                var existing = avatar == null ? null : FullSetupGenerator.Find(avatar);
                confirmedNoExisting = existing == null;
                bool reviewed = ReviewedOverrideForTests?.Invoke() ?? (preview ? FuryMinimalSps3Probe.AvailableForPreview(out _) : SpsAuthoringCompatibility.ReviewedSps3Installed);

                if (existing == null && !reviewed && UseUnreviewedBasic(false, preview ? FuryMinimalSps3Probe.AvailableForPreview(out basicIssue) : FuryMinimalSps3Probe.Available(out basicIssue)))
                    return new Route { Mode = FuryFeatureOutputPlan.Mode.NativeBasicSps3, Reason = "この Fury 版の高度機能は未検証のため、基本 Socket のみ作成します。", Validated = true };
                var contract = existing == null || (preview && existing.settings.family == settings.family &&
                    existing.AuthoringContract == SpsAuthoringContract.Sps3Beta14)
                    ? SpsAuthoringCompatibility.SelectContract(settings.family, existing, preview, preview)
                    : existing.AuthoringContract;
                if (preview) FuryPreviewEnvironmentCache.Require(settings.family, contract);
                else using (SpsBackend.Use(settings.family)) using (SpsAuthoringCompatibility.Use(contract))
                    VrcFuryCompatibility.RequireVersion();
                var socket = VrcFuryApi.FindType(SpsBackend.SocketType(settings.family));
                if (!FuryPersistentScriptProbe.HasPersistentBinding(socket, SpsBackend.PackageId(settings.family), out var issue))
                    return new Route { Mode = FuryFeatureOutputPlan.Mode.PlacementOnly, Reason = issue };
                return new Route { Mode = contract == SpsAuthoringContract.Sps3Beta14 ? FuryFeatureOutputPlan.Mode.NativeSps3 : FuryFeatureOutputPlan.Mode.LegacySps2, Validated = true };
            }
            catch (Exception e) {
                if (confirmedNoExisting && CanUseBasicAfterContractFailure(true, preview ? FuryMinimalSps3Probe.AvailableForPreview(out basicIssue) : FuryMinimalSps3Probe.Available(out basicIssue)))
                    return new Route { Mode = FuryFeatureOutputPlan.Mode.NativeBasicSps3, Reason = "高度機能を確認できないため、基本 Socket のみ作成します: " + e.Message, Validated = true };
                var detail = confirmedNoExisting && !string.IsNullOrEmpty(basicIssue)
                    ? e.Message + " / Basic Socket: " + basicIssue : e.Message;
                return new Route { Mode = FuryFeatureOutputPlan.Mode.PlacementOnly, Reason = detail };
            }
        }
    }
}
