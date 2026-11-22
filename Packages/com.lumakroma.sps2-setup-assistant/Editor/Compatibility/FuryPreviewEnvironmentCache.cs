using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.PackageManager;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Preview only. Apply, regeneration and Build always use the original live checks.
    internal static class FuryPreviewEnvironmentCache
    {
        private sealed class Entry { internal string version, root; internal Type socket; internal int epoch; }
        private static readonly object Gate = new object();
        private static Entry entry;
        private static int epoch;
        internal static int VerifiedCount { get; private set; }
        internal static int HitCount { get; private set; }
        static FuryPreviewEnvironmentCache()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, __) => Invalidate();
            AssemblyReloadEvents.beforeAssemblyReload += Invalidate;
            Events.registeredPackages += change => {
                if (change.added.Concat(change.removed).Concat(change.changedFrom).Concat(change.changedTo)
                    .Any(p => p != null && FuryNativeFeatureShapes.RelevantPackage(p.name))) Invalidate();
            };
        }
        internal static void Invalidate() { lock (Gate) { entry = null; unchecked { epoch++; } } }
        private static void RequireLive(SpsFamily family, SpsAuthoringContract contract)
        {
            using (SpsBackend.Use(family)) using (SpsAuthoringCompatibility.Use(contract)) VrcFuryCompatibility.RequireVersion();
        }
        internal static void Require(SpsFamily family, SpsAuthoringContract contract)
        {
            if (family != SpsFamily.VrcFury || contract != SpsAuthoringContract.Sps3Beta14) { RequireLive(family, contract); return; }
            var socket = VrcFuryApi.FindType(SpsBackend.SocketType(family));
            var package = socket == null ? null : UnityEditor.PackageManager.PackageInfo.FindForAssembly(socket.Assembly);
            if (package == null || package.name != SpsBackend.PackageId(family)) { RequireLive(family, contract); return; }
            Entry observed; int startEpoch;
            lock (Gate) {
                observed = entry; startEpoch = epoch;
                if (observed != null && (observed.epoch != epoch || observed.socket != socket ||
                    observed.version != package.version || observed.root != package.resolvedPath)) observed = null;
            }
            if (observed != null) {
                lock (Gate) {
                    if (ReferenceEquals(observed, entry) && epoch == startEpoch) { HitCount++; return; }
                }
            }
            // Failures are not cached. If environment changed during validation, do not store.
            RequireLive(family, contract);
            lock (Gate) {
                if (startEpoch == epoch) {
                    entry = new Entry { version = package.version, root = package.resolvedPath, socket = socket, epoch = epoch };
                    VerifiedCount++;
                }
            }
        }
    }
}
