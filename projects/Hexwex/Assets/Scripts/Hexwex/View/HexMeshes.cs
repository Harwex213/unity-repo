using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// The meshes the island is made of, generated once and shared. The prototype
    /// draws flat hexes on a canvas; here a hex is a prism standing on a tapered
    /// root of rock, so the island reads as a floating piece of land.
    /// </summary>
    public static class HexMeshes
    {
        private const int HexCorners = 6;
        private const int ConeSides = 8;
        /// <summary>The root narrows to this share of the hex radius at its tip.</summary>
        private const float RootTipRadius = 0.4f;

        private static Mesh _prism;
        private static Mesh _root;
        private static Mesh _cone;

        /// <summary>
        /// A pointy-top hex prism of radius 1, from y = 0 to y = 1. Submesh 0 is the
        /// top, submesh 1 the sides, so the two take different colours.
        /// </summary>
        public static Mesh Prism
        {
            get { return _prism != null ? _prism : _prism = BuildFrustum("HexPrism", HexCorners, 30f, 1f, 1f, 0f, 1f, true); }
        }

        /// <summary>The rock under a hex: radius 1 at y = 0, narrowing down to y = -1.</summary>
        public static Mesh Root
        {
            get { return _root != null ? _root : _root = BuildFrustum("HexRoot", HexCorners, 30f, RootTipRadius, 1f, -1f, 0f, false); }
        }

        /// <summary>A cone of base radius 1 and height 1, for trees and peaks.</summary>
        public static Mesh Cone
        {
            get { return _cone != null ? _cone : _cone = BuildFrustum("Cone", ConeSides, 0f, 1f, 0f, 0f, 1f, false); }
        }

        /// <summary>
        /// Where a hex stands in the world. The prototype's canvas has y down; the
        /// ground plane keeps its x and turns its y into -z, so the island is not
        /// mirrored. One hex radius is one world unit.
        /// </summary>
        public static Vector3 WorldPosition(int q, int r)
        {
            HexMath.HexToPixel(q, r, 1, out double x, out double y);

            return new Vector3((float)x, 0f, (float)-y);
        }

        /// <summary>
        /// A flat-shaded frustum between two rings. With <paramref name="splitCap"/>
        /// the upper cap goes to submesh 0 and everything else to submesh 1.
        /// </summary>
        private static Mesh BuildFrustum(string name, int sides, float startDeg, float bottomRadius, float topRadius, float bottomY, float topY, bool splitCap)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> cap = new List<int>();
            List<int> body = new List<int>();

            Vector3[] top = new Vector3[sides];
            Vector3[] bottom = new Vector3[sides];
            for (int index = 0; index < sides; index += 1)
            {
                float angle = (startDeg + 360f / sides * index) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                top[index] = direction * topRadius + Vector3.up * topY;
                bottom[index] = direction * bottomRadius + Vector3.up * bottomY;
            }

            // The upper cap, as a fan. Angles grow counter-clockwise seen from above,
            // so the fan runs backwards to face up.
            if (topRadius > 0f)
            {
                int center = vertices.Count;
                vertices.Add(Vector3.up * topY);
                normals.Add(Vector3.up);
                for (int index = 0; index < sides; index += 1)
                {
                    vertices.Add(top[index]);
                    normals.Add(Vector3.up);
                }

                for (int index = 0; index < sides; index += 1)
                {
                    cap.Add(center);
                    cap.Add(center + 1 + (index + 1) % sides);
                    cap.Add(center + 1 + index);
                }
            }

            for (int index = 0; index < sides; index += 1)
            {
                int next = (index + 1) % sides;
                Vector3 normal = Vector3.Cross(top[next] - bottom[index], bottom[next] - bottom[index]).normalized;
                if (Vector3.Dot(normal, bottom[index] + bottom[next]) < 0f)
                {
                    normal = -normal;
                }

                int first = vertices.Count;
                vertices.Add(top[index]);
                vertices.Add(top[next]);
                vertices.Add(bottom[next]);
                vertices.Add(bottom[index]);
                for (int corner = 0; corner < 4; corner += 1)
                {
                    normals.Add(normal);
                }

                body.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            // The lower cap faces down.
            if (bottomRadius > 0f)
            {
                int center = vertices.Count;
                vertices.Add(Vector3.up * bottomY);
                normals.Add(Vector3.down);
                for (int index = 0; index < sides; index += 1)
                {
                    vertices.Add(bottom[index]);
                    normals.Add(Vector3.down);
                }

                for (int index = 0; index < sides; index += 1)
                {
                    body.Add(center);
                    body.Add(center + 1 + index);
                    body.Add(center + 1 + (index + 1) % sides);
                }
            }

            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);

            if (splitCap)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(cap, 0);
                mesh.SetTriangles(body, 1);
            }
            else
            {
                cap.AddRange(body);
                mesh.SetTriangles(cap, 0);
            }

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
