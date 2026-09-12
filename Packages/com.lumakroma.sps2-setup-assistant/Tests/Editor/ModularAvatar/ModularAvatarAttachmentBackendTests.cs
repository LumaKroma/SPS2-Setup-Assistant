using LumaKroma.Sps2SetupAssistant.Editor.Model;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.Tests
{
    public sealed class ModularAvatarAttachmentBackendTests
    {
        [Test]
        public void AvailableBackends_IncludesModularAvatarWhenPackageIsPresent()
        {
            Assert.That(
                AttachmentBackendRegistry.IsAvailable(AttachmentBackend.VrcFuryWithModularAvatar),
                Is.True);
        }

        [Test]
        public void TryApply_ModularAvatarCreatesConfiguredPublicBoneProxy()
        {
            var anchor = new GameObject("Anchor");
            try
            {
                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Add Modular Avatar Attachment");

                Assert.That(AttachmentBackendRegistry.TryApply(
                    AttachmentBackend.VrcFuryWithModularAvatar,
                    anchor,
                    HumanBodyBones.Head,
                    out var error), Is.True, error);
                Undo.CollapseUndoOperations(undoGroup);

                var proxy = anchor.GetComponent<ModularAvatarBoneProxy>();
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.boneReference, Is.EqualTo(HumanBodyBones.Head));
                Assert.That(proxy.attachmentMode, Is.EqualTo(BoneProxyAttachmentMode.AsChildAtRoot));
                Assert.That(proxy.matchScale, Is.False);

                Undo.PerformUndo();
                Assert.That(anchor.GetComponent<ModularAvatarBoneProxy>(), Is.Null);

                Undo.PerformRedo();
                proxy = anchor.GetComponent<ModularAvatarBoneProxy>();
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.boneReference, Is.EqualTo(HumanBodyBones.Head));
                Assert.That(proxy.attachmentMode, Is.EqualTo(BoneProxyAttachmentMode.AsChildAtRoot));
                Assert.That(proxy.matchScale, Is.False);
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(anchor);
            }
        }
    }
}
