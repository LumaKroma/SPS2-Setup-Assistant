using System;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static class Sps3FreshFixtureAdmission
    {
        internal static bool Allows(bool valid, bool loaded, bool active, bool unsaved, int roots, string name) =>
            valid && loaded && active && unsaved && roots == 0 && !string.IsNullOrWhiteSpace(name);
    }

    internal struct Sps3LocalPoint
    {
        internal readonly float X, Y, Z;
        internal Sps3LocalPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        internal bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Immutable, deliberately narrow new-socket fixture contract, not the existing product settings schema.
    internal sealed class Sps3SocketSpecification
    {
        internal string Name { get; }
        internal string Mode { get; }
        internal string DepthUnits { get; }
        internal bool DepthEnabled { get; }
        internal float RangeMin { get; }
        internal float RangeMax { get; }
        internal bool IncludeInAuto { get; }
        internal bool UseRadiusOffset { get; }
        internal bool PublicChannel { get; }
        internal bool CollapsePath { get; }
        internal int PathCount => points.Length;
        private readonly Sps3LocalPoint[] points;

        internal Sps3SocketSpecification(string name, string mode, bool depth, string units,
            float min, float max, bool auto, bool fingerRing, bool radiusOffset, bool publicChannel,
            Sps3LocalPoint[] path, bool collapse, bool customTangents = false, bool legacy = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A display name is required.");
            if (mode != "Hole" && mode != "Ring" && mode != "Auto" && mode != "RingOneWay")
                throw new ArgumentException("Unverified socket mode.");
            if (units != "Meters" && units != "Plugs" && units != "Local")
                throw new ArgumentException("Units must be explicit Meters, Plugs, or Local. Ranges are never rescaled.");
            if (!Sps3LocalPoint.Finite(min) || !Sps3LocalPoint.Finite(max) || min > max)
                throw new ArgumentException("Depth range must be finite and ordered; no silent conversion.");
            if (customTangents) throw new ArgumentException("SPS3 custom tangents are not preserved by the observed native baker.");
            if (legacy) throw new ArgumentException("Avatar-wide Legacy options require a separate decision and adapter.");
            if (path == null || path.Length > 4) throw new ArgumentException("At most four explicit owned path stops are allowed.");
            foreach (var point in path) if (!point.IsFinite) throw new ArgumentException("Path points must be finite.");
            Name = name; Mode = mode; DepthEnabled = depth; DepthUnits = units;
            RangeMin = min; RangeMax = max; IncludeInAuto = auto && !fingerRing;
            UseRadiusOffset = radiusOffset; PublicChannel = publicChannel; CollapsePath = collapse;
            points = (Sps3LocalPoint[])path.Clone();
        }
        internal Sps3LocalPoint PathPoint(int index) => points[index];
    }

    // Shared orchestration is exercised with synthetic sessions; Unity implementation is compile-only.
    internal interface ISps3SocketTransactionSession
    {
        void ValidateBeforeMutation();
        void Begin();
        void CreateStaging();
        void WriteOwnedSettings();
        void VerifyStaging();
        void Commit();
        void VerifyCommitted();
        void Complete();
        void Rollback();
    }

    internal static class Sps3SocketTransaction
    {
        internal static void Execute(ISps3SocketTransactionSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.ValidateBeforeMutation(); // Missing identity/schema/ownership must not begin a mutation.
            try
            {
                // Begin itself can partially change state; every failure from here requires rollback.
                session.Begin();
                session.CreateStaging();
                session.WriteOwnedSettings();
                session.VerifyStaging();
                session.Commit();
                session.VerifyCommitted();
                session.Complete();
            }
            catch (Exception original)
            {
                try { session.Rollback(); }
                catch (Exception rollback)
                {
                    throw new AggregateException("SPS3 fixture failed and rollback is uncertain. Stop; do not retry or report success.", original, rollback);
                }
                throw;
            }
        }
    }
}
