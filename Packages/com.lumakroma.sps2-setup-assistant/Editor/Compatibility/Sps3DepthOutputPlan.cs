using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal enum Sps3DepthOutputKind { BlendShape, AnimationClip, ObjectOn, ObjectOff }

    // SourceActionIndex identifies a slot to resolve later, never a live object or ownership proof.
    internal sealed class Sps3DepthOutputCommand
    {
        internal int SourceActionIndex { get; }
        internal Sps3DepthOutputKind Kind { get; }
        internal string Shape { get; }
        internal float? Weight { get; }
        internal Sps3DepthOutputCommand(int index, Sps3DepthOutputKind kind, string shape = null, float? weight = null)
        { SourceActionIndex = index; Kind = kind; Shape = shape; Weight = weight; }
    }

    // Pure compiler for old output intent. Not a native specification or permission to mutate an avatar.
    internal sealed class Sps3DepthOutputPlan
    {
        internal bool Requested { get; }
        internal string Units { get; }
        internal float RangeMin { get; }
        internal float RangeMax { get; }
        internal bool EnableSelf => true;
        internal float SmoothingSeconds => 0;
        internal bool ReverseClip => false;
        internal IReadOnlyList<Sps3DepthOutputCommand> Commands { get; }
        internal IReadOnlyList<string> Blockers { get; }
        internal bool Prepared => Requested && Blockers.Count == 0;
        internal bool CanApply => false;
        internal bool ReferencesVerified => false;

        private Sps3DepthOutputPlan(Sps3SettingsPartReadPlan part)
        {
            var errors = new List<string>();
            var commands = new List<Sps3DepthOutputCommand>();
            if (part == null) errors.Add("MissingPart");
            else
            {
                Requested = part.Included && part.DepthEnabled;
                Units = part.DepthUnitName; RangeMin = part.RangeMin; RangeMax = part.RangeMax;
                if (Requested)
                {
                    // Preserve Local ranges verbatim. Native SPS converts the measurement using the socket owner scale.
                    if (Units != "Meters" && Units != "Plugs" && Units != "Local") errors.Add("DepthUnitsNeedResolution");
                    if (!Finite(RangeMin) || !Finite(RangeMax) || RangeMin > RangeMax) errors.Add("InvalidDepthRange");
                    if (!part.ActionsListPresent) errors.Add("MissingActionsList");
                    for (int index = 0; index < part.Actions.Count; index++)
                    {
                        var action = part.Actions[index];
                        string suffix = ":" + index;
                        if (action == null) { errors.Add("NullAction" + suffix); continue; }
                        switch ((DepthActionKind)action.RawKind)
                        {
                            case DepthActionKind.BlendShape:
                                if (!action.HasManagedRendererReference) errors.Add("MissingRenderer" + suffix);
                                if (string.IsNullOrEmpty(action.Shape)) errors.Add("MissingShape" + suffix);
                                if (!action.HistoricalClampedWeight.HasValue) errors.Add("NonfiniteWeight" + suffix);
                                commands.Add(new Sps3DepthOutputCommand(index, Sps3DepthOutputKind.BlendShape,
                                    action.Shape, action.HistoricalClampedWeight));
                                break;
                            case DepthActionKind.AnimationClip:
                                if (!action.HasManagedClipReference) errors.Add("MissingClip" + suffix);
                                commands.Add(new Sps3DepthOutputCommand(index, Sps3DepthOutputKind.AnimationClip));
                                break;
                            case DepthActionKind.Object:
                                if (!action.HasManagedTargetReference) errors.Add("MissingObject" + suffix);
                                commands.Add(new Sps3DepthOutputCommand(index,
                                    action.ObjectOn ? Sps3DepthOutputKind.ObjectOn : Sps3DepthOutputKind.ObjectOff));
                                break;
                            default: errors.Add("UnknownActionKind" + suffix); break;
                        }
                    }
                }
            }
            // Never silently drop an invalid row and shift later OFF/reference indices.
            // A diagnostic remains available, but an incomplete group has no executable command sequence.
            if (errors.Count != 0) commands.Clear();
            Commands = commands.AsReadOnly(); Blockers = errors.AsReadOnly();
        }
        internal static Sps3DepthOutputPlan Compile(Sps3SettingsPartReadPlan part) => new Sps3DepthOutputPlan(part);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
