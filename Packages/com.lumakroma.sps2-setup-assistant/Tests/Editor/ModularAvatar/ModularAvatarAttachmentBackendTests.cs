using LumaKroma.Sps2SetupAssistant.Editor.Model;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
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
                Assert.That(AttachmentBackendRegistry.TryApply(
                    AttachmentBackend.VrcFuryWithModularAvatar,
                    anchor,
                    HumanBodyBones.Head,
                    out var error), Is.True, error);

                var proxy = anchor.GetComponent<ModularAvatarBoneProxy>();
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.boneReference, Is.EqualTo(HumanBodyBones.Head));
                Assert.That(proxy.attachmentMode, Is.EqualTo(BoneProxyAttachmentMode.AsChildAtRoot));
                Assert.That(proxy.matchScale, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(anchor);
            }
        }
    }
}
