using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>Marks the collider of a hex of a battle island, so a raycast can name it.</summary>
    public sealed class BattleHexHandle : MonoBehaviour
    {
        public int Island;
        public int Hex;
    }

    /// <summary>
    /// Draws the level of the clear phase in 3D. It stands where the prototype's
    /// <c>cleanup-canvas.tsx</c> and <c>cleanup-render.ts</c> stood: it only reads
    /// the sim and never changes it. The prototype draws the battle flat, from
    /// above; here the islands are slabs of hexes over the sea of clouds, and the
    /// sim's plane lies on the ground: its x is world x, its y is world -z, and
    /// one hex radius is one world unit.
    ///
    /// Units and buildings are placeholders made of primitives, as on the island.
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        /// <summary>World units per unit of the sim's plane.</summary>
        public const float Scale = 1f / (float)HexMath.HexSize;

        /// <summary>Every hex stands this tall in battle, so a unit never sinks into a mountain.</summary>
        private const float HexHeight = 0.3f;
        private const float HexInset = 0.96f;
        private const float FlyHeight = 1.1f;
        private const float ShotHeight = 0.6f;
        /// <summary>Colours are refreshed this often, in ticks, for what changes slowly: poison, husks.</summary>
        private const int RepaintTicks = 15;

        private static readonly Color RootColor = new Color(0.27f, 0.23f, 0.21f);
        private static readonly Color ToxicColor = new Color(0.52f, 0.2f, 0.62f);
        private static readonly Color HuskColor = new Color(0.55f, 0.55f, 0.52f);
        private static readonly Color PlumeColor = new Color(0.4f, 0.16f, 0.5f);
        private static readonly Color WindowColor = new Color(0.45f, 0.9f, 0.6f);
        private static readonly Color WindowClosingColor = new Color(0.98f, 0.75f, 0.25f);
        private static readonly Color RockColor = new Color(0.36f, 0.34f, 0.38f);
        private static readonly Color EnemyColor = new Color(0.72f, 0.2f, 0.18f);
        /// <summary>The player's army wears one strong colour, so it reads against any biome.</summary>
        private static readonly Color ArmyColor = new Color(0.2f, 0.4f, 0.95f);
        private static readonly Color TargetColor = new Color(1f, 0.25f, 0.2f);

        [SerializeField] private Material surfaceMaterial;

        private readonly List<IslandVisual> _islands = new List<IslandVisual>();
        private readonly Dictionary<int, UnitVisual> _units = new Dictionary<int, UnitVisual>();
        private readonly List<Transform> _shots = new List<Transform>();
        private readonly Dictionary<int, Transform> _windows = new Dictionary<int, Transform>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly List<Effect> _effects = new List<Effect>();

        /// <summary>The level never shows more effects than this at once.</summary>
        private const int MaxEffects = 60;
        /// <summary>A figure looks at its target for this many ticks after a strike.</summary>
        private const int FaceTargetTicks = 20;
        /// <summary>A blow or a shot plays over this many ticks.</summary>
        private const float StrikeTicks = 10f;
        private const float FallSeconds = 0.7f;

        private readonly List<Effect> _fallen = new List<Effect>();
        private const float TurnDegreesPerSecond = 540f;

        private static readonly Dictionary<string, GameObject> UnitPrefabs = new Dictionary<string, GameObject>();

        private CleanupSim _sim;
        private Transform _border;
        private Color _playerColor = Color.white;
        private int _lastRepaintTick;
        private int _targetIsland = -1;
        private int _targetHex = -1;

        private sealed class IslandVisual
        {
            public Transform Root;
            public int Shape = -1;
            public int Razed = -1;
            public IslandState State;
            public readonly List<MeshRenderer> Tops = new List<MeshRenderer>();
        }

        private sealed class Effect
        {
            public Transform Root;
            public float StartAt;
            public float Seconds;
            public float Size;
            public bool IsFalling;
        }

        private sealed class UnitVisual
        {
            public Transform Root;
            public Transform Bar;
            public float BarWidth;
            public float Height;
            /// <summary>The modelled figure, turned to face where the unit goes or strikes. <c>null</c> for a placeholder.</summary>
            public Transform Model;
            public float Yaw;
            public FigurePose Pose;
        }

        public Material SurfaceMaterial
        {
            get { return surfaceMaterial; }
            set { surfaceMaterial = value; }
        }

        /// <summary>A point of the sim's plane in the world.</summary>
        public static Vector3 ToWorld(double x, double y, float height = 0f)
        {
            return new Vector3((float)x * Scale, height, (float)-y * Scale);
        }

        /// <summary>The centre of the player's island, for the camera to follow.</summary>
        public Vector3 PlayerCenter(float alpha)
        {
            SimIsland player = _sim != null ? _sim.PlayerIsland : null;
            if (player == null)
            {
                return Vector3.zero;
            }

            double x = Mathf.Lerp((float)player.Px, (float)player.Body.X, alpha) + player.Body.CenterX;
            double y = Mathf.Lerp((float)player.Py, (float)player.Body.Y, alpha) + player.Body.CenterY;

            return transform.position + ToWorld(x, y, HexHeight);
        }

        public void Bind(CleanupSim sim, Color playerColor)
        {
            Clear();
            _sim = sim;
            _playerColor = playerColor;
            _lastRepaintTick = 0;

            foreach (SimIsland island in sim.Islands)
            {
                GameObject root = new GameObject("Island " + island.Id);
                root.transform.SetParent(transform, false);
                _islands.Add(new IslandVisual { Root = root.transform });
            }

            BuildBorder();
        }

        public void Clear()
        {
            for (int index = transform.childCount - 1; index >= 0; index -= 1)
            {
                Destroy(transform.GetChild(index).gameObject);
            }

            _islands.Clear();
            _units.Clear();
            _shots.Clear();
            _effects.Clear();
            _fallen.Clear();
            _windows.Clear();
            _border = null;
            _sim = null;
            _targetIsland = -1;
            _targetHex = -1;
        }

        /// <summary>The hex under a ray. Returns <c>false</c> when the ray hits no island.</summary>
        public bool Pick(Ray ray, out int island, out int hex)
        {
            island = -1;
            hex = -1;
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000f))
            {
                return false;
            }

            BattleHexHandle handle = hit.collider.GetComponent<BattleHexHandle>();
            if (handle == null)
            {
                return false;
            }

            island = handle.Island;
            hex = handle.Hex;

            return true;
        }

        /// <summary>The hex the armed skill points at, painted as a target. -1 clears it.</summary>
        public void SetTarget(int island, int hex)
        {
            if (island == _targetIsland && hex == _targetHex)
            {
                return;
            }

            int before = _targetIsland;
            _targetIsland = island;
            _targetHex = hex;
            if (_sim == null)
            {
                return;
            }

            if (before >= 0 && before < _islands.Count)
            {
                PaintIsland(_sim.Islands[before], _islands[before]);
            }

            if (island >= 0 && island < _islands.Count)
            {
                PaintIsland(_sim.Islands[island], _islands[island]);
            }
        }

        /// <summary>
        /// Shows the sim between its last two ticks. <paramref name="alpha"/> is how
        /// far the frame is from the previous tick to the current one.
        /// </summary>
        public void Draw(float alpha, Camera viewCamera)
        {
            if (_sim == null)
            {
                return;
            }

            bool repaint = _sim.Tick - _lastRepaintTick >= RepaintTicks;
            if (repaint)
            {
                _lastRepaintTick = _sim.Tick;
            }

            for (int index = 0; index < _sim.Islands.Count; index += 1)
            {
                DrawIsland(_sim.Islands[index], _islands[index], alpha, repaint);
            }

            DrawUnits(alpha, viewCamera);
            DrawShots(alpha);
            DrawEffects();
            DrawFallen();
            DrawWindows();
        }

        /* ---------- islands ---------- */

        private void DrawIsland(SimIsland island, IslandVisual visual, float alpha, bool repaint)
        {
            // An island that joined has become hexes of the player's island; a lost one is gone.
            bool isShown = !island.IsGone && island.Hexes.Count > 0;
            if (visual.Root.gameObject.activeSelf != isShown)
            {
                visual.Root.gameObject.SetActive(isShown);
            }

            if (!isShown)
            {
                return;
            }

            float x = Mathf.Lerp((float)island.Px, (float)island.Body.X, alpha);
            float y = Mathf.Lerp((float)island.Py, (float)island.Body.Y, alpha);
            visual.Root.localPosition = ToWorld(x, y);

            int razed = island.Index == 0 ? _sim.Razed : 0;
            if (visual.Shape != island.Shape || visual.Razed != razed)
            {
                visual.Shape = island.Shape;
                visual.Razed = razed;
                visual.State = island.State;
                RebuildIsland(island, visual);
            }
            else if (repaint || visual.State != island.State)
            {
                visual.State = island.State;
                PaintIsland(island, visual);
            }
        }

        private void RebuildIsland(SimIsland island, IslandVisual visual)
        {
            for (int index = visual.Root.childCount - 1; index >= 0; index -= 1)
            {
                Destroy(visual.Root.GetChild(index).gameObject);
            }

            visual.Tops.Clear();

            for (int index = 0; index < island.Hexes.Count; index += 1)
            {
                CleanupHex hex = island.Hexes[index];
                Rng rng = Rng.FromText("battle:" + island.Id + ":" + hex.Id);

                GameObject cell = new GameObject("Hex " + hex.Id);
                cell.transform.SetParent(visual.Root, false);
                cell.transform.localPosition = ToWorld(island.Body.LocalX[index], island.Body.LocalY[index]);

                GameObject top = new GameObject("Top");
                top.transform.SetParent(cell.transform, false);
                top.transform.localScale = new Vector3(HexInset, HexHeight, HexInset);
                top.AddComponent<MeshFilter>().sharedMesh = HexMeshes.Prism;
                MeshRenderer topRenderer = top.AddComponent<MeshRenderer>();
                topRenderer.sharedMaterials = new[] { surfaceMaterial, surfaceMaterial };
                top.AddComponent<MeshCollider>().sharedMesh = HexMeshes.Prism;
                BattleHexHandle handle = top.AddComponent<BattleHexHandle>();
                handle.Island = island.Index;
                handle.Hex = index;
                visual.Tops.Add(topRenderer);

                Transform rock = Props.Part(cell.transform, HexMeshes.Root, surfaceMaterial, Vector3.zero, new Vector3(HexInset, 0.7f + (float)rng.Next() * 1.3f, HexInset), RootColor * (0.85f + (float)rng.Next() * 0.3f));
                rock.name = "Root";

                // What still stands on the player's island: the stronghold and the buildings.
                if (island.Index == 0 && index < _sim.StructureHpByHex.Length && hex.MaxHp > 0)
                {
                    StructureKind kind = hex.Stronghold ? StructureKind.Stronghold : StructureHp.ToKind(hex.Building.Value);
                    double hp = _sim.StructureHpByHex[index];
                    if (hp > 0 || hex.Stronghold)
                    {
                        GameObject structure = Props.CreateStructure(kind, surfaceMaterial, cell.transform);
                        structure.transform.localPosition = Vector3.up * HexHeight;
                        if (hp <= 0)
                        {
                            // Ruins: the same stronghold, sunk and knocked over.
                            structure.transform.localScale = new Vector3(1f, 0.35f, 1f);
                            structure.transform.localRotation = Quaternion.Euler(6f, 0f, -8f);
                        }
                    }
                }
            }

            PaintIsland(island, visual);
        }

        private void PaintIsland(SimIsland island, IslandVisual visual)
        {
            for (int index = 0; index < island.Hexes.Count && index < visual.Tops.Count; index += 1)
            {
                CleanupHex hex = island.Hexes[index];
                Biome biome = Biomes.Get(hex.Biome);
                float toxicity = Mathf.Clamp01(hex.Toxicity / 100f) * 0.75f;
                Color top = Color.Lerp(Props.Hex(biome.Color), ToxicColor, toxicity);
                Color side = Color.Lerp(Props.Hex(biome.EdgeColor), ToxicColor * 0.6f, toxicity);

                // A cleared island is an empty husk until it is docked.
                if (island.State == IslandState.Cleared)
                {
                    top = Color.Lerp(top, HuskColor, 0.45f);
                    side = Color.Lerp(side, HuskColor * 0.6f, 0.45f);
                }

                if (island.Index == _targetIsland && index == _targetHex)
                {
                    top = Color.Lerp(top, TargetColor, 0.7f);
                }

                Props.Paint(visual.Tops[index], top, 0);
                Props.Paint(visual.Tops[index], side, 1);
            }
        }

        /* ---------- units ---------- */

        private void DrawUnits(float alpha, Camera viewCamera)
        {
            _seen.Clear();
            Quaternion facing = viewCamera != null ? viewCamera.transform.rotation : Quaternion.identity;

            foreach (SimUnit unit in _sim.Units)
            {
                if (!unit.Alive)
                {
                    continue;
                }

                _seen.Add(unit.Id);
                if (!_units.TryGetValue(unit.Id, out UnitVisual visual))
                {
                    visual = CreateUnit(unit);
                    _units[unit.Id] = visual;
                }

                float x = Mathf.Lerp((float)unit.Px, (float)unit.X, alpha);
                float y = Mathf.Lerp((float)unit.Py, (float)unit.Y, alpha);
                // A hit shows as a short lunge at the target.
                float lunge = Mathf.Clamp01(1f - (_sim.Tick - unit.LastAttackTick + alpha) / 5f) * 9f;
                x += (float)unit.AimX * lunge;
                y += (float)unit.AimY * lunge;
                visual.Root.localPosition = ToWorld(x, y, unit.Node < 0 ? HexHeight + FlyHeight : HexHeight);
                Face(unit, visual, alpha);

                float share = Mathf.Clamp01((float)(unit.Hp / unit.MaxHp));
                bool isHurt = share < 0.999f;
                if (visual.Bar.gameObject.activeSelf != isHurt)
                {
                    visual.Bar.gameObject.SetActive(isHurt);
                }

                if (isHurt)
                {
                    visual.Bar.localScale = new Vector3(visual.BarWidth * share, 0.06f, 0.02f);
                    visual.Bar.rotation = facing;
                }
            }

            _gone.Clear();
            foreach (int id in _units.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (int id in _gone)
            {
                // A figure does not vanish: it falls where it stood.
                UnitVisual fallen = _units[id];
                if (fallen.Model != null)
                {
                    fallen.Bar.gameObject.SetActive(false);
                    _fallen.Add(new Effect { Root = fallen.Root, StartAt = Time.unscaledTime, Seconds = FallSeconds });
                }
                else
                {
                    Destroy(fallen.Root.gameObject);
                }

                _units.Remove(id);
            }
        }

        /// <summary>
        /// Turns a figure toward its target for a moment after it strikes, and
        /// otherwise toward where it walks or flies. A unit standing still on a
        /// sailing island keeps its heading: the island's own motion does not count.
        /// </summary>
        private void Face(SimUnit unit, UnitVisual visual, float alpha)
        {
            if (visual.Model == null)
            {
                return;
            }

            double dx = unit.X - unit.Px;
            double dy = unit.Y - unit.Py;
            SimIsland island = _sim.IslandOf(unit);
            if (island != null)
            {
                dx -= island.Body.X - island.Px;
                dy -= island.Body.Y - island.Py;
            }

            // How fast the unit itself moves, against a full walk of its kind.
            double full = System.Math.Max(0.2, unit.Stats.Speed) * CleanupCollision.HexStep * CleanupSim.TickSeconds;
            float stride = Mathf.Clamp01((float)(System.Math.Sqrt(dx * dx + dy * dy) / full));

            if (_sim.Tick - unit.LastAttackTick < FaceTargetTicks)
            {
                dx = unit.AimX;
                dy = unit.AimY;
            }
            else if (dx * dx + dy * dy < 0.25)
            {
                dx = 0;
                dy = 0;
            }

            if (dx != 0 || dy != 0)
            {
                // The sim's y is world -z, and a model's front is its +z.
                float target = Mathf.Atan2((float)dx, (float)-dy) * Mathf.Rad2Deg;
                visual.Yaw = Mathf.MoveTowardsAngle(visual.Yaw, target, TurnDegreesPerSecond * Time.unscaledDeltaTime);
            }

            float strike = (_sim.Tick - unit.LastAttackTick + alpha) / StrikeTicks;
            visual.Pose.Apply(stride, strike, unit.Stats.Projectile != Projectile.None, visual.Yaw, Time.unscaledDeltaTime);
        }

        private static GameObject UnitPrefab(string key)
        {
            if (!UnitPrefabs.TryGetValue(key, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>("Units/" + key);
                UnitPrefabs[key] = prefab;
            }

            return prefab;
        }

        private UnitVisual CreateUnit(SimUnit unit)
        {
            GameObject root = new GameObject(unit.Stats.Key + " " + unit.Id);
            root.transform.SetParent(transform, false);
            Transform t = root.transform;
            bool isPlayer = unit.Side == CleanupSide.Player;
            Color color = isPlayer ? ArmyColor : EnemyColor;
            Color dark = color * 0.55f;
            float height = 0.5f;
            Transform model = null;

            if (unit.IsStructure)
            {
                // The model belongs to the island; the unit only carries the health bar.
                height = unit.Structure == StructureKind.Stronghold ? 1.7f : 1.2f;
            }
            else if (UnitPrefab(unit.Stats.Key) != null)
            {
                // A modelled figure. The player's wear blue in the model itself; no monster does.
                model = Instantiate(UnitPrefab(unit.Stats.Key), t, false).transform;
                model.name = "Model";
                MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
                height = filter != null ? filter.sharedMesh.bounds.max.y : height;
            }
            else if (unit.Stats.Flying)
            {
                float size = Mathf.Lerp(0.34f, 0.6f, Mathf.InverseLerp(24f, 96f, unit.Stats.Hp));
                Props.Part(t, Props.Sphere, surfaceMaterial, Vector3.zero, new Vector3(size, size * 0.6f, size * 1.3f), color);
                Props.Part(t, Props.Cube, surfaceMaterial, Vector3.zero, new Vector3(size * 2.6f, 0.03f, size * 0.6f), dark);
                height = size + 0.2f;
            }
            else
            {
                // Bigger creatures stand bigger: the size follows the hit points.
                float size = Mathf.Lerp(1.5f, 3.4f, Mathf.InverseLerp(24f, 450f, unit.Stats.Hp));
                float body = 0.26f * size;
                Props.Part(t, Props.Cylinder, surfaceMaterial, Vector3.up * body * 0.5f, new Vector3(0.2f * size, body * 0.5f, 0.2f * size), color);
                Props.Part(t, Props.Sphere, surfaceMaterial, Vector3.up * (body + 0.08f * size), Vector3.one * 0.18f * size, isPlayer ? new Color(0.95f, 0.85f, 0.7f) : dark);
                if (unit.Stats.Projectile != Projectile.None)
                {
                    // Whoever shoots carries something long.
                    Props.Part(t, Props.Cube, surfaceMaterial, new Vector3(0.14f * size, body * 0.7f, 0f), new Vector3(0.03f, body * 1.3f, 0.03f), new Color(0.3f, 0.22f, 0.14f));
                }

                height = body + 0.3f * size;
            }

            Transform bar = Props.Part(t, Props.Cube, surfaceMaterial, Vector3.up * (height + 0.12f), Vector3.one, isPlayer ? new Color(0.35f, 0.9f, 0.4f) : new Color(0.95f, 0.3f, 0.25f));
            bar.name = "Health";
            bar.gameObject.SetActive(false);

            return new UnitVisual { Root = t, Model = model, Pose = model != null ? new FigurePose(model, unit.Id) : null, Bar = bar, BarWidth = unit.IsStructure ? 0.9f : 0.55f, Height = height, Yaw = isPlayer ? 180f : 0f };
        }

        /* ---------- shots ---------- */

        private void DrawShots(float alpha)
        {
            for (int index = 0; index < _sim.Projectiles.Count; index += 1)
            {
                SimProjectile shot = _sim.Projectiles[index];
                if (index >= _shots.Count)
                {
                    Transform created = Props.Part(transform, Props.Sphere, surfaceMaterial, Vector3.zero, Vector3.one * 0.12f, Color.white);
                    created.name = "Shot";
                    _shots.Add(created);
                }

                Transform visual = _shots[index];
                if (!visual.gameObject.activeSelf)
                {
                    visual.gameObject.SetActive(true);
                }

                float x = Mathf.Lerp((float)shot.Px, (float)shot.X, alpha);
                float y = Mathf.Lerp((float)shot.Py, (float)shot.Y, alpha);
                visual.localPosition = ToWorld(x, y, HexHeight + ShotHeight);
                visual.localScale = Vector3.one * (shot.Kind == Projectile.Hex || shot.Kind == Projectile.Bolt ? 0.18f : 0.1f);
                Props.Paint(visual.GetComponent<MeshRenderer>(), ShotColor(shot.Kind));
            }

            for (int index = _sim.Projectiles.Count; index < _shots.Count; index += 1)
            {
                if (_shots[index].gameObject.activeSelf)
                {
                    _shots[index].gameObject.SetActive(false);
                }
            }
        }

        private static Color ShotColor(Projectile kind)
        {
            switch (kind)
            {
                case Projectile.Stone: return new Color(0.6f, 0.6f, 0.6f);
                case Projectile.Arrow: return new Color(0.85f, 0.75f, 0.5f);
                case Projectile.Bullet: return new Color(0.2f, 0.2f, 0.22f);
                case Projectile.Hex: return new Color(0.75f, 0.3f, 0.95f);
                default: return new Color(0.5f, 0.85f, 1f);
            }
        }

        /* ---------- effects ---------- */

        /// <summary>A point of the sim's plane in the world, above the hexes, for an effect or a number.</summary>
        public Vector3 EffectPoint(double x, double y, float height)
        {
            return transform.position + ToWorld(x, y, HexHeight + height);
        }

        /// <summary>
        /// Shows what the sim says has just happened: a puff where a unit fell or was
        /// carried off, a burst where a building was razed, a purple puff over a
        /// poisoned hex, and a hex that crumbles and falls.
        /// </summary>
        public void PlayEffect(SimEvent simEvent)
        {
            if (_effects.Count >= MaxEffects)
            {
                return;
            }

            switch (simEvent.Type)
            {
                case SimEventType.Death:
                    AddPuff(simEvent, simEvent.Side == CleanupSide.Player ? ArmyColor : EnemyColor, 0.55f, 0.35f);
                    break;
                case SimEventType.Razed:
                    AddPuff(simEvent, new Color(0.95f, 0.55f, 0.2f), 1.3f, 0.55f);
                    break;
                case SimEventType.Poison:
                    AddPuff(simEvent, PlumeColor, 0.5f, 0.45f);
                    break;
                case SimEventType.Ferry:
                    AddPuff(simEvent, new Color(0.9f, 0.9f, 1f), 0.4f, 0.3f);
                    break;
                case SimEventType.Crumble:
                    Transform piece = Props.Part(transform, HexMeshes.Prism, surfaceMaterial, ToWorld(simEvent.X, simEvent.Y), new Vector3(HexInset, HexHeight, HexInset), RootColor);
                    piece.name = "Crumble";
                    // The prism has a cap and sides: both fall.
                    MeshRenderer pieceRenderer = piece.GetComponent<MeshRenderer>();
                    pieceRenderer.sharedMaterials = new[] { surfaceMaterial, surfaceMaterial };
                    Props.Paint(pieceRenderer, RootColor * 0.8f, 1);
                    _effects.Add(new Effect { Root = piece, StartAt = Time.unscaledTime, Seconds = 0.9f, Size = 1f, IsFalling = true });
                    break;
            }
        }

        private void AddPuff(SimEvent simEvent, Color color, float size, float seconds)
        {
            Transform puff = Props.Part(transform, Props.Sphere, surfaceMaterial, ToWorld(simEvent.X, simEvent.Y, HexHeight + 0.3f), Vector3.zero, color);
            puff.name = "Puff";
            _effects.Add(new Effect { Root = puff, StartAt = Time.unscaledTime, Seconds = seconds, Size = size });
        }

        /// <summary>The fallen tip over, sink into the ground and are gone.</summary>
        private void DrawFallen()
        {
            float now = Time.unscaledTime;
            for (int index = _fallen.Count - 1; index >= 0; index -= 1)
            {
                Effect effect = _fallen[index];
                float t = (now - effect.StartAt) / effect.Seconds;
                if (t >= 1f)
                {
                    Destroy(effect.Root.gameObject);
                    _fallen.RemoveAt(index);

                    continue;
                }

                float tip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
                Vector3 euler = effect.Root.localEulerAngles;
                effect.Root.localRotation = Quaternion.Euler(0f, euler.y, tip * 88f);
                float sink = Mathf.Clamp01((t - 0.45f) / 0.55f);
                Vector3 position = effect.Root.localPosition;
                effect.Root.localPosition = new Vector3(position.x, position.y - sink * 0.012f, position.z);
                effect.Root.localScale = Vector3.one * (1f - sink * 0.5f);
            }
        }

        private void DrawEffects()
        {
            float now = Time.unscaledTime;
            for (int index = _effects.Count - 1; index >= 0; index -= 1)
            {
                Effect effect = _effects[index];
                float t = (now - effect.StartAt) / effect.Seconds;
                if (t >= 1f)
                {
                    Destroy(effect.Root.gameObject);
                    _effects.RemoveAt(index);

                    continue;
                }

                if (effect.IsFalling)
                {
                    // The hex drops away under gravity, turning and shrinking as it goes.
                    Vector3 position = effect.Root.localPosition;
                    position.y = -9f * t * t;
                    effect.Root.localPosition = position;
                    effect.Root.localRotation = Quaternion.Euler(t * 70f, 0f, t * 40f);
                    effect.Root.localScale = new Vector3(HexInset, HexHeight, HexInset) * (1f - t * 0.6f);
                }
                else
                {
                    // The material is opaque, so a puff swells fast and then shrinks to nothing.
                    float swell = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                    effect.Root.localScale = Vector3.one * effect.Size * swell;
                }
            }
        }

        /* ---------- the border ---------- */

        /// <summary>The band of plumes along the map border, and the rocks standing in it.</summary>
        private void BuildBorder()
        {
            GameObject root = new GameObject("Border");
            root.transform.SetParent(transform, false);
            _border = root.transform;

            float halfWidth = (float)_sim.Bounds.HalfWidth * Scale;
            float halfHeight = (float)_sim.Bounds.HalfHeight * Scale;
            float depth = (float)_sim.Border.Depth * Scale;
            float thickness = 0.5f;
            Vector3 low = Vector3.down * 0.15f;

            // Top and bottom run the whole width; left and right fill what is between them.
            Props.Part(_border, Props.Cube, surfaceMaterial, low + new Vector3(0f, 0f, halfHeight - depth * 0.5f), new Vector3(halfWidth * 2f, thickness, depth), PlumeColor);
            Props.Part(_border, Props.Cube, surfaceMaterial, low + new Vector3(0f, 0f, -halfHeight + depth * 0.5f), new Vector3(halfWidth * 2f, thickness, depth), PlumeColor);
            Props.Part(_border, Props.Cube, surfaceMaterial, low + new Vector3(halfWidth - depth * 0.5f, 0f, 0f), new Vector3(depth, thickness, (halfHeight - depth) * 2f), PlumeColor * 0.92f);
            Props.Part(_border, Props.Cube, surfaceMaterial, low + new Vector3(-halfWidth + depth * 0.5f, 0f, 0f), new Vector3(depth, thickness, (halfHeight - depth) * 2f), PlumeColor * 0.92f);

            foreach (BorderRock rock in _sim.Border.Rocks)
            {
                float radius = (float)rock.Radius * Scale;
                Transform cone = Props.Part(_border, HexMeshes.Cone, surfaceMaterial, ToWorld(rock.X, rock.Y, -0.3f), new Vector3(radius, radius * (1.3f + (float)rock.Seed), radius), RockColor * (0.85f + (float)rock.Seed * 0.3f));
                cone.localRotation = Quaternion.Euler(0f, (float)rock.Seed * 360f, 0f);
            }
        }

        /// <summary>An open window is a clear strip across the plumes. It turns amber before it closes.</summary>
        private void DrawWindows()
        {
            _seen.Clear();
            float depth = (float)_sim.Border.Depth;

            foreach (BorderWindow window in _sim.Border.Windows)
            {
                if (!window.IsOpen(_sim.Tick))
                {
                    continue;
                }

                _seen.Add(window.Id);
                if (!_windows.TryGetValue(window.Id, out Transform visual))
                {
                    CleanupBorder.SidePoint(_sim.Bounds, window.Side, window.At, depth * 0.5, out double x, out double y);
                    bool isHorizontal = window.Side % 2 == 0;
                    float along = (float)window.Half * 2f * Scale;
                    float across = depth * Scale * 1.02f;
                    visual = Props.Part(_border, Props.Cube, surfaceMaterial, ToWorld(x, y, -0.1f), new Vector3(isHorizontal ? along : across, 0.56f, isHorizontal ? across : along), WindowColor);
                    visual.name = "Window " + window.Id;
                    _windows[window.Id] = visual;
                }

                Props.Paint(visual.GetComponent<MeshRenderer>(), window.IsClosing(_sim.Tick, CleanupSim.TickHz) ? WindowClosingColor : WindowColor);
            }

            _gone.Clear();
            foreach (int id in _windows.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (int id in _gone)
            {
                Destroy(_windows[id].gameObject);
                _windows.Remove(id);
            }
        }
    }
}
