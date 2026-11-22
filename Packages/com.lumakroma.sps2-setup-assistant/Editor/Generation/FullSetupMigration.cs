using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        private sealed class MigrationPart
        {
            public GeneratedSocket old;
            public SocketSettings settings;
            public Placement placement;
            public Component[] attachments;
        }
        internal static SetupSettings MigrationSettings(SetupSettings source, SetupSettings requested)
        {
            var comparable = requested.Copy(); comparable.family = source.family; comparable.modularAvatar = source.modularAvatar;
            // The destination Socket and global options both use selected.menuLanguage
            // in target preflight and commit; source validation still uses source.
            // No part settings, ownership, placement or references are relaxed here.
            if (!Enum.IsDefined(typeof(DisplayLanguage), requested.menuLanguage))
                throw new InvalidOperationException("Unknown menu language; existing output is unchanged.");
            comparable.menuLanguage = source.menuLanguage;
            if (JsonUtility.ToJson(comparable) != JsonUtility.ToJson(source))
                throw new InvalidOperationException(L("基盤切り替えと他の設定変更は別々に反映してください。"));
            return requested.Copy();
        }
        private static bool MigrateFamily(VRCAvatarDescriptor avatar, Sps2SetupContext old, SetupSettings requested,
            out Sps2SetupContext result, out string message)
        {
            result = null; message = null; int group = -1; Sps2SetupAsset createdAsset = null;
            var targetContract = SpsAuthoringCompatibility.ActiveContract;
            var stagedRoots = new List<GameObject>();
            try
            {
                var selected = MigrationSettings(old.settings, requested);
                SpsBackend.ValidateExisting(old, old.settings.family);
                ValidateOwnership(old, false);
                // Asset/connected Prefab conversion cannot preserve external native-component fileIDs.
                if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(old.gameObject) != null ||
                    old.transform.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != null))
                    throw new InvalidOperationException(L("Prefab接続中またはPrefab編集モードでは基盤を切り替えません。Scene上の接続されていない生成物を使用してください。"));
                var expectedNative = new HashSet<Component>(old.sockets.Select(s => s.socket));
                foreach (var component in old.transform.GetComponentsInChildren<Component>(true))
                    if (component != null && Enum.GetValues(typeof(SpsFamily)).Cast<SpsFamily>().Any(f =>
                        component.GetType().FullName == SpsBackend.SocketType(f)) && !expectedNative.Contains(component))
                        throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。"));
                if (!VrcFuryCapabilities.For(old.settings.family).CanGenerate)
                    throw new InvalidOperationException(SpsBackend.MissingSourceGuidance);
                var targetCaps = VrcFuryCapabilities.ForAuthoring(selected.family, targetContract);
                if (!targetCaps.CanGenerate) throw new InvalidOperationException(targetCaps.BlockReason);
                var effective = targetCaps.Effective(selected);
                bool normalizedObsoleteLocalOnly = targetContract == SpsAuthoringContract.Sps3Beta14 &&
                    selected.family == SpsFamily.VrcFury && selected.localOnly && !effective.localOnly;
                if (effective.penetration != selected.penetration || effective.showInternalThickness != selected.showInternalThickness ||
                    effective.autoMode != selected.autoMode || effective.legacy != selected.legacy ||
                    (effective.localOnly != selected.localOnly && !normalizedObsoleteLocalOnly) ||
                    effective.parts.Where((p, i) => p.depth != selected.parts[i].depth).Any())
                    throw new InvalidOperationException(L("移行先では現在の設定を維持できません。既存の生成物は変更しません。"));
                selected = effective;
                if (!HumanoidSnapshot.TryCapture(avatar, out var snapshot, out var error)) throw new InvalidOperationException(error);
                var basis = BodyBasisBuilder.Build(snapshot);
                using var surface = new AvatarSurface(avatar);
                var parts = new List<MigrationPart>(); var replaced = new HashSet<Component>();
                // The preflight staging group is always reverted before touching the user's components.
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
                foreach (var generated in old.sockets)
                {
                    var part = old.settings.parts.Single(p => p.id == generated.id);
                    var targetSocketType = VrcFuryApi.FindType(SpsBackend.SocketType(selected.family));
                    if (targetSocketType == null || generated.pose.GetComponents(targetSocketType).Length != 0)
                        throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。"));
                    Placement placement;
                    Component[] attachments;
                    using (SpsBackend.Use(old.settings.family))
                    using (SpsAuthoringCompatibility.Use(old.AuthoringContract))
                    {
                        placement = Place(part, avatar, basis, surface);
                        if (placement == null) throw new InvalidOperationException(L("移行に必要な追従先が見つかりません。") + part.name);
                        var stage = Create("SPS2 migration preflight", avatar.transform); stagedRoots.Add(stage);
                        var context = new Sps2SetupContext { gameObject = stage, avatar = avatar, identity = old.identity };
                        var expected = CreatePart(context, part, placement, old.settings, new List<string>(), generated.pose.name);
                        VrcFuryCompatibility.SetAuthoringIdentity(expected.socket, Sps2SetupStorage.Token(old, part.id));
                        SpsMigrationAdapter.CopyKnownPath(generated.socket, expected.socket, generated.pathStops);
                        SpsMigrationAdapter.ValidateEquivalent(generated.socket, expected.socket);
                        var expectedAttachments = expected.anchor.GetComponents<Component>().Where(c => !(c is Transform)).ToArray();
                        attachments = expectedAttachments.Select(c => {
                            var matches = generated.anchor.GetComponents(c.GetType());
                            if (matches.Length != 1) throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。") + part.name);
                            SpsMigrationAdapter.ValidateEquivalent(matches[0], c); return matches[0];
                        }).ToArray();
                    }
                    // Prove target creation with the existing real adapter before removing the source.
                    using (SpsBackend.Use(selected.family))
                    {
                        var stage = Create("SPS2 migration target preflight", avatar.transform); stagedRoots.Add(stage);
                        var context = new Sps2SetupContext { gameObject = stage, avatar = avatar, identity = old.identity };
                        var expected = CreatePart(context, part, placement, selected, new List<string>(), generated.pose.name);
                        SpsMigrationAdapter.CopyKnownPath(generated.socket, expected.socket, generated.pathStops);
                        foreach (var targetAttachment in expected.anchor.GetComponents<Component>().Where(c => !(c is Transform)))
                            if (generated.anchor.GetComponents(targetAttachment.GetType()).Any(c => !attachments.Contains(c)))
                                throw new InvalidOperationException(L("コンポーネントまたは経路の所有を確定できません。既存の生成物は変更しません。"));
                    }
                    bool boneAttachment = !part.IsFingerRing && placement.second == null && !placement.followTransform && !part.custom;
                    var replacedAttachments = boneAttachment ? attachments : Array.Empty<Component>();
                    parts.Add(new MigrationPart { old = generated, settings = part, placement = placement, attachments = replacedAttachments });
                    replaced.Add(generated.socket); foreach (var component in replacedAttachments) replaced.Add(component);
                }
                // Revert preflight, including Undo entries produced by the existing factories.
                Undo.RevertAllDownToGroup(group); group = -1; stagedRoots.Clear();
                SpsMigrationAdapter.ValidateReplacementReferences(avatar, replaced);
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
                var transforms = old.transform.GetComponentsInChildren<Transform>(true)
                    .Select(t => (transform: t, position: t.localPosition, rotation: t.localRotation, scale: t.localScale)).ToArray();
                var converted = new Sps2SetupContext { gameObject = old.gameObject, avatar = avatar, settings = selected,
                    oralBoundaryPath = old.oralBoundaryPath };
                using (SpsBackend.Use(selected.family))
                {
                    foreach (var plan in parts)
                    {
                        var pose = plan.old.pose; var anchor = plan.old.anchor;
                        // Keep all GameObjects/Transforms and unrelated components in place.
                        var socket = VrcFuryCompatibility.CreateSocket(pose.gameObject, plan.settings, selected, new List<string>());
                        SpsMigrationAdapter.CopyKnownPath(plan.old.socket, socket, plan.old.pathStops);
                        Undo.DestroyObjectImmediate(plan.old.socket);
                        if (plan.attachments.Length != 0)
                        {
                            foreach (var attachment in plan.attachments) Undo.DestroyObjectImmediate(attachment);
                            var backend = selected.family == SpsFamily.SpsForNdmf ? AttachmentBackend.SpsNdmfWithModularAvatar
                                : selected.modularAvatar ? AttachmentBackend.VrcFuryWithModularAvatar : AttachmentBackend.VrcFury;
                            if (!AttachmentBackendRegistry.TryApply(backend, anchor.gameObject, plan.placement.bone, out error)) throw new InvalidOperationException(error);
                        }
                        converted.sockets.Add(new GeneratedSocket { id = plan.old.id, socket = socket, anchor = anchor, pose = pose, pathStops = plan.old.pathStops });
                    }
                    foreach (var saved in transforms)
                    {
                        if (saved.transform == null) throw new InvalidOperationException(L("移行中に既存の階層が変更されました。変更を復元します。"));
                        if (saved.transform.localPosition == saved.position && saved.transform.localRotation == saved.rotation && saved.transform.localScale == saved.scale) continue;
                        Undo.RecordObject(saved.transform, UndoName);
                        saved.transform.localPosition = saved.position; saved.transform.localRotation = saved.rotation; saved.transform.localScale = saved.scale;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(saved.transform);
                    }
                    createdAsset = Sps2SetupStorage.Create(avatar.name, targetContract); converted.asset = createdAsset;
                    converted.identity = Sps2SetupStorage.Identity(createdAsset);
                    UpdateAuthoringIdentities(converted); SpsBackend.ValidateExisting(converted, selected.family);
                    if (targetContract == SpsAuthoringContract.Sps3Beta14) Sps3OwnedGlobalOptions.Apply(converted, selected.legacy, requested: selected);
                    CommitMetadata(converted, null);
                    foreach (var component in converted.sockets.Select(s => s.socket))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
                Undo.CollapseUndoOperations(group); group = -1;
                result = converted; message = L("基盤を切り替えました。保存後、ビルドと動作を確認してください。");
                return true;
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                foreach (var stage in stagedRoots) if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (createdAsset != null && targetContract == SpsAuthoringContract.LegacySps2) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(createdAsset));
                message = e.Message; return false;
            }
        }
    }
}
