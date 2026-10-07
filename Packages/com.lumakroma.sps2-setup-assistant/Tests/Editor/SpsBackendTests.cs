using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class SpsBackendTests
    {
        [TestCase("com.vrcfury.vrcfury", SpsFamily.VrcFury, true)]
        [TestCase("net.nrzk.vfstub", SpsFamily.VrcFury, false)]
        [TestCase("net.nrzk.spsndmf-migrator", SpsFamily.VrcFury, false)]
        [TestCase("net.nrzk.spsndmf", SpsFamily.VrcFury, false)]
        [TestCase("net.nrzk.spsndmf", SpsFamily.SpsForNdmf, true)]
        [TestCase("com.vrcfury.vrcfury", SpsFamily.SpsForNdmf, false)]
        [TestCase(null, SpsFamily.VrcFury, false)]
        public void StubOrOtherFamilyCannotSatisfySelectedPackage(string id, SpsFamily family, bool expected) =>
            Assert.AreEqual(expected, SpsBackend.PackageIdMatches(id, family));

        [Test]
        public void MissingFamilyInOldSettingsDefaultsToFury()
        {
            var settings = JsonUtility.FromJson<SetupSettings>("{\"modularAvatar\":true,\"parts\":[]}");
            Assert.AreEqual(SpsFamily.VrcFury, settings.family);
            Assert.IsTrue(settings.Copy().modularAvatar);
        }
        [Test]
        public void SelectedFamilySurvivesCopyAndJsonWithoutChangingOtherValues()
        {
            var settings = new SetupSettings { family = SpsFamily.SpsForNdmf, localOnly = true, penetration = false };
            var restored = JsonUtility.FromJson<SetupSettings>(JsonUtility.ToJson(settings.Copy()));
            Assert.AreEqual(settings.family, restored.family);
            Assert.IsTrue(restored.localOnly); Assert.IsFalse(restored.penetration);
            Assert.AreEqual(SpsFamily.SpsForNdmf, settings.family);
        }
        [Test]
        public void SelectionRestoresNestedFamilyAfterException()
        {
            Assert.AreEqual(SpsFamily.VrcFury, SpsBackend.ActiveFamily);
            using (SpsBackend.Use(SpsFamily.SpsForNdmf))
            {
                Assert.Throws<InvalidOperationException>(() =>
                { using (SpsBackend.Use(SpsFamily.VrcFury)) throw new InvalidOperationException(); });
                Assert.AreEqual(SpsFamily.SpsForNdmf, SpsBackend.ActiveFamily);
            }
            Assert.AreEqual(SpsFamily.VrcFury, SpsBackend.ActiveFamily);
        }
        [Test]
        public void UnknownFamilyFailsBeforeChangingSelection()
        {
            Assert.Throws<InvalidOperationException>(() => SpsBackend.Use((SpsFamily)99));
            Assert.AreEqual(SpsFamily.VrcFury, SpsBackend.ActiveFamily);
        }
        [TestCase(SpsFamily.VrcFury, SpsFamily.SpsForNdmf)]
        [TestCase(SpsFamily.SpsForNdmf, SpsFamily.VrcFury)]
        public void ExistingSetupCannotBeConvertedEvenDuringRegeneration(SpsFamily existing, SpsFamily requested)
        {
            var settings = new SetupSettings { family = existing };
            var root = new Sps2SetupContext { settings = settings };
            Assert.Throws<InvalidOperationException>(() => SpsBackend.ValidateExisting(root, requested));
            Assert.AreEqual(existing, settings.family);
        }
        // Uses actual installed shipped component, not a replacement native stub.
        [TestCase(false)]
        [TestCase(true)]
        public void NativeNdmfObjectActionRoundTripRetainsExplicitMode(bool turnOff)
        {
            if (!VrcFuryCapabilities.For(SpsFamily.SpsForNdmf).CanGenerate) Assert.Ignore("Pinned shipped SPSNDMF/MA/NDMF fixture required.");
            var first = new GameObject("Depth source"); var restored = new GameObject("Depth restored");
            try
            {
                var type = VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.SpsForNdmf));
                var source = first.AddComponent(type);
                NdmfDepthActions.Create(source, new Vector2(.01f, .08f), 0, true).AddTurnOn(first);
                const string path = "depthActions2.Array.data[0].actionSet.actions.Array.data[0].";
                var data = new SerializedObject(source); var mode = data.FindProperty(path + "mode");
                if (turnOff) mode.enumValueIndex = Array.IndexOf(mode.enumNames, "TurnOff");
                data.ApplyModifiedPropertiesWithoutUndo();
                var copy = restored.AddComponent(type);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
                var after = new SerializedObject(copy);
                var afterMode = after.FindProperty(path + "mode");
                Assert.AreEqual(turnOff ? "TurnOff" : "TurnOn", afterMode.enumNames[afterMode.enumValueIndex]);
                Assert.AreEqual(1, after.FindProperty(path + "version").intValue);
                Assert.AreEqual(first, after.FindProperty(path + "obj").objectReferenceValue);
                Assert.AreEqual(new Vector2(.01f, .08f), after.FindProperty("depthActions2.Array.data[0].range").vector2Value);
            }
            finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(restored); }
        }
    }
}
