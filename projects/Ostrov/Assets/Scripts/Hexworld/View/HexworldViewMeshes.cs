using UnityEngine;

/// <summary>
/// Meshes the view builds in code. The art carries no rim model, so the
/// coloured ownership band and the highlight band are flat hexagonal rings that
/// lie just above the top face of a tile.
/// </summary>
/// <remarks>
/// A wider hexagon placed under the tile would not work: the six neighbouring
/// tiles cover it completely and only the outer border of the island would
/// show. A ring on top of the tile is visible everywhere.
/// </remarks>
public static class HexworldViewMeshes
{
    /// <summary>The cached ownership band.</summary>
    private static Mesh _ownerRing;

    /// <summary>The cached highlight band.</summary>
    private static Mesh _highlightRing;

    /// <summary>
    /// The ownership band: a hexagonal ring that hugs the tile border.
    /// </summary>
    public static Mesh OwnerRing
    {
        get
        {
            if (_ownerRing == null)
            {
                _ownerRing = CreateHexRing("HexworldOwnerRing", 0.84f, 0.99f);
            }

            return _ownerRing;
        }
    }

    /// <summary>
    /// The highlight band: a hexagonal ring that sits inside the ownership one.
    /// </summary>
    public static Mesh HighlightRing
    {
        get
        {
            if (_highlightRing == null)
            {
                _highlightRing = CreateHexRing("HexworldHighlightRing", 0.60f, 0.80f);
            }

            return _highlightRing;
        }
    }

    /// <summary>
    /// Builds a flat-top hexagonal ring in the XZ plane at Y zero, with the
    /// normals pointing up.
    /// </summary>
    /// <param name="name">Name of the mesh.</param>
    /// <param name="innerRadius">Inner radius, as a fraction of the tile radius.</param>
    /// <param name="outerRadius">Outer radius, as a fraction of the tile radius.</param>
    /// <returns>A fresh mesh that is never written to disk.</returns>
    public static Mesh CreateHexRing(string name, float innerRadius, float outerRadius)
    {
        const int CornerCount = 6;

        var vertices = new Vector3[CornerCount * 2];
        var normals = new Vector3[CornerCount * 2];
        var uvs = new Vector2[CornerCount * 2];

        for (int i = 0; i < CornerCount; i++)
        {
            float angle = Mathf.Deg2Rad * 60f * i;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i] = new Vector3(
                HexLayout.HexRadius * innerRadius * cos, 0f, HexLayout.HexRadius * innerRadius * sin);
            vertices[i + CornerCount] = new Vector3(
                HexLayout.HexRadius * outerRadius * cos, 0f, HexLayout.HexRadius * outerRadius * sin);

            normals[i] = Vector3.up;
            normals[i + CornerCount] = Vector3.up;
            uvs[i] = new Vector2(0f, i / (float)CornerCount);
            uvs[i + CornerCount] = new Vector2(1f, i / (float)CornerCount);
        }

        var triangles = new int[CornerCount * 6];
        for (int i = 0; i < CornerCount; i++)
        {
            int next = (i + 1) % CornerCount;
            int innerA = i;
            int innerB = next;
            int outerA = i + CornerCount;
            int outerB = next + CornerCount;

            int t = i * 6;
            triangles[t] = innerA;
            triangles[t + 1] = outerB;
            triangles[t + 2] = outerA;
            triangles[t + 3] = innerA;
            triangles[t + 4] = innerB;
            triangles[t + 5] = outerB;
        }

        var mesh = new Mesh();
        mesh.name = name;
        mesh.hideFlags = HideFlags.HideAndDontSave;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
