using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        // A source-owned subtree may contain native fields the Setup settings do not model.
        // Prove the saved payload against the real factory/configuration before replacing it.
        // Staging is transient, never saved or linked into the avatar; only its Undo is cleared.
        internal static void ValidatePrefabGeneratedPayload(Sps2SetupContext root, bool customAvatarFrame = false,
            bool allowAddedComponents = false, bool allowNativeEdits = false)
        {
            if (!HumanoidSnapshot.TryCapture(root.avatar, out var snapshot, out var error))
                throw new InvalidOperationException(error);
            var basis = BodyBasisBuilder.Build(snapshot);
            using var surface = new AvatarSurface(root.avatar);
            foreach (var generated in root.sockets)
            {
                var part = root.settings.parts.Single(p => p.id == generated.id);
                var placement = Place(part, root.avatar, basis, surface);
                if (placement == null) throw new InvalidOperationException("Cannot verify the saved prefab attachment: " + part.id);
                var anchor = new GameObject("SPS prefab payload comparison") { hideFlags = HideFlags.HideAndDontSave };
                var pose = new GameObject(generated.pose.name) { hideFlags = HideFlags.HideAndDontSave };
                GameObject historicalComparison = null;
                GameObject comparisonFrame = null;
                try
                {
                    if (customAvatarFrame && !part.IsFingerRing)
                    {
                        // Admitted custom and single-bone sockets were authored beneath an identity setup root
                        // under the avatar. ParentConstraint rest fields are local to that frame,
                        // not world-space and not the subsequently hand-scaled generated root.
                        // Reconstruct that frame without linking any temporary object to the avatar.
                        comparisonFrame = new GameObject("SPS generated comparison avatar frame") { hideFlags = HideFlags.HideAndDontSave };
                        comparisonFrame.transform.SetPositionAndRotation(root.avatar.transform.position, root.avatar.transform.rotation);
                        comparisonFrame.transform.localScale = root.avatar.transform.lossyScale;
                        anchor.transform.SetParent(comparisonFrame.transform, false);
                    }
                    pose.transform.SetParent(anchor.transform, false);
                    anchor.transform.SetPositionAndRotation(
                        placement.second == null ? placement.first.position : (placement.first.position + placement.second.position) * .5f,
                        placement.second == null ? placement.first.rotation : Quaternion.Slerp(placement.first.rotation, placement.second.rotation, .5f));
                    pose.transform.SetPositionAndRotation(placement.position, placement.rotation);
                    var wrapper = VrcFuryApi.CreateSocket(pose);
                    wrapper.SetMode(root.settings.penetration && (part.id == "mouth" || part.id == "anus") ? "Ring" : "Auto");
                    if (part.id == "chest" || part.id == "handLeft" || part.id == "handRight" || part.id == "hands" ||
                        part.id == "footLeft" || part.id == "footRight" || part.id == "feet") wrapper.UseRadiusOffset();
                    var expectedSocket = VrcFuryCompatibility.FindSocket(pose);
                    VrcFuryCompatibility.Configure(expectedSocket, part, root.settings, new List<string>());
                    VrcFuryCompatibility.SetAuthoringIdentity(expectedSocket, Sps2SetupStorage.Token(root, generated.id));
                    // Known generated path fields are part of the old output being replaced.
                    // Extra fields/groups, active actions, tags and offsets are not copied.
                    var comparison = PrepareHistoricalComparison(root, generated, out historicalComparison);
                    var comparisonSocket = new GeneratedSocket { id = generated.id, socket = comparison,
                        pose = comparison.transform, pathStops = generated.pathStops };
                    SpsMigrationAdapter.CopyKnownPath(comparison, expectedSocket, generated.pathStops);
                    CopyVerifiedLegacyPathUpgradeResidue(root, comparisonSocket, expectedSocket);
                    try { SpsMigrationAdapter.ValidateEquivalent(comparison, expectedSocket); }
                    catch (InvalidOperationException e) when (allowNativeEdits && e.Message.StartsWith("Native settings differ:", StringComparison.Ordinal)) { }

                    var actualAttachment = generated.anchor.GetComponents<Component>().Where(c => !(c is Transform))
                        .Where(c => !allowAddedComponents ||
                            (PrefabUtility.IsPartOfPrefabInstance(generated.anchor.gameObject)
                                ? PrefabUtility.GetCorrespondingObjectFromSource(c) != null
                                : c is ParentConstraint || VrcConstraintBridge.IsVrcParent(c) ||
                                  SpsHistoricalPrefabComparison.IsLegacyFuryArmatureLink(c) ||
                                  SpsHistoricalPrefabComparison.IsModularAvatarBoneProxy(c))).Single();
                    if (part.IsFingerRing)
                        FingerRingCalibrationUtility.Configure(anchor, placement.first, placement.second, part.fingerRing);
                    else if (placement.second != null || placement.followTransform || part.custom)
                        Constrain(anchor, placement.first, placement.second);
                    else if (SpsHistoricalPrefabComparison.IsLegacyFuryArmatureLink(actualAttachment))
                    {
                        // Historical public armature-link authoring is compared, not regenerated
                        // into the new setup. This does not change the native SPS3 generator.
                        var api = VrcFuryApi.Create("CreateArmatureLink", anchor);
                        VrcFuryApi.Call(api, "LinkTo", new[] { typeof(HumanBodyBones), typeof(string) }, placement.bone, "");
                        VrcFuryApi.Call(api, "SetAlign", new[] { typeof(bool) }, true);
                    }
                    else if (SpsHistoricalPrefabComparison.IsModularAvatarBoneProxy(actualAttachment))
                    {
                        if (!AttachmentBackendRegistry.TryApply(AttachmentBackend.VrcFuryWithModularAvatar, anchor, placement.bone, out error))
                            throw new InvalidOperationException(error);
                    }
                    else
                    {
                        var constraint = anchor.AddComponent<ParentConstraint>();
                        constraint.AddSource(new ConstraintSource { sourceTransform = placement.first, weight = 1 });
                        constraint.SetTranslationOffset(0, placement.first.InverseTransformPoint(anchor.transform.position));
                        constraint.SetRotationOffset(0, (Quaternion.Inverse(placement.first.rotation) * anchor.transform.rotation).eulerAngles);
                        constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
                    }
                    if (VrcConstraintBridge.IsVrcParent(actualAttachment)) {
                        var unityConstraint = anchor.GetComponent<ParentConstraint>();
                        if (unityConstraint == null) throw new InvalidOperationException("Cannot reconstruct the VRC attachment from the reviewed Unity constraint.");
                        var staged = VrcConstraintBridge.ConvertComparison(unityConstraint);
                        VrcConstraintBridge.RetargetComparison(staged, anchor.transform, generated.anchor);
                    }
                    var expectedAttachment = anchor.GetComponents<Component>().Single(c => !(c is Transform));
                    try { SpsMigrationAdapter.ValidateEquivalent(actualAttachment, expectedAttachment); }
                    catch (InvalidOperationException e) when (allowNativeEdits && VrcConstraintBridge.IsVrcParent(actualAttachment) &&
                        e.Message.StartsWith("Native settings differ:", StringComparison.Ordinal)) { }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Prefab setup has customized or unverified native data; preserve it before regenerating. " + e.Message, e);
                }
                finally
                {
                    // No source assets/objects, global Undo ranges or user selections are touched.
                    foreach (var component in anchor.GetComponentsInChildren<Component>(true))
                        if (component != null) Undo.ClearUndo(component);
                    Undo.ClearUndo(pose); Undo.ClearUndo(anchor);
                    UnityEngine.Object.DestroyImmediate(anchor);
                    if (comparisonFrame != null) UnityEngine.Object.DestroyImmediate(comparisonFrame);
                    if (historicalComparison != null) UnityEngine.Object.DestroyImmediate(historicalComparison);
                }
            }
        }

        private static Component PrepareHistoricalComparison(Sps2SetupContext root, GeneratedSocket generated, out GameObject owned) =>
            SpsHistoricalPrefabComparison.PrepareHistoricalComparison(root, generated, out owned);

        internal static void CopyVerifiedLegacyPathUpgradeResidue(Sps2SetupContext root, GeneratedSocket generated, Component expected) =>
            SpsHistoricalPrefabComparison.CopyVerifiedLegacyPathUpgradeResidue(root, generated, expected);
    }
}
