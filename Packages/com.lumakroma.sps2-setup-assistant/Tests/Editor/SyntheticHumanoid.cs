using System.Linq;
using UnityEngine;
namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    internal static class SyntheticHumanoid
    {
        internal static GameObject Create()
        {
            const string fixtureName = "SPS test humanoid";
var avatar = new UnityEngine.GameObject(fixtureName);
var bones = new System.Collections.Generic.Dictionary<UnityEngine.HumanBodyBones, UnityEngine.Transform>();
var nodes = new System.Collections.Generic.Dictionary<string, UnityEngine.Transform> { { "Root", avatar.transform } };
System.Action<string, string, UnityEngine.Vector3, UnityEngine.HumanBodyBones> add = (name, parent, position, humanBone) => {
    var node = new UnityEngine.GameObject(name).transform;
    node.SetParent(nodes[parent], false); node.localPosition = position;
    nodes.Add(name, node);
    if (humanBone != UnityEngine.HumanBodyBones.LastBone) bones.Add(humanBone, node);
};
add("Hips", "Root", new UnityEngine.Vector3(0, 1, 0), UnityEngine.HumanBodyBones.Hips);
add("Spine", "Hips", new UnityEngine.Vector3(0, .17f, 0), UnityEngine.HumanBodyBones.Spine);
add("Chest", "Spine", new UnityEngine.Vector3(0, .18f, 0), UnityEngine.HumanBodyBones.Chest);
add("Neck", "Chest", new UnityEngine.Vector3(0, .2f, 0), UnityEngine.HumanBodyBones.Neck);
add("Head", "Neck", new UnityEngine.Vector3(0, .13f, 0), UnityEngine.HumanBodyBones.Head);
add("Jaw", "Head", new UnityEngine.Vector3(0, -.05f, .06f), UnityEngine.HumanBodyBones.Jaw);
add("LeftUpperLeg", "Hips", new UnityEngine.Vector3(-.1f, -.05f, 0), UnityEngine.HumanBodyBones.LeftUpperLeg);
add("LeftLowerLeg", "LeftUpperLeg", new UnityEngine.Vector3(0, -.42f, 0), UnityEngine.HumanBodyBones.LeftLowerLeg);
add("LeftFoot", "LeftLowerLeg", new UnityEngine.Vector3(0, -.43f, 0), UnityEngine.HumanBodyBones.LeftFoot);
add("LeftToes", "LeftFoot", new UnityEngine.Vector3(0, 0, .14f), UnityEngine.HumanBodyBones.LeftToes);
add("RightUpperLeg", "Hips", new UnityEngine.Vector3(.1f, -.05f, 0), UnityEngine.HumanBodyBones.RightUpperLeg);
add("RightLowerLeg", "RightUpperLeg", new UnityEngine.Vector3(0, -.42f, 0), UnityEngine.HumanBodyBones.RightLowerLeg);
add("RightFoot", "RightLowerLeg", new UnityEngine.Vector3(0, -.43f, 0), UnityEngine.HumanBodyBones.RightFoot);
add("RightToes", "RightFoot", new UnityEngine.Vector3(0, 0, .14f), UnityEngine.HumanBodyBones.RightToes);
add("LeftShoulder", "Chest", new UnityEngine.Vector3(-.1f, .12f, 0), UnityEngine.HumanBodyBones.LeftShoulder);
add("LeftUpperArm", "LeftShoulder", new UnityEngine.Vector3(-.1f, 0, 0), UnityEngine.HumanBodyBones.LeftUpperArm);
add("LeftLowerArm", "LeftUpperArm", new UnityEngine.Vector3(-.28f, 0, 0), UnityEngine.HumanBodyBones.LeftLowerArm);
add("LeftHand", "LeftLowerArm", new UnityEngine.Vector3(-.26f, 0, 0), UnityEngine.HumanBodyBones.LeftHand);
add("RightShoulder", "Chest", new UnityEngine.Vector3(.1f, .12f, 0), UnityEngine.HumanBodyBones.RightShoulder);
add("RightUpperArm", "RightShoulder", new UnityEngine.Vector3(.1f, 0, 0), UnityEngine.HumanBodyBones.RightUpperArm);
add("RightLowerArm", "RightUpperArm", new UnityEngine.Vector3(.28f, 0, 0), UnityEngine.HumanBodyBones.RightLowerArm);
add("RightHand", "RightLowerArm", new UnityEngine.Vector3(.26f, 0, 0), UnityEngine.HumanBodyBones.RightHand);
add("Breast_L", "Chest", new UnityEngine.Vector3(-.1f, .04f, .08f), UnityEngine.HumanBodyBones.LastBone);
add("Breast_R", "Chest", new UnityEngine.Vector3(.1f, .04f, .08f), UnityEngine.HumanBodyBones.LastBone);
var human = new UnityEngine.HumanDescription {
    human = bones.Select(pair => new UnityEngine.HumanBone { boneName = pair.Value.name, humanName = UnityEngine.HumanTrait.BoneName[(int)pair.Key], limit = new UnityEngine.HumanLimit { useDefaultValues = true } }).ToArray(),
    skeleton = avatar.GetComponentsInChildren<UnityEngine.Transform>().Select(t => new UnityEngine.SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
    upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0
};
var humanAvatar = UnityEngine.AvatarBuilder.BuildHumanAvatar(avatar, human);
humanAvatar.name = fixtureName + " Avatar";
if (!humanAvatar.isValid || !humanAvatar.isHuman) { UnityEngine.Object.DestroyImmediate(avatar); UnityEngine.Object.DestroyImmediate(humanAvatar); throw new System.InvalidOperationException("Fixture Humanoid creation failed."); }

avatar.AddComponent<UnityEngine.Animator>().avatar = humanAvatar;
            return avatar;
        }
        internal static void Destroy(GameObject root)
        {
            if (root == null) return;
            var avatar = root.GetComponent<Animator>().avatar;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(avatar);
        }
    }
}
