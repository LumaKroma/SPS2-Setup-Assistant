using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class WriteDefaultsDiagnosticTests
    {
        private const string Notice = "Warning from VRCFury!\nThis layer contains no valid animations,\nand will be removed during a real upload,\nMake sure the animated objects / components\nactually exist at the paths used in the clips";
        [TestCase(true)]
        [TestCase(false)]
        public void NativeNoticeDoesNotChoosePolicy(bool wd)
        {
            var controller = new AnimatorController();
            controller.AddLayer("Fixture (NO VALID ANIMATIONS)");
            var machine = controller.layers[0].stateMachine;
            var real = machine.AddState("Real"); real.writeDefaultValues = wd;
            var notice = machine.AddState(Notice); notice.writeDefaultValues = !wd;
            try { Assert.AreEqual(wd, Sps2BuildIntegration.InstantWriteDefaults(controller)); }
            finally { UnityEngine.Object.DestroyImmediate(controller); UnityEngine.Object.DestroyImmediate(machine); UnityEngine.Object.DestroyImmediate(real); UnityEngine.Object.DestroyImmediate(notice); }
        }
        [TestCase("ordinary")]
        [TestCase("layer")]
        [TestCase("motion")]
        [TestCase("incoming")]
        [TestCase("outgoing")]
        [TestCase("default")]
        public void RealMixedStatesStillFail(string variation)
        {
            var controller = new AnimatorController();
            controller.AddLayer(variation == "layer" ? "Fixture" : "Fixture (NO VALID ANIMATIONS)");
            var machine = controller.layers[0].stateMachine;
            var real = machine.AddState("Real"); real.writeDefaultValues = false;
            var other = machine.AddState(variation == "ordinary" ? "Warning from VRCFury! custom" : Notice);
            other.writeDefaultValues = true;
            var clip = new AnimationClip();
            if (variation == "motion") other.motion = clip;
            if (variation == "incoming") real.AddTransition(other);
            if (variation == "outgoing") other.AddTransition(real);
            if (variation == "default") machine.defaultState = other;
            try { Assert.Throws<InvalidOperationException>(() => Sps2BuildIntegration.InstantWriteDefaults(controller)); }
            finally { UnityEngine.Object.DestroyImmediate(controller); UnityEngine.Object.DestroyImmediate(machine); UnityEngine.Object.DestroyImmediate(real); UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(clip); }
        }
    }
}
