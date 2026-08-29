using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class SocketCatalogTests
    {
        [Test]
        public void Catalog_HasExactlyEightUniqueOrderedPresets()
        {
            Assert.That(SocketCatalog.Presets, Has.Count.EqualTo(8));
            Assert.That(SocketCatalog.Presets.Select(preset => preset.Id).Distinct(), Has.Count.EqualTo(8));
            Assert.That(SocketCatalog.Presets.Select(preset => preset.Order), Is.EqualTo(Enumerable.Range(1, 8)));
        }

        [Test]
        public void ResolveBone_UsesHeadWhenJawIsUnavailable()
        {
            var snapshot = Snapshot(new Dictionary<HumanBodyBones, BonePose>
            {
                [HumanBodyBones.Head] = Pose(0f, 1.6f, 0f),
            });
            var preset = SocketCatalog.Presets.Single(item => item.Id == SocketPresetId.HeadMouth);

            Assert.That(snapshot.TryResolveBone(preset, out var bone, out _), Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.Head));
        }

        [Test]
        public void ResolveBone_PrefersUpperChestOverChest()
        {
            var snapshot = Snapshot(new Dictionary<HumanBodyBones, BonePose>
            {
                [HumanBodyBones.Chest] = Pose(0f, 1.1f, 0f),
                [HumanBodyBones.UpperChest] = Pose(0f, 1.3f, 0f),
            });
            var preset = SocketCatalog.Presets.Single(item => item.Id == SocketPresetId.Chest);

            Assert.That(snapshot.TryResolveBone(preset, out var bone, out _), Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.UpperChest));
        }

        private static HumanoidSnapshot Snapshot(IReadOnlyDictionary<HumanBodyBones, BonePose> bones)
        {
            return new HumanoidSnapshot(Vector3.right, Vector3.up, Vector3.forward, bones);
        }

        private static BonePose Pose(float x, float y, float z)
        {
            return new BonePose(new Vector3(x, y, z), Quaternion.identity);
        }
    }
}
