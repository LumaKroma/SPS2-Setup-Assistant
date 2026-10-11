using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class BackendMigrationComparisonTests
    {
        [TestCase(SpsFamily.VrcFury)]
        [TestCase(SpsFamily.SpsForNdmf)]
        public void SocketComparisonIgnoresUnityOwnerChildrenButRetainsNativeFields(SpsFamily family)
        {
            if (!VrcFuryCapabilities.For(family).CanGenerate) Assert.Ignore("Real native dependency fixture required.");
            var a = new GameObject("Actual"); var b = new GameObject("Stage");
            try
            {
                using (SpsBackend.Use(family))
                {
                    VrcFuryApi.CreateSocket(a); VrcFuryApi.CreateSocket(b);
                    var actual = VrcFuryCompatibility.FindSocket(a); var expected = VrcFuryCompatibility.FindSocket(b);
                    var state = SpsMigrationAdapter.State(actual);
                    Assert.IsFalse(state.Split('\n').Any(p => p.StartsWith("m_GameObject.", StringComparison.Ordinal)));
                    SpsMigrationAdapter.ValidateEquivalent(actual, expected);
                    var data = new SerializedObject(actual); data.FindProperty("name").stringValue = "Native user override";
                    data.ApplyModifiedPropertiesWithoutUndo();
                    Assert.Throws<InvalidOperationException>(() => SpsMigrationAdapter.ValidateEquivalent(actual, expected));
                    Assert.AreEqual("Native user override", new SerializedObject(actual).FindProperty("name").stringValue);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        [Test]
        public void ArmatureSelfReferenceNormalizesOnlyExpectedSelfAndRejectsForeignActual()
        {
            if (!VrcFuryCapabilities.For(SpsFamily.VrcFury).CanGenerate) Assert.Ignore("Real VRCFury dependency fixture required.");
            var a = new GameObject("Actual"); var b = new GameObject("Stage"); var foreign = new GameObject("Foreign");
            try
            {
                using (SpsBackend.Use(SpsFamily.VrcFury))
                {
                    VrcFuryApi.Attach(a, HumanBodyBones.Head); VrcFuryApi.Attach(b, HumanBodyBones.Head);
                    var actual = a.GetComponents<Component>().Single(c => !(c is Transform));
                    var expected = b.GetComponents<Component>().Single(c => !(c is Transform));
                    SpsMigrationAdapter.ValidateEquivalent(actual, expected);
                    Assert.AreEqual(a, new SerializedObject(actual).FindProperty("content.propBone").objectReferenceValue);
                    var data = new SerializedObject(actual); data.FindProperty("content.propBone").objectReferenceValue = foreign;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    Assert.Throws<InvalidOperationException>(() => SpsMigrationAdapter.ValidateEquivalent(actual, expected));
                    Assert.AreEqual(foreign, new SerializedObject(actual).FindProperty("content.propBone").objectReferenceValue);
                    // A foreign reference on the expected side must also remain a meaningful difference.
                    data.FindProperty("content.propBone").objectReferenceValue = a; data.ApplyModifiedPropertiesWithoutUndo();
                    var stageData = new SerializedObject(expected); stageData.FindProperty("content.propBone").objectReferenceValue = foreign;
                    stageData.ApplyModifiedPropertiesWithoutUndo();
                    Assert.Throws<InvalidOperationException>(() => SpsMigrationAdapter.ValidateEquivalent(actual, expected));
                    Assert.AreEqual(foreign, new SerializedObject(expected).FindProperty("content.propBone").objectReferenceValue);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(foreign); }
        }

        [Test]
        public void MissingNativeOwnershipStopsApplyAndOffersManualRecoveryWithoutChangingRoot()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Issue186Validation/MigrationAvatarTemplate.prefab");
            if (template == null) Assert.Ignore("Owned native humanoid fixture required.");
            var avatarObject = UnityEngine.Object.Instantiate(template);
            try
            {
                var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                Assert.IsNotNull(avatar); Assert.IsTrue(avatarObject.GetComponent<Animator>().isHuman);
                var root = new GameObject(FullSetupGenerator.RootName); root.transform.SetParent(avatarObject.transform, false);
                var added = new GameObject("User addition"); added.transform.SetParent(root.transform, false);
                var before = root.GetInstanceID();
                Sps2SetupContext result; string message;
                Assert.IsFalse(FullSetupGenerator.Apply(avatar, new SetupSettings { family = SpsFamily.SpsForNdmf }, false, out result, out message));
                Assert.AreEqual(SpsBackend.MissingSourceGuidance, message);
                Assert.AreEqual(before, avatar.transform.Find(FullSetupGenerator.RootName).gameObject.GetInstanceID());
                Assert.AreEqual(root.transform, added.transform.parent);
                Assert.IsNull(result);
                // Missing schema is simulated through the existing capability contract seam; no package is removed.
                var capabilities = VrcFuryCapabilities.Inspect(null, null);
                Assert.IsFalse(capabilities.CanGenerate); Assert.IsNotEmpty(capabilities.BlockReason);
            }
            finally { UnityEngine.Object.DestroyImmediate(avatarObject); }
        }
    }
}
