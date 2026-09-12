using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Procedural responsibility: longitudinal geometry for native SPS deformation.
    internal static class LongTestPlugMesh
    {
        public const float Length = 1.5f;
        private const float Radius = .025f;
        internal static GameObject Create(Transform parent, Sps2SetupAsset asset, Material template)
        {
            const int sides = 32, caps = 8, shaft = 96;
            var rings = new List<Vector2>();
            for (int i = 0; i <= caps; i++)
            {
                float angle = Mathf.PI * .5f * i / caps;
                rings.Add(new Vector2(Radius * (1 - Mathf.Cos(angle)), Radius * Mathf.Sin(angle)));
            }
            for (int i = 1; i <= shaft; i++) rings.Add(new Vector2(Radius + (Length - 2 * Radius) * i / shaft, Radius));
            for (int i = 1; i <= caps; i++)
            {
                float angle = Mathf.PI * .5f * i / caps;
                rings.Add(new Vector2(Length - Radius + Radius * Mathf.Sin(angle), Radius * Mathf.Cos(angle)));
            }
            var vertices = new Vector3[rings.Count * (sides + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            for (int row = 0; row < rings.Count; row++)
                for (int side = 0; side <= sides; side++)
                {
                    int a = row * (sides + 1) + side;
                    float angle = 2 * Mathf.PI * side / sides;
                    vertices[a] = new Vector3(Mathf.Cos(angle) * rings[row].y, Mathf.Sin(angle) * rings[row].y, rings[row].x);
                    uv[a] = new Vector2((float)side / sides, rings[row].x / Length);
                    if (row == rings.Count - 1 || side == sides) continue;
                    int b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh { name = "SPS2 Long Capsule", vertices = vertices, uv = uv, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var material = new Material(template) { name = "SPS2 Long Capsule" };
            material.mainTexture = null; material.color = new Color(.45f, .8f, 1f, 1f);
            AssetDatabase.AddObjectToAsset(mesh, asset); AssetDatabase.AddObjectToAsset(material, asset);
            var obj = new GameObject("Capsule"); Undo.RegisterCreatedObjectUndo(obj, "SPS2 貫通テストプラグ");
            obj.transform.SetParent(parent, false);
            Undo.AddComponent<MeshFilter>(obj).sharedMesh = mesh;
            Undo.AddComponent<MeshRenderer>(obj).sharedMaterial = material;
            return obj;
        }
    }
}
