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
                error = "Select a VRCAvatarDescriptor.";
                return false;
            }

            var animator = descriptor.GetComponent<Animator>();
            if (animator == null)
            {
                error = "The selected descriptor does not have an Animator on the same GameObject.";
                return false;
            }

            if (animator.avatar == null || !animator.isHuman)
            {
                error = "The selected descriptor must use a valid Humanoid Avatar.";
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
