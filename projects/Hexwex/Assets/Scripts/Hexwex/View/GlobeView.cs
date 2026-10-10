using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    public enum CellMark
    {
        None,
        Hovered,
        Selected,
        /// <summary>The island can fly here this turn.</summary>
        Reachable,
    }

    /// <summary>
    /// Draws the global map as a sphere of raised cells. It stands where the
    /// prototype's <c>globe.tsx</c> and <c>globe-scene.ts</c> stood: it only reads
    /// the world and never changes it. The prototype paints an antique map onto a
    /// smooth sphere with a shader; here every cell is its own slab, land stands
    /// above the sea, and what is on a scouted cell is modelled on top of it.
    ///
    /// One cell is about one world unit across, the size of an island hex.
    /// </summary>
    public sealed class GlobeView : MonoBehaviour
    {
        public const float Radius = 10f;

        /// <summary>How far land stands above the sea of clouds.</summary>
        private const float LandHeight = 0.32f;
        /// <summary>The gap between two cells, as a share of the cell.</summary>
        private const float CellInset = 0.93f;
        /// <summary>The slabs reach this far below the surface, into the core.</summary>
        private const float SkirtDepth = 0.35f;
        /// <summary>A trail this big paints the cell fully toxic.</summary>
        private const float ToxicFull = 400f;

        private static readonly Color CoreColor = new Color(0.07f, 0.09f, 0.12f);
        private static readonly Color FogSea = new Color(0.22f, 0.31f, 0.42f);
        private static readonly Color FogLand = new Color(0.46f, 0.44f, 0.38f);
        private static readonly Color FrontierSea = new Color(0.33f, 0.46f, 0.6f);
        private static readonly Color FrontierLand = new Color(0.66f, 0.62f, 0.5f);
        private static readonly Color CloudSea = new Color(0.72f, 0.84f, 0.95f);
        private static readonly Color SettlementColor = new Color(0.8f, 0.71f, 0.5f);
        private static readonly Color LairColor = new Color(0.42f, 0.12f, 0.1f);
        private static readonly Color BossRing = new Color(0.75f, 0.16f, 0.12f);
        private static readonly Color ActiveRing = new Color(0.64f, 0.15f, 0.11f);
        private static readonly Color ToxicColor = new Color(0.52f, 0.2f, 0.62f);

        [SerializeField] private Material surfaceMaterial;

        private World _world;
        /// <summary>A cell is about two units across; the markers are modelled smaller and drawn up to fill it.</summary>
        private const float LairSize = 1.5f;
        private const float SettlementSize = 1.5f;

        private static readonly Dictionary<string, GameObject> MarkerPrefabs = new Dictionary<string, GameObject>();

        private CellVisual[] _visuals;
        private Vector3[] _directions;

        private sealed class CellVisual
        {
            public MeshRenderer Renderer;
            public Transform Anchor;
            public GameObject Content;
            public string ContentKey;
        }

        public Material SurfaceMaterial
        {
            get { return surfaceMaterial; }
            set { surfaceMaterial = value; }
        }

        /// <summary>
        /// The prototype's sphere is right-handed. Flipping z keeps the planet from
        /// being mirrored in Unity's left-handed space.
        /// </summary>
        public static Vector3 ToUnity(Vec3 v)
        {
            return new Vector3((float)v.X, (float)v.Y, (float)-v.Z);
        }

        /// <summary>The direction from the globe's centre to a cell, in world space.</summary>
        public Vector3 DirectionOf(WorldCell cell)
        {
            return transform.rotation * ToUnity(cell.Center);
        }

        public void Render(World world, IReadOnlyList<Player> players, System.Func<WorldCell, CellMark> markOf)
        {
            if (world != _world)
            {
                Clear();
                Build(world);
            }

            foreach (WorldCell cell in world.Cells)
            {
                CellVisual visual = _visuals[cell.Index];
                UpdateContent(cell, visual, players);
                PaintCell(world, cell, visual, markOf != null ? markOf(cell) : CellMark.None);
            }
        }

        public void Clear()
        {
            for (int index = transform.childCount - 1; index >= 0; index -= 1)
            {
                Transform child = transform.GetChild(index);
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh != Props.Sphere)
                {
                    Destroy(filter.sharedMesh);
                }

                Destroy(child.gameObject);
            }

            _world = null;
            _visuals = null;
            _directions = null;
        }

        /// <summary>
        /// The cell under a ray, or <c>null</c> when the ray misses the globe. A cell
        /// of this sphere is the set of points nearest to its centre, so no
        /// colliders are needed: the hit point names the cell.
        /// </summary>
        public string Pick(Ray ray)
        {
            if (_world == null)
            {
                return null;
            }

            Vector3 toCenter = transform.position - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            float reach = Radius + LandHeight * 0.5f;
            float offsetSquared = toCenter.sqrMagnitude - along * along;
            if (along <= 0f || offsetSquared > reach * reach)
            {
                return null;
            }

            Vector3 hit = ray.origin + ray.direction * (along - Mathf.Sqrt(reach * reach - offsetSquared));
            Vector3 direction = Quaternion.Inverse(transform.rotation) * (hit - transform.position).normalized;

            int best = -1;
            float bestDot = -2f;
            for (int index = 0; index < _directions.Length; index += 1)
            {
                float dot = Vector3.Dot(_directions[index], direction);
                if (dot > bestDot)
                {
                    best = index;
                    bestDot = dot;
                }
            }

            return best >= 0 ? _world.Cells[best].Id : null;
        }

        private void Build(World world)
        {
            _world = world;
            _visuals = new CellVisual[world.Cells.Length];
            _directions = new Vector3[world.Cells.Length];

            // The dark body of the planet shows in the gaps between the cells.
            Transform core = Props.Part(transform, Props.Sphere, surfaceMaterial, Vector3.zero, Vector3.one * (Radius - SkirtDepth * 0.5f) * 2f, CoreColor);
            core.name = "Core";

            foreach (WorldCell cell in world.Cells)
            {
                Vector3 direction = ToUnity(cell.Center);
                // Land and sea are seen from orbit, so the height is known from the start.
                float height = cell.Kind == CellKind.Void ? 0f : LandHeight;

                GameObject slab = new GameObject("Cell " + cell.Id);
                slab.transform.SetParent(transform, false);
                slab.AddComponent<MeshFilter>().sharedMesh = BuildSlab(cell, direction, height);
                MeshRenderer renderer = slab.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { surfaceMaterial, surfaceMaterial };

                GameObject anchor = new GameObject("Anchor");
                anchor.transform.SetParent(slab.transform, false);
                anchor.transform.localPosition = direction * (Radius + height);
                anchor.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

                _directions[cell.Index] = direction;
                _visuals[cell.Index] = new CellVisual { Renderer = renderer, Anchor = anchor.transform };
            }
        }

        /// <summary>
        /// One cell as a slab: its outline pulled in a little, lifted to the cell's
        /// height, with walls down into the core. Submesh 0 is the top, submesh 1
        /// the walls, so the two take different colours.
        /// </summary>
        private static Mesh BuildSlab(WorldCell cell, Vector3 direction, float height)
        {
            int corners = cell.Polygon.Length;
            Vector3[] top = new Vector3[corners];
            Vector3[] bottom = new Vector3[corners];
            for (int index = 0; index < corners; index += 1)
            {
                Vector3 corner = Vector3.Lerp(direction, ToUnity(cell.Polygon[index]), CellInset).normalized;
                top[index] = corner * (Radius + height);
                bottom[index] = corner * (Radius - SkirtDepth);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> cap = new List<int>();
            List<int> walls = new List<int>();

            int center = vertices.Count;
            vertices.Add(direction * (Radius + height));
            normals.Add(direction);
            for (int index = 0; index < corners; index += 1)
            {
                vertices.Add(top[index]);
                normals.Add(direction);
            }

            for (int index = 0; index < corners; index += 1)
            {
                AddTriangle(cap, vertices, center, center + 1 + index, center + 1 + (index + 1) % corners, direction);
            }

            for (int index = 0; index < corners; index += 1)
            {
                int next = (index + 1) % corners;
                Vector3 outward = ((top[index] + top[next]) * 0.5f - direction * (Radius + height)).normalized;

                int first = vertices.Count;
                vertices.Add(top[index]);
                vertices.Add(top[next]);
                vertices.Add(bottom[next]);
                vertices.Add(bottom[index]);
                for (int corner = 0; corner < 4; corner += 1)
                {
                    normals.Add(outward);
                }

                AddTriangle(walls, vertices, first, first + 1, first + 2, outward);
                AddTriangle(walls, vertices, first, first + 2, first + 3, outward);
            }

            Mesh mesh = new Mesh { name = "Cell " + cell.Id };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(cap, 0);
            mesh.SetTriangles(walls, 1);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>Adds a triangle wound so that it faces <paramref name="outward"/>.</summary>
        private static void AddTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outward)
        {
            Vector3 facing = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            bool isFront = Vector3.Dot(facing, outward) > 0f;

            triangles.Add(a);
            triangles.Add(isFront ? b : c);
            triangles.Add(isFront ? c : b);
        }

        /// <summary>
        /// What stands on a cell. An unscouted cell shows nothing: the player sees
        /// the land and sea from orbit, but nothing on them.
        /// </summary>
        private void UpdateContent(WorldCell cell, CellVisual visual, IReadOnlyList<Player> players)
        {
            Player owner = null;
            if (cell.Revealed && cell.OwnerId != null)
            {
                foreach (Player player in players)
                {
                    if (player.Id == cell.OwnerId)
                    {
                        owner = player;
                    }
                }
            }

            bool isHeld = cell.Kind == CellKind.Island && !cell.Cleared && cell.IslandCount > 0;
            string key = "";
            if (cell.Revealed)
            {
                key = cell.Kind + ":" + (cell.Boss ? "boss" : isHeld ? cell.Biome + ":" + cell.IslandCount + ":" + cell.Activated : "empty");
                key += owner != null ? ":" + owner.Id : "";
            }

            if (visual.ContentKey == key)
            {
                return;
            }

            if (visual.Content != null)
            {
                Destroy(visual.Content);
            }

            visual.ContentKey = key;
            visual.Content = null;
            if (key.Length == 0)
            {
                return;
            }

            GameObject content = new GameObject("Content");
            content.transform.SetParent(visual.Anchor, false);
            visual.Content = content;
            Transform t = content.transform;
            Rng rng = Rng.FromText("cell:" + cell.Id);

            if (BuildModelContent(cell, t, Rng.FromText("marker:" + cell.Id), isHeld, owner))
            {
                return;
            }

            if (cell.Boss)
            {
                // The lair: the tallest thing on the globe, in the boss's red.
                Props.Part(t, HexMeshes.Cone, surfaceMaterial, Vector3.zero, new Vector3(0.5f, 1.5f, 0.5f), BossRing);
                Props.Part(t, HexMeshes.Cone, surfaceMaterial, new Vector3(0.36f, 0f, 0.1f), new Vector3(0.22f, 0.8f, 0.22f), LairColor);
                Props.Part(t, HexMeshes.Cone, surfaceMaterial, new Vector3(-0.3f, 0f, -0.22f), new Vector3(0.2f, 0.65f, 0.2f), LairColor);
            }
            else if (cell.Kind == CellKind.Settlement)
            {
                for (int index = 0; index < 3; index += 1)
                {
                    float angle = (index / 3f + (float)rng.Next() * 0.2f) * Mathf.PI * 2f;
                    Vector3 at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f;
                    Props.Part(t, Props.Cube, surfaceMaterial, at + Vector3.up * 0.08f, new Vector3(0.2f, 0.16f, 0.2f), Props.Hex("#e9dcc0"));
                    Props.Part(t, HexMeshes.Cone, surfaceMaterial, at + Vector3.up * 0.16f, new Vector3(0.17f, 0.14f, 0.17f), Props.Hex("#8c4a32"));
                }
            }
            else if (isHeld)
            {
                // One rock per wild island, in the colour of what they are made of.
                Color rock = Props.Hex(Biomes.Get(cell.Biome).EdgeColor);
                for (int index = 0; index < cell.IslandCount; index += 1)
                {
                    float angle = (index / (float)cell.IslandCount + (float)rng.Next() * 0.15f) * Mathf.PI * 2f;
                    Vector3 at = cell.IslandCount == 1 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.34f;
                    Props.Part(t, HexMeshes.Cone, surfaceMaterial, at, new Vector3(0.24f, 0.36f + (float)rng.Next() * 0.2f, 0.24f), rock);
                }

                // An activated island: its fight is on.
                if (cell.Activated)
                {
                    Props.Part(t, Props.Sphere, surfaceMaterial, Vector3.up * 0.85f, Vector3.one * 0.22f, ActiveRing);
                }
            }

            if (owner != null)
            {
                AddIslandMarker(t, owner);
            }
        }

        /// <summary>
        /// What stands on a scouted cell, from the modelled markers of
        /// <c>Resources/Markers</c>. Returns <c>false</c> when they are not there,
        /// and the primitives stand in.
        /// </summary>
        private bool BuildModelContent(WorldCell cell, Transform t, Rng rng, bool isHeld, Player owner)
        {
            if (MarkerPrefab("lair") == null)
            {
                return false;
            }

            float yaw = (float)rng.Next() * 360f;

            if (cell.Boss)
            {
                // The lair: the tallest thing on the globe.
                Marker(t, "lair", Vector3.zero, LairSize, yaw);
            }
            else if (cell.Kind == CellKind.Settlement)
            {
                Marker(t, "settlement", Vector3.zero, SettlementSize, yaw);
            }
            else if (isHeld)
            {
                // One flying rock per wild island, in the colour of what they are made of.
                Color ground = Props.Hex(Biomes.Get(cell.Biome).Color);
                bool isAlone = cell.IslandCount == 1 && owner == null;
                for (int index = 0; index < cell.IslandCount; index += 1)
                {
                    float angle = (index / (float)cell.IslandCount) * Mathf.PI * 2f + yaw * Mathf.Deg2Rad;
                    Vector3 at = isAlone ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f;
                    Transform island = Marker(t, index % 2 == 0 ? "island_wild_a" : "island_wild_b", at, isAlone ? 1.7f : 1.05f, (float)rng.Next() * 360f);
                    Tint(island, "M_Tint", ground);
                }

                // An activated island: its fight is on.
                if (cell.Activated)
                {
                    Marker(t, "beacon", Vector3.up * (owner != null ? 2.1f : 1.25f), 2.4f, yaw);
                }
            }

            if (owner != null)
            {
                // A player's island flies over whatever else is in the cell, under its banner.
                Transform island = Marker(t, "island_player", Vector3.up * (isHeld ? 0.75f : 0.08f), owner.IsHuman ? 1.7f : 1.4f, yaw);
                Tint(island, "M_Banner", Props.Hex(owner.Color));
            }

            return true;
        }

        private static Transform Marker(Transform parent, string name, Vector3 position, float size, float yaw)
        {
            GameObject prefab = MarkerPrefab(name);
            if (prefab == null)
            {
                return null;
            }

            Transform made = Instantiate(prefab, parent, false).transform;
            made.name = name;
            made.localPosition = position;
            made.localRotation = Quaternion.Euler(0f, yaw, 0f);
            made.localScale = Vector3.one * size;

            return made;
        }

        /// <summary>Paints the faces of a marker that use the named material: a banner, a biome's ground.</summary>
        private static void Tint(Transform marker, string materialName, Color color)
        {
            if (marker == null)
            {
                return;
            }

            foreach (MeshRenderer renderer in marker.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index += 1)
                {
                    if (materials[index] != null && materials[index].name.StartsWith(materialName))
                    {
                        Props.Paint(renderer, color, index);
                    }
                }
            }
        }

        private static GameObject MarkerPrefab(string name)
        {
            if (!MarkerPrefabs.TryGetValue(name, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>("Markers/" + name);
                MarkerPrefabs[name] = prefab;
            }

            return prefab;
        }

        /// <summary>A player's island flying over the cell, under a banner in the player's colour.</summary>
        private void AddIslandMarker(Transform parent, Player owner)
        {
            float size = owner.IsHuman ? 0.5f : 0.4f;
            Color banner = Props.Hex(owner.Color);
            Vector3 hover = Vector3.up * 0.75f;

            Props.Part(parent, HexMeshes.Root, surfaceMaterial, hover, new Vector3(size, size * 0.9f, size), new Color(0.27f, 0.23f, 0.21f));
            Props.Part(parent, HexMeshes.Prism, surfaceMaterial, hover, new Vector3(size, 0.08f, size), Color.Lerp(banner, Color.white, 0.25f));
            Props.Part(parent, Props.Cylinder, surfaceMaterial, hover + Vector3.up * 0.45f, new Vector3(0.04f, 0.4f, 0.04f), new Color(0.2f, 0.18f, 0.16f));
            Props.Part(parent, Props.Sphere, surfaceMaterial, hover + Vector3.up * 0.9f, Vector3.one * (owner.IsHuman ? 0.3f : 0.24f), banner);
        }

        private static void PaintCell(World world, WorldCell cell, CellVisual visual, CellMark mark)
        {
            bool isLand = cell.Kind != CellKind.Void;
            Color top;

            switch (WorldRules.Visibility(world, cell))
            {
                case CellVisibility.Fogged:
                    top = isLand ? FogLand : FogSea;
                    break;
                case CellVisibility.Frontier:
                    top = isLand ? FrontierLand : FrontierSea;
                    break;
                default:
                    top = cell.Boss ? LairColor
                        : cell.Kind == CellKind.Island ? Props.Hex(Biomes.Get(cell.Biome).Color)
                        : cell.Kind == CellKind.Settlement ? SettlementColor
                        : CloudSea;
                    // The trail a player left here never fades.
                    top = Color.Lerp(top, ToxicColor, Mathf.Clamp01(cell.ToxicTrail / ToxicFull) * 0.75f);
                    break;
            }

            Color side = top * 0.6f;

            switch (mark)
            {
                case CellMark.Hovered:
                    top = Color.Lerp(top, Color.white, 0.3f);
                    break;
                case CellMark.Selected:
                    top = Color.Lerp(top, new Color(1f, 0.92f, 0.45f), 0.6f);
                    break;
                case CellMark.Reachable:
                    top = Color.Lerp(top, new Color(0.55f, 1f, 0.55f), 0.45f);
                    break;
            }

            Props.Paint(visual.Renderer, top, 0);
            Props.Paint(visual.Renderer, side, 1);
        }
    }
}
