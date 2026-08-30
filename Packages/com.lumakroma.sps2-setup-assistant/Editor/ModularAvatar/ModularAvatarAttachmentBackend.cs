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
        }

        public AttachmentBackend Backend => AttachmentBackend.VrcFuryWithModularAvatar;

        public string DisplayName => "VRCFury + Modular Avatar";

        public void Apply(GameObject anchor, HumanBodyBones bone)
        {
            var proxy = Undo.AddComponent<ModularAvatarBoneProxy>(anchor);
            proxy.boneReference = bone;
            proxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.matchScale = false;
        }
    }
}
