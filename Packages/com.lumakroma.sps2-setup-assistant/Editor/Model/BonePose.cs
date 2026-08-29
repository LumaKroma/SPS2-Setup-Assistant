using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public readonly struct BonePose
    {
        public BonePose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }
    }
}
