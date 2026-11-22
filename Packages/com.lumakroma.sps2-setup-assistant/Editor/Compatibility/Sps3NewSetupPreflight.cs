using System;
using System.Collections.Generic;
using System.Linq;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Existing settings only. No new persisted family, option, asset schema or native generation permission.
    internal sealed class Sps3NewSetupPreflight
    {
        internal IReadOnlyList<string> InputBlockers { get; }
        internal IReadOnlyList<string> ActivationBlockers { get; }
        internal IReadOnlyList<Sps3SettingsPartReadPlan> Parts { get; }
        internal bool ExactBeta14 { get; }
        internal bool CanPrepare => InputBlockers.Count == 0;
        internal bool CanApply => false;
        internal bool ConvertsExistingSetup => false;

        private Sps3NewSetupPreflight(SetupSettings settings, bool exactBeta14, bool existing, bool sceneAvatar = true)
        {
            ExactBeta14 = exactBeta14;
            var errors = new List<string>();
            if (!sceneAvatar) errors.Add("MissingSceneAvatar");
            var parts = new List<Sps3SettingsPartReadPlan>();
            if (!exactBeta14) errors.Add("ExactBeta14Required");
            if (existing) errors.Add("ExistingSetupMustRemainUnchanged");
            if (settings == null) errors.Add("MissingSettings");
            else
            {
                if (settings.family != SpsFamily.VrcFury) errors.Add("VrcFuryFamilyRequired");
                if (settings.displayNamesVersion != 1) errors.Add("DisplayNameVersionUnresolved");
                if ((int)settings.menuLanguage < 0 || (int)settings.menuLanguage > 4) errors.Add("UnknownDisplayLanguage");
                if (settings.legacy) errors.Add("AvatarWideLegacyPolicyUnverified");
                if (settings.parts == null) errors.Add("MissingPartsList");
                else
                {
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < settings.parts.Count; i++)
                    {
                        var part = settings.parts[i];
                        if (part == null) { errors.Add("NullPart:" + i); continue; }
                        if (string.IsNullOrEmpty(part.id) || !ids.Add(part.id)) errors.Add("InvalidPartId:" + i);
                        if (!part.included) continue;
                        var read = new Sps3SettingsPartReadPlan(part, settings); parts.Add(read);
                        if (part.IsFingerRing) errors.Add("FingerRingAttachmentUnverified:" + part.id);
                        if (read.RequestsLegacyPath) errors.Add("LegacyPathTangentsNotEquivalent:" + part.id);
                        if (part.animationModelVersion != 0) errors.Add("GroupedAnimationRequiresProductionAdapter:" + part.id);
                        if (part.actions == null) errors.Add("MissingActionsList:" + part.id);
                        if (part.depth)
                        {
                            foreach (var error in read.DepthOutput.Blockers) errors.Add(error + ":" + part.id);
                            if (read.DepthOutput.Commands.Any(c => c.Kind == Sps3DepthOutputKind.AnimationClip))
                                errors.Add("ClipNeedsFinalSocketOwnerValidation:" + part.id);
                        }
                    }
                }
                if (parts.Count == 0) errors.Add("NoSelectedParts");
            }
            InputBlockers = errors.AsReadOnly(); Parts = parts.AsReadOnly();
            ActivationBlockers = Array.AsReadOnly(new[] {
                "PersistedSpsGenerationProvenanceMissing",
                "Beta14BuildAdmissionAndWriteDefaultsParityUnverified",
                "ProductionCommitUndoReloadNotValidated"
            });
        }
        internal static Sps3NewSetupPreflight Inspect(SetupSettings settings, bool exactBeta14, bool existing, bool sceneAvatar = true) =>
            new Sps3NewSetupPreflight(settings, exactBeta14, existing, sceneAvatar);
    }
}
