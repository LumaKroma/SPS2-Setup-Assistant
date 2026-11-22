using System;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    internal static class NativeFloatDecision
    {
        internal static bool Admit(int candidates, string baseline, string requestedWithoutValue, bool conflict, float value) =>
            candidates == 1 && !conflict && baseline != null && baseline == requestedWithoutValue &&
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 100;
    }
}
