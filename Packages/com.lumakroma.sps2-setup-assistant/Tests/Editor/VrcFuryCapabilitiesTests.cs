using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    // Synthetic schema contract tests, not historical VRCFury integration evidence.
    public class CompatibilityNoSpsPlug : MonoBehaviour { public bool autoRenderer; }
    public class CompatibilitySpsPlug : MonoBehaviour { public bool enableSps; }
    public class CompatibilityEarlySpsPlug : MonoBehaviour { public bool configureSps; }
    public class CompatibilityBasicSocket : MonoBehaviour
    {
        public enum Mode { None, Hole, Ring, Auto }
        public new string name;
        public bool addMenuItem = true;
        public bool enableAuto = true;
        public Mode addLight;
    }
    public class CompatibilityInitialSps2Socket : CompatibilityBasicSocket
    {
        public bool useSharedTag = true, useLights = true, useRadiusOffset;
        public List<Transform> guidedPath = new List<Transform>();
    }
    public class CompatibilityVectorPathSocket : CompatibilityBasicSocket
    {
        public bool useSharedTag = true, useLights = true;
        [Serializable] public class Stop
        {
            public Transform transform;
            public bool customizeTangentIn, customizeTangentOut;
            public Vector3 tangentIn, tangentOut;
        }
        public List<Stop> guidedPathStops = new List<Stop>();
    }
    public class CompatibilityLocalPathSocket : CompatibilityBasicSocket
    {
        public bool useSharedTag = true, useLights = true, offsetsInLocalUnits = true;
        [Serializable] public class Stop
        {
            public Transform transform;
            public bool shrink, customizeTangentIn, customizeTangentOut;
            public Vector3 tangentInLocal, tangentOutLocal;
        }
        public List<Stop> guidedPathStops = new List<Stop>();
    }
    public sealed class VrcFuryCapabilitiesTests
    {
        [Test]
        public void MissingSpsBlocksBeforeGeneratingAndExplainsUpdate()
        {
            foreach (var caps in new[] { VrcFuryCapabilities.Inspect(null, null, false),
                VrcFuryCapabilities.Inspect(typeof(CompatibilityBasicSocket), typeof(CompatibilityNoSpsPlug), false) })
            {
                Assert.IsFalse(caps.HasSps); Assert.IsFalse(caps.CanGenerate);
                var error = Assert.Throws<InvalidOperationException>(() => caps.Effective(FullSetupCatalog.CreateDefault()));
                StringAssert.Contains("VCC", error.Message);
                Assert.IsNotEmpty(caps.Notices());
            }
        }

        [Test]
        public void Sps1AllowsBasicSocketsAndRetainsRequestedSettings()
        {
            var caps = VrcFuryCapabilities.Inspect(typeof(CompatibilityBasicSocket), typeof(CompatibilitySpsPlug), false);
            Assert.IsTrue(caps.CanGenerate); Assert.IsTrue(caps.HasSps); Assert.IsFalse(caps.Sps2);
            var requested = FullSetupCatalog.CreateDefault();
            requested.penetration = requested.legacy = true;
            requested.parts[0].depth = true;
            var warnings = new List<string>(); var effective = caps.Effective(requested, warnings);
            Assert.IsFalse(effective.penetration); Assert.IsFalse(effective.legacy);
            Assert.IsTrue(effective.autoMode); Assert.IsFalse(effective.parts[0].depth);
            Assert.IsTrue(requested.penetration); Assert.IsTrue(requested.parts[0].depth);
            Assert.AreEqual(requested.parts.Count, effective.parts.Count);
            Assert.IsTrue(warnings.Any(n => n.Contains("SPS1")));
        }

        [Test]
        public void OriginalConfigureSpsFieldIsRecognizedAsSps1()
        {
            var caps = VrcFuryCapabilities.Inspect(typeof(CompatibilityBasicSocket), typeof(CompatibilityEarlySpsPlug), false);
            Assert.IsTrue(caps.HasSps); Assert.IsTrue(caps.CanGenerate); Assert.IsFalse(caps.Sps2);
        }

        [Test]
        public void FirstSps2PathDoesNotImplyCollapseOrLegacy()
        {
            var caps = VrcFuryCapabilities.Inspect(typeof(CompatibilityInitialSps2Socket), typeof(CompatibilitySpsPlug), false);
            Assert.IsTrue(caps.Sps2); Assert.IsTrue(caps.Path); Assert.IsFalse(caps.PathStops);
            Assert.IsFalse(caps.Collapse); Assert.IsFalse(caps.Tangents);
            Assert.IsFalse(caps.Legacy);
            var requested = FullSetupCatalog.CreateDefault(); requested.penetration = true;
            var effective = caps.Effective(requested);
            Assert.IsTrue(effective.penetration); Assert.IsTrue(effective.showInternalThickness);
            Assert.IsTrue(caps.Notices().Any(n => n.Contains(Localization.Sps2Localization.L("首付近で太さを0にする"))));
        }

        [Test]
        public void VectorPathsWithoutShrinkHaveIndependentCapabilities()
        {
            var caps = VrcFuryCapabilities.Inspect(typeof(CompatibilityVectorPathSocket), typeof(CompatibilitySpsPlug), false);
            Assert.IsTrue(caps.PathStops); Assert.IsTrue(caps.Tangents); Assert.IsFalse(caps.LocalTangents);
            Assert.IsTrue(caps.Legacy); Assert.IsFalse(caps.Collapse);
        }

        [Test]
        public void LocalPathFieldsAreRecognizedWithoutObsoleteWorldFields()
        {
            var caps = VrcFuryCapabilities.Inspect(typeof(CompatibilityLocalPathSocket), typeof(CompatibilitySpsPlug), false);
            Assert.IsTrue(caps.LocalTangents); Assert.IsTrue(caps.Tangents); Assert.IsTrue(caps.Collapse);
        }

        [Test]
        public void ProbeDoesNotLeaveObjectsAndCoreHasNoVrcFuryAssemblyDependency()
        {
            int before = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            VrcFuryCapabilities.Inspect(typeof(CompatibilityBasicSocket), typeof(CompatibilitySpsPlug), false);
            Assert.AreEqual(before, Resources.FindObjectsOfTypeAll<GameObject>().Length);
            Assert.IsFalse(typeof(VrcFuryCapabilities).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("com.vrcfury")));
        }

        [Test]
        public void LegacyNameIdentityCanBeRecoveredAndMigratedToOscId()
        {
            if (!VrcFuryCapabilities.Current.CanGenerate) Assert.Ignore("Native SPS fixture unavailable.");
            var obj = new GameObject("Identity fixture");
            try
            {
                VrcFuryApi.CreateSocket(obj);
                var native = VrcFuryCompatibility.FindSocket(obj);
                var data = new SerializedObject(native);
                string token = Sps2SetupStorage.Token(new Sps2SetupContext { identity = "0123456789abcdef0123456789abcdef" }, "mouth");
                data.FindProperty("name").stringValue = token;
                var osc = data.FindProperty("oscId"); if (osc != null) osc.stringValue = "";
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(token, VrcFuryCompatibility.ReadIdentity(native));
                VrcFuryCompatibility.SetAuthoringIdentity(native, token);
                data.Update();
                Assert.AreEqual(token, (data.FindProperty("oscId") ?? data.FindProperty("name")).stringValue);
                data.FindProperty("name").stringValue = "Unrelated Socket";
                if (data.FindProperty("oscId") != null) data.FindProperty("oscId").stringValue = "foreign-id";
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsNull(VrcFuryCompatibility.ReadIdentity(native));
            }
            finally { DisposeUndoFixture(obj); }
        }

        [Test]
        public void AmbiguousNativeToggleMappingIsRejectedBeforeRestoration()
        {
            var avatar = new GameObject("Collision fixture");
            var menu = ScriptableObject.CreateInstance<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();
            var parameters = ScriptableObject.CreateInstance<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters>();
            try
            {
                var descriptor = avatar.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
                var root = new Sps2SetupContext { identity = "0123456789abcdef0123456789abcdef" };
                root.sockets.Add(new GeneratedSocket { id = "mouth" });
                for (int i = 0; i < 2; i++) menu.controls.Add(new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control {
                    name = "Native label " + i,
                    type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter {
                        name = "VF79_" + Sps2SetupStorage.Token(root, "mouth") }
                });
                Assert.Throws<InvalidOperationException>(() => Sps2BuildIntegration.Complete(descriptor, root));
                Assert.AreSame(menu, descriptor.expressionsMenu);
                Assert.AreSame(parameters, descriptor.expressionParameters);
                Assert.AreEqual("Native label 0", menu.controls[0].name);
            }
            finally { DisposeUndoFixture(avatar); UnityEngine.Object.DestroyImmediate(menu); UnityEngine.Object.DestroyImmediate(parameters); }
        }

        private static void DisposeUndoFixture(GameObject obj)
        {
            // TestRunner later reverts its group; destroyed native targets must not remain there.
            Undo.FlushUndoRecordObjects();
            foreach (var component in obj.GetComponents<Component>()) Undo.ClearUndo(component);
            Undo.ClearUndo(obj);
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
