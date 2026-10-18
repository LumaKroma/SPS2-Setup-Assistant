using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class SpsBackendTests
    {
        [TestCase(true, false, SpsFamily.VrcFury)]
        [TestCase(false, true, SpsFamily.SpsForNdmf)]
        [TestCase(true, true, SpsFamily.VrcFury)]
        [TestCase(false, false, SpsFamily.VrcFury)]
        public void NewDraftDefaultsToTheOnlyUsableBackend(bool furyAvailable, bool ndmfAvailable, SpsFamily expected)
        {
            var draft = Sps2SetupAssistantWindow.CreateSettingsForAvatar(null, furyAvailable, ndmfAvailable);
            Assert.AreEqual(expected, draft.family);
        }
        [TestCase(SpsFamily.VrcFury, true, false)]
        [TestCase(SpsFamily.VrcFury, false, true)]
        [TestCase(SpsFamily.VrcFury, true, true)]
        [TestCase(SpsFamily.VrcFury, false, false)]
        [TestCase(SpsFamily.SpsForNdmf, true, false)]
        [TestCase(SpsFamily.SpsForNdmf, false, true)]
        [TestCase(SpsFamily.SpsForNdmf, true, true)]
        [TestCase(SpsFamily.SpsForNdmf, false, false)]
        public void SavedFamilyIsPreservedRegardlessOfAvailableBackends(SpsFamily savedFamily, bool furyAvailable, bool ndmfAvailable)
        {
            var saved = new SetupSettings { family = savedFamily, localOnly = true, penetration = false };
            var existing = new Sps2SetupContext { settings = saved };
            var loaded = Sps2SetupAssistantWindow.CreateSettingsForAvatar(existing, furyAvailable, ndmfAvailable);
            Assert.AreEqual(savedFamily, loaded.family);
            Assert.AreEqual(savedFamily, saved.family);
            Assert.IsTrue(loaded.localOnly);
            Assert.IsFalse(loaded.penetration);
            Assert.AreNotSame(saved, loaded);
        }
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
        public void ExistingFamilyGuardRemainsStrictOutsideDedicatedMigration(SpsFamily existing, SpsFamily requested)
        {
            var settings = new SetupSettings { family = existing };
            var root = new Sps2SetupContext { settings = settings };
            Assert.Throws<InvalidOperationException>(() => SpsBackend.ValidateExisting(root, requested));
            Assert.AreEqual(existing, settings.family);
        }

        [Test]
        public void FurySdkCallbacksIgnoreNdmfSocketWithMissingSettingsAsset()
        {
            if (!VrcFuryCapabilities.For(SpsFamily.SpsForNdmf).CanGenerate) Assert.Ignore("Pinned shipped SPSNDMF fixture required.");
            var avatar = new GameObject("NDMF callback fixture");
            try
            {
                avatar.AddComponent<VRCAvatarDescriptor>();
                var native = avatar.AddComponent(VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.SpsForNdmf)));
                var data = new SerializedObject(native);
                data.FindProperty("oscId").stringValue = "__SPS2_0123456789abcdef0123456789abcdef_mouth";
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(Sps2BuildIntegration.Run(avatar, true));
                Assert.IsTrue(Sps2BuildIntegration.Run(avatar, false));
            }
            finally { UnityEngine.Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void FuryBuildLookupStillRejectsMissingSettingsAsset()
        {
            if (!VrcFuryCapabilities.For(SpsFamily.VrcFury).CanGenerate) Assert.Ignore("Pinned shipped VRCFury fixture required.");
            var avatar = new GameObject("Fury callback fixture");
            try
            {
                var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                var native = avatar.AddComponent(VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)));
                var data = new SerializedObject(native);
                data.FindProperty("oscId").stringValue = "__SPS2_0123456789abcdef0123456789abcdef_mouth";
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => Sps2SetupStorage.Find(descriptor, true, SpsFamily.VrcFury));
            }
            finally { UnityEngine.Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void NativeNdmfSocketNameTracksSavedDisplayWithoutChangingIdentity()
        {
            if (!VrcFuryCapabilities.For(SpsFamily.SpsForNdmf).CanGenerate) Assert.Ignore("Pinned shipped SPSNDMF fixture required.");
            var pose = new GameObject("NDMF name fixture");
            try
            {
                var part = new SocketSettings { id = "mouth", name = "Internal mouth" };
                var setup = new SetupSettings { family = SpsFamily.SpsForNdmf, menuLanguage = DisplayLanguage.English };
                using (SpsBackend.Use(setup.family))
                {
                    var native = VrcFuryCompatibility.CreateSocket(pose, part, setup, new System.Collections.Generic.List<string>());
                    var token = "__SPS2_0123456789abcdef0123456789abcdef_mouth";
                    VrcFuryCompatibility.SetAuthoringIdentity(native, token);
                    var data = new SerializedObject(native);
                    Assert.AreEqual("Mouth", data.FindProperty("name").stringValue);
                    part.menuNameOverride = "Saved custom";
                    VrcFuryCompatibility.ConfigureDisplayName(native, part, setup);
                    data.Update(); Assert.AreEqual("Saved custom", data.FindProperty("name").stringValue);
                    part.menuNameOverride = null; setup.menuLanguage = DisplayLanguage.Japanese;
                    VrcFuryCompatibility.ConfigureDisplayName(native, part, setup);
                    data.Update();
                    Assert.AreEqual(SocketDisplayNames.Resolve(part, setup.menuLanguage), data.FindProperty("name").stringValue);
                    Assert.AreEqual(token, data.FindProperty("oscId").stringValue);
                }
            }
            finally
            {
                Undo.FlushUndoRecordObjects();
                foreach (var component in pose.GetComponents<Component>()) Undo.ClearUndo(component);
                Undo.ClearUndo(pose);
                UnityEngine.Object.DestroyImmediate(pose);
            }
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
