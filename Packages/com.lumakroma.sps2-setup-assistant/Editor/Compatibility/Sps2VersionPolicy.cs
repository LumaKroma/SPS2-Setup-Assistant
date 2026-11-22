using System;
using System.Text.RegularExpressions;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Legacy Fury policy retained for old callers. Direct NDMF admission belongs to the native field probe.
    internal static class Sps2VersionPolicy
    {
        internal const string UpperExclusive = "1.1442.1";
        internal static bool Allows(string value, bool nativeNdmf)
        {
            if (nativeNdmf) return true;
            if (value == null || !Regex.IsMatch(value, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z") ||
                !Version.TryParse(value, out var version)) return false;
            return version >= new Version("1.0.0") &&
                version < new Version(UpperExclusive);
        }
    }
}
