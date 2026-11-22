namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    internal static class OptionalRingAdmission
    {
        // Missing evidence must never be treated as proof that a retained ring is disposable.
        internal static bool CanOmit(bool isRing, bool draftPristine, bool recorded,
            bool savedExists, bool savedIsRing, bool savedPristine) =>
            isRing && draftPristine && !recorded && (!savedExists || savedIsRing && savedPristine);
    }
}
