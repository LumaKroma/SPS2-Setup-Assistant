using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Planning
{
    public sealed class HumanoidSnapshot
    {
        private readonly IReadOnlyDictionary<HumanBodyBones, BonePose> bones;

        public HumanoidSnapshot(
            Vector3 avatarRight,
            Vector3 avatarUp,
            Vector3 avatarForward,
            IReadOnlyDictionary<HumanBodyBones, BonePose> bones)
        {
            AvatarRight = avatarRight.normalized;
            AvatarUp = avatarUp.normalized;
            AvatarForward = avatarForward.normalized;
            this.bones = bones;
        }

        public Vector3 AvatarRight { get; }

        public Vector3 AvatarUp { get; }

        public Vector3 AvatarForward { get; }

        public static bool TryCapture(
            VRCAvatarDescriptor descriptor,
            out HumanoidSnapshot snapshot,
            out string error)
        {
            snapshot = null;

            if (descriptor == null)
            {
                error = L("VRCAvatarDescriptor を選択してください。");
                return false;
            }

            var animator = descriptor.GetComponent<Animator>();
            if (animator == null)
            {
                error = L("選択した Descriptor と同じ GameObject に Animator がありません。");
                return false;
            }

            if (animator.avatar == null || !animator.isHuman)
            {
                error = L("有効な Humanoid Avatar を使用する Descriptor を選択してください。");
                return false;
            }

            var capturedBones = new Dictionary<HumanBodyBones, BonePose>();
            for (var value = 0; value < (int)HumanBodyBones.LastBone; value++)
            {
                var bone = (HumanBodyBones)value;
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    capturedBones.Add(bone, new BonePose(transform.position, transform.rotation));
                }
            }

            var root = descriptor.transform;
            snapshot = new HumanoidSnapshot(root.right, root.up, root.forward, capturedBones);
            error = null;
            return true;
        }

        public bool TryGetBone(HumanBodyBones bone, out BonePose pose)
        {
            return bones.TryGetValue(bone, out pose);
        }

    }
}
