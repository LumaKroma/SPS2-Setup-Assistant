using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public readonly struct BodyBasis
    {
        public BodyBasis(Vector3 right, Vector3 up, Vector3 forward, float height)
        {
            Right = right;
            Up = up;
            Forward = forward;
            Height = height;
        }

        public Vector3 Right { get; }

        public Vector3 Up { get; }

        public Vector3 Forward { get; }

        public float Height { get; }
    }
}
