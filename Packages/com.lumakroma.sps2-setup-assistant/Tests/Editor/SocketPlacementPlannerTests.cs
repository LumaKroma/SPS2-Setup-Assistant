using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class SocketPlacementPlannerTests
    {
        [Test]
        public void CreatePlan_MirrorsHandsAndPointsEntryTowardEachAnchor()
        {
            var snapshot = StandardSnapshot();

            var ok = SocketPlacementPlanner.TryCreatePlan(
                snapshot,
                new[] { SocketPresetId.LeftHand, SocketPresetId.RightHand },
                out var plan,
                out var unavailable,
                out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(unavailable, Is.Empty);
            var left = plan.Placements.Single(item => item.Preset.Id == SocketPresetId.LeftHand);
            var right = plan.Placements.Single(item => item.Preset.Id == SocketPresetId.RightHand);
            Assert.That(left.WorldPosition.x, Is.EqualTo(-right.WorldPosition.x).Within(0.0001f));
            Assert.That(left.WorldPosition.y, Is.EqualTo(right.WorldPosition.y).Within(0.0001f));
            Assert.That(Vector3.Dot(left.WorldRotation * Vector3.forward, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(right.WorldRotation * Vector3.forward, Vector3.left), Is.GreaterThan(0.999f));
        }

        [Test]
        public void CreatePlan_PointsFrontAndBackHipsInOppositeInwardDirections()
        {
            var snapshot = StandardSnapshot();

            Assert.That(SocketPlacementPlanner.TryCreatePlan(
                snapshot,
                new[] { SocketPresetId.HipsFront, SocketPresetId.HipsBack },
                out var plan,
                out _,
                out var error), Is.True, error);

            var front = plan.Placements.Single(item => item.Preset.Id == SocketPresetId.HipsFront);
            var back = plan.Placements.Single(item => item.Preset.Id == SocketPresetId.HipsBack);
            Assert.That(front.WorldPosition.z, Is.GreaterThan(0f));
            Assert.That(back.WorldPosition.z, Is.LessThan(0f));
            Assert.That(Vector3.Dot(front.WorldRotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(back.WorldRotation * Vector3.forward, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void CreatePlan_MissingOneBoneDisablesOnlyThatPreset()
        {
            var bones = StandardBones();
            bones.Remove(HumanBodyBones.LeftHand);
            bones.Remove(HumanBodyBones.LeftLowerArm);
            var snapshot = Snapshot(bones);

            Assert.That(SocketPlacementPlanner.TryCreatePlan(
                snapshot,
                SocketCatalog.Presets.Select(item => item.Id),
                out var plan,
                out var unavailable,
                out var error), Is.True, error);

            Assert.That(plan.Placements.Select(item => item.Preset.Id), Has.None.EqualTo(SocketPresetId.LeftHand));
            Assert.That(plan.Placements, Has.Count.EqualTo(7));
            Assert.That(unavailable.Single(), Does.StartWith("Left Hand:"));
        }

        private static HumanoidSnapshot StandardSnapshot()
        {
            return Snapshot(StandardBones());
        }

        private static HumanoidSnapshot Snapshot(IReadOnlyDictionary<HumanBodyBones, BonePose> bones)
        {
            return new HumanoidSnapshot(Vector3.right, Vector3.up, Vector3.forward, bones);
        }

        private static Dictionary<HumanBodyBones, BonePose> StandardBones()
        {
            return new Dictionary<HumanBodyBones, BonePose>
            {
                [HumanBodyBones.Hips] = Pose(0f, 0.9f, 0f),
                [HumanBodyBones.Head] = Pose(0f, 1.7f, 0f),
                [HumanBodyBones.Jaw] = Pose(0f, 1.58f, 0f),
                [HumanBodyBones.UpperChest] = Pose(0f, 1.3f, 0f),
                [HumanBodyBones.LeftLowerArm] = Pose(-0.5f, 1.25f, 0f),
                [HumanBodyBones.RightLowerArm] = Pose(0.5f, 1.25f, 0f),
                [HumanBodyBones.LeftHand] = Pose(-0.72f, 1.25f, 0f),
                [HumanBodyBones.RightHand] = Pose(0.72f, 1.25f, 0f),
                [HumanBodyBones.LeftToes] = Pose(-0.1f, 0f, 0.08f),
                [HumanBodyBones.RightToes] = Pose(0.1f, 0f, 0.08f),
            };
        }

        private static BonePose Pose(float x, float y, float z)
        {
            return new BonePose(new Vector3(x, y, z), Quaternion.identity);
        }
    }
}
