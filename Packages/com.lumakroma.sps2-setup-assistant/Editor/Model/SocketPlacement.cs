using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public sealed class SocketPlacement
    {
        public SocketPlacement(
            SocketPreset preset,
            HumanBodyBones anchorBone,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            Preset = preset;
            AnchorBone = anchorBone;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
        }

        public SocketPreset Preset { get; }

        public HumanBodyBones AnchorBone { get; }

        public Vector3 WorldPosition { get; }

        public Quaternion WorldRotation { get; }
    }
}
