using LumaKroma.Sps2SetupAssistant.Editor.Model;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar
{
    [InitializeOnLoad]
    internal sealed class ModularAvatarAttachmentBackend : IAttachmentBackendAdapter
    {
        static ModularAvatarAttachmentBackend()
        {
            AttachmentBackendRegistry.Register(new ModularAvatarAttachmentBackend());
            AttachmentBackendRegistry.Register(new ModularAvatarAttachmentBackend(AttachmentBackend.SpsNdmfWithModularAvatar));
        }

        public AttachmentBackend Backend { get; }
        internal ModularAvatarAttachmentBackend(AttachmentBackend backend = AttachmentBackend.VrcFuryWithModularAvatar) { Backend = backend; }

        public string DisplayName => Backend == AttachmentBackend.SpsNdmfWithModularAvatar ? "SPS for NDMF + Modular Avatar" : "VRCFury + Modular Avatar";

        public void Apply(GameObject anchor, HumanBodyBones bone)
        {
            var proxy = Undo.AddComponent<ModularAvatarBoneProxy>(anchor);
            proxy.boneReference = bone;
            proxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.matchScale = false;
        }
    }
}
