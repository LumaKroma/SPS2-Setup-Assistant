using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        private static InvalidOperationException LocalCandidateBlocked(string reason) =>
            new InvalidOperationException(SpsAuthoringCompatibility.LocalRegenerationScaleReason + ": " + reason);

        private static float StrictUniformWorldScale(Transform value, string partId = null)
        {
            var partContext = string.IsNullOrEmpty(partId) ? "" : " [part: " + partId + "]";
            if (value == null) throw LocalCandidateBlocked("Missing owner or parent frame." + partContext);
            for (var t = value; t != null; t = t.parent)
                if (!LocalRegenerationScaleMath.Uniform(t.localScale.x, t.localScale.y, t.localScale.z))
                    throw LocalCandidateBlocked("Exact positive uniform ancestry required: " + t.name + partContext);
            var world = value.lossyScale;
            if (!LocalRegenerationScaleMath.Uniform(world.x, world.y, world.z))
                throw LocalCandidateBlocked("World scale is not finite positive uniform: " + value.name + partContext);
            return world.x;
        }

        private sealed class LocalScaleCandidatePlan
        {
            internal Vector3 rootScale;
            internal readonly Dictionary<string, (float world, Vector3 anchor, Vector3 pose, Vector3 restPosition, Vector3 restRotation, Vector3 translationOffset, Vector3 rotationOffset, bool modularAvatar)> scales =
                new Dictionary<string, (float world, Vector3 anchor, Vector3 pose, Vector3 restPosition, Vector3 restRotation, Vector3 translationOffset, Vector3 rotationOffset, bool modularAvatar)>();
        }

        private static LocalScaleCandidatePlan PrepareLocalScaleCandidate(VRCAvatarDescriptor avatar,
            SetupSettings requested, Sps2SetupContext old, SpsAuthoringContract contract, bool confirmedImpact, bool confirmedNativeEdits)
        {
            if (contract != SpsAuthoringContract.Sps3Beta14 || old == null || old.AuthoringContract != contract)
                throw LocalCandidateBlocked("Candidate requires an existing reviewed SPS3 setup, not migration.");
            // The prior scale proof consumes only legacy fields. Adapt copies, never the saved/requested payload.
            Sps3SingleDepthScaleProjection.RequireRetainedEquivalentGroups(requested, old.settings);
            var requestedProof = Sps3SingleDepthScaleProjection.ForProof(requested);
            var savedProof = Sps3SingleDepthScaleProjection.ForProof(old.settings);
            ValidateOwnership(old, false);
            if (old.avatar != avatar || old.transform.parent != avatar.transform ||
                PrefabUtility.IsPartOfAnyPrefab(old.gameObject) || old.settings.penetration != requested.penetration ||
                old.settings.modularAvatar != requested.modularAvatar)
                throw LocalCandidateBlocked("Candidate requires a scene-only setup without paths or attachment-backend changes.");
            if (old.settings.penetration) Sps3OwnedPath.RequireHierarchyUnchanged(old, confirmedNativeEdits);
            if (!HumanoidSnapshot.TryCapture(avatar, out var snapshot, out var captureError))
                throw LocalCandidateBlocked(captureError);
            var basis = BodyBasisBuilder.Build(snapshot);
            using var surface = new AvatarSurface(avatar);
            // Missing non-Local optional parts are admitted only when both old/requested
            // placement still skip them. Never silently repair a missing retained Local slot.
            var included = requestedProof.parts.Where(p => p.included).ToArray();
            foreach (var part in included.Where(p => !old.sockets.Any(s => s.id == p.id)))
            {
                var saved = savedProof.parts.SingleOrDefault(p => p.id == part.id);
                if (saved == null || !saved.included || saved.IsFingerRing || part.IsFingerRing ||
                    saved.custom != part.custom || saved.target != part.target ||
                    saved.depth != part.depth || saved.units != part.units ||
                    part.depth && part.units == DepthUnits.Local ||
                    Place(saved, avatar, basis, surface) != null || Place(part, avatar, basis, surface) != null)
                    throw LocalCandidateBlocked("Only unchanged skipped non-Local parts may lack generated output: " + part.id);
            }
            var active = included.Where(p => old.sockets.Any(s => s.id == p.id)).ToArray();
            if (active.Length != old.sockets.Count)
                throw LocalCandidateBlocked("Exactly the retained generated socket IDs are required.");
            bool RequiresLocalScale(SocketSettings proof) {
                var actual = requested.parts.Single(p => p.id == proof.id);
                return proof.depth && proof.units == DepthUnits.Local || actual.animationModelVersion == 1 &&
                    actual.AnimationEnabled && actual.animationGroups.Any(g => g.kind == SocketAnimationKind.Depth && Sps3AnimationGroupNative.WeightActive(g.depthSettings));
            }
            bool mixed = active.Any(p => !RequiresLocalScale(p));
            // Shared-root scale cannot retain Local animation bindings while also resetting
            // non-Local output. Do not flatten scales or invent compensation hierarchy nodes.
            if (mixed && !old.transform.localScale.Equals(Vector3.one))
                throw LocalCandidateBlocked("Mixed Local/non-Local regeneration requires identity generated-root scale; preserving shared-root scale conflicts with non-Local reset semantics.");
            var allowed = new HashSet<Transform> { old.transform };
            var guided = old.transform.Find("Guided Paths");
            if (guided != null && old.settings.penetration && !string.IsNullOrEmpty(old.nativePathProof))
                foreach(var node in guided.GetComponentsInChildren<Transform>(true)) allowed.Add(node);
            var plan = new LocalScaleCandidatePlan { rootScale = old.transform.localScale };
            var newParentScale = StrictUniformWorldScale(avatar.transform);
            foreach (var part in active)
            {
                var saved = savedProof.parts.SingleOrDefault(p => p.id == part.id);
                var socket = old.sockets.Single(s => s.id == part.id);
                if (saved == null || !saved.included || saved.IsFingerRing || part.IsFingerRing ||
                    saved.custom != part.custom || saved.depth != part.depth || saved.units != part.units)
                    throw LocalCandidateBlocked("Retained non-ring slots require unchanged placement kind, depth enablement and units: " + part.id);
                bool preserveLocal = RequiresLocalScale(part);
                var constraint = socket.anchor.GetComponent<ParentConstraint>();
                var native = socket.anchor.GetComponents<Component>().SingleOrDefault(VrcConstraintBridge.IsVrcParent);
                var proxy = socket.anchor.GetComponents<Component>().SingleOrDefault(SpsHistoricalPrefabComparison.IsModularAvatarBoneProxy);
                if ((constraint != null ? 1 : 0) + (native != null ? 1 : 0) + (proxy != null ? 1 : 0) != 1)
                    throw LocalCandidateBlocked("Exactly one reviewed attachment is required: " + part.id);
                if (proxy != null && (!requested.modularAvatar || part.custom))
                    throw LocalCandidateBlocked("Unexpected Modular Avatar attachment: " + part.id);
                var nativeData = native == null || !preserveLocal ? null : VrcConstraintBridge.ReadSingleSource(native);
                if (part.custom)
                {
                    if (saved.target != part.target || part.target == null || part.target.IsChildOf(old.transform))
                        throw LocalCandidateBlocked("The retained custom attachment target changed: " + part.id);
                    // Only Local depth preservation uses this target as a scale
                    // proof. Other retained sockets are checked by the ordinary
                    // placement validation and do not contribute to its ratio.
                    if (LocalRegenerationScaleMath.NeedsTargetScaleProof(preserveLocal)) StrictUniformWorldScale(part.target, part.id);
                }
                else
                {
                    var placement = Place(part, avatar, basis, surface);
                    if (placement == null || placement.first == null)
                        throw LocalCandidateBlocked("既存の追従先を確認できません。元の Socket は変更されていません: " + part.id);
                    bool directConstraint = placement.second != null || placement.followTransform;
                    if (!LocalRegenerationScaleMath.AttachmentMatches(requested.modularAvatar, directConstraint, proxy != null) ||
                        VrcFuryCapabilities.Current.PublicAttachment)
                        throw LocalCandidateBlocked("既存の追従方式と再生成後の方式が一致しません。元の Socket は変更されていません: " + part.id);
                    if (preserveLocal &&
                        (placement.second != null || placement.followTransform || placement.bone == HumanBodyBones.LastBone))
                        throw LocalCandidateBlocked("Local の深度・半径を保ったまま、この表面追従または中点配置を再生成できるか確認できません。既存の Socket は変更されていません: " + part.id);
                    if (preserveLocal && proxy == null && (constraint != null ? constraint.sourceCount != 1 || constraint.GetSource(0).sourceTransform != placement.first : nativeData.source != placement.first))
                        throw LocalCandidateBlocked("The retained builtin bone reference changed: " + part.id);
                    if (LocalRegenerationScaleMath.NeedsTargetScaleProof(preserveLocal)) StrictUniformWorldScale(placement.first, part.id);
                }
                if (preserveLocal)
                {
                    var world = StrictUniformWorldScale(socket.pose, part.id); // Native socket.owner() measurement point.
                    LocalRegenerationScaleMath.ReplacementScale(world, newParentScale);
                    plan.scales.Add(part.id, (world, socket.anchor.localScale, socket.pose.localScale,
                        constraint != null ? constraint.translationAtRest : nativeData != null ? nativeData.positionRest : Vector3.zero,
                        constraint != null ? constraint.rotationAtRest : nativeData != null ? nativeData.rotationRest : Vector3.zero,
                        constraint != null ? constraint.GetTranslationOffset(0) : nativeData != null ? nativeData.positionOffset : Vector3.zero,
                        constraint != null ? constraint.GetRotationOffset(0) : nativeData != null ? nativeData.rotationOffset : Vector3.zero, proxy != null));
                }
                // No non-Local snapshot: CreatePart retains the original scale/rest reset rules.
                allowed.Add(socket.anchor); allowed.Add(socket.pose);
                var anchorComponents = socket.anchor.GetComponents<Component>();
                var poseComponents = socket.pose.GetComponents<Component>();
                if (anchorComponents.Any(c => c == null) || poseComponents.Any(c => c == null) ||
                    !confirmedImpact && (anchorComponents.Length != 2 || anchorComponents.Any(c => !(c is Transform) && !(c is ParentConstraint) && !VrcConstraintBridge.IsVrcParent(c) && !SpsHistoricalPrefabComparison.IsModularAvatarBoneProxy(c)) ||
                    poseComponents.Length != 2 || poseComponents.Any(c => !(c is Transform) && c != socket.socket)) ||
                    socket.pathStops.Length != 0 && !old.settings.penetration)
                    throw LocalCandidateBlocked("Unowned component/path in retained socket: " + part.id);
            }
            foreach (var node in old.gameObject.GetComponentsInChildren<Transform>(true))
                if (!confirmedImpact && !allowed.Contains(node)) throw LocalCandidateBlocked("Unowned child in generated hierarchy: " + node.name);
            Sps3OwnedGlobalOptions.VerifyGeneratedRoot(old, old.gameObject.GetComponents<Component>().Where(c => !(c is Transform)).ToArray());
            // Also checks dormant settings refs, external object refs and exact native payload/constraint values.
            // It stages only owned transient comparison objects; no persistent data is changed.
            SpsPrefabRegeneration.ValidateReplacementReferences(old, requested, customAvatarFrame: true,
                allowAdditionalObjects: confirmedImpact, allowExternalReferences: confirmedImpact, allowNativeEdits: confirmedNativeEdits);
            return plan;
        }
    }
}
