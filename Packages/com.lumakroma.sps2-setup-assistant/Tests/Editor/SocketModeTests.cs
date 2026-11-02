using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class SocketModeTests
    {
        private static string Mode(Component socket)
        {
            var mode = new SerializedObject(socket).FindProperty("addLight");
            return mode.enumNames[mode.enumValueIndex];
        }

        [TestCase(SpsFamily.VrcFury, "mouth", false, "Auto")]
        [TestCase(SpsFamily.SpsForNdmf, "mouth", false, "Auto")]
        [TestCase(SpsFamily.VrcFury, "anus", false, "Auto")]
        [TestCase(SpsFamily.SpsForNdmf, "anus", false, "Auto")]
        [TestCase(SpsFamily.VrcFury, "mouth", true, "Ring")]
        [TestCase(SpsFamily.SpsForNdmf, "mouth", true, "Ring")]
        [TestCase(SpsFamily.VrcFury, "anus", true, "Ring")]
        [TestCase(SpsFamily.SpsForNdmf, "anus", true, "Ring")]
        [TestCase(SpsFamily.VrcFury, "vagina", true, "Auto")]
        [TestCase(SpsFamily.SpsForNdmf, "vagina", true, "Auto")]
        public void CreationUsesPenetrationOnlyForEndpoints(SpsFamily family, string id, bool penetration, string expected)
        {
            using (SpsBackend.Use(family))
            {
                if (!VrcFuryCapabilities.Current.CanGenerate) Assert.Ignore("Native SPS dependency required.");
                var obj = new GameObject("Socket mode test");
                try
                {
                    var socket = VrcFuryCompatibility.CreateSocket(obj,
                        new SocketSettings { id = id, name = id },
                        new SetupSettings { penetration = penetration }, new List<string>());
                    Assert.That(Mode(socket), Is.EqualTo(expected));
                }
                finally { Undo.ClearAll(); Object.DestroyImmediate(obj); }
            }
        }

        [TestCase(SpsFamily.VrcFury)]
        [TestCase(SpsFamily.SpsForNdmf)]
        public void ExistingRingBecomesAutoAndUndoRestoresRing(SpsFamily family)
        {
            using (SpsBackend.Use(family))
            {
                if (!VrcFuryCapabilities.Current.CanGenerate) Assert.Ignore("Native SPS dependency required.");
                var obj = new GameObject("Socket mode undo test");
                try
                {
                    var settings = new SetupSettings { penetration = true };
                    var socket = VrcFuryCompatibility.CreateSocket(obj,
                        new SocketSettings { id = "mouth", name = "mouth" }, settings, new List<string>());
                    Undo.ClearAll();
                    Undo.IncrementCurrentGroup();
                    settings.penetration = false;
                    VrcFuryCompatibility.ConfigurePenetrationMode(socket, settings);
                    Undo.FlushUndoRecordObjects();
                    Assert.That(Mode(socket), Is.EqualTo("Auto"));
                    Undo.PerformUndo();
                    Assert.That(Mode(socket), Is.EqualTo("Ring"));
                    Undo.PerformRedo();
                    Assert.That(Mode(socket), Is.EqualTo("Auto"));
                    settings.penetration = true;
                    VrcFuryCompatibility.ConfigurePenetrationMode(socket, settings);
                    Assert.That(Mode(socket), Is.EqualTo("Ring"));
                }
                finally { Undo.ClearAll(); Object.DestroyImmediate(obj); }
            }
        }
    }
}
