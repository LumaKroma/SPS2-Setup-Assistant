using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class BackendMigrationTests
    {
        [TestCase(SpsFamily.VrcFury, SpsFamily.SpsForNdmf)]
        [TestCase(SpsFamily.SpsForNdmf, SpsFamily.VrcFury)]
        public void MigrationRetainsCommonDataAndDoesNotMutateSource(SpsFamily from, SpsFamily to)
        {
            var source = new SetupSettings { family = from, menuLanguage = DisplayLanguage.English, localOnly = true,
                autoMode = false, legacy = false, penetration = false, showInternalThickness = true };
            source.parts.Add(new SocketSettings { id = "custom", custom = true, included = true,
                menuNameOverride = "Custom display", depth = true, range = new Vector2(.01f, .09f),
                fingerRing = new FingerRingCalibration { calibrated = true, center = Vector3.one } });
            source.parts[0].actions.Add(new DepthActionSettings { kind = DepthActionKind.Object, objectOn = false });
            var before = JsonUtility.ToJson(source);
            var requested = source.Copy(); requested.family = to;
            var migrated = FullSetupGenerator.MigrationSettings(source, requested);
            Assert.AreEqual(to, migrated.family); Assert.AreEqual(before, JsonUtility.ToJson(source));
            Assert.IsFalse(migrated.parts[0].actions[0].objectOn);
            Assert.AreEqual("Custom display", migrated.parts[0].menuNameOverride);
            Assert.AreEqual(Vector3.one, migrated.parts[0].fingerRing.center);
            migrated.parts[0].actions[0].objectOn = true;
            Assert.IsFalse(source.parts[0].actions[0].objectOn);
            Assert.IsFalse(requested.parts[0].actions[0].objectOn);
        }
        [Test]
        public void MigrationRejectsSimultaneousDestructivePartEdits()
        {
            var source = new SetupSettings(); source.parts.Add(new SocketSettings { id = "preserved", included = true });
            var requested = source.Copy(); requested.family = SpsFamily.SpsForNdmf; requested.parts.Clear();
            Assert.Throws<InvalidOperationException>(() => FullSetupGenerator.MigrationSettings(source, requested));
            Assert.AreEqual(1, source.parts.Count);
        }
        [Test]
        public void LegacyLocalTangentsPreserveWorldVectorAcrossRotatedNonuniformScale()
        {
            var parent = new GameObject("Tangent parent"); var stop = new GameObject("Tangent frame");
            try
            {
                parent.transform.rotation = Quaternion.Euler(13, 28, 41);
                parent.transform.localScale = new Vector3(2, 3, .7f);
                stop.transform.SetParent(parent.transform, false);
                stop.transform.localRotation = Quaternion.Euler(27, 33, 12);
                stop.transform.localScale = new Vector3(.5f, 1.7f, 2.3f);
                var legacy = new Vector3(.02f, -.04f, .03f);
                var local = SpsMigrationAdapter.ConvertTangent(legacy, stop.transform, false, true);
                Assert.That(Vector3.Distance(stop.transform.rotation * legacy, stop.transform.TransformVector(local)), Is.LessThan(1e-6f));
                var roundtrip = SpsMigrationAdapter.ConvertTangent(local, stop.transform, true, false);
                Assert.That(Vector3.Distance(legacy, roundtrip), Is.LessThan(1e-6f));
                stop.transform.localScale = Vector3.zero;
                Assert.Throws<InvalidOperationException>(() => SpsMigrationAdapter.ConvertTangent(legacy, stop.transform, false, true));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
        [Test]
        public void NativeCustomModificationFailsPreflightWithoutChangingSource()
        {
            if (!VrcFuryCapabilities.For(SpsFamily.VrcFury).CanGenerate) Assert.Ignore("Real VRCFury dependency fixture required.");
            using (SpsBackend.Use(SpsFamily.VrcFury))
            {
                var a = new GameObject("Source"); var b = new GameObject("Expected");
                try
                {
                    var source = VrcFuryApi.CreateSocket(a); var expected = VrcFuryApi.CreateSocket(b);
                    source.SetName("User native override"); expected.SetName("Generated name");
                    var original = SpsMigrationAdapter.State(VrcFuryCompatibility.FindSocket(a));
                    Assert.Throws<InvalidOperationException>(() => SpsMigrationAdapter.ValidateEquivalent(
                        VrcFuryCompatibility.FindSocket(a), VrcFuryCompatibility.FindSocket(b)));
                    Assert.AreEqual(original, SpsMigrationAdapter.State(VrcFuryCompatibility.FindSocket(a)));
                }
                finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
            }
        }
    }
}
