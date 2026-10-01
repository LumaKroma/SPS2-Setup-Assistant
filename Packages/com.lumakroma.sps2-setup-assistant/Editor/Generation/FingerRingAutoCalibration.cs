using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static class FingerRingAutoCalibration
    {
        private const string Failure = "指わっかを自動推定できませんでした。輪を閉じた姿勢で手動校正してください。";

        public static FingerRingCalibration Estimate(VRCAvatarDescriptor avatar, SocketSettings part)
        {
            FingerRingCalibrationUtility.Bones(avatar, part, out var thumb, out var index);
            // Validate original frames before cloning: cloning must not hide parent shear.
            FingerRingCalibrationUtility.Capture(avatar.transform, thumb, index, thumb.position, thumb.rotation, part.fingerRing?.thumbWeight ?? .5f);
            bool left = part.id == "fingerRingLeft";
            var animator = avatar.GetComponent<Animator>();
            var bones = RequiredBones(left).Select(animator.GetBoneTransform).ToArray();
            if (bones.Any(b => b == null || !b.IsChildOf(avatar.transform)) || bones.Distinct().Count() != bones.Length)
                throw new InvalidOperationException(L(Failure));
            return WithSkeletonCopy(avatar.transform, (root, map) => {
                using (var handler = new HumanPoseHandler(animator.avatar, root))
                {
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    ApplyReferencePose(ref pose, left);
                    handler.SetHumanPose(ref pose);
                }
                var points = bones.Select(b => map[b].position).ToArray();
                EstimateGeometry(points, left, out var center, out var rotation);
                var value = FingerRingCalibrationUtility.Capture(root, map[thumb], map[index], center, rotation, part.fingerRing?.thumbWeight ?? .5f);
                // Never return references to the disposable hierarchy.
                value.thumbPath = AnimationUtility.CalculateTransformPath(thumb, avatar.transform);
                value.indexPath = AnimationUtility.CalculateTransformPath(index, avatar.transform);
                return value;
            });
        }

        private static HumanBodyBones[] RequiredBones(bool left) => left ? new[] {
            HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal
        } : new[] {
            HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal
        };

        internal static void ApplyReferencePose(ref HumanPose pose, bool left)
        {
            string side = left ? "Left " : "Right ";
            var values = new Dictionary<string, float> {
                { "Thumb 1 Stretched", -.5f }, { "Thumb Spread", .5f },
                { "Thumb 2 Stretched", -.7f }, { "Thumb 3 Stretched", -.7f },
                { "Index 1 Stretched", -.3f }, { "Index Spread", 0f },
                { "Index 2 Stretched", -.8f }, { "Index 3 Stretched", -.6f }
            };
            if (pose.muscles == null || pose.muscles.Length != HumanTrait.MuscleCount)
                throw new InvalidOperationException(L(Failure));
            foreach (var entry in values)
            {
                int i = Array.IndexOf(HumanTrait.MuscleName, side + entry.Key);
                if (i < 0) throw new InvalidOperationException(L(Failure));
                pose.muscles[i] = entry.Value;
            }
        }

        internal static T WithSkeletonCopy<T>(Transform source, Func<Transform, Dictionary<Transform, Transform>, T> measure)
        {
            GameObject temporary = null;
            try
            {
                var map = new Dictionary<Transform, Transform>();
                Transform Copy(Transform from, Transform parent)
                {
                    var go = new GameObject(from.name) { hideFlags = HideFlags.HideAndDontSave };
                    if (temporary == null) temporary = go;
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = from.localPosition;
                    go.transform.localRotation = from.localRotation;
                    go.transform.localScale = from.localScale;
                    map.Add(from, go.transform);
                    foreach (Transform child in from) Copy(child, go.transform);
                    return go.transform;
                }
                var root = Copy(source, null);
                root.SetPositionAndRotation(source.position, source.rotation);
                root.localScale = source.lossyScale;
                // No Animator, scripts, renderers or constraints are copied or run.
                return measure(root, map);
            }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }

        // Points: Hand, Thumb proximal/intermediate/distal, Index proximal/intermediate/distal.
        // Bone contour is a bounded approximation of the opening, not a mesh measurement.
        internal static void EstimateGeometry(Vector3[] p, bool left, out Vector3 center, out Quaternion rotation)
        {
            center = default; rotation = Quaternion.identity;
            void Reject() => throw new InvalidOperationException(L(Failure));
            if (p == null || p.Length != 7 || p.Any(v => !Finite(v))) Reject();
            float thumbLast = Vector3.Distance(p[2], p[3]), indexLast = Vector3.Distance(p[5], p[6]);
            float length = Vector3.Distance(p[4], p[5]) + indexLast * 1.6f;
            if (thumbLast < .00001f || indexLast < .00001f || length < .0001f || Vector3.Distance(p[1], p[2]) < .00001f || Vector3.Distance(p[4], p[5]) < .00001f) Reject();
            // Distal Humanoid bones mark a joint rather than the fingertip. No mesh/tip mapping is assumed.
            var thumbTip = p[3] + (p[3] - p[2]) * .6f;
            var indexTip = p[6] + (p[6] - p[5]) * .6f;
            if (Vector3.Distance(thumbTip, indexTip) > length * .35f) Reject();
            var contour = new[] { p[4], p[5], p[6], indexTip, thumbTip, p[3], p[2], p[1] };
            var origin = contour[0]; var areaVector = Vector3.zero;
            for (int i = 1; i < contour.Length - 1; i++) areaVector += Vector3.Cross(contour[i] - origin, contour[i + 1] - origin);
            float span = contour.Max(v => Vector3.Distance(v, origin));
            if (!Finite(areaVector) || span < .0001f || areaVector.magnitude < span * span * .08f) Reject();
            var normal = areaVector.normalized;
            var palmNormal = Vector3.Cross(p[4] - p[0], p[1] - p[0]) * (left ? 1 : -1);
            if (palmNormal.magnitude < span * span * .01f || Mathf.Abs(Vector3.Dot(normal, palmNormal.normalized)) < .25f) Reject();
            if (Vector3.Dot(normal, palmNormal) < 0) normal = -normal;
            if (contour.Any(v => Mathf.Abs(Vector3.Dot(v - origin, normal)) > span * .2f)) Reject();
            float twiceArea = 0; var weighted = Vector3.zero;
            for (int i = 1; i < contour.Length - 1; i++)
            {
                float a = Vector3.Dot(Vector3.Cross(contour[i] - origin, contour[i + 1] - origin), normal);
                twiceArea += a; weighted += (origin + contour[i] + contour[i + 1]) / 3 * a;
            }
            if (Mathf.Abs(twiceArea) < span * span * .08f) Reject();
            center = weighted / twiceArea;
            var up = Vector3.ProjectOnPlane(p[4] - center, normal);
            if (!Finite(center) || up.sqrMagnitude < span * span * .001f) Reject();
            rotation = Quaternion.LookRotation(normal, up.normalized);
        }

        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
    }
}
