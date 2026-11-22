using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // Read-only estimate. Never claims that native Apply/generation preflight will succeed.
    internal sealed class AutoCapacityEstimate
    {
        internal readonly int Draft;
        internal readonly int? Current, Owned, Prospective;
        internal bool ExceedsLimit => Draft > 16 || Current > 16 || Prospective > 16;
        internal bool PossibleOverflow => !ExceedsLimit && !Prospective.HasValue &&
            (Current.HasValue ? Current.Value + Draft > 16 : Draft > 0);
        internal AutoCapacityEstimate(int draft, int? current, int? owned)
        {
            Draft = Math.Max(0, draft);
            Current = current >= 0 ? current : null;
            Owned = Current.HasValue && owned >= 0 && owned <= Current ? owned : null;
            Prospective = Owned.HasValue ? Current - Owned + Draft : null;
        }
        internal static AutoCapacityEstimate Inspect(VRCAvatarDescriptor avatar, SetupSettings draft) => InspectCore(avatar, draft, false, null);
        // The caller has already performed fresh ownership validation for this snapshot.
        internal static AutoCapacityEstimate InspectKnownRoot(VRCAvatarDescriptor avatar, SetupSettings draft, Sps2SetupContext root) =>
            InspectCore(avatar, draft, true, root);
        private static AutoCapacityEstimate InspectCore(VRCAvatarDescriptor avatar, SetupSettings draft, bool rootKnown, Sps2SetupContext knownRoot)
        {
            int selected = draft.parts.Count(p => p != null && p.included && draft.Sps3IncludeInAuto(p));
            int? current = null, owned = null;
            if (avatar == null) return new AutoCapacityEstimate(selected, null, null);
            try
            {
                var sockets = VrcFuryCompatibility.AllSockets(avatar.gameObject);
                var candidates = sockets.Where(VrcFuryCompatibility.ParticipatesInAuto).ToArray();
                current = candidates.Length;
                var root = rootKnown ? knownRoot : FullSetupGenerator.Find(avatar); // validates persisted ownership, not name-based guessing
                if (root == null) owned = 0;
                else if (root.AuthoringContract == SpsAuthoringContract.Sps3Beta14 && root.settings?.parts != null)
                {
                    int count = 0;
                    foreach (var generated in root.sockets)
                    {
                        var socket = generated.socket;
                        var saved = root.settings.parts.SingleOrDefault(p => p != null && p.id == generated.id);
                        if (socket == null || !sockets.Contains(socket) || saved == null ||
                            VrcFuryCompatibility.ReadIdentity(socket) != Sps2SetupStorage.Token(root, generated.id))
                            throw new InvalidOperationException("Unverified replacement ownership");
                        VrcFuryCompatibility.RequireSavedAutoFlags(socket, root.settings, saved);
                        if (candidates.Contains(socket)) count++;
                    }
                    owned = count;
                }
            }
            catch { /* Retain a verified current count, but never assume unknown replacement count is zero. */ }
            return new AutoCapacityEstimate(selected, current, owned);
        }
    }
}
