using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class BodyBasisBuilderTests
    {
        [Test]
        public void Build_ProducesRightHandedAvatarAlignedBasisAndMeasuredHeight()
        {
            var snapshot = new HumanoidSnapshot(
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                new Dictionary<HumanBodyBones, BonePose>
                {
                    [HumanBodyBones.Hips] = Pose(0f, 0.9f, 0f),
                    [HumanBodyBones.Head] = Pose(0f, 1.7f, 0f),
                    [HumanBodyBones.LeftHand] = Pose(-0.7f, 1.2f, 0f),
                    [HumanBodyBones.RightHand] = Pose(0.7f, 1.2f, 0f),
                    [HumanBodyBones.LeftFoot] = Pose(-0.1f, 0f, 0f),
                    [HumanBodyBones.RightFoot] = Pose(0.1f, 0f, 0f),
                });

            var basis = BodyBasisBuilder.Build(snapshot);

            Assert.That(Vector3.Dot(basis.Forward, Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(basis.Right, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(basis.Up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(basis.Height, Is.EqualTo(1.7f).Within(0.0001f));
        }

        [Test]
        public void Build_FallsBackToAvatarAxesWhenLimbDirectionsAreMissing()
        {
            var snapshot = new HumanoidSnapshot(
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                new Dictionary<HumanBodyBones, BonePose>());

            var basis = BodyBasisBuilder.Build(snapshot);

            Assert.That(basis.Right, Is.EqualTo(Vector3.right));
            Assert.That(basis.Up, Is.EqualTo(Vector3.up));
            Assert.That(basis.Forward, Is.EqualTo(Vector3.forward));
            Assert.That(basis.Height, Is.EqualTo(1f));
        }

        private static BonePose Pose(float x, float y, float z)
        {
            return new BonePose(new Vector3(x, y, z), Quaternion.identity);
        }
    }
}
