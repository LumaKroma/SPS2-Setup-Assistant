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
            public Snapshot(SkinnedMeshRenderer renderer)
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
                        skin.SetBlendShapeWeight(i, hideUnderClothing ? 0 : renderer.GetBlendShapeWeight(i));
                    }
                    skin.BakeMesh(mesh, true);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
                world = mesh.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
                helper = new GameObject("SPS2 temporary surface query") { hideFlags = HideFlags.HideAndDontSave };
                helper.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                helper.transform.localScale = renderer.transform.lossyScale;
                collider = helper.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                Physics.SyncTransforms();
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(helper); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private readonly VRCAvatarDescriptor avatar;
        private readonly Dictionary<SkinnedMeshRenderer, Snapshot> snapshots = new Dictionary<SkinnedMeshRenderer, Snapshot>();
        private readonly SkinnedMeshRenderer body;
        public bool HasBody => body != null;
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
        private Snapshot Get(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMesh == null) return null;
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
