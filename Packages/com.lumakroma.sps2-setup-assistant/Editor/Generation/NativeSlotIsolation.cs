namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    internal static class NativeSlotIsolation
    {
        // Null or aliased native collections do not prove independent storage.
        internal static bool Separate(object depth, object entry) =>
            depth != null && entry != null && !object.ReferenceEquals(depth, entry);
    }
}
