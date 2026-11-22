using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        // This limited operation never rewrites the native Socket or settings asset.
        // Only a previously owned, single-Socket scene instance is eligible.
        internal sealed class ExistingPlacementPlan
        {
            internal VRCAvatarDescriptor Avatar;
            internal SetupSettings Draft;
            internal Sps2SetupContext Root;
            internal Transform Pose;
            internal ExistingSps3Snapshot Snapshot;
            internal string SettingsState, AssetState;
            internal Vector3 Position;
            internal Quaternion Rotation;
        }

        private static string ExistingSettingsState(Sps2SetupContext root, SetupSettings settings)
        {
            return Sps2SetupStorage.Serialize(new Sps2SetupContext {
                avatar = root.avatar, gameObject = root.gameObject, asset = root.asset,
                identity = root.identity, settings = settings, oralBoundaryPath = root.oralBoundaryPath,
                nativePathProof = root.nativePathProof, sockets = root.sockets
            });
        }

        internal static ExistingPlacementPlan PrepareExistingPlacement(VRCAvatarDescriptor avatar, SetupSettings draft)
        {
            if (avatar == null || draft == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorUtility.IsPersistent(avatar) || !avatar.gameObject.scene.IsValid() || !avatar.gameObject.scene.isLoaded)
                throw new InvalidOperationException("読み込み済みシーンのアバターを Edit モードで選択してください。既存データは変更していません。");
            var root = Find(avatar);
            if (root == null || root.avatar != avatar || root.asset == null || root.settings == null ||
                root.AuthoringContract != SpsAuthoringContract.Sps3Beta14 || root.sockets.Count != 1)
                throw new InvalidOperationException("所有済みの単一 SPS3 Socket を確認できません。既存データは変更していません。");
            var generated = root.sockets[0];
            if (generated == null || generated.anchor == null || generated.pose == null || generated.socket == null ||
                generated.anchor.parent != root.transform || generated.pose.parent != generated.anchor ||
                generated.socket.transform != generated.pose || generated.pathStops == null || generated.pathStops.Length != 0 ||
                SpsBackend.AllSockets(root.gameObject).Length != 1 ||
                !VrcFuryCompatibility.IsNativeSocketType(generated.socket))
                throw new InvalidOperationException("Socket の型・所有・経路構造を安全に確認できません。既存データは変更していません。");
            if (!FuryPersistentScriptProbe.HasPersistentBinding(generated.socket.GetType(), "com.vrcfury.vrcfury", out var issue))
                throw new InvalidOperationException("Socket の保存可能性を確認できません: " + issue);
            var before = ExistingSettingsState(root, root.settings);
            if (before != ExistingSettingsState(root, draft))
                throw new InvalidOperationException("配置以外の設定変更は未対応です。既存設定を保持し、変更を行いません。");
            var included = draft.parts.Where(x => x != null && x.included).ToArray();
            if (included.Length != 1 || included[0].id != generated.id || included[0].IsFingerRing ||
                generated.pose.GetComponents<Behaviour>().Any(c => c != null && c != generated.socket))
                throw new InvalidOperationException("この配置更新は単一の通常 Socket のみ対応します。既存データは変更していません。");
            if (!HumanoidSnapshot.TryCapture(avatar, out var humanoid, out var error)) throw new InvalidOperationException(error);
            Placement placement;
            using (var surface = new AvatarSurface(avatar)) placement = Place(included[0], avatar, BodyBasisBuilder.Build(humanoid), surface);
            var constraint = generated.anchor.GetComponent<ParentConstraint>();
            if (placement == null || placement.first == null || placement.second != null || constraint == null ||
                !constraint.enabled || !constraint.constraintActive || constraint.sourceCount != 1 ||
                constraint.GetSource(0).sourceTransform != placement.first ||
                generated.anchor.GetComponents<Component>().Any(VrcConstraintBridge.IsVrcParent))
                throw new InvalidOperationException("追従先を保持したまま配置できません。既存の制約は変更していません。");
            return new ExistingPlacementPlan { Avatar = avatar, Draft = draft, Root = root, Pose = generated.pose,
                Snapshot = ExistingSps3Snapshot.Capture(root.gameObject), SettingsState = before,
                AssetState = root.asset.stateJson, Position = placement.position, Rotation = placement.rotation };
        }

        internal static void CommitExistingPlacement(ExistingPlacementPlan plan)
        {
            if (plan == null || plan.Pose == null || !plan.Snapshot.StillMatches() ||
                plan.Root.asset == null || plan.Root.asset.stateJson != plan.AssetState ||
                ExistingSettingsState(plan.Root, plan.Draft) != plan.SettingsState)
                throw new InvalidOperationException("確認後に対象や設定が変わりました。変更を行いません。");
            var fresh = PrepareExistingPlacement(plan.Avatar, plan.Draft);
            if (fresh.Pose != plan.Pose || fresh.Position != plan.Position || fresh.Rotation != plan.Rotation)
                throw new InvalidOperationException("配置条件が変わりました。変更を行いません。");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update existing SPS Socket placement");
            try {
                Undo.RecordObject(plan.Pose, "Update existing SPS Socket placement");
                plan.Pose.SetPositionAndRotation(plan.Position, plan.Rotation);
                if (!plan.Snapshot.StillMatchesExcept(plan.Pose) || plan.Root.asset.stateJson != plan.AssetState ||
                    plan.Pose.position != plan.Position || plan.Pose.rotation != plan.Rotation)
                    throw new InvalidOperationException("配置以外の状態を確認できません。操作を戻します。");
                Undo.CollapseUndoOperations(group);
            } catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}
