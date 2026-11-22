using System;
using System.Linq;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // UI-only memo. Never use the stored value to authorize a mutation.
    internal sealed class AutoDisplayCache<T> where T : class
    {
        internal const double LifetimeSeconds = 1;
        private T value;
        private double capturedAt;
        private Func<bool> contextCurrent;
        internal int CaptureCount { get; private set; }
        internal void Invalidate() { value = null; contextCurrent = null; }
        internal T Get(double now, Func<T> capture, Func<Func<bool>> captureContext, bool refreshExpired = true)
        {
            // Display snapshots expire on explicit invalidation or a changed context,
            // not merely because Unity delivered another Layout after one second.
            if (value != null && now >= capturedAt && contextCurrent()) return value;
            // Failed reads must not leave an older trusted-looking display snapshot behind.
            Invalidate();
            value = capture(); capturedAt = now; contextCurrent = captureContext(); CaptureCount++;
            return value;
        }
    }

    internal sealed class AutoDraftStamp
    {
        private readonly SetupSettings draft;
        private readonly SocketSettings[] parts;
        private readonly string[] ids;
        private readonly Transform[] targets;
        private readonly bool[] included, effective, custom, targetAlive;
        private readonly SpsFamily family;
        private readonly int policy, eligibility;
        private readonly bool autoMode;
        internal AutoDraftStamp(SetupSettings draft)
        {
            this.draft = draft;
            family = draft == null ? default : draft.family;
            policy = draft?.sps3PolicyVersion ?? 0; eligibility = draft?.sps3AutoEligibilityVersion ?? 0;
            autoMode = draft?.autoMode ?? false;
            parts = draft?.parts?.ToArray();
            ids = parts?.Select(p => p?.id).ToArray(); targets = parts?.Select(p => p?.target).ToArray();
            targetAlive = targets?.Select(t => t != null).ToArray();
            included = parts?.Select(p => p != null && p.included).ToArray();
            custom = parts?.Select(p => p != null && p.custom).ToArray();
            effective = parts?.Select(draft.Sps3IncludeInAuto).ToArray();
        }
        internal bool Matches(SetupSettings current)
        {
            if (!ReferenceEquals(current, draft)) return false;
            if (current == null) return true;
            if (current.family != family || current.sps3PolicyVersion != policy ||
                current.sps3AutoEligibilityVersion != eligibility || current.autoMode != autoMode) return false;
            if (parts == null) return current.parts == null;
            if (current.parts == null || current.parts.Count != parts.Length) return false;
            for (int i = 0; i < parts.Length; i++) {
                var p = current.parts[i];
                if (!ReferenceEquals(p, parts[i]) || p?.id != ids[i] || !ReferenceEquals(p?.target, targets[i]) ||
                    (p?.target != null) != targetAlive[i] ||
                    (p != null && p.included) != included[i] || (p != null && p.custom) != custom[i] ||
                    current.Sps3IncludeInAuto(p) != effective[i]) return false;
            }
            return true;
        }
    }
}
