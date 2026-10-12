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
        private static Mesh _tile;
        private static Mesh _wedge;

        /// <summary>
        /// A sixth of a hex: the wedge between its middle and one side, from y = 0
        /// to y = 1. Six of them, each turned a sixth further, make the prism; a hex
        /// that breaks up falls apart into them. It carries the ground shader's uv.
        /// </summary>
        public static Mesh Wedge
        {
            get { return _wedge != null ? _wedge : _wedge = BuildWedge(); }
        }

        private static Mesh BuildWedge()
        {
            Vector3 a = new Vector3(Mathf.Cos(30f * Mathf.Deg2Rad), 0f, Mathf.Sin(30f * Mathf.Deg2Rad));
            Vector3 b = new Vector3(Mathf.Cos(90f * Mathf.Deg2Rad), 0f, Mathf.Sin(90f * Mathf.Deg2Rad));
            Vector3[] foot = { Vector3.zero, a, b };

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> parts = new List<Vector2>();
            List<int> triangles = new List<int>();

            // The top faces up: the corners run backwards, as on the prism.
            for (int index = 0; index < 3; index += 1)
            {
                vertices.Add(foot[index] + Vector3.up);
                normals.Add(Vector3.up);
                parts.Add(new Vector2(index == 0 ? 0f : 1f, 0f));
            }

            triangles.AddRange(new[] { 0, 2, 1 });

            for (int index = 0; index < 3; index += 1)
            {
                vertices.Add(foot[index]);
                normals.Add(Vector3.down);
                parts.Add(new Vector2(1f, 2f));
            }

            triangles.AddRange(new[] { 3, 4, 5 });

            Vector3 middle = (a + b) / 3f;
            for (int index = 0; index < 3; index += 1)
            {
                Vector3 from = foot[index];
                Vector3 to = foot[(index + 1) % 3];
                Vector3 normal = Vector3.Cross(Vector3.up, to - from).normalized;
                if (Vector3.Dot(normal, (from + to) * 0.5f - middle) < 0f)
                {
                    normal = -normal;
                }

                int first = vertices.Count;
                vertices.Add(from + Vector3.up);
                vertices.Add(to + Vector3.up);
                vertices.Add(to);
                vertices.Add(from);
                for (int corner = 0; corner < 4; corner += 1)
                {
                    normals.Add(normal);
                    parts.Add(new Vector2(1f, corner < 2 ? 1f : 2f));
                }

                // Wound so that the face looks the way its normal does.
                bool isFront = Vector3.Dot(Vector3.Cross(vertices[first + 1] - vertices[first], vertices[first + 2] - vertices[first]), normal) > 0f;
                triangles.AddRange(isFront
                    ? new[] { first, first + 1, first + 2, first, first + 2, first + 3 }
                    : new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }

            Mesh mesh = new Mesh { name = "HexWedge", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, parts);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
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
            get { return _root != null ? _root : _root = BuildFrustum("HexRoot", HexCorners, 30f, RootTipRadius, 1f, -1f, 0f, false, 2f); }
        }

        /// <summary>
        /// What a hex is drawn with: the prism with the rim of its top worn round.
        /// Submesh 0 is the top and its rim, submesh 1 the sides. The prism itself
        /// stays the shape a click is tested against.
        /// </summary>
        public static Mesh Tile
        {
            get { return _tile != null ? _tile : _tile = BuildTile(); }
        }

        private static Mesh BuildTile()
        {
            // The top is flat out to this share of the radius, then falls away to the side.
            const float flat = 0.9f;
            const float drop = 0.1f;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> parts = new List<Vector2>();
            List<int> cap = new List<int>();
            List<int> body = new List<int>();

            Vector3[] around = new Vector3[HexCorners];
            for (int index = 0; index < HexCorners; index += 1)
            {
                float angle = (30f + 360f / HexCorners * index) * Mathf.Deg2Rad;
                around[index] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            // The flat of the top, as a fan, and the rim round it. They share
            // vertices, and the rim leans its normals outward, so it shades round.
            vertices.Add(Vector3.up);
            normals.Add(Vector3.up);
            parts.Add(Vector2.zero);
            for (int index = 0; index < HexCorners; index += 1)
            {
                vertices.Add(around[index] * flat + Vector3.up);
                normals.Add(Vector3.up);
                parts.Add(new Vector2(flat, 0f));
            }

            for (int index = 0; index < HexCorners; index += 1)
            {
                vertices.Add(around[index] + Vector3.up * (1f - drop));
                normals.Add((around[index] + Vector3.up * 0.8f).normalized);
                parts.Add(new Vector2(1f, 0f));
            }

            for (int index = 0; index < HexCorners; index += 1)
            {
                int next = (index + 1) % HexCorners;
                cap.AddRange(new[] { 0, 1 + next, 1 + index });
                int inner = 1 + index;
                int innerNext = 1 + next;
                int outer = 1 + HexCorners + index;
                int outerNext = 1 + HexCorners + next;
                cap.AddRange(new[] { inner, innerNext, outerNext, inner, outerNext, outer });
            }

            for (int index = 0; index < HexCorners; index += 1)
            {
                int next = (index + 1) % HexCorners;
                Vector3 normal = (around[index] + around[next]).normalized;
                int first = vertices.Count;
                vertices.Add(around[index] + Vector3.up * (1f - drop));
                vertices.Add(around[next] + Vector3.up * (1f - drop));
                vertices.Add(around[next]);
                vertices.Add(around[index]);
                for (int corner = 0; corner < 4; corner += 1)
                {
                    normals.Add(normal);
                    // 1 at the top of the side, 2 at its foot.
                    parts.Add(new Vector2(1f, corner < 2 ? 1f + drop : 2f));
                }

                body.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            int under = vertices.Count;
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.down);
            parts.Add(new Vector2(1f, 2f));
            for (int index = 0; index < HexCorners; index += 1)
            {
                vertices.Add(around[index]);
                normals.Add(Vector3.down);
                parts.Add(new Vector2(1f, 2f));
            }

            for (int index = 0; index < HexCorners; index += 1)
            {
                body.AddRange(new[] { under, under + 1 + index, under + 1 + (index + 1) % HexCorners });
            }

            Mesh mesh = new Mesh { name = "HexTile", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, parts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(cap, 0);
            mesh.SetTriangles(body, 1);
            mesh.RecalculateBounds();

            return mesh;
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
        /// the upper cap goes to submesh 0 and everything else to submesh 1. For the
        /// ground shader uv0 says what a point is: x how near the rim of the cap, y 0
        /// on the cap and from <paramref name="sideBase"/> at the top of a side to
        /// one more at its foot.
        /// </summary>
        private static Mesh BuildFrustum(string name, int sides, float startDeg, float bottomRadius, float topRadius, float bottomY, float topY, bool splitCap, float sideBase = 1f)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> parts = new List<Vector2>();
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
                parts.Add(Vector2.zero);
                for (int index = 0; index < sides; index += 1)
                {
                    vertices.Add(top[index]);
                    normals.Add(Vector3.up);
                    parts.Add(new Vector2(1f, 0f));
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
                    parts.Add(new Vector2(1f, corner < 2 ? sideBase : sideBase + 1f));
                }

                body.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            // The lower cap faces down.
            if (bottomRadius > 0f)
            {
                int center = vertices.Count;
                vertices.Add(Vector3.up * bottomY);
                normals.Add(Vector3.down);
                parts.Add(new Vector2(1f, sideBase + 1f));
                for (int index = 0; index < sides; index += 1)
                {
                    vertices.Add(bottom[index]);
                    normals.Add(Vector3.down);
                    parts.Add(new Vector2(1f, sideBase + 1f));
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
            mesh.SetUVs(0, parts);

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
