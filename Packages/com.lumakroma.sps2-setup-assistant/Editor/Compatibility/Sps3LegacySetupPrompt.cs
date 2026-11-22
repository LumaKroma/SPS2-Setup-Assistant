using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Review-only UI. Never calls Apply, regeneration, migration, asset saves or dependency factories.
    internal static class Sps3LegacySetupPrompt
    {
        internal static bool ObservedPackagePresent()
        {
            try
            {
                return FuryMinimalSps3Probe.AvailableForPreview(out _);
            }
            catch { return false; }
        }

        internal static Sps3LegacySetupNotice Inspect(VRCAvatarDescriptor avatar) => InspectCore(avatar, false);
        internal static Sps3LegacySetupNotice InspectForUpgradeNews(VRCAvatarDescriptor avatar) => InspectCore(avatar, true);

        private static Sps3LegacySetupNotice InspectCore(VRCAvatarDescriptor avatar, bool includeNdmf)
        {
            if (avatar == null) return Sps3LegacySetupNotice.None;
            try
            {
                int roots = avatar.transform.Cast<Transform>().Count(t => t.name == FullSetupGenerator.RootName);
                bool candidate = roots != 0 || SpsBackend.AllSockets(avatar.gameObject)
                    .Any(s => Sps2SetupStorage.TryToken(VrcFuryCompatibility.ReadIdentity(s), out _, out _));
                if (!candidate) return Sps3LegacySetupNotice.None;
                // Find validates marker tokens, schema, GUID-resolved asset, identity uniqueness,
                // socket records, backend family and ownership. It copies settings before upgrades.
                var context = FullSetupGenerator.Find(avatar);
                if (context == null) return Sps3LegacySetupNotice.Unverified;
                if (context.AuthoringContract == SpsAuthoringContract.Sps3Beta14) return Sps3LegacySetupNotice.None;
                int connected = context.sockets.Count(s => s != null && s.socket != null && s.pose != null &&
                    s.anchor != null && s.anchor.parent == context.transform && s.pose.parent == s.anchor &&
                    s.socket.transform == s.pose && s.pathStops != null &&
                    s.pathStops.All(t => t != null && t.IsChildOf(context.transform)));
                return Sps3LegacySetupNotice.Evaluate(candidate, roots, true, context.schema, context.identity,
                    Sps2SetupStorage.Identity(context.asset), context.sockets.Count, connected,
                    context.settings.family == SpsFamily.VrcFury || (includeNdmf && context.settings.family == SpsFamily.SpsForNdmf));
            }
            catch { return Sps3LegacySetupNotice.Unverified; }
        }
    }
}
