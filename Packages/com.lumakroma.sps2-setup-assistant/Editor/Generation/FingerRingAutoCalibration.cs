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
            if (bones.Any(b => b == null || !b.IsChildOf(avatar.transform)) || bones.Distinct().Count() != bones.Length ||
                bones.Any(b => avatar.transform.Find(AnimationUtility.CalculateTransformPath(b, avatar.transform)) != b))
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
                // The index base changes less between reference and gesture poses than
                // its intermediate joint. Keep the chosen frame in the existing path field.
                var trackingIndex = bones[4];
                var value = FingerRingCalibrationUtility.Capture(root, map[thumb], map[trackingIndex], center, rotation, part.fingerRing?.thumbWeight ?? .5f);
                // Never return references to the disposable hierarchy.
                value.thumbPath = AnimationUtility.CalculateTransformPath(thumb, avatar.transform);
                value.indexPath = AnimationUtility.CalculateTransformPath(trackingIndex, avatar.transform);
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
                { "Thumb 1 Stretched", -1f }, { "Thumb Spread", -.75f },
                { "Thumb 2 Stretched", -.75f }, { "Thumb 3 Stretched", -.75f },
                { "Index 1 Stretched", -.25f }, { "Index Spread", 0f },
                { "Index 2 Stretched", -1f }, { "Index 3 Stretched", -1f }
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
            if (Vector3.Distance(thumbTip, indexTip) > length * .2f) Reject();
            // ThumbProximal belongs to the palm/web, not the opening boundary.
            var contour = new[] { p[4], p[5], p[6], indexTip, thumbTip, p[3], p[2] };
            var origin = contour[0]; var areaVector = Vector3.zero;
            for (int i = 1; i < contour.Length - 1; i++) areaVector += Vector3.Cross(contour[i] - origin, contour[i + 1] - origin);
            float span = contour.Max(v => Vector3.Distance(v, origin));
            if (!Finite(areaVector) || span < .0001f || areaVector.magnitude < span * span * .08f) Reject();
            var planeOrigin = contour.Aggregate(Vector3.zero, (sum, v) => sum + v) / contour.Length;
            var normal = FitPlane(contour, planeOrigin, areaVector.normalized, span);
            var palmNormal = Vector3.Cross(p[4] - p[0], p[1] - p[0]) * (left ? 1 : -1);
            if (palmNormal.magnitude < span * span * .01f || Mathf.Abs(Vector3.Dot(normal, palmNormal.normalized)) < .25f) Reject();
            if (Vector3.Dot(normal, palmNormal) < 0) normal = -normal;
            if (contour.Any(v => Mathf.Abs(Vector3.Dot(v - planeOrigin, normal)) > span * .2f)) Reject();
            float twiceArea = 0; var weighted = Vector3.zero;
            for (int i = 1; i < contour.Length - 1; i++)
            {
                float a = Vector3.Dot(Vector3.Cross(contour[i] - origin, contour[i + 1] - origin), normal);
                twiceArea += a; weighted += (origin + contour[i] + contour[i + 1]) / 3 * a;
            }
            if (Mathf.Abs(twiceArea) < span * span * .08f) Reject();
            center = weighted / twiceArea;
            center -= normal * Vector3.Dot(center - planeOrigin, normal);
            var up = Vector3.ProjectOnPlane(p[4] - center, normal);
            if (!Finite(center) || up.sqrMagnitude < span * span * .001f) Reject();
            var xAxis = Vector3.Cross(up.normalized, normal);
            bool inside = false;
            for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
            {
                var a = contour[i] - center; var b = contour[j] - center;
                float ay = Vector3.Dot(a, up.normalized), by = Vector3.Dot(b, up.normalized);
                float ax = Vector3.Dot(a, xAxis), bx = Vector3.Dot(b, xAxis);
                if ((ay > 0) != (by > 0) && ax + (bx - ax) * -ay / (by - ay) > 0) inside = !inside;
            }
            if (!inside) Reject();
            rotation = Quaternion.LookRotation(normal, up.normalized);
        }

        // Small symmetric covariance eigensolve: use the least-variance axis,
        // rather than letting a warped fan triangulation tilt the opening plane.
        private static Vector3 FitPlane(Vector3[] contour, Vector3 origin, Vector3 hint, float span)
        {
            var covariance = new double[3, 3]; var axes = new double[3, 3];
            for (int i = 0; i < 3; i++) axes[i, i] = 1;
            foreach (var point in contour)
            {
                var d = (point - origin) / span;
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) covariance[i, j] += d[i] * d[j];
            }
            for (int iteration = 0; iteration < 24; iteration++)
            {
                int a = 0, b = 1;
                if (Math.Abs(covariance[0, 2]) > Math.Abs(covariance[a, b])) { a = 0; b = 2; }
                if (Math.Abs(covariance[1, 2]) > Math.Abs(covariance[a, b])) { a = 1; b = 2; }
                if (Math.Abs(covariance[a, b]) < 1e-12) break;
                double angle = .5 * Math.Atan2(2 * covariance[a, b], covariance[b, b] - covariance[a, a]);
                var turn = new double[3, 3]; for (int i = 0; i < 3; i++) turn[i, i] = 1;
                turn[a, a] = turn[b, b] = Math.Cos(angle); turn[a, b] = Math.Sin(angle); turn[b, a] = -turn[a, b];
                var next = new double[3, 3]; var nextAxes = new double[3, 3];
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        nextAxes[i, j] += axes[i, k] * turn[k, j];
                        for (int l = 0; l < 3; l++) next[i, j] += turn[k, i] * covariance[k, l] * turn[l, j];
                    }
                }
                covariance = next; axes = nextAxes;
            }
            var order = Enumerable.Range(0, 3).OrderBy(i => covariance[i, i]).ToArray();
            if (covariance[order[1], order[1]] < .001 || covariance[order[0], order[0]] > covariance[order[1], order[1]] * .5)
                throw new InvalidOperationException(L(Failure));
            var normal = new Vector3((float)axes[0, order[0]], (float)axes[1, order[0]], (float)axes[2, order[0]]).normalized;
            return Vector3.Dot(normal, hint) < 0 ? -normal : normal;
        }

        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
    }
}
