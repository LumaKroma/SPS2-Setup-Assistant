using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Planning
{
    public static class SocketPlacementPlanner
    {
        public static bool TryCreatePlan(
            HumanoidSnapshot snapshot,
            IEnumerable<SocketPresetId> enabledPresetIds,
            out SocketSetupPlan plan,
            out IReadOnlyList<string> unavailableReasons,
            out string error)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var enabled = new HashSet<SocketPresetId>(enabledPresetIds ?? Array.Empty<SocketPresetId>());
            var basis = BodyBasisBuilder.Build(snapshot);
            var placements = new List<SocketPlacement>();
            var unavailable = new List<string>();

            foreach (var preset in SocketCatalog.Presets)
            {
                if (!snapshot.TryResolveBone(preset, out var bone, out var anchor))
                {
                    var candidates = string.Join(" or ", preset.BoneCandidates.Select(candidate => candidate.ToString()));
                    unavailable.Add($"{preset.DisplayName}: missing {candidates}.");
                    continue;
                }

                if (!enabled.Contains(preset.Id))
                {
                    continue;
                }

                var position = ResolvePosition(snapshot, preset.Id, anchor, basis);
                var inward = ResolveInwardDirection(preset.Id, anchor, position, basis);
                var rotation = CreateEntryRotation(inward, basis);
                placements.Add(new SocketPlacement(preset, bone, position, rotation));
            }

            unavailableReasons = unavailable;
            if (placements.Count == 0)
            {
                plan = null;
                error = "No enabled Socket preset has an available Humanoid bone.";
                return false;
            }

            plan = new SocketSetupPlan(basis, placements);
            error = null;
            return true;
        }

        private static Vector3 ResolvePosition(
            HumanoidSnapshot snapshot,
            SocketPresetId id,
            BonePose anchor,
            BodyBasis basis)
        {
            var height = basis.Height;
            switch (id)
            {
                case SocketPresetId.HeadMouth:
                    if (snapshot.TryGetBone(HumanBodyBones.Jaw, out var jaw))
                    {
                        return jaw.Position + basis.Forward * (height * 0.025f);
                    }

                    return anchor.Position
                           + basis.Forward * (height * 0.045f)
                           - basis.Up * (height * 0.035f);

                case SocketPresetId.Chest:
                    return anchor.Position + basis.Forward * (height * 0.055f);

                case SocketPresetId.HipsFront:
                    return anchor.Position + basis.Forward * (height * 0.06f);

                case SocketPresetId.HipsBack:
                    return anchor.Position - basis.Forward * (height * 0.06f);

                case SocketPresetId.LeftHand:
                    return PlaceHand(snapshot, true, anchor, basis);

                case SocketPresetId.RightHand:
                    return PlaceHand(snapshot, false, anchor, basis);

                case SocketPresetId.LeftFoot:
                case SocketPresetId.RightFoot:
                    return anchor.Position
                           + basis.Forward * (height * 0.05f)
                           + basis.Up * (height * 0.012f);

                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        private static Vector3 PlaceHand(
            HumanoidSnapshot snapshot,
            bool left,
            BonePose anchor,
            BodyBasis basis)
        {
            var lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            var outward = left ? -basis.Right : basis.Right;
            if (snapshot.TryGetBone(lowerArm, out var lowerArmPose))
            {
                var measured = anchor.Position - lowerArmPose.Position;
                if (measured.sqrMagnitude > 0.000001f)
                {
                    outward = measured.normalized;
                }
            }

            return anchor.Position + outward * (basis.Height * 0.022f);
        }

        private static Vector3 ResolveInwardDirection(
            SocketPresetId id,
            BonePose anchor,
            Vector3 position,
            BodyBasis basis)
        {
            switch (id)
            {
                case SocketPresetId.HeadMouth:
                case SocketPresetId.Chest:
                case SocketPresetId.HipsFront:
                    return -basis.Forward;

                case SocketPresetId.HipsBack:
                    return basis.Forward;

                case SocketPresetId.LeftHand:
                case SocketPresetId.RightHand:
                case SocketPresetId.LeftFoot:
                case SocketPresetId.RightFoot:
                    var towardAnchor = anchor.Position - position;
                    return towardAnchor.sqrMagnitude > 0.000001f ? towardAnchor.normalized : -basis.Forward;

                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        private static Quaternion CreateEntryRotation(Vector3 inward, BodyBasis basis)
        {
            var forward = inward.sqrMagnitude > 0.000001f ? inward.normalized : -basis.Forward;
            var up = basis.Up;
            if (Vector3.Cross(forward, up).sqrMagnitude < 0.000001f)
            {
                up = basis.Right;
            }

            return Quaternion.LookRotation(forward, up);
        }
    }
}
