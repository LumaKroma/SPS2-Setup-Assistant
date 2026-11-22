using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Only incomplete, never-produced optional inputs may be skipped. This never
    // authorizes dropping existing output, repairing references, or changing settings.
    internal static class GenerationWarningPolicy
    {
        internal const string EmptyDepthNotice = "アニメーションを使う場合はメッシュとシェイプなどの対象を指定してください。未設定のアクションを除いて生成します。";
        internal const string MissingCustomNotice = "カスタム部位の追従先を指定してください。未指定の新規部位を除いて生成します。";
        internal const string MissingRingNotice = "Humanoidの関節マッピングを確認してください: {0}。この指輪は生成せず、他の部位を生成します。";
        internal const string NoOutputNotice = "生成する部位と追従先を指定してください。生成できる部位がないため、設定と生成物は変更していません。";
        internal const string UnappliedExclusionNotice = "有効な部位と追従先を確認して再実行してください。今回の変更は未適用で、OFFにした既存部位も残っています。";
        internal const string ExistingPartNotice = "既存部位の追従先または関節マッピングを修正してください。安全に再生成できないため、設定と生成物を保持して中止しました。";

        internal static bool HasRecordedSocket(Sps2SetupContext existing, string id) =>
            existing != null && (existing.sockets == null || existing.sockets.Any(s => s == null || s.id == id));
        private static SocketSettings SavedPart(Sps2SetupContext existing, string id) =>
            existing?.settings?.parts?.SingleOrDefault(p => p != null && p.id == id);

        internal static bool CanSkipUnassignedCustom(SocketSettings part, Sps2SetupContext existing)
        {
            if (part == null || !part.custom || !ReferenceEquals(part.target, null) || HasRecordedSocket(existing, part.id)) return false;
            var saved = SavedPart(existing, part.id);
            return (saved == null || saved.custom && ReferenceEquals(saved.target, null)) &&
                Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, part.id + ":target");
        }

        internal static bool PristineRing(FingerRingCalibration value) => value == null ||
            !value.calibrated && string.IsNullOrEmpty(value.thumbPath) && string.IsNullOrEmpty(value.indexPath) &&
            value.center.Equals(Vector3.zero) && value.euler.Equals(Vector3.zero) && value.thumbOffset.Equals(Vector3.zero) && value.indexOffset.Equals(Vector3.zero) &&
            value.thumbRotation.Equals(Quaternion.identity) && value.indexRotation.Equals(Quaternion.identity) &&
            !float.IsNaN(value.thumbWeight) && !float.IsInfinity(value.thumbWeight) && value.thumbWeight >= 0 && value.thumbWeight <= 1;

        // An unusable frame may skip a never-generated pristine optional ring.
        // A retained or calibrated ring requires preservation, so cannot use this path.
        internal static bool CanSkipUnusableNewRing(SocketSettings part, Sps2SetupContext existing)
        {
            if (part == null) return false;
            var saved = SavedPart(existing, part.id);
            return OptionalRingAdmission.CanOmit(part.IsFingerRing, PristineRing(part.fingerRing),
                HasRecordedSocket(existing, part.id), saved != null, saved != null && saved.IsFingerRing,
                saved == null || PristineRing(saved.fingerRing));
        }

        internal static bool TryMissingNewRingBones(VRCAvatarDescriptor avatar, SocketSettings part,
            Sps2SetupContext existing, out string warning)
        {
            warning = null;
            if (part == null || !part.IsFingerRing || !PristineRing(part.fingerRing) || HasRecordedSocket(existing, part.id)) return false;
            var saved = SavedPart(existing, part.id);
            if (saved != null && (!saved.IsFingerRing || !PristineRing(saved.fingerRing))) return false;
            var missing = FingerRingAutoCalibration.MissingMappedBones(avatar, part);
            if (missing.Length == 0) return false;
            warning = part.name + ": " + string.Format(L(MissingRingNotice), string.Join(", ", missing));
            return true;
        }

        // A managed null is distinct from a destroyed Unity reference. A nonempty
        // shape name is a real saved selection even if its renderer cannot resolve.
        internal static bool Unassigned(DepthActionSettings action)
        {
            if (action == null || !ReferenceEquals(action.renderer, null) || !ReferenceEquals(action.target, null) ||
                !ReferenceEquals(action.clip, null) || !string.IsNullOrEmpty(action.shape) ||
                float.IsNaN(action.weight) || float.IsInfinity(action.weight)) return false;
            switch (action.kind) {
                case DepthActionKind.BlendShape: return ReferenceEquals(action.renderer, null) && string.IsNullOrEmpty(action.shape) &&
                    !float.IsNaN(action.weight) && !float.IsInfinity(action.weight);
                case DepthActionKind.Object: return ReferenceEquals(action.target, null);
                case DepthActionKind.AnimationClip: return ReferenceEquals(action.clip, null);
                default: return false;
            }
        }
        internal static bool CanSkipUnassignedAction(SocketSettings part, int index, Sps2SetupContext existing)
        {
            if (part?.actions == null || index < 0 || index >= part.actions.Count || !Unassigned(part.actions[index])) return false;
            var saved = SavedPart(existing, part.id);
            if (existing != null && saved == null && HasRecordedSocket(existing, part.id)) return false;
            if (saved != null) {
                if (saved.actions == null) return false;
                // No persistent row IDs exist. A changed/reordered prior row is not
                // inferred to be new: only appended or equally unassigned rows qualify.
                if (index < saved.actions.Count && (saved.actions[index] == null ||
                    saved.actions[index].kind != part.actions[index].kind || !Unassigned(saved.actions[index]))) return false;
            }
            string key = part.id + ":" + index + ":";
            return Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "renderer") &&
                Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "object") &&
                Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "clip");
        }
        internal static bool CanSkipUnassignedGroupAction(SocketSettings part, SocketAnimationGroup group, int index, Sps2SetupContext existing)
        {
            if (part.animationModelVersion == 0) return CanSkipUnassignedAction(part, index, existing);
            if (group?.actions == null || index < 0 || index >= group.actions.Count || !Unassigned(group.actions[index])) return false;
            var saved = SavedPart(existing, part.id);
            if (existing != null && saved == null && HasRecordedSocket(existing, part.id)) return false;
            string key = group.ReferenceKey(part.id, index) + ":";
            if (saved != null)
            {
                List<DepthActionSettings> prior;
                if (saved.animationModelVersion == 0 && group.id == SocketAnimationGroup.LegacyId)
                { prior = saved.actions; key = part.id + ":" + index + ":"; }
                else
                {
                    if (saved.animationModelVersion != 0 && saved.animationGroups == null) return false;
                    var matches = saved.animationGroups?.Where(g => g != null && g.id == group.id).ToArray();
                    if (matches != null && matches.Length > 1) return false;
                    prior = matches?.SingleOrDefault()?.actions;
                    if (matches?.Length == 1 && prior == null) return false;
                }
                if (prior != null && index < prior.Count && (prior[index] == null ||
                    prior[index].kind != group.actions[index].kind || !Unassigned(prior[index]))) return false;
            }
            return Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "renderer") &&
                Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "object") &&
                Sps2SetupStorage.ProvesReferenceNeverAssigned(existing, key + "clip");
        }
        internal static void RequireRetainedPlacements(Sps2SetupContext existing, SetupSettings settings, System.Collections.Generic.ICollection<string> placedIds)
        {
            if (existing == null) return;
            foreach (var part in settings.parts.Where(p => p.included))
                if (HasRecordedSocket(existing, part.id) && !placedIds.Contains(part.id))
                    throw new InvalidOperationException(part.name + ": " + L(ExistingPartNotice));
        }
    }
}
