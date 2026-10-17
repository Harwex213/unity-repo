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
    /// above the sea, and what is on a scouted cell is modelled on top of it. The
    /// slabs are drawn with the same antique map, by <c>Hexwex/Globe</c>, a port
    /// of the prototype's shader, and the same muted stars stand behind.
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

        /// <summary>The night behind the globe, as in the prototype.</summary>
        public static readonly Color Sky = new Color(0.027f, 0.031f, 0.04f);

        private static readonly Color CoreColor = new Color(0.05f, 0.045f, 0.04f);
        private static readonly Color FogSea = new Color(0.13f, 0.14f, 0.15f);
        private static readonly Color FogLand = new Color(0.23f, 0.22f, 0.2f);
        private static readonly Color FrontierSea = new Color(0.36f, 0.37f, 0.36f);
        private static readonly Color FrontierLand = new Color(0.52f, 0.5f, 0.43f);
        private static readonly Color Sea = new Color(0.17f, 0.23f, 0.27f);
        private static readonly Color SettlementColor = new Color(0.8f, 0.71f, 0.5f);
        private static readonly Color LairColor = new Color(0.42f, 0.12f, 0.1f);
        private static readonly Color BossRing = new Color(0.75f, 0.16f, 0.12f);
        private static readonly Color ActiveRing = new Color(0.64f, 0.15f, 0.11f);

        private const int StarCount = 2600;
        private static readonly Color[] StarTints =
        {
            new Color(0.75f, 0.82f, 1f),
            new Color(1f, 0.95f, 0.85f),
            new Color(1f, 0.82f, 0.62f),
            new Color(0.9f, 0.9f, 0.95f),
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CellId = Shader.PropertyToID("_Cell");
        private static readonly int OwnerId = Shader.PropertyToID("_Owner");
        private static readonly int MarkId = Shader.PropertyToID("_Mark");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int PaperId = Shader.PropertyToID("_Paper");
        private static MaterialPropertyBlock _block;

        private Material _mapMaterial;
        private Material _starMaterial;

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

            if (_mapMaterial != null)
            {
                _mapMaterial.SetVector(CenterId, transform.position);
            }

            foreach (WorldCell cell in world.Cells)
            {
                CellVisual visual = _visuals[cell.Index];
                UpdateContent(cell, visual, players);
                PaintCell(world, cell, visual, markOf != null ? markOf(cell) : CellMark.None, OwnerOf(cell, players));
            }
        }

        /// <summary>The player whose island flies over a scouted cell, or <c>null</c>.</summary>
        private static Player OwnerOf(WorldCell cell, IReadOnlyList<Player> players)
        {
            if (!cell.Revealed || cell.OwnerId == null)
            {
                return null;
            }

            foreach (Player player in players)
            {
                if (player.Id == cell.OwnerId)
                {
                    return player;
                }
            }

            return null;
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

            // The map is drawn by the port of the prototype's shader, over its own paper.
            Shader mapShader = Resources.Load<Shader>("Shaders/HexwexGlobe");
            if (_mapMaterial == null && mapShader != null)
            {
                _mapMaterial = new Material(mapShader) { name = "M_Globe" };
                _mapMaterial.SetTexture(PaperId, Resources.Load<Texture2D>("Globe/paper"));
            }

            Material cellMaterial = _mapMaterial != null ? _mapMaterial : surfaceMaterial;
            BuildStars(world);

            // The dark body of the planet shows in the gaps between the cells, as the ink of the grid.
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
                renderer.sharedMaterials = new[] { cellMaterial, cellMaterial };

                GameObject anchor = new GameObject("Anchor");
                anchor.transform.SetParent(slab.transform, false);
                anchor.transform.localPosition = direction * (Radius + height);
                anchor.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

                _directions[cell.Index] = direction;
                _visuals[cell.Index] = new CellVisual { Renderer = renderer, Anchor = anchor.transform };
            }
        }

        /// <summary>
        /// The starfield of the prototype: the same count, the same four tints, the
        /// same spread of sizes, far behind the globe. A star is a small square that
        /// faces the globe.
        /// </summary>
        private void BuildStars(World world)
        {
            Shader shader = Resources.Load<Shader>("Shaders/HexwexStars");
            if (shader == null)
            {
                return;
            }

            if (_starMaterial == null)
            {
                _starMaterial = new Material(shader) { name = "M_Stars" };
            }

            Rng rng = Rng.FromText(world.Seed + ":stars");
            Vector3[] vertices = new Vector3[StarCount * 4];
            Color[] colors = new Color[StarCount * 4];
            Vector2[] corners = new Vector2[StarCount * 4];
            Vector2[] phases = new Vector2[StarCount * 4];
            int[] triangles = new int[StarCount * 6];

            for (int index = 0; index < StarCount; index += 1)
            {
                float z = (float)rng.Next() * 2f - 1f;
                float angle = (float)rng.Next() * Mathf.PI * 2f;
                float ring = Mathf.Sqrt(1f - z * z);
                // The prototype's planet has radius 1 and its stars stand 60 to 90 away.
                float distance = (16f + (float)rng.Next() * 8f) * Radius;
                Vector3 direction = new Vector3(Mathf.Cos(angle) * ring, z, Mathf.Sin(angle) * ring);

                float brightness = (Mathf.Pow((float)rng.Next(), 3f) * 0.9f + 0.1f) * 0.95f;
                Color tint = StarTints[Mathf.Min(StarTints.Length - 1, (int)(rng.Next() * StarTints.Length))] * brightness;
                float size = (1.5f + Mathf.Pow((float)rng.Next(), 6f) * 4f) * distance * 0.0014f;
                float phase = (float)rng.Next();

                Vector3 right = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.9f ? Vector3.right : Vector3.up).normalized * size;
                Vector3 up = Vector3.Cross(right, direction).normalized * size;
                Vector3 at = direction * distance;
                int first = index * 4;
                vertices[first] = at - right - up;
                vertices[first + 1] = at + right - up;
                vertices[first + 2] = at + right + up;
                vertices[first + 3] = at - right + up;
                corners[first] = new Vector2(0f, 0f);
                corners[first + 1] = new Vector2(1f, 0f);
                corners[first + 2] = new Vector2(1f, 1f);
                corners[first + 3] = new Vector2(0f, 1f);
                for (int corner = 0; corner < 4; corner += 1)
                {
                    colors[first + corner] = tint;
                    phases[first + corner] = new Vector2(phase, 0f);
                }

                int triangle = index * 6;
                triangles[triangle] = first;
                triangles[triangle + 1] = first + 1;
                triangles[triangle + 2] = first + 2;
                triangles[triangle + 3] = first;
                triangles[triangle + 4] = first + 2;
                triangles[triangle + 5] = first + 3;
            }

            Mesh mesh = new Mesh { name = "Stars" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = corners;
            mesh.uv2 = phases;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            GameObject stars = new GameObject("Stars");
            stars.transform.SetParent(transform, false);
            stars.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer starRenderer = stars.AddComponent<MeshRenderer>();
            starRenderer.sharedMaterial = _starMaterial;
            starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;
        }

        /// <summary>
        /// One cell as a slab: its outline pulled in a little, lifted to the cell's
        /// height, with walls down into the core. Submesh 0 is the top, submesh 1
        /// the walls. For the map shader a vertex also carries its place in the
        /// cell's own plane, how near the border it is (0 in the middle, 1 on the
        /// outline), and whether it is on a wall.
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
            List<Vector2> locals = new List<Vector2>();
            List<Vector2> edges = new List<Vector2>();
            List<int> cap = new List<int>();
            List<int> walls = new List<int>();

            // The cell's own plane: east and north on the sphere, one unit to the farthest corner.
            Vector3 middle = direction * (Radius + height);
            Vector3 east = Vector3.Cross(Mathf.Abs(direction.y) > 0.98f ? Vector3.forward : Vector3.up, direction).normalized;
            Vector3 north = Vector3.Cross(direction, east);
            float reach = 0.001f;
            for (int index = 0; index < corners; index += 1)
            {
                reach = Mathf.Max(reach, (top[index] - middle).magnitude);
            }

            Vector2 Local(Vector3 point)
            {
                return new Vector2(Vector3.Dot(point - middle, east), Vector3.Dot(point - middle, north)) / reach;
            }

            int center = vertices.Count;
            vertices.Add(middle);
            normals.Add(direction);
            locals.Add(Vector2.zero);
            edges.Add(Vector2.zero);
            for (int index = 0; index < corners; index += 1)
            {
                vertices.Add(top[index]);
                normals.Add(direction);
                locals.Add(Local(top[index]));
                edges.Add(new Vector2(1f, 0f));
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
                locals.Add(Local(top[index]));
                locals.Add(Local(top[next]));
                locals.Add(Local(top[next]));
                locals.Add(Local(top[index]));
                for (int corner = 0; corner < 4; corner += 1)
                {
                    normals.Add(outward);
                    edges.Add(new Vector2(1f, 1f));
                }

                AddTriangle(walls, vertices, first, first + 1, first + 2, outward);
                AddTriangle(walls, vertices, first, first + 2, first + 3, outward);
            }

            Mesh mesh = new Mesh { name = "Cell " + cell.Id };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, locals);
            mesh.SetUVs(1, edges);
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

        private static void PaintCell(World world, WorldCell cell, CellVisual visual, CellMark mark, Player owner)
        {
            bool isLand = cell.Kind != CellKind.Void;
            Color ground;
            float visibility;

            switch (WorldRules.Visibility(world, cell))
            {
                case CellVisibility.Fogged:
                    ground = isLand ? FogLand : FogSea;
                    visibility = 0f;
                    break;
                case CellVisibility.Frontier:
                    ground = isLand ? FrontierLand : FrontierSea;
                    visibility = 0.5f;
                    break;
                default:
                    ground = cell.Boss ? LairColor
                        : cell.Kind == CellKind.Island ? Props.Hex(Biomes.Get(cell.Biome).Color)
                        : cell.Kind == CellKind.Settlement ? SettlementColor
                        : Sea;
                    visibility = 1f;
                    break;
            }

            // The trail a player left here never fades: the shader soaks the cell in it.
            float toxic = visibility > 0.75f ? Mathf.Clamp01(cell.ToxicTrail / ToxicFull) : 0f;
            float kind = cell.Boss ? 2f : isLand ? 1f : 0f;
            Color ownerColor = owner != null ? Props.Hex(owner.Color) : Color.white;
            ownerColor.a = owner == null ? 0f : owner.IsHuman ? 1f : 0.5f;

            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            _block.SetColor(BaseColorId, ground);
            _block.SetVector(CellId, new Vector4(visibility, toxic, cell.Index, kind));
            _block.SetColor(OwnerId, ownerColor);
            _block.SetVector(MarkId, new Vector4(mark == CellMark.Hovered ? 1f : 0f, mark == CellMark.Selected ? 1f : 0f, mark == CellMark.Reachable ? 1f : 0f, 0f));
            visual.Renderer.SetPropertyBlock(_block);
        }
    }
}
