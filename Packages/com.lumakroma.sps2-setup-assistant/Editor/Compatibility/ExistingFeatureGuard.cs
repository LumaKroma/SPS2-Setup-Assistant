using System;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Decision helper only: callers must obtain trustworthy observations from
    // the actual target and prove save/reload on an isolated copy first.
    internal static class ExistingFeatureGuard
    {
        [Flags]
        internal enum Feature
        {
            None = 0, ActiveObject = 1, ActiveBlendShape = 2, ActiveClip = 4,
            GuidedPath = 8, Collapse = 16, PointRadius = 32, Depth = 64,
            RadiusWeight = 128, GlobalOptions = 256
        }
        internal struct Evidence
        {
            internal bool ExistingOwnershipVerified, PersistentSocketBinding;
            internal bool CompleteSettingsSnapshot, ReferencesResolved;
            internal bool RoundTripVerifiedForInstalledContract;
            internal Feature Used, Supported, PreservationVerified;
        }
        internal struct Result
        {
            internal bool CanReplace;
            internal Feature Missing, UnverifiedPreservation;
        }
        internal static Result Inspect(Evidence e)
        {
            var missing = e.Used & ~e.Supported;
            var unverified = e.Used & ~e.PreservationVerified;
            return new Result {
                Missing = missing,
                UnverifiedPreservation = unverified,
                CanReplace = e.ExistingOwnershipVerified && e.PersistentSocketBinding &&
                    e.CompleteSettingsSnapshot && e.ReferencesResolved &&
                    e.RoundTripVerifiedForInstalledContract && missing == Feature.None && unverified == Feature.None
            };
        }
    }
}
