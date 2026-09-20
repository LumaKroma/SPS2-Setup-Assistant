using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Planning
{
    internal static class BreastBoneResolver
    {
        internal static Transform Resolve(IEnumerable<Transform> skinBones, Transform torso, bool left)
        {
            if (torso == null) return null;
            var names = left
                ? new[] { "breastl", "bustl", "munel", "leftbreast", "leftbust", "breastleft" }
                : new[] { "breastr", "bustr", "muner", "rightbreast", "rightbust", "breastright" };
            var candidates = new HashSet<Transform>();
            foreach (var bone in skinBones)
            {
                // Separate outfit armatures must not compete with the humanoid skeleton.
                if (bone == null || !bone.IsChildOf(torso)) continue;
                for (var current = bone; current != torso; current = current.parent)
                {
                    var normalized = new string(current.name.Where(c => c != '_' && c != '.' && c != ' '
                        && !(c >= '0' && c <= '9')).ToArray()).ToLowerInvariant();
                    if (names.Contains(normalized)) candidates.Add(current);
                }
            }
            // Numbered segments belong to one breast chain, not independent candidates.
            var roots = candidates.Where(t => !candidates.Any(other => other != t && t.IsChildOf(other))).ToArray();
            return roots.Length == 1 ? roots[0] : null;
        }
    }
}
