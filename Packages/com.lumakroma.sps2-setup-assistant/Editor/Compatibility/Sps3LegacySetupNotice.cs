using System;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal enum Sps3LegacySetupState { None, VerifiedLegacySetup, NeedsInspection }

    // Read-only evidence policy. Neither this notice nor a recognized version grants generation.
    internal sealed class Sps3LegacySetupNotice
    {
        internal Sps3LegacySetupState State { get; }
        internal string Identity { get; }
        internal bool CanRegenerate => false;
        private Sps3LegacySetupNotice(Sps3LegacySetupState state, string identity = null)
        { State = state; Identity = identity; }

        internal static Sps3LegacySetupNotice None => new Sps3LegacySetupNotice(Sps3LegacySetupState.None);
        internal static Sps3LegacySetupNotice Unverified => new Sps3LegacySetupNotice(Sps3LegacySetupState.NeedsInspection);

        internal static Sps3LegacySetupNotice Evaluate(bool candidateSeen, int rootCount,
            bool contextValidated, int schema, string identity, string assetIdentity,
            int recordedSockets, int connectedSockets, bool eligibleFamily)
        {
            if (!candidateSeen) return None;
            if (!contextValidated || rootCount != 1 || schema != 1 ||
                !Guid.TryParseExact(identity, "N", out _) ||
                !string.Equals(identity, assetIdentity, StringComparison.Ordinal) ||
                recordedSockets <= 0 || recordedSockets != connectedSockets) return Unverified;
            return eligibleFamily
                ? new Sps3LegacySetupNotice(Sps3LegacySetupState.VerifiedLegacySetup, identity) : None;
        }

        internal string Message(DisplayLanguage language) =>
            Localization.Sps2Localization.L("旧 Setup の設定と生成 Socket の対応を確認できません。SPS3 向けの再生成はできません。設定アセットと元の依存パッケージを確認してください。", language);
    }
}
