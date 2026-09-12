using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.api;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static class FullSetupGenerator
    {
        private const string UndoName = "SPS2 セットアップ";
        private const string PathName = "Guided Paths";
        public const string PackagePath = "Packages/com.lumakroma.sps2-setup-assistant";

        public const string RootName = "SPS2";
        public static bool HasGeneratedRoot(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return false;
            foreach (Transform child in avatar.transform)
                if (child.name == RootName || child.name == SocketSetupGenerator.OwnedRootName || child.GetComponent<Sps2SetupRoot>() != null) return true;
            return false;
        }

        public static Sps2SetupContext Find(VRCAvatarDescriptor avatar)
        {
            var root = Sps2SetupStorage.Find(avatar);
            if (root != null && (root.schema != 1 || string.IsNullOrEmpty(root.identity)))
                throw new InvalidOperationException("SPS2 生成データの形式が一致しません。");
            if (root != null) ValidateOwnership(root, true);
            return root;
        }
        private static void ValidateOwnership(Sps2SetupContext root, bool allowMissing)
        {
            if (root.settings == null || root.sockets == null) throw new InvalidOperationException("SPS2 生成データが欠けています。");
            if (root.testPlug != null && root.testPlug.transform.parent != root.transform)
                throw new InvalidOperationException("テストプラグが所有ルートの外へ移動されています。");
            var anchors = new HashSet<Transform>();
            var ids = new HashSet<string>();
            foreach (var socket in root.sockets)
            {
                if (socket == null || string.IsNullOrEmpty(socket.id) || !ids.Add(socket.id) ||
                    (!allowMissing && (socket.anchor == null || socket.pose == null || socket.socket == null)) ||
                    (socket.anchor != null && (socket.anchor.parent != root.transform || !anchors.Add(socket.anchor))) ||
                    (socket.pose != null && (socket.anchor == null || socket.pose.parent != socket.anchor)) ||
                    (socket.socket != null && (socket.pose == null || socket.socket.transform != socket.pose)))
                    throw new InvalidOperationException("Socket の所有参照が不正です。変更は行いません。");
                if (socket.pathStops == null || socket.pathStops.Any(t => t == null ? !allowMissing : !t.IsChildOf(root.transform)))
                    throw new InvalidOperationException("貫通経路が所有ルートの外を参照しています。");
            }
        }

        private sealed class Placement
        {
            public Transform first, second;
            public bool followTransform;
            public HumanBodyBones bone = HumanBodyBones.LastBone;
            public Vector3 position;
            public Quaternion rotation;
        }

        private static Transform Breast(VRCAvatarDescriptor avatar, bool left)
        {
            string[] names = left ? new[] { "breastl", "bustl", "munel", "leftbreast", "leftbust", "breastleft" }
                : new[] { "breastr", "bustr", "muner", "rightbreast", "rightbust", "breastright" };
            var bones = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(t => t != null).Distinct();
            var matches = bones.Where(t => names.Contains(t.name.Replace("_", "").Replace(".", "").Replace(" ", "").ToLowerInvariant())).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static Placement Place(SocketSettings part, VRCAvatarDescriptor avatar, BodyBasis basis, AvatarSurface surface)
        {
            var animator = avatar.GetComponent<Animator>();
            Transform Bone(params HumanBodyBones[] candidates) => candidates.Select(animator.GetBoneTransform).FirstOrDefault(t => t != null);
            var p = new Placement();
            var h = basis.Height;
            var forward = avatar.transform.forward;
            var up = avatar.transform.up;
            var right = avatar.transform.right;
            Vector3 offset = Vector3.zero, direction = forward;
            var chest = Bone(HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine);
            if (part.custom)
            {
                if (part.target == null || !part.target.IsChildOf(avatar.transform)) return null;
                p.first = part.target; p.position = part.target.position; p.rotation = part.target.rotation;
                return p;
            }
            switch (part.id)
            {
                case "mouth":
                    p.first = Bone(HumanBodyBones.Jaw, HumanBodyBones.Head);
                    p.bone = Bone(HumanBodyBones.Jaw) != null ? HumanBodyBones.Jaw : HumanBodyBones.Head;
                    offset = forward * h * (p.bone == HumanBodyBones.Jaw ? .025f : .045f);
                    if (p.bone == HumanBodyBones.Head) offset -= up * h * .035f;
                    break;
                case "earLeft": case "earRight":
                    p.first = Bone(HumanBodyBones.Head); p.bone = HumanBodyBones.Head;
                    offset = right * h * .052f * (part.id == "earLeft" ? -1 : 1);
                    direction = part.id == "earLeft" ? -right : right;
                    break;
                case "nippleLeft": case "nippleRight":
                    p.first = Breast(avatar, part.id == "nippleLeft");
                    p.followTransform = p.first != null;
                    if (p.first == null) p.first = chest;
                    p.bone = Bone(HumanBodyBones.UpperChest) != null ? HumanBodyBones.UpperChest : Bone(HumanBodyBones.Chest) != null ? HumanBodyBones.Chest : HumanBodyBones.Spine;
                    offset = forward * h * .055f;
                    if (!p.followTransform) offset += right * h * .045f * (part.id == "nippleLeft" ? -1 : 1);
                    break;
                case "chest":
                    p.first = Breast(avatar, true); p.second = Breast(avatar, false);
                    offset = forward * h * .055f;
                    break;
                case "vagina": case "anus":
                    p.first = Bone(HumanBodyBones.Hips); p.bone = HumanBodyBones.Hips;
                    offset = forward * h * .06f * (part.id == "vagina" ? 1 : -1);
                    direction = part.id == "vagina" ? -forward : forward;
                    break;
                case "handLeft": case "handRight":
                    bool left = part.id == "handLeft";
                    p.bone = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    p.first = Bone(p.bone);
                    var arm = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    var armDirection = p.first != null && arm != null ? (p.first.position - arm.position).normalized : right * (left ? -1 : 1);
                    offset = armDirection * h * .022f; direction = -armDirection;
                    break;
                case "hands":
                    p.first = Bone(HumanBodyBones.LeftHand); p.second = Bone(HumanBodyBones.RightHand);
                    break;
                case "thighs":
                    p.first = Bone(HumanBodyBones.LeftUpperLeg); p.second = Bone(HumanBodyBones.RightUpperLeg);
                    offset = -up * h * .02f;
                    break;
                case "footLeft": case "footRight":
                    bool leftFoot = part.id == "footLeft";
                    var toe = leftFoot ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
                    p.bone = Bone(toe) != null ? toe : leftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                    p.first = Bone(p.bone); offset = forward * h * .05f + up * h * .012f; direction = -offset.normalized;
                    break;
                case "feet":
                    p.first = Bone(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot);
                    p.second = Bone(HumanBodyBones.RightToes, HumanBodyBones.RightFoot);
                    offset = forward * h * .025f;
                    break;
                default: throw new InvalidOperationException("Unknown socket identifier: " + part.id);
            }
            bool middle = part.id == "chest" || part.id == "hands" || part.id == "thighs" || part.id == "feet";
            if (p.first == null || (middle && p.second == null)) return null;
            p.position = (p.second == null ? p.first.position : (p.first.position + p.second.position) * .5f) + offset;
            if (Vector3.Cross(direction, up).sqrMagnitude < .000001f) up = right;
            Vector3 point;
            if (part.id == "mouth" && surface.Mouth(out point)) p.position = point;
            else if (part.id == "earLeft" || part.id == "earRight")
            {
                float side = part.id == "earLeft" ? -1 : 1;
                if (surface.Extreme(p.first, right, side, out point, side, avatar.VisemeSkinnedMesh)) p.position = point;
            }
            else if (part.id == "nippleLeft" || part.id == "nippleRight")
            {
                if (p.followTransform && surface.Extreme(p.first, forward, 1, out point)) p.position = point;
                else if (surface.Ray(p.position, forward, h * .15f, out point)) p.position = point;
            }
            else if (part.id == "chest")
            {
                if (surface.Extreme(p.first, forward, 1, out var leftTip) && surface.Extreme(p.second, forward, 1, out var rightTip))
                {
                    var center = (leftTip + rightTip) * .5f;
                    p.position = surface.Ray(center, forward, h * .15f, out point) ? point : center;
                }
                direction = -up; up = forward;
            }
            else if (part.id == "vagina" || part.id == "anus")
            {
                // Locate the crotch surface below the pelvis, not the hips' front/back at bone height.
                var center = p.first.position + forward * h * (part.id == "vagina" ? .012f : -.014f);
                direction = -up;
                if (surface.Ray(center, -up, h * .2f, out point, out var normal))
                { p.position = point; direction = Vector3.ProjectOnPlane(normal, right).normalized; }
                up = forward;
            }
            else if (part.id == "handLeft" || part.id == "handRight")
            {
                var finger = Bone(part.id == "handLeft" ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                if (finger != null)
                {
                    var center = Vector3.Lerp(p.first.position, finger.position, .75f);
                    var palmNormal = -avatar.transform.up;
                    if (surface.Ray(center, palmNormal, h * .08f, out point, out var normal))
                    { p.position = point; palmNormal = normal; }
                    else p.position = center;
                    // The plug travels along the palm; native radius offset lifts it off the skin.
                    direction = Vector3.ProjectOnPlane(-forward, palmNormal).normalized;
                    up = palmNormal;
                }
            }
            else if (part.id == "footLeft" || part.id == "footRight")
            {
                bool left = part.id == "footLeft";
                var ankle = Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                var toe = Bone(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                var center = toe != null ? Vector3.Lerp(ankle.position, toe.position, .6f) : ankle.position;
                if (surface.Extreme(ankle, forward, 1, out var front) && surface.Extreme(ankle, forward, -1, out var back) &&
                    surface.Extreme(ankle, right, 1, out var outside) && surface.Extreme(ankle, right, -1, out var inside))
                {
                    center = Vector3.Lerp(back, front, .6f);
                    center += right * (Vector3.Dot((outside + inside) * .5f - center, right));
                }
                var soleNormal = -up;
                if (surface.Ray(center, -up, h * .12f, out point, out var normal)) { p.position = point; soleNormal = normal; }
                else p.position = center;
                direction = Vector3.ProjectOnPlane(toe != null ? toe.position - ankle.position : forward, soleNormal).normalized;
                up = soleNormal;
            }
            else if (part.id == "hands" || part.id == "feet")
            {
                string leftId = part.id == "hands" ? "handLeft" : "footLeft";
                string rightId = part.id == "hands" ? "handRight" : "footRight";
                var leftPose = Place(new SocketSettings { id = leftId }, avatar, basis, surface);
                var rightPose = Place(new SocketSettings { id = rightId }, avatar, basis, surface);
                if (leftPose != null && rightPose != null)
                {
                    p.position = (leftPose.position + rightPose.position) * .5f;
                    direction = (leftPose.rotation * Vector3.forward + rightPose.rotation * Vector3.forward).normalized;
                    up = (leftPose.rotation * Vector3.up + rightPose.rotation * Vector3.up).normalized;
                }
            }
            else if (part.id == "thighs")
            {
                var leftKnee = Bone(HumanBodyBones.LeftLowerLeg); var rightKnee = Bone(HumanBodyBones.RightLowerLeg);
                if (leftKnee != null && rightKnee != null)
                    p.position = (Vector3.Lerp(p.first.position, leftKnee.position, .18f) + Vector3.Lerp(p.second.position, rightKnee.position, .18f)) * .5f;
            }
            if (direction.sqrMagnitude < .000001f) direction = forward;
            if (Vector3.Cross(direction, up).sqrMagnitude < .000001f) up = right;
            p.rotation = Quaternion.LookRotation(direction, up);
            return p;
        }

        private static GameObject Create(string name, Transform parent)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, UndoName);
            Undo.SetTransformParent(obj.transform, parent, UndoName);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            return obj;
        }

        private static void Constrain(GameObject anchor, Transform first, Transform second = null)
        {
            var constraint = anchor.GetComponent<ParentConstraint>();
            if (constraint == null) constraint = Undo.AddComponent<ParentConstraint>(anchor);
            Undo.RecordObject(constraint, UndoName);
            constraint.constraintActive = false;
            constraint.locked = false;
            constraint.SetSources(new List<ConstraintSource> { new ConstraintSource { sourceTransform = first, weight = second == null ? 1 : .5f } });
            if (second != null) constraint.AddSource(new ConstraintSource { sourceTransform = second, weight = .5f });
            for (int i = 0; i < constraint.sourceCount; i++)
            {
                constraint.SetTranslationOffset(i, Vector3.zero);
                constraint.SetRotationOffset(i, Vector3.zero);
            }
            constraint.weight = 1;
            constraint.locked = true;
            constraint.constraintActive = true;
        }

        private static GeneratedSocket CreatePart(Sps2SetupContext root, SocketSettings part, Placement placement, SetupSettings setup, List<string> warnings)
        {
            var anchor = Create(part.id, root.transform);
            anchor.transform.SetPositionAndRotation(
                placement.second == null ? placement.first.position : (placement.first.position + placement.second.position) * .5f,
                placement.second == null ? placement.first.rotation : Quaternion.Slerp(placement.first.rotation, placement.second.rotation, .5f));
            if (placement.second != null || placement.followTransform || part.custom)
                Constrain(anchor, placement.first, placement.second);
            else
            {
                var backend = setup.modularAvatar ? AttachmentBackend.VrcFuryWithModularAvatar : AttachmentBackend.VrcFury;
                if (!AttachmentBackendRegistry.TryApply(backend, anchor, placement.bone, out var error))
                    throw new InvalidOperationException(error);
            }
            var pose = Create(SocketSetupGenerator.SocketPoseName, anchor.transform);
            pose.transform.SetPositionAndRotation(placement.position, placement.rotation);
            var socket = VrcFuryCompatibility.CreateSocket(pose, part, setup, warnings);
            return new GeneratedSocket { id = part.id, anchor = anchor.transform, pose = pose.transform, socket = socket };
        }

        public static bool Apply(VRCAvatarDescriptor avatar, SetupSettings settings, bool regenerate,
            out Sps2SetupContext root, out string message)
        {
            root = null; message = null;
            int group = -1;
            Sps2SetupAsset createdAsset = null;
            try
            {
                VrcFuryCompatibility.RequireVersion();
                if (avatar == null || EditorUtility.IsPersistent(avatar)) throw new InvalidOperationException("Scene または Prefab Mode のアバターを選択してください。");
                if (!HumanoidSnapshot.TryCapture(avatar, out var snapshot, out var error)) throw new InvalidOperationException(error);
                var old = Find(avatar);
                if (old != null && !regenerate) ValidateOwnership(old, false);
                var basis = BodyBasisBuilder.Build(snapshot);
                var warnings = new List<string>();
                if (settings.parts.GroupBy(p => p.id).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() != 1))
                    throw new InvalidOperationException("部位の識別子が重複または欠落しています。");
                using var surface = new AvatarSurface(avatar);
                if (!surface.HasBody) warnings.Add("体の表面を特定できませんでした。ボーンを基準に配置したため、位置と向きを確認してください。");
                var placements = new Dictionary<string, Placement>();
                foreach (var part in settings.parts.Where(p => p.included))
                {
                    if (part.custom && old != null && part.target != null && part.target.IsChildOf(old.transform))
                        throw new InvalidOperationException("カスタム追従先に自分の生成物は指定できません。");
                    var placement = Place(part, avatar, basis, surface);
                    if (placement == null) warnings.Add(part.name + ": 追従先が見つからないため省きました。");
                    else placements.Add(part.id, placement);
                }
                if (placements.Count == 0) throw new InvalidOperationException("生成できる部位がありません。");
                Transform legacy = null;
                if (old == null && !SocketSetupGenerator.TryFindOwnedRoot(avatar.transform, out legacy, out error)) throw new InvalidOperationException(error);
                if (legacy != null && !regenerate) throw new InvalidOperationException("旧PoCの生成物には「プレハブを再生成」を使用してください。");
                if (old != null && old.settings.modularAvatar != settings.modularAvatar && !regenerate)
                    throw new InvalidOperationException("追従方式の変更には再生成を使用してください。");
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
                var previous = old != null ? old.settings.Copy() : null;
                // Preserve the settings referenced by saved scenes and Prefabs when edits are discarded.
                var asset = createdAsset = Sps2SetupStorage.Create(avatar.name);
                var identity = Sps2SetupStorage.Identity(asset);
                GameObject existingPlug = old != null ? old.testPlug : null;
                if (regenerate && existingPlug != null) Undo.SetTransformParent(existingPlug.transform, avatar.transform, UndoName);
                if (regenerate && old != null) Undo.DestroyObjectImmediate(old.gameObject);
                if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);
                root = regenerate ? null : old;
                if (root == null)
                {
                    var obj = Create(RootName, avatar.transform);
                    root = new Sps2SetupContext { gameObject = obj, avatar = avatar, asset = asset, identity = identity, settings = settings.Copy() };
                    if (existingPlug != null) { Undo.SetTransformParent(existingPlug.transform, root.transform, UndoName); root.testPlug = existingPlug; }
                }
                root.asset = asset; root.identity = identity;
                // RegisterCreatedObjectUndo ends pending RecordObject tracking. The metadata
                // list spans many subsequent creations, so keep a complete-object snapshot.
                if (root.asset == null)
                {
                    root.asset = createdAsset ?? Sps2SetupStorage.Create(avatar.name);
                    createdAsset = root.asset;
                    root.identity = Sps2SetupStorage.Identity(root.asset);
                }
                Undo.RegisterCompleteObjectUndo(root.asset, UndoName);
                string metadataBefore = root.asset.stateJson;
                bool structureChanged = false;
                foreach (var generated in root.sockets.ToArray())
                    if (!placements.ContainsKey(generated.id))
                    {
                        if (generated.anchor != null) Undo.DestroyObjectImmediate(generated.anchor.gameObject);
                        root.sockets.Remove(generated);
                        structureChanged = true;
                    }
                foreach (var part in settings.parts.Where(p => placements.ContainsKey(p.id)))
                {
                    var generated = root.sockets.Find(p => p.id == part.id);
                    var oldPart = previous?.parts.Find(p => p.id == part.id);
                    if (generated == null) { root.sockets.Add(CreatePart(root, part, placements[part.id], settings, warnings)); structureChanged = true; }
                    else
                    {
                        if (generated.anchor == null || generated.pose == null || generated.socket == null)
                            throw new InvalidOperationException(part.name + ": 生成物が欠けています。再生成してください。");
                        if (!SamePart(oldPart, part)) VrcFuryCompatibility.Configure(generated.socket, part, settings, warnings);
                        else if (previous.autoMode != settings.autoMode || previous.legacy != settings.legacy)
                            VrcFuryCompatibility.ConfigureCommon(generated.socket, settings);
                        if (part.custom && oldPart != null && oldPart.target != part.target)
                        {
                            var position = generated.pose.position; var rotation = generated.pose.rotation;
                            Undo.RecordObject(generated.anchor, UndoName);
                            generated.anchor.SetPositionAndRotation(part.target.position, part.target.rotation);
                            Constrain(generated.anchor.gameObject, part.target);
                            Undo.RecordObject(generated.pose, UndoName);
                            generated.pose.SetPositionAndRotation(position, rotation);
                        }
                    }
                }
                if (regenerate || structureChanged || previous == null || previous.penetration != settings.penetration ||
                    previous.parts.Any(p => (p.id == "mouth" || p.id == "anus") && p.included != settings.parts.Find(n => n.id == p.id)?.included))
                    ConfigurePath(root, avatar, settings, warnings);
                root.settings = settings.Copy();
                if (VrcFuryCompatibility.AutoSocketCount(avatar.gameObject) > 16)
                    throw new InvalidOperationException("既存分を含む Auto Mode 対象が16個を超えます。Auto Mode を外すか対象を減らしてください。");
                if (root.legacy != null) { Undo.DestroyObjectImmediate(root.legacy); root.legacy = null; }
                Undo.RecordObject(root.gameObject, UndoName); root.gameObject.name = RootName;
                foreach (var generated in root.sockets) VrcFuryCompatibility.SetAuthoringIdentity(generated.socket, Sps2SetupStorage.Token(root, generated.id));
                CommitMetadata(root, metadataBefore);
                EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
                Selection.activeGameObject = root.gameObject;
                Undo.CollapseUndoOperations(group);
                message = string.Join("\n", warnings.Distinct());
                return true;
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                if (createdAsset != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(createdAsset));
                root = null; message = e.Message; return false;
            }
        }

        private static bool SamePart(SocketSettings a, SocketSettings b)
        {
            if (a == null || a.name != b.name || a.target != b.target || a.depth != b.depth || a.range != b.range || a.units != b.units || a.actions.Count != b.actions.Count) return false;
            for (int i = 0; i < a.actions.Count; i++)
            {
                var x = a.actions[i]; var y = b.actions[i];
                if (x.kind != y.kind || x.renderer != y.renderer || x.shape != y.shape || x.weight != y.weight || x.clip != y.clip || x.target != y.target || x.objectOn != y.objectOn) return false;
            }
            return true;
        }

        private static void CommitMetadata(Sps2SetupContext root, string before)
        {
            string after = Sps2SetupStorage.Serialize(root);
            root.asset.stateJson = before;
            Undo.RecordObject(root.asset, UndoName);
            root.asset.stateJson = after;
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(root.asset);
            AssetDatabase.SaveAssetIfDirty(root.asset);
        }
        private static void ConfigurePath(Sps2SetupContext root, VRCAvatarDescriptor avatar, SetupSettings settings, List<string> warnings)
        {
            var old = root.transform.Find(PathName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            foreach (var socket in root.sockets.Where(s => s.id == "mouth" || s.id == "anus"))
            { VrcFuryCompatibility.SetPath(socket.socket, Array.Empty<Transform>(), avatar.transform); socket.pathStops = Array.Empty<Transform>(); }
            var mouth = root.sockets.Find(s => s.id == "mouth"); var anus = root.sockets.Find(s => s.id == "anus");
            if (!settings.penetration || mouth == null || anus == null) return;
            var animator = avatar.GetComponent<Animator>();
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (chest == null || hips == null) { warnings.Add("貫通: 胴体の参照が不足しています。"); return; }
            var path = Create(PathName, root.transform);
            Transform Stop(string name, Transform target)
            {
                var point = Create(name, path.transform); point.transform.SetPositionAndRotation(target.position, target.rotation);
                Constrain(point, target); return point.transform;
            }
            var upper = Stop("Upper", chest); var lower = Stop("Lower", hips);
            var mouthExit = Stop("Mouth Exit", mouth.pose); var anusExit = Stop("Anus Exit", anus.pose);
            mouth.pathStops = new[] { upper, lower, anusExit };
            anus.pathStops = new[] { lower, upper, mouthExit };
            VrcFuryCompatibility.SetPath(mouth.socket, mouth.pathStops, avatar.transform);
            VrcFuryCompatibility.SetPath(anus.socket, anus.pathStops, avatar.transform);
        }

        public static bool ShowTestPlug(VRCAvatarDescriptor avatar, out string error)
        {
            int group = -1;
            Sps2SetupAsset createdAsset = null;
            try
            {
                VrcFuryCompatibility.RequireVersion();
                var root = Find(avatar);
                if (root == null) throw new InvalidOperationException("先にセットアップを生成してください。");
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("SPS2 テストプラグ");
                if (root.asset == null || root.testPlug == null)
                {
                    root.asset = createdAsset ?? Sps2SetupStorage.Create(avatar.name);
                    createdAsset = root.asset;
                    root.identity = Sps2SetupStorage.Identity(root.asset);
                }
                bool metadataChanged = createdAsset != null || root.legacy != null || root.testPlug == null;
                if (metadataChanged) Undo.RegisterCompleteObjectUndo(root.asset, UndoName);
                string metadataBefore = root.asset.stateJson;
                if (root.testPlug == null)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "/Assets/IcePop/IcePop.fbx");
                    var material = AssetDatabase.LoadAssetAtPath<Material>(PackagePath + "/Assets/IcePop/IcePop.mat");
                    if (model == null || material == null || material.shader == null) throw new InvalidOperationException("IcePop の表示用アセットが見つかりません。");
                    var plug = Create("SPS2 Test Plug", root.transform);
                    var mesh = UnityEngine.Object.Instantiate(model, plug.transform, false);
                    Undo.RegisterCreatedObjectUndo(mesh, UndoName);
                    mesh.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    foreach (var renderer in mesh.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    VrcFuryCompatibility.CreateTestPlug(plug, mesh.GetComponentsInChildren<Renderer>(true));
                    root.testPlug = plug;
                }
                Undo.RecordObject(root.testPlug.transform, UndoName);
                var head = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                root.testPlug.transform.SetPositionAndRotation((head != null ? head.position : avatar.transform.position + avatar.transform.up) + avatar.transform.forward * .35f, Quaternion.LookRotation(-avatar.transform.forward, avatar.transform.up));
                Undo.RecordObject(root.testPlug, UndoName); root.testPlug.SetActive(true);
                if (root.legacy != null) { Undo.DestroyObjectImmediate(root.legacy); root.legacy = null; }
                Undo.RecordObject(root.gameObject, UndoName); root.gameObject.name = RootName;
                if (metadataChanged)
                {
                    foreach (var generated in root.sockets) VrcFuryCompatibility.SetAuthoringIdentity(generated.socket, Sps2SetupStorage.Token(root, generated.id));
                    CommitMetadata(root, metadataBefore);
                }
                EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
                Selection.activeGameObject = root.testPlug;
                Undo.CollapseUndoOperations(group); error = null; return true;
            }
            catch (Exception e) { if (group >= 0) Undo.RevertAllDownToGroup(group); if (createdAsset != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(createdAsset)); error = e.Message; return false; }
        }
    }
}
