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
        /// <summary>The top of a hex that breaks up: the event does not say what grew on it.</summary>
        private static readonly Color CrumbleColor = new Color(0.42f, 0.47f, 0.3f);
        private static readonly Color EnemyColor = new Color(0.72f, 0.2f, 0.18f);
        /// <summary>The player's army wears one strong colour, so it reads against any biome.</summary>
        private static readonly Color ArmyColor = new Color(0.2f, 0.4f, 0.95f);
        private static readonly Color TargetColor = new Color(1f, 0.25f, 0.2f);

        [SerializeField] private Material surfaceMaterial;

        private readonly List<IslandVisual> _islands = new List<IslandVisual>();
        private readonly Dictionary<int, UnitVisual> _units = new Dictionary<int, UnitVisual>();
        private readonly List<Transform> _shots = new List<Transform>();
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
        private DebrisField _debris;
        private const float TurnDegreesPerSecond = 540f;

        private static readonly Dictionary<string, GameObject> UnitPrefabs = new Dictionary<string, GameObject>();

        private CleanupSim _sim;
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

            BuildFog();
            _debris = new DebrisField(transform, surfaceMaterial);
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
            _debris = null;

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
            _debris?.Step(Time.unscaledDeltaTime);
            DrawFallen();
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
                Material ground = Props.Ground != null ? Props.Ground : surfaceMaterial;
                top.AddComponent<MeshFilter>().sharedMesh = HexMeshes.Tile;
                MeshRenderer topRenderer = top.AddComponent<MeshRenderer>();
                topRenderer.sharedMaterials = new[] { ground, ground };
                top.AddComponent<MeshCollider>().sharedMesh = HexMeshes.Prism;
                BattleHexHandle handle = top.AddComponent<BattleHexHandle>();
                handle.Island = island.Index;
                handle.Hex = index;
                visual.Tops.Add(topRenderer);

                Transform rock = Props.Part(cell.transform, HexMeshes.Root, ground, Vector3.zero, new Vector3(HexInset, 0.7f + (float)rng.Next() * 1.3f, HexInset), RootColor * (0.85f + (float)rng.Next() * 0.3f));
                rock.name = "Root";

                // What still stands on the player's island: the stronghold and the buildings.
                if (island.Index == 0 && index < _sim.StructureHpByHex.Length && hex.MaxHp > 0)
                {
                    StructureKind kind = hex.Stronghold ? StructureKind.Stronghold : StructureHp.ToKind(hex.Building.Value);
                    double hp = _sim.StructureHpByHex[index];
                    GameObject structure = Props.CreateStructure(kind, surfaceMaterial, cell.transform);
                    structure.transform.localPosition = Vector3.up * HexHeight;
                    if (hp <= 0)
                    {
                        // What was razed stays as its ruin: broken walls and a heap of rubble.
                        Props.MakeRuin(structure, surfaceMaterial);
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

            return new UnitVisual { Root = t, Model = model, Pose = model != null ? new FigurePose(model, unit.Id, unit.Stats.Key) : null, Bar = bar, BarWidth = unit.IsStructure ? 0.9f : 0.55f, Height = height, Yaw = isPlayer ? 180f : 0f };
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
        /// carried off, a burst where a building was razed, and a hex that crumbles
        /// and falls.
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
                    _debris?.Collapse(ToWorld(simEvent.X, simEvent.Y, HexHeight));
                    break;
                case SimEventType.Ferry:
                    AddPuff(simEvent, new Color(0.9f, 0.9f, 1f), 0.4f, 0.3f);
                    break;
                case SimEventType.Crumble:
                    // The hex splits into its six wedges and falls away with the rock under it.
                    _debris?.Shatter(ToWorld(simEvent.X, simEvent.Y), HexHeight, CrumbleColor);
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

        /* ---------- the fog ---------- */

        /// <summary>
        /// One sheet of the fog: a flat ring round the clearing at its own height.
        /// It thickens from <see cref="From"/> to <see cref="To"/>, measured in the
        /// sim's units from the edge of the clearing outward, up to <see cref="Thickness"/>.
        /// </summary>
        private readonly struct FogSheet
        {
            public readonly float Height;
            public readonly float From;
            public readonly float To;
            public readonly float Thickness;

            public FogSheet(float height, float from, float to, float thickness)
            {
                Height = height;
                From = from;
                To = to;
                Thickness = thickness;
            }
        }

        /// <summary>
        /// The sheets lie one over another, so an island sinks into the fog by
        /// degrees. The lowest runs under the hex tops and creeps into the clearing
        /// as a mist; the highest lies over the roofs and starts a little way in.
        /// </summary>
        private static readonly FogSheet[] FogSheets =
        {
            new FogSheet(0.08f, -340f, 380f, 0.92f),
            new FogSheet(1.0f, -90f, 620f, 0.55f),
            new FogSheet(2.2f, 70f, (float)CleanupFog.Depth, 0.72f),
        };

        /// <summary>Where the rings of a sheet lie, from the edge of the clearing outward.</summary>
        private static readonly float[] FogRings = { -340f, -210f, -90f, 70f, 220f, 380f, 620f, (float)CleanupFog.Depth, (float)CleanupFog.Depth + 420f, 16000f };
        private const int FogSegments = 144;

        private static Material _fogMaterial;

        private static Material FogMaterial
        {
            get
            {
                if (_fogMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>("Shaders/HexwexFog");
                    _fogMaterial = shader != null ? new Material(shader) { name = "M_Fog" } : null;
                }

                return _fogMaterial;
            }
        }

        /// <summary>
        /// The fog round the clearing. It follows the edge the sim keeps, so what
        /// looks like fog is fog: whoever is under it is hidden. Past the depth at
        /// which an island leaves the battle every sheet is solid.
        /// </summary>
        private void BuildFog()
        {
            if (FogMaterial == null)
            {
                Debug.LogWarning("Hexwex: the fog shader is missing from Resources/Shaders; the fog is not drawn.");

                return;
            }

            GameObject root = new GameObject("Fog");
            root.transform.SetParent(transform, false);

            for (int index = 0; index < FogSheets.Length; index += 1)
            {
                GameObject sheet = new GameObject("Sheet " + index);
                sheet.transform.SetParent(root.transform, false);
                sheet.AddComponent<MeshFilter>().sharedMesh = FogMesh(FogSheets[index], index);
                MeshRenderer sheetRenderer = sheet.AddComponent<MeshRenderer>();
                sheetRenderer.sharedMaterial = FogMaterial;
                sheetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sheetRenderer.receiveShadows = false;
                // The higher sheet is drawn over the lower one.
                sheetRenderer.sortingOrder = index;
            }
        }

        private Mesh FogMesh(FogSheet sheet, int index)
        {
            CleanupFog fog = _sim.Fog;
            int across = FogRings.Length;
            Vector3[] vertices = new Vector3[(FogSegments + 1) * across];
            Color[] colors = new Color[vertices.Length];
            Vector2[] sheets = new Vector2[vertices.Length];
            int[] triangles = new int[FogSegments * (across - 1) * 6];

            for (int segment = 0; segment <= FogSegments; segment += 1)
            {
                double angle = segment % FogSegments * System.Math.PI * 2 / FogSegments;
                double edge = fog.EdgeAt(angle);

                for (int ring = 0; ring < across; ring += 1)
                {
                    float depth = FogRings[ring];
                    double reach = edge + depth;
                    int vertex = segment * across + ring;
                    vertices[vertex] = ToWorld(fog.CenterX + System.Math.Cos(angle) * reach, fog.CenterY + System.Math.Sin(angle) * reach, sheet.Height);

                    float thick = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sheet.From, sheet.To, depth)) * sheet.Thickness;
                    // Beyond the way out nothing shows through.
                    float solid = Mathf.InverseLerp((float)CleanupFog.Depth, (float)CleanupFog.Depth + 420f, depth);
                    colors[vertex] = new Color(1f, 1f, 1f, Mathf.Lerp(thick, 1f, solid));
                    sheets[vertex] = new Vector2(index, 0f);
                }
            }

            int next = 0;
            for (int segment = 0; segment < FogSegments; segment += 1)
            {
                for (int ring = 0; ring < across - 1; ring += 1)
                {
                    int a = segment * across + ring;
                    int b = a + across;
                    triangles[next++] = a;
                    triangles[next++] = a + 1;
                    triangles[next++] = b;
                    triangles[next++] = b;
                    triangles[next++] = a + 1;
                    triangles[next++] = b + 1;
                }
            }

            Mesh mesh = new Mesh { name = "Fog sheet " + index };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = sheets;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
