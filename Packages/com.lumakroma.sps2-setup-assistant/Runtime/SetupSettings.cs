using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant
{
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
        // Missing in older snapshots: false keeps their previously enabled Collapse.
        public bool showInternalThickness;
        public bool autoMode = true;
        public bool legacy = true;
        public bool localOnly;
        public bool modularAvatar;

        public SetupSettings Copy()
        {
            var copy = new SetupSettings { penetration = penetration, showInternalThickness = showInternalThickness, autoMode = autoMode,
                legacy = legacy, localOnly = localOnly, modularAvatar = modularAvatar };
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
