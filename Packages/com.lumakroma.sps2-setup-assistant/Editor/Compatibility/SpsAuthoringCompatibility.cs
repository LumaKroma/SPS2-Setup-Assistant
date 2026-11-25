using System;
using System.Linq;
using System.Collections.Generic;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Generation provenance is separate from backend family and defaults to legacy for old assets.
    internal static class SpsAuthoringCompatibility
    {
        internal static bool ProductionUiEnabled => true; // Exact beta14 native authoring/build/Undo suite passed; requested settings still validate.
        internal static SpsAuthoringContract ActiveContract { get; private set; }
        internal static bool ReviewedSps3Installed => FuryMinimalSps3Probe.Available(out _);

        internal static void ValidateContract(SpsAuthoringContract contract)
        {
            if (contract != SpsAuthoringContract.LegacySps2 && contract != SpsAuthoringContract.Sps3Beta14)
                throw new InvalidOperationException("Unknown SPS authoring contract. Existing data is unchanged.");
        }
        internal static void RequireVersion(SpsFamily family, SpsAuthoringContract contract)
        {
            ValidateContract(contract);
            if (contract == SpsAuthoringContract.LegacySps2) { SpsBackend.RequireSupportedPackageVersion(family); return; }
            if (family != SpsFamily.VrcFury || !ReviewedSps3Installed)
                throw new InvalidOperationException("A saveable VRCFury SPS3 Socket is required; existing data is unchanged.");
            Sps3Beta14SocketAdapter.RequireObservedNativeContract();
        }
        internal static SpsAuthoringContract SelectContract(SpsFamily family, Sps2SetupContext existing, bool regenerateLegacy = false, bool preview = false)
        {
            if (existing != null) {
                ValidateContract(existing.AuthoringContract);
                if (regenerateLegacy && family == SpsFamily.VrcFury &&
                    existing.settings.family == family && existing.AuthoringContract == SpsAuthoringContract.Sps3Beta14)
                    return SelectContract(family, null, false, preview);
                if (CanMigrateNdmfToSps3(family, existing, preview)) return SpsAuthoringContract.Sps3Beta14;
                return existing.AuthoringContract;
            }
            return family == SpsFamily.VrcFury && (preview ? FuryMinimalSps3Probe.AvailableForPreview(out _) : ReviewedSps3Installed) ? SpsAuthoringContract.Sps3Beta14 : SpsAuthoringContract.LegacySps2;
        }
        internal static bool CanRegenerateLegacy(SpsFamily family, Sps2SetupContext existing, bool preview = false) =>
            family == SpsFamily.VrcFury && existing != null && (preview ? FuryMinimalSps3Probe.AvailableForPreview(out _) : ReviewedSps3Installed) &&
            existing.settings.family == SpsFamily.VrcFury && existing.AuthoringContract == SpsAuthoringContract.LegacySps2;
        internal static bool CanMigrateNdmfToSps3(SpsFamily family, Sps2SetupContext existing, bool preview = false) =>
            family == SpsFamily.VrcFury && (preview ? FuryMinimalSps3Probe.AvailableForPreview(out _) : ReviewedSps3Installed) && existing != null &&
            existing.settings.family == SpsFamily.SpsForNdmf && existing.AuthoringContract == SpsAuthoringContract.LegacySps2;

        internal static void ValidatePrefabBoundary(VRCAvatarDescriptor avatar, Sps2SetupContext existing, bool regenerate = false, bool allowPrefabAdditions = false)
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(avatar.gameObject) != null || PrefabUtility.IsPartOfPrefabAsset(avatar))
                throw new InvalidOperationException(L("Prefabをシーンに配置してから実行してください。元のPrefabアセットは変更しません。"));
            if (SpsPrefabRegeneration.IsSourceOwned(existing))
            {
                if (!regenerate)
                    throw new InvalidOperationException(L("「再生成」で、このシーン内の生成物を更新してください。元のPrefabアセットは保持します。"));
                SpsPrefabRegeneration.ValidateSourceHierarchy(avatar, existing, allowPrefabAdditions);
            }
        }
        internal static IDisposable Use(SpsAuthoringContract contract) => new Selection(contract);
        private sealed class Selection : IDisposable
        {
            private readonly SpsAuthoringContract previous;
            internal Selection(SpsAuthoringContract next) { ValidateContract(next); previous = ActiveContract; ActiveContract = next; }
            public void Dispose() { ActiveContract = previous; }
        }
        internal static VrcFuryCapabilities ForSelection(SpsFamily family, VRCAvatarDescriptor avatar, bool preview = false)
        {
            try { return VrcFuryCapabilities.ForAuthoring(family, SelectContract(family, FullSetupGenerator.Find(avatar), true, preview), preview); }
            catch { return VrcFuryCapabilities.For(family); } // Ambiguous/missing ownership never enables Apply.
        }

        internal static void ValidateRequestedSettings(VRCAvatarDescriptor avatar, SetupSettings settings, Sps2SetupContext existing, bool migratingLegacy = false, bool regenerate = false, ICollection<string> warnings = null, bool allowPrefabAdditions = false, bool validatePlacementScales = true, bool previewOptionalOmission = false)
        {
            if (avatar == null || EditorUtility.IsPersistent(avatar) || !avatar.gameObject.scene.IsValid() ||
                !avatar.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabUtility.IsPartOfPrefabAsset(avatar))
                throw new InvalidOperationException("SPS3 authoring currently requires a loaded scene avatar, not a saved Prefab asset or PlayMode.");
            ValidatePrefabBoundary(avatar, existing, regenerate, allowPrefabAdditions);
            if (existing != null && existing.AuthoringContract != SpsAuthoringContract.Sps3Beta14 && !(migratingLegacy && (CanRegenerateLegacy(settings.family, existing) || CanMigrateNdmfToSps3(settings.family, existing))))
                throw new InvalidOperationException(L("SPS2の設定を移行するには「再生成」を使用してください。現在の設定と生成物は変更していません。"));
            if (settings == null || settings.family != SpsFamily.VrcFury || settings.parts == null || settings.parts.Any(p => p == null))
                throw new InvalidOperationException("Incomplete SPS3 settings.");
            // The obsolete flag is not a privacy control in SPS3. Keep the source draft
            // unchanged but tell the user before generation and in the result message.
            if (settings.localOnly)
                warnings?.Add(L("旧 Local Only 設定は SPS3 に引き継がれません。生成後は Local Only ではありません。元の設定は生成するまで変更しません。"));
            RequireSupportedScaleChain(avatar.transform);
            if (existing != null) foreach (var socket in existing.sockets) RequireSupportedScaleChain(socket.pose);
            // Global SPS Options are independently staged and committed by the
            // generator for both Apply and Regenerate. They do not gate Socket UI.
            bool Owned(Transform value) => value != null && value.GetComponentInParent<VRCAvatarDescriptor>() == avatar;
            // A fresh replacement recreates each slot. Its preview must use the
            // same omission policy as the new-output writer, without mutating it.
            var placementBaseline = previewOptionalOmission && regenerate ? null : existing;
            foreach (var part in settings.parts.Where(p => p.included))
            {
                if (part.IsFingerRing && GenerationWarningPolicy.TryMissingNewRingBones(avatar, part, placementBaseline, out var ringWarning))
                    warnings?.Add(ringWarning);
                else if (part.IsFingerRing) {
                    FingerRingCalibrationUtility.Bones(avatar, part, out var thumb, out var index);
                    if (!FingerRingCalibrationUtility.CanUseFrame(thumb) || !FingerRingCalibrationUtility.CanUseFrame(index)) {
                        if (GenerationWarningPolicy.CanSkipUnusableNewRing(part, placementBaseline)) {
                            warnings?.Add(part.name + ": 指の関節のスケールを使用できないため、この新しいリングは生成せず、他の生成可能な Socket は生成します。");
                            continue;
                        }
                        else throw new InvalidOperationException("Cannot safely preserve the existing or calibrated finger ring with unusable mapped joints: " + part.id);
                    }
                }
                if (settings.penetration && (part.id == "mouth" || part.id == "anus") &&
                    (!Sps3OwnedPath.Supported || (existing != null && existing.AuthoringContract != SpsAuthoringContract.Sps3Beta14))) {
                    if ((!Sps3OwnedPath.Supported && existing == null && !regenerate) || previewOptionalOmission)
                        warnings?.Add(part.name + ": Guided Path は対応を確認できないため省略します。入力設定は保持します。");
                    else throw new InvalidOperationException("SPS3 does not preserve the existing guided-path tangents. Turn Penetration off before applying.");
                }
                if (part.custom && !Owned(part.target)) {
                    if (GenerationWarningPolicy.CanSkipUnassignedCustom(part, existing)) warnings?.Add(part.name + ": " + L(GenerationWarningPolicy.MissingCustomNotice));
                    else throw new InvalidOperationException("Custom target is outside the selected avatar: " + part.id);
                }
                // Match the writer's operation: an unchanged existing group is not rewritten.
                // A newly created or replaced socket still requires all requested groups.
                var prior = !regenerate && existing?.sockets?.Any(s => s != null && s.id == part.id) == true
                    ? existing.settings?.parts?.SingleOrDefault(p => p != null && p.id == part.id) : null;
                bool rewriteDepth = prior == null || !Sps3AnimationGroupNative.SameOutput(prior, part, 3);
                bool rewriteEntry = prior == null || !Sps3AnimationGroupNative.SameOutput(prior, part, 4);
                bool rewriteActive = prior == null || !Sps3AnimationGroupNative.SameOutput(prior, part, 2);
                if (((existing == null && !regenerate) || previewOptionalOmission) && part.animationModelVersion != 0) {
                    foreach (var kind in new[] { SocketAnimationKind.Depth, SocketAnimationKind.PlugEntryRadius, SocketAnimationKind.Active })
                        if (part.AnimationEnabled && part.EffectiveAnimationGroups().Any(g => g.kind == kind) &&
                            !Sps3AnimationGroupNative.CanWriteOptional(part, kind, out var optionalReason))
                            warnings?.Add(part.name + ": " + kind + " は利用できないため省略します。入力設定は保持します: " + optionalReason);
                } else Sps3AnimationGroupNative.RequireCapabilities(part, rewriteDepth, rewriteActive, rewriteEntry);
                if (!previewOptionalOmission) Sps3AnimationGroupNative.RequireNoPendingWeightLoss(part, existing);
                if (!part.AnimationEnabled) continue;
                foreach (var group in part.EffectiveAnimationGroups())
                {
                // Unchanged existing output is preserved, including references that
                // cannot presently be re-resolved; do not validate it as a new write.
                if (existing != null && !regenerate &&
                    (group.kind == SocketAnimationKind.Depth ? !rewriteDepth :
                     group.kind == SocketAnimationKind.PlugEntryRadius ? !rewriteEntry : !rewriteActive)) continue;
                // New output and its display preflight use the same per-kind
                // capability decision as the isolated optional writer.
                if (part.animationModelVersion != 0 && ((existing == null && !regenerate) || previewOptionalOmission) &&
                    !Sps3AnimationGroupNative.CanWriteOptional(part, group.kind, out _)) continue;
                var values = group.ActiveSettings;
                if (group.kind == SocketAnimationKind.Depth && Sps3AnimationGroupNative.WeightPending(values) &&
                    !(group.actions?.Count > 0 && group.actions.All(a => GenerationWarningPolicy.MissingMouthShape(part, group, a))))
                    warnings?.Add(part.name + ": " + L("半径の重み0・1を入力するか、自動補正をオンにしてください。重み付けを適用せず、他の生成を続けます。"));
                if (group.actions == null ||
                    group.kind != SocketAnimationKind.Active && ((values.units != DepthUnits.Meters && values.units != DepthUnits.Plugs && values.units != DepthUnits.Local) ||
                    !Finite(values.range.x) || !Finite(values.range.y) || values.range.x > values.range.y))
                    throw new InvalidOperationException("SPS3 depth requires finite ordered Meters/Plugs/Local values and an action list: " + part.id);
                for (int index = 0; index < group.actions.Count; index++)
                {
                    var action = group.actions[index];
                    if (action == null) throw new InvalidOperationException("Missing depth action: " + part.id);
                    if ((existing == null || previewOptionalOmission || regenerate) &&
                        GenerationWarningPolicy.MissingMouthShape(part, group, action)) {
                        warnings?.Add(string.Format(L("{0}の BlendShape はメッシュまたはシェイプを確認できないため反映できませんでした。"), SocketDisplayNames.Resolve(part, settings.menuLanguage)));
                        continue;
                    }
                    if (GenerationWarningPolicy.CanSkipUnassignedGroupAction(part, group, index, previewOptionalOmission ? null : existing)) {
                        warnings?.Add(part.name + ": " + L(GenerationWarningPolicy.EmptyDepthNotice));
                        continue;
                    }
                    switch (action.kind)
                    {
                        case DepthActionKind.BlendShape:
                            if (action.renderer == null || !Owned(action.renderer.transform) || action.renderer.sharedMesh == null ||
                                string.IsNullOrEmpty(action.shape) || action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0 || !Finite(action.weight))
                                throw new InvalidOperationException("Invalid BlendShape reference: " + part.id);
                            break;
                        case DepthActionKind.Object:
                            if (action.target == null || !Owned(action.target.transform)) throw new InvalidOperationException("Invalid object action reference: " + part.id);
                            break;
                        case DepthActionKind.AnimationClip:
                            // Preserve native owner-relative resolution and the original persistent clip. No copying/rebasing.
                            if (action.clip == null || !AssetDatabase.Contains(action.clip)) throw new InvalidOperationException("Use a saved AnimationClip asset: " + part.id);
                            break;
                        default: throw new InvalidOperationException("Unknown depth action kind: " + part.id);
                    }
                }
            }
            }
            if (validatePlacementScales) FullSetupGenerator.ValidateSps3PlacementScales(avatar, settings);
        }
        internal const string LocalRegenerationScaleReason = "LocalDepthRegenerationScaleUnverified";

        internal static bool HasRetainedActiveLocalDepth(SetupSettings settings, Sps2SetupContext existing) =>
            existing != null && settings.parts.Where(p => p.included && Sps3AnimationGroupNative.HasLocal(p))
                .Any(part => existing.sockets.Any(socket => socket.id == part.id) ||
                    existing.settings?.parts?.Any(saved => saved != null && saved.id == part.id) == true);


        // Actual regeneration only, not the shared SelectionBlocker preview (which probes with
        // regenerate=true for both buttons). Keep safe non-regenerating Apply available.
        internal static void RequireLocalRegenerationScaleProof(SetupSettings settings, Sps2SetupContext existing)
        {
            if (existing == null) return;
            // Recreated ancestry/placement can change native socket-owner scale, even when
            // its current scale is 1 or a finger-ring pose would be restored. We cannot yet
            // prove equivalence here. No output, Options or saved data is modified.
            foreach (var part in settings.parts.Where(p => p.included && Sps3AnimationGroupNative.HasLocal(p)))
                if (existing.sockets.Any(socket => socket.id == part.id) ||
                    existing.settings?.parts?.Any(saved => saved != null && saved.id == part.id) == true)
                    throw new InvalidOperationException(LocalRegenerationScaleReason + ": Local depth regeneration cannot yet prove unchanged socket-owner scale. Use Apply without Regenerate to retain the existing output, or explicitly change/disable this depth setting. Existing output and saved data are unchanged: " + part.id);
        }
        internal static string SelectionBlocker(VRCAvatarDescriptor avatar, SetupSettings settings) => SelectionBlocker(avatar, settings, out _);
        internal static string SelectionBlocker(VRCAvatarDescriptor avatar, SetupSettings settings, out string warning, bool contractAlreadyVerified = false)
        {
            warning = null;
            var warnings = new List<string>();
            if (settings == null || settings.family != SpsFamily.VrcFury || !(contractAlreadyVerified ? FuryMinimalSps3Probe.AvailableForPreview(out _) : ReviewedSps3Installed)) return null;
            if (avatar == null) return null; // Existing avatar-selection notice owns this case.
            try {
                var existing = FullSetupGenerator.Find(avatar);
                var contract = SelectContract(settings.family, existing, true, contractAlreadyVerified);
                if (!contractAlreadyVerified) RequireVersion(settings.family, contract);
                if (contract == SpsAuthoringContract.LegacySps2) return null;
                using var selection = Use(contract);
                ValidateRequestedSettings(avatar, settings, existing, CanRegenerateLegacy(settings.family, existing, contractAlreadyVerified) || CanMigrateNdmfToSps3(settings.family, existing, contractAlreadyVerified), true, warnings, allowPrefabAdditions: true, validatePlacementScales: false, previewOptionalOmission: true);
                warning = string.Join("\n", warnings.Distinct());
                return null;
            } catch (Exception e) { return e.Message; }
        }
        // Validate authoring geometry only. Downstream native bake behavior is not a Setup admission condition.
        internal static void RequireSupportedScaleChain(Transform target)
        {
            for (var current = target; current != null; current = current.parent) {
                if (!SupportsNativeScale(current.localScale))
                    throw new InvalidOperationException("SPS3 placement requires finite positive, nonzero scale: " + current.name);
                var matrix = current.localToWorldMatrix;
                for (var i = 0; i < 16; i++)
                    if (!Finite(matrix[i])) throw new InvalidOperationException("SPS3 placement has non-finite transform: " + current.name);
                var inverse = matrix.inverse;
                var product = matrix * inverse;
                for (var i = 0; i < 16; i++) {
                    var expected = (i / 4 == i % 4) ? 1f : 0f;
                    if (!Finite(inverse[i]) || !Finite(product[i]) || Math.Abs(product[i] - expected) > .001f)
                        throw new InvalidOperationException("SPS3 placement has non-invertible transform: " + current.name);
                }
            }
        }
        internal static bool SupportsNativeScale(Vector3 scale) =>
            Finite(scale.x) && Finite(scale.y) && Finite(scale.z) && scale.x > 0 && scale.y > 0 && scale.z > 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
