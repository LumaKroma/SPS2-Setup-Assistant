using System;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Version-independent decision model. A native mode is eligible only after its
    // component types have survived a save/reload and its required member shapes
    // have been checked. This class does not authorize mutations on its own.
    internal static class FuryFeatureOutputPlan
    {
        internal enum Mode { PreserveExisting, PlacementOnly, LegacySps2, NativeSps3, NativeBasicSps3 }
        [Flags] internal enum OptionalFeature { None = 0, Depth = 1, RadiusWeight = 2, GuidedPath = 4, ActiveAnimation = 8, TestPlug = 16, ModularAvatarFollow = 32 }
        internal struct Inputs
        {
            internal bool HasExisting;
            internal bool ExistingOwnershipVerified;
            internal Mode ExistingMode;
            internal bool NativeSps3SaveReloadVerified;
            internal bool LegacySps2SaveReloadVerified;
            internal OptionalFeature Requested;
            internal OptionalFeature Supported;
        }
        internal struct Decision
        {
            internal Mode Output;
            internal OptionalFeature Enabled;
            internal OptionalFeature Unavailable;
            internal bool RequiresExplicitReplacement;
        }
        internal static Decision Choose(Inputs input)
        {
            // An unverified or unavailable existing output is retained; a new
            // placement scaffold must never silently replace working user data.
            Mode candidate = input.NativeSps3SaveReloadVerified ? Mode.NativeSps3 :
                input.LegacySps2SaveReloadVerified ? Mode.LegacySps2 : Mode.PlacementOnly;
            if (input.HasExisting && (!input.ExistingOwnershipVerified ||
                (input.ExistingMode != Mode.NativeSps3 && input.ExistingMode != Mode.LegacySps2)))
                return new Decision { Output = Mode.PreserveExisting, Unavailable = input.Requested };
            if (input.HasExisting && ((input.ExistingMode == Mode.LegacySps2 && input.LegacySps2SaveReloadVerified) ||
                (input.ExistingMode == Mode.NativeSps3 && input.NativeSps3SaveReloadVerified)))
                candidate = input.ExistingMode;
            if (input.HasExisting && input.ExistingMode != candidate)
                return new Decision { Output = Mode.PreserveExisting, Unavailable = input.Requested, RequiresExplicitReplacement = true };
            var supported = candidate == Mode.PlacementOnly ? input.Supported & OptionalFeature.ModularAvatarFollow : input.Supported;
            return new Decision { Output = candidate, Enabled = input.Requested & supported, Unavailable = input.Requested & ~supported };
        }
    }
}
