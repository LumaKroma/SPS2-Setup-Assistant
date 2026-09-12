using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace LumaKroma.Sps2SetupAssistant
{
    // Serialized authoring data only. The build integration removes this component.
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class Sps2SetupRoot : MonoBehaviour, IEditorOnly
    {
        public const int CurrentSchema = 1;
        public int schema = CurrentSchema;
        public string identity;
        public SetupSettings settings = new SetupSettings();
        public List<GeneratedSocket> sockets = new List<GeneratedSocket>();
        public GameObject testPlug;
    }

    [Serializable]
    public sealed class GeneratedSocket
    {
        public string id;
        public Transform anchor;
        public Transform pose;
        public Component socket;
        public Transform[] pathStops = Array.Empty<Transform>();
        public string buildToken;
    }

    [Serializable]
    public sealed class SetupSettings
    {
        public List<SocketSettings> parts = new List<SocketSettings>();
        public bool penetration = true;
        public bool autoMode = true;
        public bool legacy = true;
        public bool instant = true;
        public bool localOnly;
        public bool modularAvatar;

        public SetupSettings Copy()
        {
            var copy = new SetupSettings { penetration = penetration, autoMode = autoMode,
                legacy = legacy, instant = instant, localOnly = localOnly, modularAvatar = modularAvatar };
            foreach (var part in parts) copy.parts.Add(part.Copy());
            return copy;
        }
    }

    public enum DepthActionKind { BlendShape, AnimationClip, Object }
    public enum DepthUnits { Meters, Plugs, Local }

    [Serializable]
    public sealed class SocketSettings
    {
        public string id;
        public string name;
        public int category;
        public bool custom;
        public bool included;
        public Transform target;
        public bool depth;
        public Vector2 range = new Vector2(0, .05f);
        public DepthUnits units;
        public List<DepthActionSettings> actions = new List<DepthActionSettings>();

        public SocketSettings Copy()
        {
            var copy = new SocketSettings { id = id, name = name, category = category,
                custom = custom, included = included, target = target, depth = depth,
                range = range, units = units };
            foreach (var action in actions) copy.actions.Add(action.Copy());
            return copy;
        }
    }

    [Serializable]
    public sealed class DepthActionSettings
    {
        public DepthActionKind kind;
        // All type-specific inputs survive switching kinds.
        public SkinnedMeshRenderer renderer;
        public string shape = "";
        public float weight = 100;
        public AnimationClip clip;
        public GameObject target;
        public bool objectOn = true;
        public DepthActionSettings Copy() => (DepthActionSettings)MemberwiseClone();
    }
}
