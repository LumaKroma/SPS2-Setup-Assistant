using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public sealed class SocketPreset
    {
        public SocketPreset(
            SocketPresetId id,
            int order,
            string displayName,
            params HumanBodyBones[] boneCandidates)
        {
            Id = id;
            Order = order;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            BoneCandidates = boneCandidates ?? throw new ArgumentNullException(nameof(boneCandidates));

            if (boneCandidates.Length == 0)
            {
                throw new ArgumentException("At least one Humanoid bone candidate is required.", nameof(boneCandidates));
            }
        }

        public SocketPresetId Id { get; }

        public int Order { get; }

        public string DisplayName { get; }

        public IReadOnlyList<HumanBodyBones> BoneCandidates { get; }

        public string NodeName => $"{Order:00} {DisplayName}";
    }
}
