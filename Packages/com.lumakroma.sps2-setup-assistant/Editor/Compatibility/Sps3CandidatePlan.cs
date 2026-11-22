using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal enum Sps3ExistingSetupKind { None, Sps2, Sps3Candidate, Unknown, Mixed }

    // Value-only proposed mapping. Never pass native objects or write callbacks into this layer.
    internal sealed class Sps3CandidatePlan
    {
        internal string DepthUnitName { get; }
        internal bool IncludeInAuto { get; }
        internal int PathStopCount { get; }
        internal IReadOnlyList<string> Blockers { get; }
        internal bool CanApply => false;
        internal bool RequiresAvatarOptionsDecision { get; }

        private Sps3CandidatePlan(string units, bool auto, int stops, bool legacy, List<string> blockers)
        {
            DepthUnitName = units; IncludeInAuto = auto; PathStopCount = stops;
            RequiresAvatarOptionsDecision = legacy; Blockers = blockers.AsReadOnly();
        }

        internal static Sps3CandidatePlan Create(Sps3ExistingSetupKind existing, bool ownershipVerified,
            string explicitDepthUnits, bool includeInAuto, bool fingerRing, int pathStops,
            bool hasCustomTangents, bool requestsLegacy)
        {
            var blockers = new List<string>();
            if (existing != Sps3ExistingSetupKind.None)
                blockers.Add("Existing setups are not converted or modified by this candidate.");
            if (!ownershipVerified) blockers.Add("Ownership is not verified.");
            if (explicitDepthUnits != "Meters" && explicitDepthUnits != "Plugs" && explicitDepthUnits != "Local")
                blockers.Add("Depth units must be explicit; the public API default must not select units.");
            if (pathStops < 0 || pathStops > 4) blockers.Add("The observed SPS3 path limit is four stops.");
            if (hasCustomTangents) blockers.Add("Observed SPS3 baking does not preserve custom tangent behavior.");
            if (requestsLegacy) blockers.Add("Legacy options affect the avatar; never silently translate per-socket settings.");
            blockers.Add("Generation adapter is intentionally disconnected pending native validation.");
            return new Sps3CandidatePlan(explicitDepthUnits, includeInAuto && !fingerRing, pathStops, requestsLegacy, blockers);
        }

        // Do not copy SerializedProperty.enumValueIndex across generations with different enum order.
        internal static int ResolveEnumByName(string[] targetNames, string requested)
        {
            if (targetNames == null || string.IsNullOrEmpty(requested)) return -1;
            int found = -1;
            for (int i = 0; i < targetNames.Length; i++)
                if (targetNames[i] == requested) { if (found >= 0) return -1; found = i; }
            return found;
        }
    }
}
