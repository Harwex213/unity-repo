using System.Collections.Generic;
using System.Text;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// The life of the island between the turns: the people who work its
    /// buildings and the animals of its wild hexes. The prototype draws none of
    /// it; nothing here changes the game.
    ///
    /// Every staffed building sends its worker out to the hex it takes from: the
    /// lumberjack to the forest, the miner to the rock, the farmer to the field.
    /// He works there a while and walks back. A building without its worker sends
    /// nobody. The soldiers of the stronghold walk its walls. The wild hexes hold
    /// the animals of their biome, and gulls wheel over the whole island.
    ///
    /// Everything is seeded by the hex, so an island looks the same each time it
    /// is drawn. The figures come from <c>Resources/Units</c> and are moved by
    /// <see cref="FigurePose"/>, as the units of a battle are.
    /// </summary>
    public sealed class IslandLife : MonoBehaviour
    {
        private const float WorkerSize = 0.62f;
        private const float AnimalSize = 0.6f;
        private const float WalkSpeed = 0.42f;
        private const int MaxAnimals = 16;
        private const int MaxSoldiers = 3;
        private const int GullCount = 3;

        private enum Role
        {
            Worker,
            Grazer,
            Sentry,
            Flyer,
        }

        private enum State
        {
            ToWork,
            Working,
            ToHome,
            Resting,
        }

        private sealed class Critter
        {
            public Role Role;
            public Transform Root;
            public FigurePose Pose;
            public State State;
            public float Timer;
            public float Speed;
            public float Yaw;
            public bool Swings;
            public System.Random Dice;

            // A worker walks between these two hexes.
            public Vector3 Home;
            public Vector3 WorkHex;
            public Vector3 Target;

            // A grazer keeps to a ring about this point; a sentry walks the corners of it.
            public Vector3 Center;
            public float InnerRadius;
            public float OuterRadius;
            public int Corner;

            // A flyer wheels about the island's middle.
            public float Angle;
            public float AngularSpeed;
            public float Height;
        }

        private readonly List<Critter> _critters = new List<Critter>();
        private string _signature;
        private IslandView _view;

        /// <summary>One world unit of the island: a hex is two of them corner to corner.</summary>
        private float Unit
        {
            get { return transform.lossyScale.x; }
        }

        /// <summary>
        /// Brings the life up to date with the island. It is rebuilt only when what
        /// it depends on changed: the hexes, what stands on them and who works there.
        /// </summary>
        public void Sync(Player player, IslandView view)
        {
            _view = view;
            string signature = Signature(player);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            Clear();
            Populate(player);
        }

        private static string Signature(Player player)
        {
            StringBuilder text = new StringBuilder(player.Id).Append('|').Append(player.StrongholdHexId).Append('|').Append(Mathf.Min(player.Army, MaxSoldiers));
            foreach (HexTile hex in player.Hexes)
            {
                text.Append(';').Append(hex.Id).Append(':').Append(hex.Biome).Append(':').Append(hex.Building).Append(':').Append(Population.IsStaffed(hex) ? 1 : 0);
            }

            return text.ToString();
        }

        private void Clear()
        {
            foreach (Critter critter in _critters)
            {
                if (critter.Root != null)
                {
                    Destroy(critter.Root.gameObject);
                }
            }

            _critters.Clear();
        }

        private void Populate(Player player)
        {
            Dictionary<string, HexTile> byId = new Dictionary<string, HexTile>();
            foreach (HexTile hex in player.Hexes)
            {
                byId[hex.Id] = hex;
            }

            int animals = 0;
            foreach (HexTile hex in player.Hexes)
            {
                if (!_view.TryGetHexTop(hex.Id, out Vector3 top))
                {
                    continue;
                }

                System.Random dice = new System.Random(unchecked((int)Rng.HashSeed("life:" + player.Id + ":" + hex.Id)));

                if (hex.Id == player.StrongholdHexId)
                {
                    for (int index = 0; index < Mathf.Min(player.Army, MaxSoldiers); index += 1)
                    {
                        AddSentry(top, index, dice);
                    }

                    continue;
                }

                if (hex.Building.HasValue)
                {
                    if (Population.IsStaffed(hex))
                    {
                        AddWorker(player, hex, top, byId, dice);
                    }

                    // The farm keeps a few beasts in its fields.
                    if (hex.Building == BuildingId.Farm && animals < MaxAnimals)
                    {
                        string beast = dice.NextDouble() < 0.5 ? "cow" : "sheep";
                        animals += AddHerd(beast, top, 0.62f, 0.86f, beast == "cow" ? 1 : 2, dice);
                    }

                    continue;
                }

                if (animals < MaxAnimals && dice.NextDouble() < 0.5)
                {
                    WildOf(hex.Biome, dice, out string beast, out int count);
                    if (beast != null)
                    {
                        animals += AddHerd(beast, top, 0.12f, 0.62f, count, dice);
                    }
                }
            }

            AddGulls(player);
        }

        /// <summary>Which animals live on a wild hex of the biome, and how many go together.</summary>
        private static void WildOf(BiomeId biome, System.Random dice, out string beast, out int count)
        {
            bool first = dice.NextDouble() < 0.6;
            switch (biome)
            {
                case BiomeId.Taiga:
                    beast = first ? "deer" : "fox";
                    count = first ? 2 : 1;
                    break;
                case BiomeId.Forrest:
                    beast = first ? "deer" : "boar";
                    count = first ? 2 : 1;
                    break;
                case BiomeId.Rainforest:
                case BiomeId.Swamp:
                    beast = "boar";
                    count = 1;
                    break;
                case BiomeId.Grassland:
                    beast = first ? "sheep" : "deer";
                    count = first ? 3 : 1;
                    break;
                case BiomeId.Plains:
                    beast = first ? "cow" : "sheep";
                    count = 2;
                    break;
                case BiomeId.Savanna:
                case BiomeId.Tundra:
                    beast = first ? "deer" : "fox";
                    count = first ? 2 : 1;
                    break;
                case BiomeId.Hills:
                    beast = first ? "goat" : "sheep";
                    count = 2;
                    break;
                case BiomeId.Mountains:
                case BiomeId.Cliffs:
                case BiomeId.Badlands:
                    beast = "goat";
                    count = first ? 2 : 1;
                    break;
                case BiomeId.Desert:
                    beast = "camel";
                    count = 1;
                    break;
                case BiomeId.PolarDesert:
                    beast = "fox";
                    count = 1;
                    break;
                default:
                    // Volcano and crater: nothing lives there.
                    beast = null;
                    count = 0;
                    break;
            }
        }

        /// <summary>Who works the building, and the biomes he likes to work in.</summary>
        private static string WorkerOf(BuildingId building, out BiomeId[] prefers, out bool swings)
        {
            swings = true;
            switch (building)
            {
                case BuildingId.Farm:
                    prefers = new[] { BiomeId.Grassland, BiomeId.Plains, BiomeId.Savanna };
                    return "farmer";
                case BuildingId.Mine:
                    prefers = new[] { BiomeId.Mountains, BiomeId.Hills, BiomeId.Cliffs, BiomeId.Badlands, BiomeId.Crater };
                    return "miner";
                case BuildingId.Sawmill:
                    prefers = new[] { BiomeId.Taiga, BiomeId.Forrest, BiomeId.Rainforest };
                    return "lumberjack";
                case BuildingId.MasonsGuild:
                    prefers = new[] { BiomeId.Mountains, BiomeId.Hills, BiomeId.Cliffs };
                    return "mason";
                case BuildingId.Village:
                    prefers = new BiomeId[0];
                    swings = false;
                    return "porter";
                default:
                    prefers = new BiomeId[0];
                    swings = false;
                    return "scholar";
            }
        }

        private void AddWorker(Player player, HexTile hex, Vector3 top, Dictionary<string, HexTile> byId, System.Random dice)
        {
            string key = WorkerOf(hex.Building.Value, out BiomeId[] prefers, out bool swings);

            // The porter and the scholar carry things to the stronghold; the others go to the wild hex they take from.
            HexTile goal = null;
            if (!swings && player.StrongholdHexId != null && byId.TryGetValue(player.StrongholdHexId, out HexTile stronghold))
            {
                goal = stronghold;
            }
            else
            {
                List<HexTile> near = new List<HexTile>();
                foreach (Axial axial in HexMath.Neighbors(hex.Q, hex.R))
                {
                    if (byId.TryGetValue(HexMath.HexId(axial.Q, axial.R), out HexTile neighbor) && !neighbor.Building.HasValue && neighbor.Id != player.StrongholdHexId)
                    {
                        near.Add(neighbor);
                    }
                }

                List<HexTile> liked = near.FindAll(candidate => System.Array.IndexOf(prefers, candidate.Biome) >= 0);
                List<HexTile> pool = liked.Count > 0 ? liked : near;
                goal = pool.Count > 0 ? pool[dice.Next(pool.Count)] : null;
            }

            Vector3 work = top;
            if (goal != null && _view.TryGetHexTop(goal.Id, out Vector3 goalTop))
            {
                work = goalTop;
            }

            // He leaves from the side of the building that faces his work.
            Vector3 toWork = work - top;
            toWork.y = 0f;
            Vector3 heading = toWork.sqrMagnitude > 0.0001f ? toWork.normalized : Quaternion.Euler(0f, dice.Next(360), 0f) * Vector3.forward;
            Critter critter = Spawn(key, WorkerSize, top + heading * 0.6f * Unit, Role.Worker, dice);
            if (critter == null)
            {
                return;
            }

            critter.Home = top + heading * 0.6f * Unit;
            critter.WorkHex = goal != null ? work : top + heading * 0.75f * Unit;
            critter.Swings = swings;
            critter.Speed = WalkSpeed * (0.85f + (float)dice.NextDouble() * 0.3f);
            // The workers do not all leave together.
            critter.State = State.Resting;
            critter.Timer = (float)dice.NextDouble() * 4f;
        }

        private int AddHerd(string beast, Vector3 center, float inner, float outer, int count, System.Random dice)
        {
            int made = 0;
            for (int index = 0; index < count; index += 1)
            {
                Vector3 at = RingPoint(center, inner, outer, dice);
                Critter critter = Spawn(beast, AnimalSize, at, Role.Grazer, dice);
                if (critter == null)
                {
                    continue;
                }

                critter.Center = center;
                critter.InnerRadius = inner;
                critter.OuterRadius = outer;
                critter.Speed = (beast == "fox" ? 0.36f : beast == "deer" ? 0.26f : 0.16f) * (0.85f + (float)dice.NextDouble() * 0.3f);
                critter.State = State.Resting;
                critter.Timer = (float)dice.NextDouble() * 5f;
                critter.Yaw = (float)dice.NextDouble() * 360f;
                made += 1;
            }

            return made;
        }

        private void AddSentry(Vector3 center, int index, System.Random dice)
        {
            Critter critter = Spawn(Units.ArmyUnit, WorkerSize, center + Corner(index * 2) * 0.8f * Unit, Role.Sentry, dice);
            if (critter == null)
            {
                return;
            }

            critter.Center = center;
            critter.Corner = index * 2;
            critter.Speed = WalkSpeed * 0.7f;
            critter.State = State.Resting;
            critter.Timer = 1f + index * 1.5f;
        }

        private void AddGulls(Player player)
        {
            Vector3 middle = Vector3.zero;
            float highest = float.MinValue;
            int count = 0;
            foreach (HexTile hex in player.Hexes)
            {
                if (_view.TryGetHexTop(hex.Id, out Vector3 top))
                {
                    middle += top;
                    highest = Mathf.Max(highest, top.y);
                    count += 1;
                }
            }

            if (count == 0)
            {
                return;
            }

            middle /= count;
            System.Random dice = new System.Random(unchecked((int)Rng.HashSeed("gulls:" + player.Id)));
            for (int index = 0; index < GullCount; index += 1)
            {
                Critter gull = Spawn("gull", 0.4f, middle, Role.Flyer, dice);
                if (gull == null)
                {
                    continue;
                }

                gull.Center = middle;
                gull.OuterRadius = (2.6f + (float)dice.NextDouble() * 2.2f) * Unit;
                gull.Height = highest + (2.2f + (float)dice.NextDouble() * 1.4f) * Unit;
                gull.Angle = (float)dice.NextDouble() * Mathf.PI * 2f;
                gull.AngularSpeed = (0.16f + (float)dice.NextDouble() * 0.12f) * (index % 2 == 0 ? 1f : -1f);
            }
        }

        private Critter Spawn(string key, float size, Vector3 at, Role role, System.Random dice)
        {
            GameObject prefab = Resources.Load<GameObject>("Units/" + key);
            if (prefab == null)
            {
                return null;
            }

            GameObject root = new GameObject(key);
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * size;
            Transform model = Instantiate(prefab, root.transform, false).transform;
            model.name = "Model";

            Critter critter = new Critter
            {
                Role = role,
                Root = root.transform,
                Pose = new FigurePose(model, dice.Next(1000), key),
                Dice = dice,
            };
            _critters.Add(critter);

            return critter;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (Critter critter in _critters)
            {
                if (critter.Root == null)
                {
                    continue;
                }

                switch (critter.Role)
                {
                    case Role.Worker:
                        StepWorker(critter, deltaTime);
                        break;
                    case Role.Grazer:
                        StepGrazer(critter, deltaTime);
                        break;
                    case Role.Sentry:
                        StepSentry(critter, deltaTime);
                        break;
                    default:
                        StepFlyer(critter, deltaTime);
                        break;
                }
            }
        }

        /// <summary>Out to the work, a spell of work, back to the building, a rest, and out again.</summary>
        private void StepWorker(Critter critter, float deltaTime)
        {
            float strike = -1f;
            float stride = 0f;
            switch (critter.State)
            {
                case State.ToWork:
                case State.ToHome:
                    stride = 1f;
                    if (WalkTo(critter, critter.Target, deltaTime))
                    {
                        critter.State = critter.State == State.ToWork ? State.Working : State.Resting;
                        critter.Timer = critter.State == State.Working ? 3f + (float)critter.Dice.NextDouble() * 3f : 1.5f + (float)critter.Dice.NextDouble() * 2f;
                    }

                    break;
                case State.Working:
                    critter.Timer -= deltaTime;
                    // A tool comes down again and again; a porter sets his load down and a scholar looks about.
                    strike = critter.Swings ? Time.time * 0.9f % 1f : -1f;
                    if (critter.Timer <= 0f)
                    {
                        critter.State = State.ToHome;
                        critter.Target = critter.Home;
                    }

                    break;
                default:
                    critter.Timer -= deltaTime;
                    if (critter.Timer <= 0f)
                    {
                        critter.State = State.ToWork;
                        critter.Target = RingPoint(critter.WorkHex, 0f, 0.45f, critter.Dice);
                    }

                    break;
            }

            critter.Pose.Apply(stride, strike, false, critter.Yaw, deltaTime);
        }

        /// <summary>A grazer stands and grazes, then ambles to another spot of its ground.</summary>
        private void StepGrazer(Critter critter, float deltaTime)
        {
            float stride = 0f;
            if (critter.State == State.ToWork)
            {
                stride = 0.6f;
                if (WalkTo(critter, critter.Target, deltaTime))
                {
                    critter.State = State.Resting;
                    critter.Timer = 2f + (float)critter.Dice.NextDouble() * 5f;
                }
            }
            else
            {
                critter.Timer -= deltaTime;
                if (critter.Timer <= 0f)
                {
                    critter.State = State.ToWork;
                    critter.Target = RingPoint(critter.Center, critter.InnerRadius, critter.OuterRadius, critter.Dice);
                }
            }

            critter.Pose.Apply(stride, -1f, false, critter.Yaw, deltaTime);
        }

        /// <summary>A soldier walks the stronghold's walls from corner to corner, and stands a moment at each.</summary>
        private void StepSentry(Critter critter, float deltaTime)
        {
            float stride = 0f;
            if (critter.State == State.ToWork)
            {
                stride = 0.8f;
                if (WalkTo(critter, critter.Target, deltaTime))
                {
                    critter.State = State.Resting;
                    critter.Timer = 1.2f + (float)critter.Dice.NextDouble() * 1.5f;
                }
            }
            else
            {
                critter.Timer -= deltaTime;
                if (critter.Timer <= 0f)
                {
                    critter.Corner = (critter.Corner + 1) % 6;
                    critter.State = State.ToWork;
                    critter.Target = critter.Center + Corner(critter.Corner) * 0.8f * Unit;
                }
            }

            critter.Pose.Apply(stride, -1f, false, critter.Yaw, deltaTime);
        }

        /// <summary>A gull wheels round the island, rising and sinking a little on the wind.</summary>
        private void StepFlyer(Critter critter, float deltaTime)
        {
            critter.Angle += critter.AngularSpeed * deltaTime;
            Vector3 offset = new Vector3(Mathf.Cos(critter.Angle), 0f, Mathf.Sin(critter.Angle) * 0.75f) * critter.OuterRadius;
            Vector3 at = critter.Center + offset;
            at.y = critter.Height + Mathf.Sin(critter.Angle * 3f) * 0.25f * Unit;
            Vector3 travel = at - critter.Root.position;
            travel.y = 0f;
            if (travel.sqrMagnitude > 0.000001f)
            {
                critter.Yaw = Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;
            }

            critter.Root.position = at;
            critter.Pose.Apply(1f, -1f, false, critter.Yaw, deltaTime);
        }

        /// <summary>Walks toward the point over the ground. Returns <c>true</c> on arriving.</summary>
        private bool WalkTo(Critter critter, Vector3 target, float deltaTime)
        {
            Vector3 position = critter.Root.position;
            Vector3 flat = target - position;
            flat.y = 0f;
            float distance = flat.magnitude;
            float step = critter.Speed * Unit * deltaTime;
            if (distance <= step)
            {
                position.x = target.x;
                position.z = target.z;
                critter.Root.position = OnGround(position);

                return true;
            }

            Vector3 direction = flat / distance;
            // It turns toward where it goes rather than snapping round.
            float wanted = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            critter.Yaw = Mathf.MoveTowardsAngle(critter.Yaw, wanted, 360f * deltaTime);
            critter.Root.position = OnGround(position + direction * step);

            return false;
        }

        /// <summary>The point dropped onto the top of the hex under it. Over a gap it keeps its height.</summary>
        private static Vector3 OnGround(Vector3 point)
        {
            Vector3 from = point + Vector3.up * 20f;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 60f);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponent<HexHandle>() != null && hit.point.y > best)
                {
                    best = hit.point.y;
                }
            }

            if (best > float.MinValue)
            {
                point.y = best;
            }

            return point;
        }

        private Vector3 RingPoint(Vector3 center, float inner, float outer, System.Random dice)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f;
            float reach = Mathf.Lerp(inner, outer, Mathf.Sqrt((float)dice.NextDouble())) * Unit;

            return OnGround(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach);
        }

        /// <summary>A corner of a pointy-topped hexagon of radius 1.</summary>
        private static Vector3 Corner(int index)
        {
            float angle = (30f + index * 60f) * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
