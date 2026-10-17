using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Localization;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class FingerRingAutoCalibrationTests
    {
        private static Vector3[] Closed() => new[] {
            new Vector3(0, -.02f, 0), new Vector3(-.02f, 0, 0), new Vector3(-.02f, .02f, 0),
            new Vector3(-.01f, .04f, 0), new Vector3(.02f, 0, 0), new Vector3(.02f, .02f, 0), new Vector3(.01f, .04f, 0)
        };

        [Test]
        public void ClosedContourHasInteriorCenterAndMirroredForward()
        {
            var points = Closed();
            FingerRingAutoCalibration.EstimateGeometry(points, true, out var center, out var rotation);
            // The trimmed web edge is asymmetric; its independent planar area
            // centroid is slightly towards the index side, then mirrors exactly.
            Assert.That(center.x, Is.EqualTo(.002283105f).Within(.000001f));
            Assert.That(center.y, Is.InRange(.015f, .04f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(.999f));
            var mirror = points.Select(v => new Vector3(-v.x, v.y, v.z)).ToArray();
            FingerRingAutoCalibration.EstimateGeometry(mirror, false, out var other, out var facing);
            Assert.That(Vector3.Distance(new Vector3(-center.x, center.y, center.z), other), Is.LessThan(.00001f));
            var mirroredRotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            Assert.That(Quaternion.Angle(mirroredRotation, facing), Is.LessThan(.01f));
            var turn = Quaternion.Euler(40, 75, -30); var offset = new Vector3(2, 3, -4);
            FingerRingAutoCalibration.EstimateGeometry(points.Select(v => offset + turn * v).ToArray(), true, out other, out facing);
            Assert.That(Vector3.Distance(other, offset + turn * center), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(facing, turn * rotation), Is.LessThan(.05f));
        }

        [TestCase(.5f)] [TestCase(2f)]
        public void UniformScalePreservesEstimatedCenterAndFacing(float scale)
        {
            FingerRingAutoCalibration.EstimateGeometry(Closed(), true, out var center, out var facing);
            var turn = Quaternion.Euler(40, 75, -30); var offset = new Vector3(2, 3, -4);
            FingerRingAutoCalibration.EstimateGeometry(Closed().Select(v => offset + turn * (v * scale)).ToArray(),
                true, out var actual, out var rotation);
            Assert.That(Vector3.Distance(actual, offset + turn * (center * scale)), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(rotation, turn * facing), Is.LessThan(.05f));
        }

        [TestCase("collinear", "指わっかを自動配置できませんでした。自動計算した指ポーズで、輪の面積が不足しているか、輪郭が退化しています。")]
        [TestCase("nan", "指わっかを自動配置できませんでした。自動計算した指ポーズで、7点の有限なボーン位置を取得できません。")]
        [TestCase("nonplanar", "指わっかを自動配置できませんでした。自動計算した指ポーズで、輪の法線と手のひらの向きを対応付けられません。")]
        [TestCase("missing", "指わっかを自動配置できませんでした。自動計算した指ポーズで、7点の有限なボーン位置を取得できません。")]
        public void UnsafeGeometryDoesNotReturnCalibration(string kind, string reason)
        {
            var p = Closed();
            if (kind == "collinear") for (int i = 0; i < p.Length; i++) p[i] = Vector3.up * i * .01f;
            if (kind == "nan") p[2].x = float.NaN;
            if (kind == "nonplanar") p[2].z = .1f;
            if (kind == "missing") p = p.Take(6).ToArray();
            // A large gap must not bypass any of the later geometry checks.
            var error = Assert.Throws<InvalidOperationException>(() => FingerRingAutoCalibration.EstimateGeometry(p, true, out _, out _));
            Assert.That(error.Message, Is.EqualTo(Sps2Localization.L(reason)));
        }

        [TestCase(.199f, false)] [TestCase(.2f, false)] [TestCase(.201f, true)]
        public void TipGapBoundaryWarnsWithoutRejectingValidContour(float ratio, bool expectedWarning)
        {
            // Two vertical terminal segments make the gap independent of segment
            // length. The opening is a rectangle with a known interior centroid.
            float length = .02f + .02f * 1.6f;
            float gap = length * ratio;
            var p = new[] {
                new Vector3(0, -.02f, 0), new Vector3(-.02f, 0, 0),
                new Vector3(-gap, .02f, 0), new Vector3(-gap, .04f, 0),
                Vector3.zero, new Vector3(0, .02f, 0), new Vector3(0, .04f, 0)
            };
            foreach (bool left in new[] { true, false })
            {
                var points = p.Select(v => new Vector3(left ? v.x : -v.x, v.y, v.z)).ToArray();
                bool warning = FingerRingAutoCalibration.EstimateGeometry(points, left, out var center, out var rotation);
                Assert.That(warning, Is.EqualTo(expectedWarning));
                Assert.That(center.x, Is.InRange(-gap, gap));
                Assert.That(center.y, Is.InRange(.02f, .052f));
                Assert.That(float.IsNaN(center.sqrMagnitude) || float.IsInfinity(center.sqrMagnitude), Is.False);
                Assert.That(Mathf.Abs(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w - 1), Is.LessThan(.00001f));
                Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(.999f));
            }
        }

        [Test]
        public void WidePlanarContourWarnsAndStillReturnsFiniteInteriorCenter()
        {
            // This particular open fixture passes every later check. Other open
            // contours still fail whenever their plane/area/normal is unusable.
            var p = Closed(); p[5].x += .2f; p[6].x += .2f;
            Assert.That(FingerRingAutoCalibration.EstimateGeometry(p, true, out var center, out var rotation), Is.True);
            Assert.That(center.x, Is.InRange(-.02f, .22f));
            Assert.That(center.y, Is.InRange(0f, .052f));
            Assert.That(float.IsNaN(center.sqrMagnitude) || float.IsInfinity(center.sqrMagnitude), Is.False);
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(.999f));
        }

        [Test]
        public void FailedEstimateDoesNotAppendPlacementWarning()
        {
            var go = new GameObject("Invalid avatar");
            var warnings = new System.Collections.Generic.List<string> { "Existing warning" };
            try
            {
                var avatar = go.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                var part = Model.FullSetupCatalog.CreateDefault().parts.Find(p => p.id == "fingerRingLeft");
                Assert.Throws<InvalidOperationException>(() => FingerRingAutoCalibration.Estimate(avatar, part, warnings));
                Assert.That(warnings, Is.EqualTo(new[] { "Existing warning" }));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(true)] [TestCase(false)]
        public void InvalidReferencePoseReportsPoseFailure(bool nullMuscles)
        {
            var pose = new HumanPose { muscles = nullMuscles ? null : new float[HumanTrait.MuscleCount - 1] };
            var error = Assert.Throws<InvalidOperationException>(() => FingerRingAutoCalibration.ApplyReferencePose(ref pose, true));
            Assert.That(error.Message, Is.EqualTo(Sps2Localization.L("指わっかを自動配置できませんでした。自動計算用の指ポーズを設定できません。")));
        }

        [Test]
        public void PalmBaseDoesNotBiasOpeningCenterOrPlane()
        {
            var p = Closed();
            FingerRingAutoCalibration.EstimateGeometry(p, true, out var center, out var rotation);
            p[1] = new Vector3(-.05f, -.01f, .008f);
            FingerRingAutoCalibration.EstimateGeometry(p, true, out var other, out var facing);
            Assert.That(Vector3.Distance(center, other), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(rotation, facing), Is.LessThan(.01f));
        }

        [TestCase(false)] [TestCase(true)]
        public void SkeletonCopyAlwaysCleansUpWithoutOriginalMutation(bool fail)
        {
            var source = new GameObject("Original avatar"); Transform temporary = null;
            try
            {
                var bone = new GameObject("Finger").transform; bone.SetParent(source.transform, false);
                source.AddComponent<Animator>(); bone.localPosition = Vector3.one; bone.localRotation = Quaternion.Euler(20, 30, 40);
                var originalRotation = bone.localRotation;
                Action measure = () => FingerRingAutoCalibration.WithSkeletonCopy<object>(source.transform, (root, map) => {
                    temporary = root;
                    Assert.That(root.GetComponentsInChildren<Component>(true).All(c => c is Transform), Is.True);
                    Assert.That(map[bone].localPosition, Is.EqualTo(Vector3.one));
                    map[bone].localPosition = Vector3.zero; map[bone].localRotation = Quaternion.identity;
                    if (fail) throw new InvalidOperationException("Interrupted measurement");
                    return null;
                });
                if (fail) Assert.Throws<InvalidOperationException>(() => measure()); else measure();
                Assert.That(temporary == null, Is.True, "Disposable copy leaked");
                Assert.That(bone.localPosition, Is.EqualTo(Vector3.one));
                Assert.That(bone.localRotation, Is.EqualTo(originalRotation));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase(true)] [TestCase(false)]
        public void ReferencePoseChangesOnlyChosenThumbAndIndexMuscles(bool left)
        {
            var pose = new HumanPose { muscles = Enumerable.Repeat(.123f, HumanTrait.MuscleCount).ToArray() };
            FingerRingAutoCalibration.ApplyReferencePose(ref pose, left);
            var changed = Enumerable.Range(0, pose.muscles.Length).Where(i => pose.muscles[i] != .123f).ToArray();
            Assert.That(changed.Length, Is.EqualTo(8));
            Assert.That(changed.All(i => HumanTrait.MuscleName[i].StartsWith(left ? "Left " : "Right ") &&
                (HumanTrait.MuscleName[i].Contains("Thumb") || HumanTrait.MuscleName[i].Contains("Index"))), Is.True);
            Assert.That(changed.All(i => pose.muscles[i] >= -1 && pose.muscles[i] <= 1), Is.True);
        }
    }
}
