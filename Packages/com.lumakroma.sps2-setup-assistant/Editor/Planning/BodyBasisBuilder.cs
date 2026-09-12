using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Planning
{
    public static class BodyBasisBuilder
    {
        public static BodyBasis Build(HumanoidSnapshot snapshot)
        {
            var up = TryDirection(snapshot, HumanBodyBones.Hips, HumanBodyBones.Head, snapshot.AvatarUp);
            var right = TryDirection(snapshot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, snapshot.AvatarRight);

            if (Vector3.Cross(up, right).sqrMagnitude < 0.000001f)
            {
                right = snapshot.AvatarRight;
            }

            var forward = Vector3.Cross(right, up).normalized;
            if (Vector3.Dot(forward, snapshot.AvatarForward) < 0f)
            {
                forward = -forward;
            }

            right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;

            return new BodyBasis(right, up, forward, MeasureHeight(snapshot, up));
        }

        private static Vector3 TryDirection(
            HumanoidSnapshot snapshot,
            HumanBodyBones from,
            HumanBodyBones to,
            Vector3 fallback)
        {
            if (snapshot.TryGetBone(from, out var fromPose) &&
                snapshot.TryGetBone(to, out var toPose))
            {
                var direction = toPose.Position - fromPose.Position;
                if (direction.sqrMagnitude > 0.000001f)
                {
                    return direction.normalized;
                }
            }

            return fallback.normalized;
        }

        private static float MeasureHeight(HumanoidSnapshot snapshot, Vector3 up)
        {
            if (!snapshot.TryGetBone(HumanBodyBones.Head, out var head))
            {
                return 1f;
            }

            var hasLeft = TryFoot(snapshot, true, out var left);
            var hasRight = TryFoot(snapshot, false, out var right);
            if (hasLeft || hasRight)
            {
                var foot = hasLeft && hasRight
                    ? (left.Position + right.Position) * 0.5f
                    : (hasLeft ? left.Position : right.Position);
                return Mathf.Max(0.01f, Mathf.Abs(Vector3.Dot(head.Position - foot, up)));
            }

            if (snapshot.TryGetBone(HumanBodyBones.Hips, out var hips))
            {
                return Mathf.Max(0.01f, Mathf.Abs(Vector3.Dot(head.Position - hips.Position, up)) * 2.1f);
            }

            return 1f;
        }

        private static bool TryFoot(HumanoidSnapshot snapshot, bool left, out BonePose pose)
        {
            var toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            var foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            return snapshot.TryGetBone(toes, out pose) || snapshot.TryGetBone(foot, out pose);
        }
    }
}
