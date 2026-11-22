using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Descriptive settings values only. This is not an asset clone, migration, native spec or ownership certificate.
    // Unity references are reduced to managed-reference presence without invoking Unity's overloaded null operator.
    internal sealed class Sps3SettingsReadPlan
    {
        internal bool SourceSettingsPresent { get; }
        internal int SourceFamily { get; }
        internal int MenuLanguage { get; }
        internal int DisplayNamesVersion { get; }
        internal bool Penetration { get; }
        internal bool ShowInternalThickness { get; }
        internal bool AutoMode { get; }
        internal bool Legacy { get; }
        internal bool LocalOnly { get; }
        internal bool ModularAvatar { get; }
        internal IReadOnlyList<Sps3SettingsPartReadPlan> Parts { get; }
        internal IReadOnlyList<string> Blockers { get; }
        internal bool CanGenerate => false;
        internal bool CanApply => false;
        internal bool SourceOwnershipVerified => false;
        internal bool DestinationOwnershipVerified => false;
        internal bool ConvertsExistingSetup => false;

        private Sps3SettingsReadPlan(SetupSettings settings)
        {
            var blockers = new List<string> { "GenerationDisconnected", "SourceAndDestinationOwnershipUnverified" };
            var parts = new List<Sps3SettingsPartReadPlan>();
            SourceSettingsPresent = settings != null;
            if (settings == null) blockers.Add("MissingSettings");
            else
            {
                SourceFamily = (int)settings.family; MenuLanguage = (int)settings.menuLanguage;
                DisplayNamesVersion = settings.displayNamesVersion;
                Penetration = settings.penetration; ShowInternalThickness = settings.showInternalThickness;
                AutoMode = settings.autoMode; Legacy = settings.legacy;
                LocalOnly = settings.localOnly; ModularAvatar = settings.modularAvatar;
                if (settings.parts == null) blockers.Add("MissingPartsList");
                else
                {
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    for (int index = 0; index < settings.parts.Count; index++)
                    {
                        var part = settings.parts[index];
                        if (part == null) { parts.Add(null); blockers.Add("NullPart:" + index); continue; }
                        // The existing generator validates IDs across all parts, including excluded ones.
                        if (string.IsNullOrEmpty(part.id)) blockers.Add("MissingPartId:" + index);
                        else if (!ids.Add(part.id)) blockers.Add("DuplicatePartId:" + index);
                        parts.Add(new Sps3SettingsPartReadPlan(part, settings));
                    }
                }
                if (parts.Any(p => p != null && p.Included))
                {
                    if (settings.family == SpsFamily.SpsForNdmf) blockers.Add("NdmfToFuryTranslationNotImplemented");
                    else if (settings.family != SpsFamily.VrcFury) blockers.Add("UnknownSpsFamily");
                    if (settings.displayNamesVersion < 1) blockers.Add("OldDisplayNameUpgradeNeedsExplicitReview");
                    else if (settings.displayNamesVersion != 1) blockers.Add("UnknownDisplayNameVersion");
                    if (MenuLanguage < 0 || MenuLanguage > 4) blockers.Add("UnknownDisplayLanguage");
                    // false is not authorization to clear an existing avatar-wide setting either.
                    blockers.Add("AvatarLegacyPolicyUnresolved");
                    blockers.Add("PlacementAndAttachmentNotInspected");
                }
            }
            Parts = parts.AsReadOnly(); Blockers = blockers.AsReadOnly();
        }

        internal static Sps3SettingsReadPlan Read(SetupSettings settings) => new Sps3SettingsReadPlan(settings);
    }

    internal sealed class Sps3SettingsPartReadPlan
    {
        internal string Id { get; }
        internal string RawName { get; }
        internal string MenuNameOverride { get; }
        internal int Category { get; }
        internal bool Custom { get; }
        internal bool Included { get; }
        internal bool IsFingerRing { get; }
        internal bool HasManagedTargetReference { get; }
        internal Sps3FingerCalibrationReadValues FingerCalibration { get; }
        internal string NameSource { get; }
        // Null for a localized standard name; no dictionary lookup or name upgrade is performed.
        internal string LiteralName { get; }
        internal string HistoricalModeIntent { get; }
        internal bool RequestsRadiusOffset { get; }
        internal bool IncludeInAuto { get; }
        internal int AnimationModelVersion { get; }
        internal bool AnimationEnabled { get; }
        internal IReadOnlyList<Sps3AnimationGroupReadValues> AnimationGroups { get; }
        internal bool DepthEnabled { get; }
        internal int RawDepthUnits { get; }
        internal string DepthUnitName { get; }
        internal float RangeMin { get; }
        internal float RangeMax { get; }
        internal IReadOnlyList<Sps3DepthActionReadValues> Actions { get; }
        internal bool ActionsListPresent { get; }
        internal Sps3DepthOutputPlan DepthOutput { get; }
        internal bool RequestsLegacyPath { get; }
        internal int? HistoricalPathStopCountHint => RequestsLegacyPath ? 3 : (int?)null;
        internal bool HistoricalPathRequestsCustomTangents => RequestsLegacyPath;
        internal int? HistoricalUncollapsedSegmentHint => RequestsLegacyPath ? (Id == "mouth" ? 0 : 2) : (int?)null;
        internal bool RequestedCollapseInterior { get; }
        // Settings contain none of these facts. Never synthesize false/zero or a verified destination.
        internal bool? PublicChannel => null;
        internal int? ObservedPathStopCount => null;
        internal bool? ObservedCustomTangents => null;
        internal IReadOnlyList<string> Blockers { get; }
        internal IReadOnlyList<string> DormantDiagnostics { get; }

        internal Sps3SettingsPartReadPlan(SocketSettings part, SetupSettings setup)
        {
            Id = part.id; RawName = part.name; MenuNameOverride = part.menuNameOverride;
            Category = part.category; Custom = part.custom; Included = part.included;
            IsFingerRing = part.IsFingerRing;
            HasManagedTargetReference = !ReferenceEquals(part.target, null);
            FingerCalibration = part.fingerRing == null ? null : new Sps3FingerCalibrationReadValues(part.fingerRing);
            IncludeInAuto = setup.Sps3IncludeInAuto(part);
            HistoricalModeIntent = setup.penetration && IsPathId(Id) ? "Ring" : "Auto";
            RequestsRadiusOffset = IsRadiusId(Id);
            AnimationModelVersion = part.animationModelVersion;
            AnimationEnabled = part.AnimationEnabled;
            AnimationGroups = part.EffectiveAnimationGroups().Select(g => g == null ? null : new Sps3AnimationGroupReadValues(g)).ToList().AsReadOnly();
            // The historical fixture compiler must not flatten or substitute old fields for groups.
            DepthEnabled = part.animationModelVersion == 0 && part.depth; RawDepthUnits = (int)part.units;
            DepthUnitName = part.units == DepthUnits.Meters ? "Meters" : part.units == DepthUnits.Plugs ? "Plugs" : part.units == DepthUnits.Local ? "Local" : null;
            RangeMin = part.range.x; RangeMax = part.range.y;
            RequestedCollapseInterior = !setup.showInternalThickness;
            RequestsLegacyPath = Included && setup.penetration && IsPathId(Id);
            if (!string.IsNullOrWhiteSpace(MenuNameOverride)) { NameSource = "Override"; LiteralName = MenuNameOverride; }
            else if (!Custom && IsStandardId(Id)) NameSource = "LocalizedStandardNameUnresolved";
            else { NameSource = "RawName"; LiteralName = RawName; }

            var blockers = new List<string>(); var dormant = new List<string>();
            void Issue(string code, bool applies = true) { (Included && applies ? blockers : dormant).Add(code); }
            if (!Custom && !IsStandardId(Id)) Issue("UnknownNoncustomPartPlacementUnsupported");
            if (Included)
            {
                blockers.Add("PublicChannelNotStoredInSettings");
                blockers.Add("ObservedNativePathAndTangentsUnknown");
                blockers.Add("FreshDestinationAndOwnershipUnverified");
                if (Custom) blockers.Add("CustomTargetPlacementUnverified");
                if (IsFingerRing) blockers.Add("FingerCalibrationAndLiveFramesUnverified");
                if (RequestsLegacyPath) blockers.Add("LegacyPathGeometryAndCustomTangentTranslationUnsupported");
                if (NameSource == "LocalizedStandardNameUnresolved") blockers.Add("LocalizedDisplayNameUnresolved");
                else if (string.IsNullOrWhiteSpace(LiteralName)) blockers.Add("MissingLiteralDisplayName");
            }
            if (AnimationModelVersion != 0) Issue("GroupedAnimationRequiresGroupNativeAdapter", AnimationEnabled);
            if (DepthUnitName == null) Issue("UnknownDepthUnits", DepthEnabled);
            if (!Finite(RangeMin) || !Finite(RangeMax) || RangeMin > RangeMax) Issue("InvalidDepthRangeWithoutNormalization", DepthEnabled);
            var actions = new List<Sps3DepthActionReadValues>();
            ActionsListPresent = part.actions != null;
            if (part.actions == null) Issue("MissingActionsList", DepthEnabled);
            else foreach (var action in part.actions)
            {
                if (action == null) { actions.Add(null); Issue("NullDepthAction", DepthEnabled); continue; }
                var value = new Sps3DepthActionReadValues(action); actions.Add(value);
                if (action.kind != DepthActionKind.BlendShape && action.kind != DepthActionKind.AnimationClip && action.kind != DepthActionKind.Object)
                    Issue("UnknownDepthActionKind", DepthEnabled);
                else if (action.kind == DepthActionKind.BlendShape)
                {
                    Issue("BlendShapeRendererMeshAndShapeUnverified", DepthEnabled);
                    Issue("BlendShapeAdapterNotImplemented", DepthEnabled);
                    if (!Finite(action.weight)) Issue("NonfiniteBlendShapeWeight", DepthEnabled);
                }
                else if (action.kind == DepthActionKind.Object)
                {
                    Issue("ObjectTargetReferenceUnverified", DepthEnabled);
                    Issue(action.objectOn ? "ObjectOnAdapterNotImplemented" : "ObjectOffAdapterNotImplemented", DepthEnabled);
                }
                else Issue("AnimationClipReferenceAndBindingsUnverified", DepthEnabled);
            }
            if (DepthEnabled && actions.Count != 1) Issue("CurrentFixtureAdapterRequiresOneClipAction");
            Actions = actions.AsReadOnly(); Blockers = blockers.AsReadOnly(); DormantDiagnostics = dormant.AsReadOnly();
            DepthOutput = Sps3DepthOutputPlan.Compile(this);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsPathId(string id) => id == "mouth" || id == "anus";
        private static bool IsRadiusId(string id) => id == "chest" || id == "handLeft" || id == "handRight" || id == "hands" || id == "footLeft" || id == "footRight" || id == "feet";
        private static bool IsStandardId(string id) => IsPathId(id) || IsRadiusId(id) || id == "earLeft" || id == "earRight" || id == "nippleLeft" || id == "nippleRight" || id == "vagina" || id == "thighs" || id == "fingerRingLeft" || id == "fingerRingRight";
    }

    internal sealed class Sps3AnimationGroupReadValues
    {
        internal string Id { get; }
        internal int Kind { get; }
        internal Vector2 Range { get; }
        internal int Units { get; }
        internal bool EnableSelf { get; }
        internal bool EnableOthers { get; }
        internal float SmoothingSeconds { get; }
        internal bool RadiusWeightEnabled { get; }
        internal float RadiusZero { get; }
        internal float RadiusFull { get; }
        internal bool SettingsPresent { get; }
        internal bool ActionsPresent { get; }
        internal IReadOnlyList<Sps3DepthActionReadValues> Actions { get; }
        internal Sps3AnimationGroupReadValues(SocketAnimationGroup group)
        {
            Id = group.id; Kind = (int)group.kind;
            var value = group.ActiveSettings; SettingsPresent = value != null;
            if (value != null) {
                Range = value.range; Units = (int)value.units;
                EnableSelf = value.enableSelf; EnableOthers = value.enableOthers; SmoothingSeconds = value.smoothingSeconds;
                RadiusWeightEnabled = value.radiusWeightEnabled; RadiusZero = value.radiusZero; RadiusFull = value.radiusFull;
            }
            ActionsPresent = group.actions != null;
            Actions = (group.actions ?? new List<DepthActionSettings>()).Select(a => a == null ? null : new Sps3DepthActionReadValues(a)).ToList().AsReadOnly();
        }
    }

    internal sealed class Sps3DepthActionReadValues
    {
        internal int RawKind { get; }
        internal bool HasManagedRendererReference { get; }
        internal string Shape { get; }
        internal float RawWeight { get; }
        internal float? HistoricalClampedWeight { get; }
        internal bool HasManagedClipReference { get; }
        internal bool HasManagedTargetReference { get; }
        internal bool ObjectOn { get; }
        internal Sps3DepthActionReadValues(DepthActionSettings action)
        {
            RawKind = (int)action.kind; Shape = action.shape; RawWeight = action.weight;
            if (!float.IsNaN(RawWeight) && !float.IsInfinity(RawWeight)) HistoricalClampedWeight = Math.Max(0, Math.Min(100, RawWeight));
            HasManagedRendererReference = !ReferenceEquals(action.renderer, null);
            HasManagedClipReference = !ReferenceEquals(action.clip, null);
            HasManagedTargetReference = !ReferenceEquals(action.target, null); ObjectOn = action.objectOn;
        }
    }

    internal sealed class Sps3FingerCalibrationReadValues
    {
        internal bool Calibrated { get; }
        internal float ThumbWeight { get; }
        internal string ThumbPath { get; }
        internal string IndexPath { get; }
        internal IReadOnlyList<float> ThumbOffset { get; }
        internal IReadOnlyList<float> IndexOffset { get; }
        internal IReadOnlyList<float> ThumbRotation { get; }
        internal IReadOnlyList<float> IndexRotation { get; }
        internal IReadOnlyList<float> Center { get; }
        internal IReadOnlyList<float> Euler { get; }
        internal Sps3FingerCalibrationReadValues(FingerRingCalibration value)
        {
            Calibrated = value.calibrated; ThumbWeight = value.thumbWeight; ThumbPath = value.thumbPath; IndexPath = value.indexPath;
            ThumbOffset = Vector(value.thumbOffset); IndexOffset = Vector(value.indexOffset);
            ThumbRotation = Rotation(value.thumbRotation); IndexRotation = Rotation(value.indexRotation);
            Center = Vector(value.center); Euler = Vector(value.euler);
        }
        private static IReadOnlyList<float> Vector(Vector3 value) => Array.AsReadOnly(new[] { value.x, value.y, value.z });
        private static IReadOnlyList<float> Rotation(Quaternion value) => Array.AsReadOnly(new[] { value.x, value.y, value.z, value.w });
    }
}
