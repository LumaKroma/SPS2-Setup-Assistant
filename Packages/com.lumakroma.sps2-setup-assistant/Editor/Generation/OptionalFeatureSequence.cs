using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    internal static class OptionalFeatureSequence
    {
        // Probe failures can omit a feature before mutation. Write failures are
        // deliberately propagated: the caller must roll the generation back.
        internal static void Run<T>(Action basic, IEnumerable<T> requested,
            Func<T, string> probe, Action<T> write, Action<T, string> omitted)
        {
            basic();
            foreach (var feature in requested) {
                string reason = probe(feature);
                if (reason != null) { omitted(feature, reason); continue; }
                write(feature);
            }
        }
    }
}
