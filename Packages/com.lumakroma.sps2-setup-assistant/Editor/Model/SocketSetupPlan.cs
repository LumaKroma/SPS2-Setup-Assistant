using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public sealed class SocketSetupPlan
    {
        public SocketSetupPlan(BodyBasis basis, IReadOnlyList<SocketPlacement> placements)
        {
            Basis = basis;
            Placements = placements;
        }

        public BodyBasis Basis { get; }

        public IReadOnlyList<SocketPlacement> Placements { get; }
    }
}
