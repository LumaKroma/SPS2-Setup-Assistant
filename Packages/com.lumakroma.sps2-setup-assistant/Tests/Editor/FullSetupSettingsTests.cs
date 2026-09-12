using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class FullSetupSettingsTests
    {
        [Test]
        public void PresetRoundTripPreservesCustomAndDisabledActionInputs()
        {
            var target = new GameObject("Custom target");
            var clip = new AnimationClip();
            try
            {
                var setup = FullSetupCatalog.CreateDefault();
                var mouth = setup.parts.Single(p => p.id == "mouth");
                mouth.range = new Vector2(-.15f, .4f); mouth.units = DepthUnits.Plugs; mouth.depth = false;
                mouth.actions[0].kind = DepthActionKind.Object; mouth.actions[0].clip = clip;
                mouth.actions[0].target = target; mouth.actions[0].weight = 63; mouth.actions[0].objectOn = false;
                setup.parts.Add(new SocketSettings { id = "custom", custom = true, included = true, target = target.transform });
                var copy = setup.Copy();
                foreach (int preset in new[] { 0, 2, 1 }) FullSetupCatalog.ApplyPreset(copy, preset);
                var copiedMouth = copy.parts.Single(p => p.id == "mouth");
                Assert.That(copiedMouth.range, Is.EqualTo(mouth.range));
                Assert.That(copiedMouth.units, Is.EqualTo(DepthUnits.Plugs));
                Assert.That(copiedMouth.depth, Is.False);
                Assert.That(copiedMouth.actions[0].clip, Is.SameAs(clip));
                Assert.That(copiedMouth.actions[0].target, Is.SameAs(target));
                Assert.That(copiedMouth.actions[0].weight, Is.EqualTo(63));
                Assert.That(copiedMouth.actions[0].objectOn, Is.False);
                Assert.That(copy.parts.Single(p => p.custom).target, Is.SameAs(target.transform));
                Assert.That(copy.parts.Single(p => p.custom).included, Is.True);
                copiedMouth.actions[0].weight = 12;
                Assert.That(mouth.actions[0].weight, Is.EqualTo(63), "A draft must not change applied settings before Apply.");
            }
            finally { UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(clip); }
        }

        [Test]
        public void NonlinearDistanceControlIsMonotonicAndRoundTripsBothSidesOfZero()
        {
            float previous = -2;
            for (int i = 0; i <= 1000; i++)
            {
                float position = i / 1000f;
                float distance = FullSetupCatalog.SliderToDistance(position);
                Assert.That(distance, Is.GreaterThanOrEqualTo(previous));
                Assert.That(FullSetupCatalog.DistanceToSlider(distance), Is.EqualTo(position).Within(.00001));
                previous = distance;
            }
            Assert.That(FullSetupCatalog.SliderToDistance(.25f), Is.Zero);
            Assert.That(FullSetupCatalog.SliderToDistance(0), Is.EqualTo(-1));
            Assert.That(FullSetupCatalog.SliderToDistance(1), Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForeignMetadataReferencesAreRejectedWithoutMovingOrDeletingThem(bool plugReference)
        {
            var avatar = new GameObject("Avatar");
            var foreign = new GameObject("Unrelated user object");
            try
            {
                var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                var obj = new GameObject("Owned"); obj.transform.SetParent(avatar.transform);
                var root = obj.AddComponent<Sps2SetupRoot>(); root.identity = "test";
                if (plugReference) root.testPlug = foreign;
                else root.sockets.Add(new GeneratedSocket { id = "mouth", anchor = foreign.transform });
                Assert.Throws<InvalidOperationException>(() => FullSetupGenerator.Find(descriptor));
                Assert.That(foreign, Is.Not.Null);
                Assert.That(foreign.transform.parent, Is.Null);
                Assert.That(root.transform.parent, Is.SameAs(avatar.transform));
            }
            finally { UnityEngine.Object.DestroyImmediate(avatar); UnityEngine.Object.DestroyImmediate(foreign); }
        }
    }
}
