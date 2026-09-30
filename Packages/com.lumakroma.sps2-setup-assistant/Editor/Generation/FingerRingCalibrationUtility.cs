using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Prototype: no gesture detection, runtime scripts or inferred anatomical center.
    public static class FingerRingCalibrationUtility
    {
        public static void Bones(VRCAvatarDescriptor avatar, SocketSettings part, out Transform thumb, out Transform index)
        {
            var animator = avatar != null ? avatar.GetComponent<Animator>() : null;
            if (!part.IsFingerRing || animator == null || animator.avatar == null || !animator.isHuman)
                throw new InvalidOperationException("Finger ring requires a Humanoid avatar.");
            bool left = part.id == "fingerRingLeft";
            thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
            index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
            if (thumb == null || index == null || thumb == index)
                throw new InvalidOperationException("Finger ring requires ThumbDistal and IndexIntermediate. Check the avatar's joint mapping.");
        }

        public static FingerRingCalibration Capture(Transform avatar, Transform thumb, Transform index,
            Vector3 center, Quaternion rotation, float thumbWeight)
        {
            if (avatar == null || thumb == null || index == null || !thumb.IsChildOf(avatar) || !index.IsChildOf(avatar))
                throw new InvalidOperationException("Calibration joints must belong to this avatar.");
            ValidateFrame(thumb); ValidateFrame(index);
            if (!Finite(center) || !Finite(rotation) || float.IsNaN(thumbWeight) || float.IsInfinity(thumbWeight) || thumbWeight < 0 || thumbWeight > 1)
                throw new InvalidOperationException("Calibration requires finite values and a weight from 0 to 1.");
            return new FingerRingCalibration {
                calibrated = true, thumbWeight = thumbWeight,
                thumbPath = AnimationUtility.CalculateTransformPath(thumb, avatar), indexPath = AnimationUtility.CalculateTransformPath(index, avatar),
                thumbOffset = Quaternion.Inverse(thumb.rotation) * (center - thumb.position),
                indexOffset = Quaternion.Inverse(index.rotation) * (center - index.position),
                thumbRotation = Quaternion.Inverse(thumb.rotation) * rotation,
                indexRotation = Quaternion.Inverse(index.rotation) * rotation,
                center = avatar.InverseTransformPoint(center), euler = (Quaternion.Inverse(avatar.rotation) * rotation).eulerAngles
            };
        }

        public static void Validate(Transform avatar, Transform thumb, Transform index, FingerRingCalibration value)
        {
            ValidateFrame(thumb); ValidateFrame(index);
            if (value == null || !value.calibrated)
                throw new InvalidOperationException("Close the finger ring, set its center and forward direction, then capture calibration before generating.");
            if (AnimationUtility.CalculateTransformPath(thumb, avatar) != value.thumbPath || AnimationUtility.CalculateTransformPath(index, avatar) != value.indexPath)
                throw new InvalidOperationException("Finger joint paths changed. Capture calibration again.");
            if (!Finite(value.thumbOffset) || !Finite(value.indexOffset) || !Finite(value.thumbRotation) || !Finite(value.indexRotation) ||
                float.IsNaN(value.thumbWeight) || float.IsInfinity(value.thumbWeight) || value.thumbWeight < 0 || value.thumbWeight > 1)
                throw new InvalidOperationException("Invalid saved finger calibration.");
        }

        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
        private static bool Finite(Quaternion q) => Finite(new Vector3(q.x,q.y,q.z)) && !float.IsNaN(q.w) && !float.IsInfinity(q.w) && Mathf.Abs(1 - (q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w)) < .001f;
        private static void ValidateFrame(Transform frame)
        {
            if (frame == null || !Finite(frame.position) || !Finite(frame.rotation)) throw new InvalidOperationException("Missing or invalid finger frame.");
            var scale = frame.lossyScale;
            // Scale-dependent offset semantics are not yet certified by the prototype.
            if ((scale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Finger calibration prototype requires unit world scale on its joint frames.");
        }

        public static void Evaluate(Transform thumb, Transform index, FingerRingCalibration value, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(index.position + index.rotation * value.indexOffset, thumb.position + thumb.rotation * value.thumbOffset, value.thumbWeight);
            // Match the normalized linear rotation blend used by the native two-source constraint.
            rotation = Quaternion.Lerp(index.rotation * value.indexRotation, thumb.rotation * value.thumbRotation, value.thumbWeight);
        }

        public static void Configure(GameObject anchor, Transform thumb, Transform index, FingerRingCalibration value)
        {
            var constraint = anchor.GetComponent<ParentConstraint>() ?? Undo.AddComponent<ParentConstraint>(anchor);
            Undo.RecordObject(constraint, "Finger ring calibration");
            constraint.constraintActive = false; constraint.locked = false;
            constraint.SetSources(new List<ConstraintSource> {
                new ConstraintSource { sourceTransform = thumb, weight = value.thumbWeight },
                new ConstraintSource { sourceTransform = index, weight = 1 - value.thumbWeight }
            });
            constraint.SetTranslationOffset(0, value.thumbOffset); constraint.SetTranslationOffset(1, value.indexOffset);
            constraint.SetRotationOffset(0, value.thumbRotation.eulerAngles); constraint.SetRotationOffset(1, value.indexRotation.eulerAngles);
            constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
        }
    }
}
