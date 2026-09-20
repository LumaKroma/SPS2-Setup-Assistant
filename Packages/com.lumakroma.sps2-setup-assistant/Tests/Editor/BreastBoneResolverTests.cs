using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class BreastBoneResolverTests
    {
        private GameObject avatar;
        private Transform torso;
        [SetUp] public void SetUp() { avatar = new GameObject("Avatar"); torso = Child(avatar.transform, "Chest"); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(avatar); }
        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(parent); return t;
        }

        [TestCase("Breast_L", "Breast_R")]
        [TestCase("Breast.L", "Breast.R")]
        [TestCase("breast_L", "breast_R")]
        [TestCase("Breast_1_L", "Breast_1_R")]
        public void ResolvesBothSidesAndNumberedDescendants(string leftName, string rightName)
        {
            var left = Child(torso, leftName); var right = Child(torso, rightName);
            var segment = Child(left, "Breast_2_L");
            var tip = Child(segment, "Breast_3_L");
            var bones = new[] { tip, right, segment, left, left, null };
            Assert.That(BreastBoneResolver.Resolve(bones, torso, true), Is.SameAs(left));
            Assert.That(BreastBoneResolver.Resolve(bones, torso, false), Is.SameAs(right));
            Assert.That(BreastBoneResolver.Resolve(new[] { tip }, torso, true), Is.SameAs(left));
        }

        [Test]
        public void SeparateOutfitArmatureDoesNotCompeteRegardlessOfOrder()
        {
            var body = Child(torso, "Breast_L");
            var outfit = Child(Child(avatar.transform, "OutfitChest"), "Breast_L");
            Assert.That(BreastBoneResolver.Resolve(new[] { outfit, body }, torso, true), Is.SameAs(body));
            Assert.That(BreastBoneResolver.Resolve(new[] { body, outfit }, torso, true), Is.SameAs(body));
            Assert.That(BreastBoneResolver.Resolve(new[] { outfit }, torso, true), Is.Null);
        }

        [Test]
        public void AmbiguousSameSkeletonRootsRemainUnresolved()
        {
            var first = Child(torso, "Breast_L"); var second = Child(torso, "Breast_1_L");
            Assert.That(BreastBoneResolver.Resolve(new[] { first, second }, torso, true), Is.Null);
            Assert.That(BreastBoneResolver.Resolve(new[] { second, first }, torso, true), Is.Null);
            Assert.That(BreastBoneResolver.Resolve(new[] { first }, null, true), Is.Null);
        }

        [Test]
        public void ChestScopeIncludesBreastsOnEitherSideOfUpperChest()
        {
            var upper = Child(torso, "UpperChest");
            var left = Child(torso, "Breast_L"); var right = Child(upper, "Breast_R");
            Assert.That(BreastBoneResolver.Resolve(new[] { left, right }, torso, true), Is.SameAs(left));
            Assert.That(BreastBoneResolver.Resolve(new[] { left, right }, torso, false), Is.SameAs(right));
        }
    }
}
