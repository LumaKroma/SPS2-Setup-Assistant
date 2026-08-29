using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public static class SocketCatalog
    {
        private static readonly IReadOnlyList<SocketPreset> PresetsValue = new[]
        {
            new SocketPreset(SocketPresetId.HeadMouth, 1, "Head Mouth", HumanBodyBones.Jaw, HumanBodyBones.Head),
            new SocketPreset(SocketPresetId.Chest, 2, "Chest", HumanBodyBones.UpperChest, HumanBodyBones.Chest),
            new SocketPreset(SocketPresetId.HipsFront, 3, "Hips Front", HumanBodyBones.Hips),
            new SocketPreset(SocketPresetId.HipsBack, 4, "Hips Back", HumanBodyBones.Hips),
            new SocketPreset(SocketPresetId.LeftHand, 5, "Left Hand", HumanBodyBones.LeftHand),
            new SocketPreset(SocketPresetId.RightHand, 6, "Right Hand", HumanBodyBones.RightHand),
            new SocketPreset(SocketPresetId.LeftFoot, 7, "Left Foot", HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot),
            new SocketPreset(SocketPresetId.RightFoot, 8, "Right Foot", HumanBodyBones.RightToes, HumanBodyBones.RightFoot),
        };

        public static IReadOnlyList<SocketPreset> Presets => PresetsValue;
    }
}
