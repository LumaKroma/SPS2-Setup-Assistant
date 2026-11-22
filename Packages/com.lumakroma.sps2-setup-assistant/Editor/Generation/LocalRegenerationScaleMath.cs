using System;
namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Pure arithmetic boundary, executable without Unity. No enum/range conversion.
    internal static class LocalRegenerationScaleMath
    {
        internal static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Equivalent(float a, float b) => PositiveFinite(a) && PositiveFinite(b) && Math.Abs((double)a - b) <= Math.Max(a, b) * 0.000001;
        internal static bool Uniform(float x, float y, float z) => Equivalent(x, y) && Equivalent(x, z);
        // Target scale affects the preservation ratio only for a retained Local
        // depth/action. Shared-root and Local socket ancestry are checked separately.
        internal static bool NeedsTargetScaleProof(bool retainsLocalDepth) => retainsLocalDepth;
        // CreatePart uses a constraint for midpoint/surface-following placements even
        // when MA is selected; MA's proxy applies only to a direct mapped bone.
        internal static bool AttachmentMatches(bool modularAvatar, bool directConstraint, bool hasProxy) =>
            directConstraint ? !hasProxy : modularAvatar == hasProxy;
        internal static float ReplacementScale(float owner, float parent)
        {
            if (!PositiveFinite(owner) || !PositiveFinite(parent)) throw new InvalidOperationException("Invalid Local regeneration scale.");
            float result = owner / parent;
            if (!PositiveFinite(result) || !Equivalent(owner, result * parent)) throw new InvalidOperationException("Unrepresentable Local regeneration scale.");
            return result;
        }
    }
}
