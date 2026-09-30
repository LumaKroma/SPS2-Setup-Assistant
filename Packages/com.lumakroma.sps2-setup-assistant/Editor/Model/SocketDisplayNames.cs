using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    public static class SocketDisplayNames
    {
        private static readonly Dictionary<string, string> StandardNames = new Dictionary<string, string>
        {
            { "mouth", "口" }, { "earLeft", "左耳" }, { "earRight", "右耳" },
            { "nippleLeft", "左乳首" }, { "nippleRight", "右乳首" }, { "chest", "胸" },
            { "fingerRingLeft", "左手の指輪" }, { "fingerRingRight", "右手の指輪" },
            { "handLeft", "左手" }, { "handRight", "右手" }, { "hands", "両手" },
            { "vagina", "膣" }, { "anus", "肛門" }, { "thighs", "ふとももの間" },
            { "footLeft", "左足" }, { "footRight", "右足" }, { "feet", "両足" }
        };
        public static string Standard(SocketSettings part, DisplayLanguage language) =>
            !part.custom && part.id != null && StandardNames.TryGetValue(part.id, out var name)
                ? Sps2Localization.L(name, language) : part.name;
        public static string Resolve(SocketSettings part, DisplayLanguage language) =>
            string.IsNullOrWhiteSpace(part.menuNameOverride) ? Standard(part, language) : part.menuNameOverride;

        internal static void Upgrade(SetupSettings setup)
        {
            if (setup.displayNamesVersion >= 1) return;
            // Old assets have no language field, so their zero value remains Japanese.
            foreach (var part in setup.parts)
            {
                if (part.custom || part.id == null || !StandardNames.TryGetValue(part.id, out var standard)) continue;
                if (part.id == "chest" && part.name == "胸の間") part.name = standard;
                if (!string.IsNullOrWhiteSpace(part.name) && part.name != standard && string.IsNullOrWhiteSpace(part.menuNameOverride))
                    part.menuNameOverride = part.name;
            }
            setup.displayNamesVersion = 1;
        }
    }
}
