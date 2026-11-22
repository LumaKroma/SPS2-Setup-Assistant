using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Prototype: no gesture detection, runtime scripts or inferred anatomical center.
    public static class FingerRingCalibrationUtility
    {
        public static void Bones(VRCAvatarDescriptor avatar, SocketSettings part, out Transform thumb, out Transform index)
        {
            var animator = avatar != null ? avatar.GetComponent<Animator>() : null;
            if (!part.IsFingerRing || animator == null || animator.avatar == null || !animator.isHuman)
                throw new InvalidOperationException(L("輪ソケットにはHumanoidアバターが必要です。"));
            bool left = part.id == "fingerRingLeft";
            thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
            index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
            index = ResolveIndexFrame(avatar.transform, index,
                animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal), part.fingerRing);
            if (thumb == null || index == null || thumb == index)
                throw new InvalidOperationException(L("輪ソケットにはThumbDistalと校正対象のIndexProximalまたはIndexIntermediateが必要です。アバターの関節マッピングを確認してください。"));
        }

        internal static Transform ResolveIndexFrame(Transform avatar, Transform intermediate, Transform proximal, FingerRingCalibration value) =>
            value != null && value.calibrated && proximal != null &&
            AnimationUtility.CalculateTransformPath(proximal, avatar) == value.indexPath ? proximal : intermediate;

        public static FingerRingCalibration Capture(Transform avatar, Transform thumb, Transform index,
            Vector3 center, Quaternion rotation, float thumbWeight)
        {
            if (avatar == null || thumb == null || index == null || !thumb.IsChildOf(avatar) || !index.IsChildOf(avatar))
                throw new InvalidOperationException(L("校正の関節はこのアバター内に配置してください。"));
            ValidateFrame(avatar); ValidateFrame(thumb); ValidateFrame(index);
            if (!Finite(center) || !Finite(rotation) || float.IsNaN(thumbWeight) || float.IsInfinity(thumbWeight) || thumbWeight < 0 || thumbWeight > 1)
                throw new InvalidOperationException(L("校正には有限値と0～1の重みが必要です。"));
            var captured = new FingerRingCalibration {
                calibrated = true, thumbWeight = thumbWeight,
                thumbPath = AnimationUtility.CalculateTransformPath(thumb, avatar), indexPath = AnimationUtility.CalculateTransformPath(index, avatar),
                // Unit-scale saves already use these coordinates; no schema migration.
                thumbOffset = thumb.InverseTransformPoint(center),
                indexOffset = index.InverseTransformPoint(center),
                thumbRotation = Quaternion.Inverse(thumb.rotation) * rotation,
                indexRotation = Quaternion.Inverse(index.rotation) * rotation,
                center = avatar.InverseTransformPoint(center), euler = (Quaternion.Inverse(avatar.rotation) * rotation).eulerAngles
            };
            if (!Finite(captured.thumbOffset) || !Finite(captured.indexOffset) || !Finite(captured.center))
                throw new UnusableFrameException();
            return captured;
        }

        public static void Validate(Transform avatar, Transform thumb, Transform index, FingerRingCalibration value)
        {
            ValidateFrame(avatar); ValidateFrame(thumb); ValidateFrame(index);
            if (value == null || !value.calibrated)
                throw new InvalidOperationException(L("未完了の指わっか調整が残っています。設定を保持して処理を中止しました。該当する指わっかをオフにしてください。"));
            if (AnimationUtility.CalculateTransformPath(thumb, avatar) != value.thumbPath || AnimationUtility.CalculateTransformPath(index, avatar) != value.indexPath)
                throw new InvalidOperationException(L("指の関節パスが変わりました。設定を保持して処理を中止しました。該当する指わっかをオフにしてください。"));
            if (!Finite(value.center) || !Finite(value.euler) || !Finite(value.thumbOffset) || !Finite(value.indexOffset) || !Finite(value.thumbRotation) || !Finite(value.indexRotation) ||
                float.IsNaN(value.thumbWeight) || float.IsInfinity(value.thumbWeight) || value.thumbWeight < 0 || value.thumbWeight > 1)
                throw new InvalidOperationException(L("保存された指の校正値が無効です。"));
        }

        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
        private static bool Finite(Quaternion q) => Finite(new Vector3(q.x,q.y,q.z)) && !float.IsNaN(q.w) && !float.IsInfinity(q.w) && Mathf.Abs(1 - (q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w)) < .001f;
        internal sealed class UnusableFrameException : InvalidOperationException
        {
            public UnusableFrameException() : base(L("指の座標系が未設定または無効です。")) { }
        }

        // A usable inverse is a safety requirement; uniform positive scale is only
        // the accuracy guarantee. Reflections and shear are allowed with a warning.
        internal static bool CanUseFrame(Transform frame)
        {
            if (frame == null || !Finite(frame.position) || !Finite(frame.rotation)) return false;
            var matrix = frame.localToWorldMatrix;
            float determinant = matrix.determinant;
            if (float.IsNaN(determinant) || float.IsInfinity(determinant) || determinant == 0) return false;
            var inverse = frame.worldToLocalMatrix;
            float inverseDeterminant = inverse.determinant;
            if (float.IsNaN(inverseDeterminant) || float.IsInfinity(inverseDeterminant) || inverseDeterminant == 0) return false;
            for (int i = 0; i < 16; i++)
                if (float.IsNaN(matrix[i]) || float.IsInfinity(matrix[i]) || float.IsNaN(inverse[i]) || float.IsInfinity(inverse[i])) return false;
            return true;
        }

        internal static bool HasGuaranteedScale(Transform frame)
        {
            if (!CanUseFrame(frame)) return false;
            for (var ancestor = frame; ancestor != null; ancestor = ancestor.parent)
                if (!PositiveUniform(ancestor.localScale)) return false;
            if (!PositiveUniform(frame.lossyScale)) return false;
            var expected = Matrix4x4.Rotate(frame.rotation);
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 actual = frame.localToWorldMatrix.GetColumn(axis);
                Vector3 basis = expected.GetColumn(axis);
                if ((actual / frame.lossyScale.x - basis).sqrMagnitude > .000001f) return false;
            }
            return true;
        }

        private static bool PositiveUniform(Vector3 scale) => Finite(scale) && scale.x > 0 && scale.y > 0 && scale.z > 0 &&
            Mathf.Abs(scale.y / scale.x - 1) <= .00001f && Mathf.Abs(scale.z / scale.x - 1) <= .00001f;

        private static void ValidateFrame(Transform frame)
        {
            if (frame == null) throw new InvalidOperationException(L("指の座標系が未設定または無効です。"));
            if (!CanUseFrame(frame)) throw new UnusableFrameException();
        }

        internal static void ValidateScale(Vector3 scale)
        {
            if (!Finite(scale) || scale.x == 0 || scale.y == 0 || scale.z == 0)
                throw new InvalidOperationException(L("指の座標系が未設定または無効です。"));
        }

        internal static FingerRingCalibration CaptureProvisional(Transform avatar, Transform thumb, Transform index, float weight) =>
            Capture(avatar, thumb, index, thumb.position * .5f + index.position * .5f,
                Quaternion.Lerp(index.rotation, thumb.rotation, weight), weight);

        internal static Vector3 NativeOffset(Transform frame, Vector3 localOffset)
        {
            ValidateFrame(frame);
            // Keep the certified positive-uniform calculation unchanged.
            var offset = HasGuaranteedScale(frame) ? localOffset * frame.lossyScale.x :
                Quaternion.Inverse(frame.rotation) * frame.TransformVector(localOffset);
            if (!Finite(offset)) throw new UnusableFrameException();
            return offset;
        }

        public static void Evaluate(Transform thumb, Transform index, FingerRingCalibration value, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(index.TransformPoint(value.indexOffset), thumb.TransformPoint(value.thumbOffset), value.thumbWeight);
            // Match the normalized linear rotation blend used by the native two-source constraint.
            rotation = Quaternion.Lerp(index.rotation * value.indexRotation, thumb.rotation * value.thumbRotation, value.thumbWeight);
            if (!Finite(position) || !Finite(rotation)) throw new UnusableFrameException();
        }

        public static void Configure(GameObject anchor, Transform thumb, Transform index, FingerRingCalibration value)
        {
            // Validate both distances before adding or changing any component.
            var thumbOffset = NativeOffset(thumb, value.thumbOffset);
            var indexOffset = NativeOffset(index, value.indexOffset);
            var constraint = anchor.GetComponent<ParentConstraint>();
            // Unity missing-component wrappers compare == null but are not C# null.
            if (constraint == null) constraint = Undo.AddComponent<ParentConstraint>(anchor);
            Undo.RecordObject(constraint, L("指の輪の校正"));
            constraint.constraintActive = false; constraint.locked = false;
            constraint.SetSources(new List<ConstraintSource> {
                new ConstraintSource { sourceTransform = thumb, weight = value.thumbWeight },
                new ConstraintSource { sourceTransform = index, weight = 1 - value.thumbWeight }
            });
            // Native ParentConstraint offsets are world distances in source rotation axes.
            // Keep saved offsets in joint-local units; refresh these distances on Apply.
            constraint.SetTranslationOffset(0, thumbOffset);
            constraint.SetTranslationOffset(1, indexOffset);
            constraint.SetRotationOffset(0, value.thumbRotation.eulerAngles); constraint.SetRotationOffset(1, value.indexRotation.eulerAngles);
            constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
        }
    }
}
