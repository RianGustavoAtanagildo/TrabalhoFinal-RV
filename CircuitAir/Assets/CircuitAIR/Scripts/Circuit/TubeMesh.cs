using System.Collections.Generic;
using UnityEngine;

namespace CircuitAIR
{
    /// <summary>Gera malhas procedurais: tubo ao longo de um caminho, disco e toro.</summary>
    public static class TubeMesh
    {
        /// <summary>Tubo de raio constante ao longo dos pontos, com tampas nas pontas.</summary>
        public static Mesh Build(IList<Vector3> path, float radius, int sides = 12, int stride = 1)
        {
            stride = Mathf.Max(1, stride);
            var samples = new List<Vector3>();
            for (int i = 0; i < path.Count; i += stride)
                samples.Add(path[i]);
            if ((path.Count - 1) % stride != 0)
                samples.Add(path[path.Count - 1]);

            int rings = samples.Count;
            var vertices = new List<Vector3>(rings * (sides + 1) + 2);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(rings * sides * 6 + sides * 6);

            // Quadro de transporte paralelo: evita que o tubo "torça" nas curvas.
            Vector3 tangent = (samples[1] - samples[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float travelled = 0f;

            for (int i = 0; i < rings; i++)
            {
                Vector3 nextTangent;
                if (i == 0)
                    nextTangent = (samples[1] - samples[0]).normalized;
                else if (i == rings - 1)
                    nextTangent = (samples[i] - samples[i - 1]).normalized;
                else
                    nextTangent = (samples[i + 1] - samples[i - 1]).normalized;

                if (i > 0)
                {
                    travelled += Vector3.Distance(samples[i], samples[i - 1]);
                    Quaternion rotation = Quaternion.FromToRotation(tangent, nextTangent);
                    normal = (rotation * normal).normalized;
                }

                tangent = nextTangent;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                normal = Vector3.Cross(binormal, tangent).normalized;

                for (int s = 0; s <= sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    Vector3 direction = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    vertices.Add(samples[i] + direction * radius);
                    normals.Add(direction);
                    uvs.Add(new Vector2(s / (float)sides, travelled * 10f));
                }
            }

            for (int i = 0; i < rings - 1; i++)
            {
                int a = i * (sides + 1);
                int b = a + sides + 1;
                for (int s = 0; s < sides; s++)
                {
                    // Ordem horária vista de fora (face frontal na Unity).
                    triangles.Add(a + s); triangles.Add(a + s + 1); triangles.Add(b + s);
                    triangles.Add(a + s + 1); triangles.Add(b + s + 1); triangles.Add(b + s);
                }
            }

            AddCap(samples[0], -(samples[1] - samples[0]).normalized, 0, sides, vertices, normals, uvs, triangles, true);
            AddCap(samples[rings - 1], (samples[rings - 1] - samples[rings - 2]).normalized, (rings - 1) * (sides + 1), sides, vertices, normals, uvs, triangles, false);

            var mesh = new Mesh { name = "CircuitAIR_Tube" };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(Vector3 center, Vector3 outward, int ringStart, int sides,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, bool reverse)
        {
            int centerIndex = vertices.Count;
            vertices.Add(center);
            normals.Add(outward);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s < sides; s++)
            {
                if (reverse)
                {
                    triangles.Add(centerIndex); triangles.Add(ringStart + s + 1); triangles.Add(ringStart + s);
                }
                else
                {
                    triangles.Add(centerIndex); triangles.Add(ringStart + s); triangles.Add(ringStart + s + 1);
                }
            }
        }

        /// <summary>Cilindro achatado (base dos postes), deitado no plano XZ.</summary>
        public static Mesh Disc(float radius, float height, int sides = 32)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Topo
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f)); normals.Add(Vector3.up);
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius)); normals.Add(Vector3.up);
            }
            for (int s = 0; s < sides; s++)
            {
                triangles.Add(topCenter); triangles.Add(topCenter + s + 2); triangles.Add(topCenter + s + 1);
            }

            // Lateral
            int sideStart = vertices.Count;
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * radius); normals.Add(n);
                vertices.Add(n * radius + Vector3.up * height); normals.Add(n);
            }
            for (int s = 0; s < sides; s++)
            {
                int i = sideStart + s * 2;
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i + 3);
            }

            var mesh = new Mesh { name = "CircuitAIR_Disc" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Toro (argola) no plano XY, com normal no eixo Z.</summary>
        public static Mesh Torus(float radius, float tubeRadius, int segments = 48, int sides = 10)
        {
            var path = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                path.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            Mesh mesh = Build(path, tubeRadius, sides);
            mesh.name = "CircuitAIR_Torus";
            return mesh;
        }
    }
}
