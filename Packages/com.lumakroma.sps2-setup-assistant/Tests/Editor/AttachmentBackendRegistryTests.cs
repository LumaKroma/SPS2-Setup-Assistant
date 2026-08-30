using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class AttachmentBackendRegistryTests
    {
        [Test]
        public void AvailableBackends_AlwaysIncludesVrcFury()
        {
            Assert.That(AttachmentBackendRegistry.IsAvailable(AttachmentBackend.VrcFury), Is.True);
            Assert.That(AttachmentBackendRegistry.AvailableBackends, Does.Contain(AttachmentBackend.VrcFury));
        }

        [Test]
        public void TryApply_VrcFuryCreatesAnAuthoringComponent()
        {
            var anchor = new GameObject("Anchor");
            try
            {
                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Add VRCFury Attachment");

                Assert.That(AttachmentBackendRegistry.TryApply(
                    AttachmentBackend.VrcFury,
                    anchor,
                    HumanBodyBones.Head,
                    out var error), Is.True, error);
                Undo.CollapseUndoOperations(undoGroup);

                Assert.That(anchor.GetComponents<Component>().Length, Is.GreaterThan(1));

                Undo.PerformUndo();
                Assert.That(anchor.GetComponents<Component>(), Has.Length.EqualTo(1));

                Undo.PerformRedo();
                Assert.That(anchor.GetComponents<Component>().Length, Is.GreaterThan(1));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(anchor);
            }
        }

        [Test]
        public void TryApply_UnknownBackendFailsBeforeMutation()
        {
            var anchor = new GameObject("Anchor");
            try
            {
                Assert.That(AttachmentBackendRegistry.TryApply(
                    (AttachmentBackend)999,
                    anchor,
                    HumanBodyBones.Head,
                    out var error), Is.False);
                Assert.That(error, Does.Contain("unavailable"));
                Assert.That(anchor.GetComponents<Component>(), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(anchor);
            }
        }
    }
}
