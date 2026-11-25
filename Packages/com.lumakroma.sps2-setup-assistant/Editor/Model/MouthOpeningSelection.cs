using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    // Only used while seeding a new SPS3 mouth action. The configured Oh viseme
    // supplies a known motion and the fallback. Never search by expression name.
    internal static class MouthOpeningSelection
    {
        private const long VertexReadBudget = 16000000;
        private const long PairBudget = 500000;

        internal static int Choose(SkinnedMeshRenderer renderer, Transform avatar, int referenceIndex)
        {
            var mesh = renderer.sharedMesh;
            if (!mesh.isReadable || mesh.vertexCount > 200000 || mesh.blendShapeCount < 2 ||
                !renderer.transform.IsChildOf(avatar) ||
                (long)mesh.vertexCount * mesh.blendShapeCount > VertexReadBudget ||
                !SingleLinearFrame(mesh, referenceIndex)) return referenceIndex;

            var reference = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(referenceIndex, 0, reference, null, null);
            double energy = 0;
            float peakSquared = 0;
            int peak = 0;
            for (int i = 0; i < reference.Length; ++i) {
                float size = reference[i].sqrMagnitude;
                energy += size;
                if (size > peakSquared) { peakSquared = size; peak = i; }
            }
            if (!(energy > 0) || double.IsInfinity(energy)) return referenceIndex;
            float length = Mathf.Sqrt(peakSquared);
            var rotation = Quaternion.Inverse(renderer.transform.rotation) * avatar.rotation;
            var up = rotation * Vector3.up;
            if (!TryFindOpeningPair(mesh.vertices, reference, rotation, length, out int upper, out int lower))
                return referenceIndex;
            float separation = Vector3.Dot(reference[upper] - reference[lower], up);
            var delta = new Vector3[reference.Length];
            Vector3[] accepted = null;
            int selected = referenceIndex;
            for (int shape = 0; shape < mesh.blendShapeCount; ++shape) {
                if (shape == referenceIndex || !SingleLinearFrame(mesh, shape)) continue;
                mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
                // An accepted candidate extends the same pair's vertical opening.
                if (!(Vector3.Dot(delta[upper] - delta[lower], up) >= 1.2f * separation) ||
                    !(Vector3.Dot(delta[peak], reference[peak]) >= peakSquared)) continue;
                double dot = 0, squared = 0;
                for (int i = 0; i < delta.Length; ++i) {
                    dot += Vector3.Dot(delta[i], reference[i]);
                    squared += delta[i].sqrMagnitude;
                }
                double gain = dot / energy;
                // Require a modest 25-75% gain, with RMS error below 20% of the
                // reference. The separate per-vertex test rejects localized side effects.
                if (!(gain >= 1.25 && gain <= 1.75 && squared - dot * gain <= .04 * energy)) continue;
                bool fits = true, same = true;
                for (int i = 0; i < delta.Length; ++i) {
                    bool movedOutsideReference = reference[i].sqrMagnitude < .0001f * peakSquared &&
                                                 delta[i].sqrMagnitude > .0025f * peakSquared;
                    if (movedOutsideReference || (delta[i] - (float)gain * reference[i]).sqrMagnitude > .0625f * peakSquared) {
                        fits = false;
                        break;
                    }
                    if (accepted != null && (delta[i] - accepted[i]).sqrMagnitude > .000001f * peakSquared) same = false;
                }
                if (!fits) continue;
                if (accepted != null) {
                    if (!same) return referenceIndex;
                } else {
                    accepted = (Vector3[])delta.Clone();
                    selected = shape;
                }
            }
            return selected;
        }

        private static bool SingleLinearFrame(Mesh mesh, int shape) =>
            mesh.GetBlendShapeFrameCount(shape) == 1 && Mathf.Approximately(mesh.GetBlendShapeFrameWeight(shape, 0), 100);

        private static bool TryFindOpeningPair(Vector3[] vertices, Vector3[] delta, Quaternion rotation,
                                               float peak, out int upper, out int lower)
        {
            upper = lower = -1;
            Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward, up = rotation * Vector3.up;
            var depths = new List<float>();
            float left = float.PositiveInfinity, rightEdge = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; ++i) {
                if (delta[i].sqrMagnitude < .0025f * peak * peak) continue;
                float x = Vector3.Dot(vertices[i], right);
                left = Mathf.Min(left, x); rightEdge = Mathf.Max(rightEdge, x);
                depths.Add(Vector3.Dot(vertices[i], forward));
            }
            if (depths.Count < 8 || !(rightEdge > left)) return false;
            depths.Sort();
            float front = depths[(depths.Count - 1) * 7 / 10], center = (left + rightEdge) * .5f;
            var rising = new List<int>();
            var falling = new List<int>();
            for (int i = 0; i < vertices.Length; ++i) {
                if (Mathf.Abs(Vector3.Dot(vertices[i], right) - center) > .2f * (rightEdge - left) ||
                    Vector3.Dot(vertices[i], forward) < front) continue;
                float dy = Vector3.Dot(delta[i], up);
                if (dy > .05f * peak) rising.Add(i);
                if (dy < -.05f * peak) falling.Add(i);
            }
            if ((long)rising.Count * falling.Count > PairBudget) return false;
            float best = .2f * peak;
            foreach (int u in rising) foreach (int l in falling) {
                Vector3 gap = vertices[u] - vertices[l];
                float neutral = Vector3.Dot(gap, up);
                if (neutral < -.05f * peak || neutral > .5f * peak ||
                    (gap - neutral * up).sqrMagnitude > .16f * peak * peak) continue;
                float opening = Vector3.Dot(delta[u] - delta[l], up);
                if (opening > best) { best = opening; upper = u; lower = l; }
            }
            return upper >= 0;
        }
    }
}
