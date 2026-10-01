using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
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
            Assert.That(Vector3.Distance(center, other), Is.LessThan(.00001f));
            var mirroredRotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            Assert.That(Quaternion.Angle(mirroredRotation, facing), Is.LessThan(.01f));
            var turn = Quaternion.Euler(40, 75, -30); var offset = new Vector3(2, 3, -4);
            FingerRingAutoCalibration.EstimateGeometry(points.Select(v => offset + turn * v).ToArray(), true, out other, out facing);
            Assert.That(Vector3.Distance(other, offset + turn * center), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(facing, turn * rotation), Is.LessThan(.05f));
        }

        [TestCase("open")] [TestCase("collinear")] [TestCase("nan")] [TestCase("nonplanar")] [TestCase("missing")]
        public void UnsafeGeometryDoesNotReturnCalibration(string kind)
        {
            var p = Closed();
            if (kind == "open") { p[5].x += .2f; p[6].x += .2f; }
            if (kind == "collinear") for (int i = 0; i < p.Length; i++) p[i] = Vector3.up * i * .01f;
            if (kind == "nan") p[2].x = float.NaN;
            if (kind == "nonplanar") p[2].z = .1f;
            if (kind == "missing") p = p.Take(6).ToArray();
            Assert.Throws<InvalidOperationException>(() => FingerRingAutoCalibration.EstimateGeometry(p, true, out _, out _));
        }

        [Test]
        public void PalmBaseDoesNotBiasOpeningCenterOrPlane()
        {
            var p = Closed();
            FingerRingAutoCalibration.EstimateGeometry(p, true, out var center, out var rotation);
            p[1] = new Vector3(-.05f, -.01f, .008f);
            FingerRingAutoCalibration.EstimateGeometry(p, true, out var other, out var facing);
            Assert.That(Vector3.Distance(new Vector3(-center.x, center.y, center.z), other), Is.LessThan(.00001f));
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
