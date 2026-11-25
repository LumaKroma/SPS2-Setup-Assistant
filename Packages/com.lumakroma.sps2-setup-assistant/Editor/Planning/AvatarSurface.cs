using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Planning
{
    // Independent mesh-surface placement implementation. No third-party source/assets are bundled.
    internal sealed class AvatarSurface : IDisposable
    {
        private sealed class Snapshot : IDisposable
        {
            public readonly SkinnedMeshRenderer renderer;
            public readonly Mesh mesh;
            public readonly Vector3[] world;
            public readonly GameObject helper;
            public readonly MeshCollider collider;
            private int[] triangles;
            public int[] Triangles
            {
                get
                {
                    if (triangles != null) return triangles;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        if (mesh.GetTopology(i) != MeshTopology.Triangles) return triangles = Array.Empty<int>();
                    return triangles = mesh.triangles;
                }
            }
            public Snapshot(SkinnedMeshRenderer renderer, int sampleShape = -1, float sampleWeight = 0)
            {
                this.renderer = renderer;
                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                var source = new GameObject("SPS2 temporary skin measurement") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    source.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    source.transform.localScale = renderer.transform.lossyScale;
                    var skin = source.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = renderer.sharedMesh; skin.bones = renderer.bones; skin.rootBone = renderer.rootBone;
                    skin.quality = renderer.quality;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        string name = renderer.sharedMesh.GetBlendShapeName(i);
                        bool hideUnderClothing = name.StartsWith("Shrink", StringComparison.OrdinalIgnoreCase);
                        skin.SetBlendShapeWeight(i, i == sampleShape ? sampleWeight : hideUnderClothing ? 0 : renderer.GetBlendShapeWeight(i));
                    }
                    skin.BakeMesh(mesh, true);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }

                helper = new GameObject("SPS2 temporary surface query") { hideFlags = HideFlags.HideAndDontSave };
                helper.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                helper.transform.localScale = renderer.transform.lossyScale;
                // Match the frame used by the scratch renderer and surface collider.
                // The original renderer may inherit shear that a flattened TRS cannot represent.
                world = mesh.vertices.Select(v => helper.transform.TransformPoint(v)).ToArray();
                collider = helper.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                Physics.SyncTransforms();
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(helper); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private readonly VRCAvatarDescriptor avatar;
        private readonly Dictionary<SkinnedMeshRenderer, Snapshot> snapshots = new Dictionary<SkinnedMeshRenderer, Snapshot>();
        private readonly SkinnedMeshRenderer body;
        public bool HasBody => body != null;
        public Transform Breast(Transform torso, bool left)
        {
            var bones = body != null ? body.bones.AsEnumerable()
                : avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones);
            return BreastBoneResolver.Resolve(bones, torso, left);
        }
        public AvatarSurface(VRCAvatarDescriptor avatar)
        {
            this.avatar = avatar;
            var animator = avatar.GetComponent<Animator>();
            var major = new[] { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Select(animator.GetBoneTransform).Where(t => t != null).ToArray();
            var candidates = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.bones.Length > 0)
                .Select(r => new { renderer = r, coverage = WeightedCoverage(r, major),
                    named = r.name.Equals("Body", StringComparison.OrdinalIgnoreCase) || r.name.EndsWith("_body", StringComparison.OrdinalIgnoreCase) })
                .Where(r => r.coverage >= 3).OrderByDescending(r => r.coverage).ThenByDescending(r => r.named).ToArray();
            // An ambiguous clothing/body pair is not an anatomical measurement.
            if (candidates.Length != 0 && (candidates.Length == 1 || candidates[0].named != candidates[1].named || candidates[0].coverage != candidates[1].coverage))
                body = candidates[0].renderer;
        }
        private static int WeightedCoverage(SkinnedMeshRenderer renderer, Transform[] major)
        {
            var totals = new float[renderer.bones.Length];
            foreach (var weight in renderer.sharedMesh.GetAllBoneWeights())
                if (weight.boneIndex < totals.Length) totals[weight.boneIndex] += weight.weight;
            return major.Count(b => { int index = Array.IndexOf(renderer.bones, b); return index >= 0 && totals[index] >= 1; });
        }
        internal static bool IsUsableFrame(Matrix4x4 matrix)
        {
            for (int i = 0; i < 16; i++)
                if (float.IsNaN(matrix[i]) || float.IsInfinity(matrix[i])) return false;
            var x = (Vector3)matrix.GetColumn(0); var y = (Vector3)matrix.GetColumn(1); var z = (Vector3)matrix.GetColumn(2);
            if (!(x.magnitude > 0) || !(y.magnitude > 0) || !(z.magnitude > 0)) return false;
            float normalizedDeterminant = Vector3.Dot(Vector3.Cross(x.normalized, y.normalized), z.normalized);
            return !float.IsNaN(normalizedDeterminant) && Mathf.Abs(normalizedDeterminant) > 0.000001f;
        }
        private Snapshot Get(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMesh == null) return null;
            if (!IsUsableFrame(renderer.transform.localToWorldMatrix))
            {
                Debug.LogWarning("SPS surface measurement skipped: invalid or singular renderer transform.", renderer);
                return null;
            }
            if (!snapshots.TryGetValue(renderer, out var snapshot)) snapshots.Add(renderer, snapshot = new Snapshot(renderer));
            return snapshot;
        }
        public bool Ray(Vector3 center, Vector3 outward, float reach, out Vector3 point, SkinnedMeshRenderer renderer = null)
            => Ray(center, outward, reach, out point, out _, renderer);
        public bool Ray(Vector3 center, Vector3 outward, float reach, out Vector3 point, out Vector3 normal, SkinnedMeshRenderer renderer = null)
        {
            point = center; normal = outward.normalized;
            var snapshot = Get(renderer != null ? renderer : body);
            if (snapshot == null || outward.sqrMagnitude < .000001f) return false;
            outward.Normalize();
            if (!snapshot.collider.Raycast(new Ray(center + outward * reach, -outward), out var hit, reach * 2)) return false;
            point = hit.point; normal = hit.normal.normalized; return true;
        }
        public bool Mouth(out Vector3 point)
        {
            point = Vector3.zero;
            var renderer = avatar.VisemeSkinnedMesh;
            var snapshot = Get(renderer);
            int slot = (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.oh;
            if (snapshot == null || avatar.VisemeBlendShapes == null || slot >= avatar.VisemeBlendShapes.Length) return false;
            var mesh = renderer.sharedMesh; int shape = mesh.GetBlendShapeIndex(avatar.VisemeBlendShapes[slot] ?? "");
            if (shape < 0 || mesh.GetBlendShapeFrameCount(shape) == 0) return false;
            var delta = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape) - 1, delta, null, null);
            float total = 0; var center = Vector3.zero;
            for (int i = 0; i < delta.Length; i++) { float w = delta[i].sqrMagnitude; total += w; center += snapshot.world[i] * w; }
            if (total < 1e-12f) return false;
            center /= total;
            // The deformation center estimates the opening; the surface query removes depth bias.
            if (!Ray(center, avatar.transform.forward, .15f * Mathf.Abs(avatar.transform.lossyScale.y), out point, renderer)) point = center;
            return true;
        }
        // Measure only the visible opening. One recessed interval must be enclosed by
        // two lip surfaces on both sides of the centre line. Retain the old depth and axis.
        public Vector3 VisibleMouthCenter(Vector3 estimate, float height, SocketSettings part)
        {
            var face = avatar.VisemeSkinnedMesh;
            int oh = (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.oh;
            bool Open(string name)
            {
                if (string.IsNullOrEmpty(name)) return false;
                int i = face.sharedMesh.GetBlendShapeIndex(name);
                return i >= 0 && face.GetBlendShapeWeight(i) > 0;
            }
            if (!(height > 0) || !(Open(avatar.VisemeBlendShapes[oh]) ||
                part.EffectiveAnimationGroups().Any(g => g?.kind == SocketAnimationKind.Depth && g.actions != null &&
                    g.actions.Any(a => a?.kind == DepthActionKind.BlendShape && a.renderer == face && Open(a.shape)))))
                return estimate;
            var right = avatar.transform.right;
            var up = avatar.transform.up;
            var forward = avatar.transform.forward;
            const int half = 32;
            float step = height * .0005f;
            var depth = new float[half * 2 + 1];
            float halfWidth = height * .0008f;
            bool Center(float lateral, out float offset)
            {
                offset = 0;
                var start = estimate + right * lateral;
                for (int i = 0; i < depth.Length; i++) {
                    var sample = start + up * ((i - half) * step);
                    if (!Ray(sample, forward, height * .12f, out var hit, face)) return false;
                    depth[i] = Vector3.Dot(hit - estimate, forward);
                }
                // Use both rims of the entire opening. A short neighbourhood clips
                // the wider side first and biases the centre when the mouth is open.
                var lowerRim = new float[depth.Length];
                lowerRim[0] = depth[0];
                for (int i = 1; i < depth.Length; i++)
                    lowerRim[i] = Mathf.Max(lowerRim[i-1], depth[i]);
                float upperRim = depth[depth.Length-1];
                int first = -1, last = -1;
                for (int i = depth.Length-2; i >= 1; i--) {
                    upperRim = Mathf.Max(upperRim, depth[i]);
                    if (Mathf.Min(lowerRim[i], upperRim) - depth[i] < height * .0045f) continue;
                    if (first >= 0 && i != first - 1) return false;
                    if (last < 0) last = i;
                    first = i;
                }
                if (first <= 1 || last >= depth.Length - 2 || last - first < 3) return false;
                offset = ((first + last) * .5f - half) * step;
                return Mathf.Abs(offset) <= height * .008f;
            }
            if (!Center(-halfWidth, out float left) || !Center(halfWidth, out float rightOffset) ||
                Mathf.Abs(left-rightOffset) > 2*step) return estimate;
            return estimate + up * ((left + rightOffset) * .5f);
        }
        // Fresh automatic placement only. Keep a fixed point between the resting
        // estimate and the widest aperture; never move the receiver at runtime.
        internal Vector3 RestingApertureCenter(Vector3 point, Vector3 normal, float height, SocketSettings part)
        {
            var face = avatar.VisemeSkinnedMesh;
            if (part.custom || part.target || !part.AnimationEnabled || !face || !face.sharedMesh || !(height > 0)) return point;
            var groups = part.EffectiveAnimationGroups().Where(g => g?.kind == SocketAnimationKind.Depth).Take(2).ToArray();
            if (groups.Length != 1 || groups[0].actions?.Count != 1) return point;
            var action = groups[0].actions[0];
            if (action == null || action.kind != DepthActionKind.BlendShape || action.renderer != face ||
                action.blendShapeMode != BlendShapeMode.Normal || string.IsNullOrEmpty(action.shape) ||
                !(action.weight > 0 && action.weight <= 100)) return point;
            int index = face.sharedMesh.GetBlendShapeIndex(action.shape);
            if (index < 0 || face.GetBlendShapeWeight(index) != 0 ||
                face.sharedMesh.GetBlendShapeFrameCount(index) != 1) return point;
            bool UnitScale(Transform t) {
                for (; t != null; t = t.parent) {
                    var v = t.localScale;
                    if (!(Mathf.Approximately(v.x,1f) && Mathf.Approximately(v.y,1f) && Mathf.Approximately(v.z,1f))) return false;
                }
                return true;
            }
            var head = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var jaw = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Jaw);
            if (!head || !UnitScale(head) || !UnitScale(jaw) || !UnitScale(face.transform)) return point;
            const int half = 64;
            float step = height * .00025f, threshold = height * .004f;
            var front = avatar.transform.forward;
            var up = avatar.transform.up;
            var rest = Get(face);
            bool Depth(Snapshot snap, Vector3 o, out float z) {
                bool ok = snap.collider.Raycast(new Ray(o + front * (.12f * height), -front), out var hit, height * .24f);
                z = Vector3.Dot(hit.point - point, front);
                return ok;
            }
            var origins = new Vector3[3,half*2+1];
            var neutral = new float[3,half*2+1];
            for (int x=0; x<3; x++) for (int y=0; y<=2*half; y++) {
                origins[x,y] = point + avatar.transform.right * ((x-1) * .0008f * height) + up * ((y-half)*step);
                if (!Depth(rest, origins[x,y], out neutral[x,y])) return point;
            }
            bool Opening(float fraction, out float center) {
                center = 0;
                using var sample = new Snapshot(face, index, fraction * action.weight);
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                for (int x=0; x<3; x++) {
                    int first=-1, last=-1, start=-1;
                    // A single triangle at a silhouette can give an isolated hit.
                    // Only a continuous interval with resolvable height is an opening.
                    for (int y=0; y<=2*half; y++) {
                        if (!Depth(sample, origins[x,y], out float z)) return false;
                        if (neutral[x,y]-z >= threshold) {
                            if (y==0 || y==2*half) return false;
                            if (start<0) start=y;
                        } else if (start>=0) {
                            if (y-start>=5) {
                                if (first>=0) return false;
                                first=start; last=y-1;
                            }
                            start=-1;
                        }
                    }
                    if (first<0) return false;
                    float c=((first+last)*.5f-half)*step;
                    low=Mathf.Min(low,c); high=Mathf.Max(high,c); center+=c/3;
                }
                return high-low<2*step;
            }
            if (!Opening(.25f,out var shallow) || !Opening(.5f,out var mid) || !Opening(1,out var full)) return point;
            if (shallow*mid<=0 || mid*full<=0 || Mathf.Abs(shallow)>Mathf.Abs(mid)+step ||
                Mathf.Abs(mid)>Mathf.Abs(full)+step) return point;
            float shift=Mathf.Sign(full)*Mathf.Min(Mathf.Abs(full)*.5f, Mathf.Abs(mid), Mathf.Abs(shallow));
            // Move within the original entrance plane; keep the previous axial depth.
            Vector3 tangent = Vector3.ProjectOnPlane(up, normal);
            float vertical = Vector3.Dot(tangent, up);
            return vertical >= .5f ? point + tangent * (shift / vertical) : point;
        }
        public bool MouthDirection(Vector3 mouth, float height, out Vector3 direction)
        {
            var forward = avatar.transform.forward;
            var up = avatar.transform.up;
            direction = forward;
            float span = height * .012f;
            if (!Ray(mouth + up * span, forward, height * .15f, out var upper, avatar.VisemeSkinnedMesh) ||
                !Ray(mouth - up * span, forward, height * .15f, out var lower, avatar.VisemeSkinnedMesh)) return false;
            // Use the side-profile slope, not a single lip triangle's unstable normal.
            var line = upper - lower;
            float rise = Vector3.Dot(line, up);
            if (rise <= .000001f) return false;
            float pitch = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(line, forward), rise) * Mathf.Rad2Deg, -45f, 45f);
            direction = Quaternion.AngleAxis(pitch, avatar.transform.right) * forward;
            return true;
        }
        // A closed triangular fan on the front of the selected body is stronger
        // evidence than its most forward vertex. Ambiguous fans keep that estimate.
        public Vector3 BreastRingCenter(Vector3 estimate, float height, float side)
        {
            var snapshot = Get(body);
            if (snapshot == null || !(height > 0)) return estimate;
            var vertices = snapshot.world;
            var triangles = snapshot.Triangles;
            if (triangles.Length == 0) return estimate;
            var right = avatar.transform.right;
            var up = avatar.transform.up;
            var origin = avatar.transform.position;
            float reachSquared = Mathf.Pow(.035f * height, 2);
            var inRegion = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                inRegion[i] = (vertices[i] - estimate).sqrMagnitude <= reachSquared &&
                    Vector3.Dot(vertices[i] - origin, right) * side > 0;

            // Use exact baked positions so UV seams do not split a geometric fan.
            var incident = new Dictionary<Vector3, int>();
            foreach (int i in triangles) {
                if (!inRegion[i]) continue;
                incident.TryGetValue(vertices[i], out int count);
                incident[vertices[i]] = count + 1;
            }
            var fans = new Dictionary<Vector3, List<(Vector3, Vector3)>>();
            foreach (var entry in incident)
                if (entry.Value >= 12) fans.Add(entry.Key, new List<(Vector3, Vector3)>());
            if (fans.Count == 0) return estimate;
            for (int t = 0; t < triangles.Length; t += 3) {
                for (int j = 0; j < 3; j++) {
                    int i = triangles[t + j];
                    if (inRegion[i] && fans.TryGetValue(vertices[i], out var edges))
                        edges.Add((vertices[triangles[t + (j + 1) % 3]], vertices[triangles[t + (j + 2) % 3]]));
                }
            }
            Vector3? found = null;
            foreach (var fan in fans) {
                if (!IsRoundFan(fan.Key, fan.Value, right, up, height)) continue;
                if (found.HasValue) return estimate;
                found = fan.Key;
            }
            return found ?? estimate;
        }

        private static bool IsRoundFan(Vector3 center, List<(Vector3, Vector3)> edges, Vector3 right, Vector3 up, float height)
        {
            var next = new Dictionary<Vector3, Vector3>();
            foreach (var edge in edges) {
                if (next.ContainsKey(edge.Item1) || edge.Item1.Equals(edge.Item2)) return false;
                next.Add(edge.Item1, edge.Item2);
            }
            Vector3 start = edges[0].Item1, at = start;
            float winding = 0, min = float.PositiveInfinity, max = 0;
            for (int i = 0; i < next.Count; i++) {
                if (!next.TryGetValue(at, out var after)) return false;
                var a = new Vector2(Vector3.Dot(at-center, right), Vector3.Dot(at-center, up));
                var b = new Vector2(Vector3.Dot(after-center, right), Vector3.Dot(after-center, up));
                float angle = Vector2.SignedAngle(a, b);
                if (Mathf.Abs(angle) < .01f || Mathf.Abs(angle) > 60 || winding * angle < 0) return false;
                winding += angle;
                min = Mathf.Min(min, a.magnitude); max = Mathf.Max(max, a.magnitude);
                at = after;
                if (at.Equals(start) && i != next.Count - 1) return false;
            }
            return at.Equals(start) && Mathf.Abs(Mathf.Abs(winding)-360) < 1 &&
                max < height * .016f && min > height * .0001f && min > max * .55f;
        }

        public bool Extreme(Transform bone, Vector3 axis, float sign, out Vector3 point, float side = 0, SkinnedMeshRenderer renderer = null)
        {
            point = bone != null ? bone.position : Vector3.zero;
            renderer = renderer != null ? renderer : body;
            var snapshot = Get(renderer); if (snapshot == null || bone == null) return false;
            var mesh = renderer.sharedMesh; var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
            var bones = renderer.bones; int cursor = 0; float best = float.NegativeInfinity; bool found = false;
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    float influence = 0;
                    for (int n = 0; n < counts[i]; n++)
                    {
                        var w = weights[cursor++];
                        if (w.boneIndex < bones.Length && bones[w.boneIndex] != null && (bones[w.boneIndex] == bone || bones[w.boneIndex].IsChildOf(bone))) influence += w.weight;
                    }
                    if (influence < .5f) continue;
                    var vertex = snapshot.world[i];
                    if (side != 0 && Vector3.Dot(vertex - avatar.transform.position, avatar.transform.right) * side < 0) continue;
                    float projection = Vector3.Dot(vertex, axis) * sign;
                    if (projection > best) { best = projection; point = vertex; found = true; }
                }
            }
            // Native arrays are read-only mesh-owned views; do not dispose them.
            return found;
        }
        public void Dispose() { foreach (var snapshot in snapshots.Values) snapshot.Dispose(); snapshots.Clear(); }
    }
}
