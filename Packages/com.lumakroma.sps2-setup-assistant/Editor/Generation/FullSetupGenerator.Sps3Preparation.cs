using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    internal sealed class Sps3PreparedPlacement
    {
        internal string Id { get; }
        internal string RootName => FullSetupGenerator.Sps3RootName;
        internal string AnchorName => Id;
        internal string PoseName { get; }
        internal Transform First { get; }
        internal Transform Second { get; }
        internal bool FollowTransform { get; }
        internal HumanBodyBones Bone { get; }
        internal Vector3 Position { get; }
        internal Quaternion Rotation { get; }
        internal Sps3SocketSpecification Specification { get; }
        internal Sps3DepthReferenceResolver DepthCapture { get; }
        internal Sps3DepthReferenceResult DepthReferences { get; }
        internal Sps3PreparedPlacement(string id, Transform first, Transform second, bool follow,
            HumanBodyBones bone, Vector3 position, Quaternion rotation, Sps3SocketSpecification spec,
            Sps3DepthReferenceResolver capture, Sps3DepthReferenceResult references)
        {
            Id = id; PoseName = FullSetupGenerator.SocketPoseName(id); First = first; Second = second;
            FollowTransform = follow; Bone = bone; Position = position; Rotation = rotation;
            Specification = spec; DepthCapture = capture; DepthReferences = references;
        }
    }

    public static partial class FullSetupGenerator
    {
        // Shared existing placement code; no alternate generator, asset identity, scene writes or Undo.
        // This value is a point-in-time preparation, never an authoring/ownership capability.
        internal static IReadOnlyList<Sps3PreparedPlacement> PrepareSps3NewSetup(VRCAvatarDescriptor avatar, SetupSettings settings)
        {
            bool exact = FuryMinimalSps3Probe.Available(out _);
            return PrepareSps3NewSetupCore(avatar, settings, exact);
        }

        internal static IReadOnlyList<Sps3PreparedPlacement> PrepareSps3NewSetupCore(
            VRCAvatarDescriptor avatar, SetupSettings settings, bool exactBeta14)
        {
            if (avatar == null || EditorUtility.IsPersistent(avatar) || !avatar.gameObject.scene.IsValid() ||
                !avatar.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabUtility.IsPartOfPrefabAsset(avatar) || PrefabUtility.IsPartOfPrefabInstance(avatar))
                throw new InvalidOperationException("A loaded scene avatar is required; prefab authoring remains unvalidated.");
            bool existing = HasGeneratedRoot(avatar) || avatar.transform.Find(Sps3Beta14SocketAdapter.RootName) != null ||
                SpsBackend.AllSockets(avatar.gameObject).Any(c => VrcFuryCompatibility.ReadIdentity(c) != null);
            var preflight = Sps3NewSetupPreflight.Inspect(settings, exactBeta14, existing);
            if (!preflight.CanPrepare) throw new InvalidOperationException(string.Join(",", preflight.InputBlockers));
            if (!HumanoidSnapshot.TryCapture(avatar, out var humanoid, out var error)) throw new InvalidOperationException(error);
            var basis = BodyBasisBuilder.Build(humanoid);
            using var surface = new AvatarSurface(avatar);
            var result = new List<Sps3PreparedPlacement>();
            foreach (var part in settings.parts.Where(p => p.included))
            {
                var placement = Place(part, avatar, basis, surface);
                if (placement == null || placement.first == null ||
                    (placement.first != avatar.transform && !placement.first.IsChildOf(avatar.transform)) ||
                    (placement.second != null && !placement.second.IsChildOf(avatar.transform)) ||
                    placement.first.GetComponentInParent<VRCAvatarDescriptor>() != avatar ||
                    (placement.second != null && placement.second.GetComponentInParent<VRCAvatarDescriptor>() != avatar))
                    throw new InvalidOperationException("Placement reference unresolved:" + part.id);
                var read = new Sps3SettingsPartReadPlan(part, settings);
                var specification = new Sps3SocketSpecification(SocketDisplayNames.Resolve(part, settings.menuLanguage),
                    read.HistoricalModeIntent, part.depth, part.depth ? read.DepthUnitName : "Meters",
                    part.depth ? read.RangeMin : 0, part.depth ? read.RangeMax : 0, read.IncludeInAuto, false,
                    read.RequestsRadiusOffset, false, new Sps3LocalPoint[0], false);
                var capture = part.depth ? Sps3DepthReferenceResolver.Capture(part, settings) : null;
                var references = capture?.Resolve(avatar);
                if (references != null && !references.ReferencesVerified)
                    throw new InvalidOperationException("Depth references:" + part.id + ":" + string.Join(",", references.Blockers));
                result.Add(new Sps3PreparedPlacement(part.id, placement.first, placement.second, placement.followTransform,
                    placement.bone, placement.position, placement.rotation, specification, capture, references));
            }
            return result.AsReadOnly();
        }

        internal static void ValidateSps3PlacementScales(VRCAvatarDescriptor avatar, SetupSettings settings)
        {
            if (!HumanoidSnapshot.TryCapture(avatar, out var humanoid, out var error)) throw new InvalidOperationException(error);
            var basis = BodyBasisBuilder.Build(humanoid);
            using var surface = new AvatarSurface(avatar);
            foreach (var part in settings.parts.Where(p => p.included)) {
                if (part.IsFingerRing) continue; // Dual-joint constraint; calibration is reused/computed in Apply.
                var placement = Place(part, avatar, basis, surface, scaleValidationOnly: true);
                if (placement == null || placement.second != null || placement.followTransform || part.custom) continue;
                // Position/rotation-only ParentConstraint does not inherit its target's scale.
                // Armature attachment may move the generated owner under this ancestry during native build.
                SpsAuthoringCompatibility.RequireSupportedScaleChain(placement.first);
            }
        }

        internal static void RequireSps3ProductionApplyReady()
        {
            // Never route __SPS2_ identities to beta14 or legacy WD restoration without explicit provenance.
            throw new InvalidOperationException("SPS3 production Apply is blocked: persisted generation provenance, build admission, Write Defaults parity and commit/Undo/reload require validation.");
        }
    }
}
