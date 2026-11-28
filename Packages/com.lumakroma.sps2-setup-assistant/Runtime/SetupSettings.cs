using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant
{
    // Persisted values: never reorder. Missing language in older settings means Japanese.
    public enum DisplayLanguage { Japanese = 0, English = 1, Korean = 2, ChineseSimplified = 3, ChineseTraditional = 4 }

    // Persisted identity: missing in old settings means original VRCFury. Never reorder.
    public enum SpsFamily { VrcFury = 0, SpsForNdmf = 1 }

    [Serializable]
    public sealed class GeneratedSocket
    {
        public string id;
        public Transform anchor;
        public Transform pose;
        public Component socket;
        public Transform[] pathStops = Array.Empty<Transform>();
        public string buildToken;
    }

    [Serializable]
    public sealed class SetupSettings
    {
        public SpsFamily family;
        public DisplayLanguage menuLanguage;
        public int displayNamesVersion;
        public List<SocketSettings> parts = new List<SocketSettings>();
        public bool penetration = true;
        // Missing in older snapshots: false keeps their previously enabled Collapse.
        public bool showInternalThickness;
        public bool autoMode = true;
        public bool legacy = true;
        // Zero means historical saved settings. Never initialize recommendations during deserialization.
        public int sps3PolicyVersion;
        public string sps3MenuPath = "";
        public bool sps3AutoAvailable, sps3AutoDefaultOn, sps3AutoSaved, sps3SaveSockets;
        public bool sps3LegacyAvailable, sps3LegacyDefaultOn, sps3LegacySaved;
        // Policy 2: master affects emitted flags only; subordinate choices remain stored.
        public bool sps3LegacyMaster, sps3LegacyPlugs, sps3LegacySockets;
        public bool sps3LegacyTpsAnimations, sps3LegacyDpsAnimations;
        public int sps3LegacyAdoptionVersion, sps3LegacyAdoptionMask;
        public void BeginLegacyTree() => BeginLegacyTreeFromNative(0);
        public void BeginLegacyTreeFromNative(int nativeMask)
        {
            if (nativeMask < 0 || nativeMask > 3) throw new InvalidOperationException("Unknown Legacy animation flags.");
            if (sps3PolicyVersion == 2) return;
            if (sps3PolicyVersion != 1) throw new InvalidOperationException("Initialize SPS3 policy before editing Legacy.");
            sps3LegacyMaster = sps3LegacyAvailable || legacy;
            sps3LegacyPlugs = sps3LegacyAvailable;
            sps3LegacySockets = legacy;
            sps3LegacyTpsAnimations = (nativeMask & 1) != 0;
            sps3LegacyDpsAnimations = (nativeMask & 2) != 0;
            sps3LegacyAdoptionVersion = 1;
            sps3LegacyAdoptionMask = nativeMask;
            sps3PolicyVersion = 2;
        }
        public bool LegacyAdoptionMatches(int nativeMask) => nativeMask >= 0 && nativeMask <= 3 &&
            (sps3LegacyAdoptionVersion == 1 && sps3LegacyAdoptionMask == nativeMask ||
             sps3LegacyAdoptionVersion == 0 && nativeMask == 0);
        public int LegacyTreeMask() => (sps3LegacyMaster && sps3LegacyPlugs ? 16 : 0) |
            (sps3LegacyDefaultOn ? 32 : 0) | (sps3LegacySaved ? 64 : 0) |
            (sps3LegacyMaster && sps3LegacySockets ? 128 : 0) |
            (sps3LegacyMaster && sps3LegacyPlugs && sps3LegacyTpsAnimations ? 256 : 0) |
            (sps3LegacyMaster && sps3LegacySockets && sps3LegacyDpsAnimations ? 512 : 0);
        // Independent of global options/Legacy ownership. Missing means historical eligibility.
        public int sps3AutoEligibilityVersion;
        public bool Sps3IncludeInAuto(SocketSettings part) => part != null &&
            (sps3AutoEligibilityVersion == 1 ? part.sps3AutoParticipant :
                !part.IsFingerRing && (sps3PolicyVersion == 0 ? autoMode : part.sps3AutoParticipant));

        public void BeginExplicitAutoSelection()
        {
            if (sps3AutoEligibilityVersion == 1) return;
            if (parts != null)
                foreach (var part in parts)
                    if (part != null) part.sps3AutoParticipant = Sps3IncludeInAuto(part);
            sps3AutoEligibilityVersion = 1;
        }
        public bool localOnly;
        public bool modularAvatar;

        public SetupSettings Copy()
        {
            var copy = new SetupSettings { family = family, menuLanguage = menuLanguage, displayNamesVersion = displayNamesVersion,
                penetration = penetration, showInternalThickness = showInternalThickness, autoMode = autoMode,
                legacy = legacy, localOnly = localOnly, modularAvatar = modularAvatar,
                sps3AutoEligibilityVersion = sps3AutoEligibilityVersion, sps3PolicyVersion = sps3PolicyVersion, sps3AutoAvailable = sps3AutoAvailable, sps3MenuPath = sps3MenuPath,
                sps3AutoDefaultOn = sps3AutoDefaultOn, sps3AutoSaved = sps3AutoSaved, sps3SaveSockets = sps3SaveSockets,
                sps3LegacyAvailable = sps3LegacyAvailable, sps3LegacyDefaultOn = sps3LegacyDefaultOn, sps3LegacySaved = sps3LegacySaved,
                sps3LegacyMaster=sps3LegacyMaster, sps3LegacyPlugs=sps3LegacyPlugs, sps3LegacySockets=sps3LegacySockets,
                sps3LegacyTpsAnimations=sps3LegacyTpsAnimations, sps3LegacyDpsAnimations=sps3LegacyDpsAnimations,
                sps3LegacyAdoptionVersion=sps3LegacyAdoptionVersion, sps3LegacyAdoptionMask=sps3LegacyAdoptionMask };
            foreach (var part in parts) copy.parts.Add(part.Copy());
            return copy;
        }
    }

    public enum DepthActionKind { BlendShape, AnimationClip, Object }
    public enum DepthUnits { Meters, Plugs, Local }

    [Serializable]
    public sealed class SocketSettings
    {
        public string id;
        public string name;
        public string menuNameOverride;
        public int category;
        public bool custom;
        public bool included;
        public bool sps3AutoParticipant;
        public FingerRingCalibration fingerRing = new FingerRingCalibration();
        public bool IsFingerRing => !custom && (id == "fingerRingLeft" || id == "fingerRingRight");
        public Transform target;
        public bool depth;
        public Vector2 range = new Vector2(0, .05f);
        public DepthUnits units;
        public List<DepthActionSettings> actions = new List<DepthActionSettings>();

        // Zero is historical, including a missing JSON field. Reading never migrates.
        public int animationModelVersion;
        public bool animationEnabled;
        public List<SocketAnimationGroup> animationGroups = new List<SocketAnimationGroup>();
        public bool AnimationEnabled => animationModelVersion == 0 ? depth : animationEnabled;

        public IEnumerable<SocketAnimationGroup> EffectiveAnimationGroups()
        {
            if (animationModelVersion == 0)
            {
                yield return new SocketAnimationGroup {
                    id = SocketAnimationGroup.LegacyId, kind = SocketAnimationKind.Depth,
                    depthSettings = new SocketAnimationSettings { range = range, units = units },
                    actions = actions
                };
                yield break;
            }
            if (animationGroups != null)
                foreach (var group in animationGroups) yield return group;
        }

        // Initialize a new group or an imported working copy, never the serialized source.
        public void InitializeAnimationGroups()
        {
            if (animationModelVersion != 0) return;
            var groups = new List<SocketAnimationGroup>();
            foreach (var group in EffectiveAnimationGroups()) groups.Add(group.Copy());
            animationGroups = groups;
            animationEnabled = depth;
            animationModelVersion = 1;
        }

        public SocketSettings Copy()
        {
            var copy = new SocketSettings { id = id, name = name, menuNameOverride = menuNameOverride, category = category,
                custom = custom, included = included, sps3AutoParticipant = sps3AutoParticipant, target = target, depth = depth,
                range = range, units = units, fingerRing = fingerRing?.Copy() ?? new FingerRingCalibration(),
                animationModelVersion = animationModelVersion, animationEnabled = animationEnabled,
                animationGroups = animationGroups == null ? null : new List<SocketAnimationGroup>() };
            if (actions == null) copy.actions = null;
            else foreach (var action in actions) copy.actions.Add(action?.Copy());
            if (animationGroups != null) foreach (var group in animationGroups) copy.animationGroups.Add(group?.Copy());
            return copy;
        }
    }

    public enum SocketAnimationKind { Depth = 0, PlugEntryRadius = 1, Active = 2 }

    [Serializable]
    public sealed class SocketAnimationSettings
    {
        public Vector2 range = new Vector2(-.25f, 0);
        public DepthUnits units = DepthUnits.Local;
        public bool enableSelf = true, enableOthers = true;
        public float smoothingSeconds;
        // The weight axis is always Socket Local, independently of range units.
        public bool radiusWeightEnabled;
        public float radiusZero, radiusFull;
        // Opt-in prototype. Old data remains manual; the saved manual bank survives auto generation.
        public bool radiusAutoCorrection;
        public float radiusManualZero, radiusManualFull;
        // Persist display results independently of the manual bank. A result is a
        // last-known value, not proof that the current mesh/scale still matches.
        public bool radiusAutoHasResult, radiusAutoUsedStandard;
        // Generation-only signal: never persisted in the user's settings asset.
        [NonSerialized] public bool radiusKeepNative;
        public float radiusAutoZero, radiusAutoFull;
        public string radiusAutoMessage = "";
        public void RecordRadiusAutoResult(float zero, float full, bool standard, string message)
        {
            if (!radiusAutoCorrection || float.IsNaN(zero) || float.IsInfinity(zero) ||
                float.IsNaN(full) || float.IsInfinity(full) || zero < 0 || full <= zero)
                throw new InvalidOperationException("Invalid automatic radius result.");
            radiusAutoZero = zero; radiusAutoFull = full; radiusAutoHasResult = true;
            radiusAutoUsedStandard = standard; radiusAutoMessage = message ?? "";
            radiusZero = zero; radiusFull = full;
        }

        public void SetRadiusAutoCorrection(bool enabled)
        {
            if (enabled == radiusAutoCorrection) return;
            if (enabled) { radiusManualZero = radiusZero; radiusManualFull = radiusFull;
                if (radiusAutoHasResult) { radiusZero = radiusAutoZero; radiusFull = radiusAutoFull; } }
            else { radiusZero = radiusManualZero; radiusFull = radiusManualFull; }
            radiusAutoCorrection = enabled;
        }
        // Defaults for new SPS3 depth fields, including fields absent in a legacy Setup asset.
        public static SocketAnimationSettings NewDepthForPart(string partId, bool custom)
        {
            var result = new SocketAnimationSettings { radiusWeightEnabled = false, radiusAutoCorrection = true };
            if (!custom && partId == "mouth") { result.range = new Vector2(0f, .05f); result.units = DepthUnits.Meters; }
            return result;
        }
        public SocketAnimationSettings Copy() => (SocketAnimationSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class SocketAnimationGroup
    {
        public const string LegacyId = "legacy-depth";
        public string id;
        public SocketAnimationKind kind;
        // Keep both payloads when switching type, including inactive Plugs/weight values.
        public SocketAnimationSettings depthSettings = new SocketAnimationSettings();
        public SocketAnimationSettings entrySettings = new SocketAnimationSettings { range = new Vector2(0, .1f) };
        public List<DepthActionSettings> actions = new List<DepthActionSettings>();
        public SocketAnimationSettings ActiveSettings => kind == SocketAnimationKind.Depth ? depthSettings : entrySettings;
        public bool interferenceEnabled;
        [NonSerialized] public AnimationClip interferenceClip;
        public bool mouthNeutralEnabled;
        // A separate renderer binding. It is never a mesh-measurement or preview input.
        public DepthActionSettings mouthNeutral = new DepthActionSettings { weight = 0 };
        public static SocketAnimationGroup Create(SocketAnimationKind kind, string partId = null, bool custom = false) => new SocketAnimationGroup {
            id = Guid.NewGuid().ToString("N"), kind = kind,
            depthSettings = kind == SocketAnimationKind.Depth ? SocketAnimationSettings.NewDepthForPart(partId, custom) :
                new SocketAnimationSettings { radiusWeightEnabled = false, radiusAutoCorrection = true },
            actions = new List<DepthActionSettings> { new DepthActionSettings() }
        };
        public SocketAnimationGroup Copy()
        {
            var copy = new SocketAnimationGroup { id = id, kind = kind, depthSettings = depthSettings?.Copy(),
                entrySettings = entrySettings?.Copy(), actions = actions == null ? null : new List<DepthActionSettings>(),
                interferenceEnabled = interferenceEnabled, interferenceClip = interferenceClip,
                mouthNeutralEnabled = mouthNeutralEnabled, mouthNeutral = mouthNeutral?.Copy() };
            if (actions != null) foreach (var action in actions) copy.actions.Add(action?.Copy());
            return copy;
        }
        public string ReferenceKey(string partId, int index) => partId + ":group:" + id + ":" + index;
    }

    [Serializable]
    public sealed class FingerRingCalibration
    {
        public bool calibrated;
        public float thumbWeight = .5f;
        public string thumbPath, indexPath;
        public Vector3 thumbOffset, indexOffset;
        public Quaternion thumbRotation = Quaternion.identity, indexRotation = Quaternion.identity;
        // Avatar-local UI inputs. Capture explicitly commits these to joint-local frames.
        public Vector3 center, euler;
        public FingerRingCalibration Copy() => (FingerRingCalibration)MemberwiseClone();
    }

    [Serializable]
    public sealed class BlendShapePoint
    {
        public float position, value;
        public BlendShapePoint Copy() => (BlendShapePoint)MemberwiseClone();
    }
    public enum BlendShapeMode { Normal = 0, Custom = 1 }
    public enum BlendShapeInterpolation { Linear = 0, Smooth = 1, CircularInOut = 2 }
    [Serializable]
    public sealed class DepthActionSettings
    {
        public DepthActionKind kind;
        // All type-specific inputs survive switching kinds.
        public SkinnedMeshRenderer renderer;
        public string shape = "";
        public float weight = 100;
        public BlendShapeMode blendShapeMode;
        public BlendShapeInterpolation blendShapeInterpolation;
        public List<BlendShapePoint> blendShapePoints = new List<BlendShapePoint>();
        // Generated artifact is separate from the user's AnimationClip bank.
        public AnimationClip generatedBlendShapeClip;
        public string generatedBlendShapeSignature;
        public AnimationClip clip;
        public GameObject target;
        public bool objectOn = true;
        public DepthActionSettings Copy() {
            var copy = (DepthActionSettings)MemberwiseClone();
            copy.blendShapePoints = blendShapePoints == null ? null : new List<BlendShapePoint>();
            if (blendShapePoints != null) foreach(var p in blendShapePoints) copy.blendShapePoints.Add(p?.Copy());
            return copy;
        }
    }
}
