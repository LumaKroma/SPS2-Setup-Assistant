using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Ephemeral proof input only. Never serialize, generate from, or assign these copies to the live context.
    internal static class Sps3SingleDepthScaleProjection
    {
        private static InvalidOperationException Block(string detail) => new InvalidOperationException(
            SpsAuthoringCompatibility.LocalRegenerationScaleReason + ": " + detail + ". Use Apply without Regenerate.");

        internal static SetupSettings ForProof(SetupSettings source)
        {
            if (source?.parts == null || source.parts.Any(p => p == null)) throw Block("Missing scale-proof input");
            var result = source.Copy();
            foreach (var part in result.parts)
            {
                if (!part.included || part.animationModelVersion == 0) continue;
                Sps3AnimationGroupNative.RequireModel(part);
                if (!part.AnimationEnabled)
                {
                    // Dormant group payload remains on the real settings and in reference checks.
                    part.depth = false; part.animationModelVersion = 0;
                    continue;
                }
                if (part.animationGroups.Count != 1) throw Block("Only one legacy-equivalent Depth group is scale-proven: " + part.id);
                var group = part.animationGroups[0]; var value = group.depthSettings;
                if (group.kind != SocketAnimationKind.Depth || !value.enableSelf || !value.enableOthers ||
                    value.smoothingSeconds != 0 || Sps3AnimationGroupNative.WeightPending(value) || group.actions == null ||
                    group.actions.Any(a => a == null || a.kind != DepthActionKind.BlendShape &&
                        a.kind != DepthActionKind.AnimationClip && a.kind != DepthActionKind.Object))
                    throw Block("Entry, weighted/pending, filtered, smoothed or unknown animation output is not scale-proven: " + part.id);
                part.depth = true; part.range = value.range; part.units = value.units;
                part.actions = group.actions.Select(a => a.Copy()).ToList();
                part.animationModelVersion = 0;
                // Exact range, filters, smoothing, weight and actions must equal the existing legacy output contract.
                var original = source.parts.Single(p => p.id == part.id).Copy();
                // Weight is checked for equality between saved/requested settings and its
                // world scale is retained by the candidate plan. It is intentionally
                // absent only from this temporary legacy geometry projection.
                if (Sps3AnimationGroupNative.WeightActive(value))
                    original.animationGroups[0].depthSettings.radiusWeightEnabled = false;
                if (!Sps3AnimationGroupNative.SameOutput(original, part)) throw Block("Projection would change native animation output: " + part.id);
            }
            return result;
        }

        internal static void RequireRetainedEquivalentGroups(SetupSettings requested, SetupSettings saved)
        {
            if (requested?.parts == null || saved?.parts == null) throw Block("Missing saved scale-proof input");
            foreach (var next in requested.parts.Where(p => p.included && p.AnimationEnabled && Sps3AnimationGroupNative.HasLocal(p)))
            {
                var before = saved.parts.SingleOrDefault(p => p.id == next.id);
                if ((next.animationModelVersion != 0 || before?.animationModelVersion != 0) &&
                    (before == null || !Sps3AnimationGroupNative.SameOutput(before, next)))
                    throw Block("Grouped Local regeneration requires unchanged saved native-equivalent animation output: " + next.id);
            }
        }
    }
}
