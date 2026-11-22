using System;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        private const string LimitedMarkerPrefix = "__SPS_LIMITED_DRAFT_";
        private static string LimitedDraftIdentity(Transform root)
        {
            if (root == null) return null;
            string Parse(string name, string prefix) {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) return null;
                var value = name.Substring(prefix.Length);
                return Guid.TryParseExact(value, "N", out _) ? value : null;
            }
            var markers = root.Cast<Transform>().Select(t => Parse(t.name, LimitedMarkerPrefix)).Where(x => x != null).Distinct().ToArray();
            if (markers.Length > 1) throw new InvalidOperationException("限定出力の設定参照が複数あります。保存済みデータは変更していません。");
            if (markers.Length == 1) return markers[0];
            // Backward compatibility with earlier limited-output drafts.
            return Parse(root.name, "Fury-BasicSocket-Limited-") ?? Parse(root.name, "SPS-PlacementOnly-NotFunctional-");
        }
        internal static bool HasLimitedDraft(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return false;
            try { return avatar.transform.Cast<Transform>().Any(t => LimitedDraftIdentity(t) != null); }
            catch { return true; } // Let the explicit load operation report the ambiguity.
        }
        internal static bool HasLimitedNativeOutput(VRCAvatarDescriptor avatar) => avatar != null &&
            avatar.transform.Cast<Transform>().Any(t => LimitedDraftIdentity(t) != null && SpsBackend.AllSockets(t.gameObject).Length > 0);
        internal static SetupSettings FindLimitedDraft(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return null;
            var ids = avatar.transform.Cast<Transform>().Select(LimitedDraftIdentity).Where(x => x != null).Distinct().ToArray();
            if (ids.Length == 0) return null;
            if (ids.Length != 1) throw new InvalidOperationException("複数の限定出力があります。設定の自動選択は行いません。保存された設定アセットは変更していません。");
            return Sps2SetupStorage.LoadLimitedDraft(ids[0], avatar);
        }
        // Explicitly separate from SPS output. Never replaces an existing hierarchy.
        // The reusable Prefab has only Transforms. Scene-instance ParentConstraints
        // target avatar bones; those scene overrides are retained by scene saving.
        internal static bool ApplyPlacementOnly(VRCAvatarDescriptor avatar, SetupSettings settings,
            out GameObject instance, out string message) { instance = null; message = FuryUnavailableNotice.Explain(null); return false; }
        internal static bool ApplyBasicSocketOnly(VRCAvatarDescriptor avatar, SetupSettings settings,
            out GameObject instance, out string message) => ApplyBasicOutput(avatar, settings, true, out instance, out message);
        private static bool ApplyBasicOutput(VRCAvatarDescriptor avatar, SetupSettings settings, bool basicSocket,
            out GameObject instance, out string message)
        {
            instance = null; message = null;
            GameObject scratch = null; string assetPath = null; bool createdAsset = false; Sps2SetupAsset draftAsset = null; int undoGroup = -1;
            var activeWarnings = new List<string>();
            try
            {
                if (avatar == null || settings == null || settings.parts == null ||
                    EditorUtility.IsPersistent(avatar) || !avatar.gameObject.scene.IsValid() ||
                    !avatar.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Select a loaded scene avatar in Edit mode.");
                if (basicSocket && HasLimitedNativeOutput(avatar)) throw new InvalidOperationException("Existing limited Socket output is retained; replacement is not yet supported.");
                if (!HumanoidSnapshot.TryCapture(avatar, out var snapshot, out var why))
                    throw new InvalidOperationException(why);
                if (basicSocket && !FuryMinimalSps3Probe.Available(out var probeFailure))
                    throw new InvalidOperationException(probeFailure);
                var decision = FuryFeatureOutputPlan.Choose(new FuryFeatureOutputPlan.Inputs {
                    Requested = FuryFeatureOutputPlan.OptionalFeature.Depth | FuryFeatureOutputPlan.OptionalFeature.GuidedPath,
                    Supported = FuryFeatureOutputPlan.OptionalFeature.None
                });
                if (decision.Output != FuryFeatureOutputPlan.Mode.PlacementOnly)
                    throw new InvalidOperationException("Unexpected placement-only decision.");
                var selected = settings.parts.Where(p => p != null && p.included && !p.IsFingerRing).ToArray();
                if (selected.GroupBy(p => p.id).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() != 1))
                    throw new InvalidOperationException("Part identifiers are missing or duplicated.");
                var basis = BodyBasisBuilder.Build(snapshot);
                var placements = new List<KeyValuePair<SocketSettings, Placement>>();
                using (var surface = new AvatarSurface(avatar))
                    foreach (var part in selected) {
                        var placement = Place(part, avatar, basis, surface);
                        if (placement != null && placement.first != null)
                            placements.Add(new KeyValuePair<SocketSettings, Placement>(part, placement));
                    }
                if (placements.Count == 0) throw new InvalidOperationException("No usable placement target was found; existing output is unchanged.");
                draftAsset = Sps2SetupStorage.CreateLimitedDraft(avatar, settings);
                string draftIdentity = Sps2SetupStorage.Identity(draftAsset);
                if (string.IsNullOrEmpty(draftIdentity)) throw new InvalidOperationException("Saved draft identity unavailable.");
                scratch = new GameObject(basicSocket ? "FURY BASIC SOCKET - LIMITED" : "SPS PLACEMENT ONLY - NOT FUNCTIONAL");
                scratch.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
                var marker = new GameObject(LimitedMarkerPrefix + draftIdentity) { hideFlags = HideFlags.HideInHierarchy };
                marker.transform.SetParent(scratch.transform, false);
                foreach (var pair in placements) {
                    var anchor = new GameObject(pair.Key.id); anchor.transform.SetParent(scratch.transform, false);
                    var p = pair.Value;
                    anchor.transform.SetPositionAndRotation(p.second == null ? p.first.position : (p.first.position + p.second.position) * .5f,
                        p.second == null ? p.first.rotation : Quaternion.Slerp(p.first.rotation, p.second.rotation, .5f));
                    var pose = new GameObject("Placement - no SPS"); pose.transform.SetParent(anchor.transform, false);
                    pose.transform.SetPositionAndRotation(p.position, p.rotation);
                    if (basicSocket) using (SpsBackend.Use(SpsFamily.VrcFury)) {
                        var wrapper = VrcFuryApi.CreateSocket(pose);
                        wrapper.SetName(pair.Key.name ?? pair.Key.id);
                        wrapper.SetMode("Auto");
                        if (FuryBasicOptionalCapabilities.WantsSocketRadius(pair.Key.id)) {
                            var native = VrcFuryCompatibility.FindSocket(pose);
                            if (!FuryBasicOptionalCapabilities.TrySetSocketRadius(native, out var whyRadius))
                                activeWarnings.Add(pair.Key.id + ": " + whyRadius);
                        }
                    }
                }
                assetPath = (basicSocket ? "Assets/Fury-BasicSocket-Limited-" : "Assets/SPS-PlacementOnly-NotFunctional-") + draftIdentity + ".prefab";
                if (System.IO.File.Exists(assetPath) || System.IO.File.Exists(assetPath+".meta") || AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                { assetPath = null; throw new InvalidOperationException("New Prefab path unexpectedly exists."); }
                var asset = PrefabUtility.SaveAsPrefabAsset(scratch, assetPath);
                if (asset == null) throw new InvalidOperationException("Could not save the placement Prefab.");
                createdAsset = true;
                RecordFreshAsset(assetPath);
                UnityEngine.Object.DestroyImmediate(scratch); scratch = null;
                Sps3OwnedGlobalOptions.ReplacementPlan optionsPlan = null;
                if (basicSocket && !Sps3OwnedGlobalOptions.TryPrepareReplacement(avatar, settings, out optionsPlan, out var optionsIssue))
                    activeWarnings.Add(SetupFeedback.OptionalIssue(optionsIssue, "SPS Options を安全に書き込めなかったため、既存の設定を保持しました。"));
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Create SPS placement-only copy");
                instance = PrefabUtility.InstantiatePrefab(asset, avatar.gameObject.scene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate the placement Prefab.");
                Undo.RegisterCreatedObjectUndo(instance, "Create SPS placement-only copy");
                Undo.SetTransformParent(instance.transform, avatar.transform, "Create SPS placement-only copy");
                instance.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
                foreach (var pair in placements) {
                    var anchor = instance.transform.Find(pair.Key.id);
                    if (anchor == null) throw new InvalidOperationException("Prefab placement anchor missing.");
                    Constrain(anchor.gameObject, pair.Value.first, pair.Value.second);
                    if (basicSocket) {
                        var pose = anchor.Find("Placement - no SPS");
                        Component socket;
                        using (SpsBackend.Use(SpsFamily.VrcFury)) socket = pose == null ? null : VrcFuryCompatibility.FindSocket(pose.gameObject);
                        if (socket == null) activeWarnings.Add("Socket missing; Active input retained: " + pair.Key.id);
                        else {
                            Sps3BasicActiveWriter.TryWrite(socket, avatar, pair.Key, activeWarnings);
                            FuryBasicDepthWriter.TryWrite(socket, avatar, pair.Key, activeWarnings);
                        }
                    }
                }
                if (basicSocket && optionsPlan != null) {
                    var context = new Sps2SetupContext { avatar = avatar, gameObject = instance, asset = draftAsset, settings = settings };
                    Sps3OwnedGlobalOptions.CommitReplacement(avatar, context, optionsPlan, activeFreshAssetScope?.PreviousAsset, out _);
                }
                Undo.CollapseUndoOperations(undoGroup);
                activeWarnings.Add(L("この構成で保存先を確認できない高度な設定は反映していません。入力は下書きに残ります。"));
                message = string.Join("\n", activeWarnings.Distinct());
                return true;
            }
            catch (Exception e)
            {
                if (scratch != null) UnityEngine.Object.DestroyImmediate(scratch);
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                if (createdAsset && !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                if (draftAsset != null) { var draftPath = AssetDatabase.GetAssetPath(draftAsset); if (!string.IsNullOrEmpty(draftPath)) AssetDatabase.DeleteAsset(draftPath); }
                instance = null; message = SetupFeedback.Error(e, "基本 Socket を保存できませんでした。導入状態と配置先を確認してください。");
                return false;
            }
        }
    }
}
