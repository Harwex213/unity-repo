using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    public enum SimStatus
    {
        Running,
        Won,
        Lost,
        Retreated,
    }

    /// <summary>
    /// Active: guarded by monsters. Cleared: an empty husk that floats free until
    /// the player docks it. Attached: it joined the player's island. Lost: it
    /// drifted off before anyone docked it.
    /// </summary>
    public enum IslandState
    {
        Active,
        Cleared,
        Attached,
        Lost,
    }

    public sealed class SimIsland
    {
        public int Index;
        public string Id;
        public string Label;
        public CleanupSide Side;
        public IslandBehavior Behavior;
        /// <summary>The player's island grows when another island joins it.</summary>
        public List<CleanupHex> Hexes;
        public Body Body;
        /// <summary>The graph node of each hex, by hex index.</summary>
        public List<int> Nodes;
        public double HomeX;
        public double HomeY;
        public double WobblePhase;
        public int GarrisonTotal;
        /// <summary>The body position one tick ago, for the view to interpolate.</summary>
        public double Px;
        public double Py;
        public IslandState State;
        public int StateTick;
        /// <summary>How many hexes joined the player's island, once attached.</summary>
        public int Joined;
        /// <summary>The hexes' outer edges relative to the body position.</summary>
        public double MinX;
        public double MaxX;
        public double MinY;
        public double MaxY;
        /// <summary>Grows by one every time the hex set changes, so the view builds the island again.</summary>
        public int Shape;

        public bool IsGone
        {
            get { return State == IslandState.Attached || State == IslandState.Lost; }
        }

        public double CenterX
        {
            get { return Body.X + Body.CenterX; }
        }

        public double CenterY
        {
            get { return Body.Y + Body.CenterY; }
        }
    }

    public sealed class SimUnit
    {
        public int Id;
        public CleanupSide Side;
        public Combatant Stats;
        /// <summary>What the structure is, for a building or the stronghold. <c>null</c> for a unit.</summary>
        public StructureKind? Structure;
        /// <summary>
        /// The hex index on the player's island for a building or the stronghold,
        /// -1 for a unit. A structure never moves; only the stronghold shoots.
        /// </summary>
        public int StructureHex = -1;
        public int MaxHp;
        public double Damage;
        public int HomeIsland;
        /// <summary>Offset from the hex centre, so units on one hex do not stack.</summary>
        public double SlotX;
        public double SlotY;
        public double Hp;
        public bool Alive = true;
        /// <summary>Ground units: the hex node they stand on. -1 for a flyer.</summary>
        public int Node;
        /// <summary>The node the unit walks to, or -1.</summary>
        public int NextNode = -1;
        /// <summary>Ground units: position relative to the island of <see cref="Node"/>.</summary>
        public double Lx;
        public double Ly;
        public double X;
        public double Y;
        public double Px;
        public double Py;
        public double Vx;
        public double Vy;
        public double Cooldown;
        public int LastHurtTick = -1000;
        public int LastAttackTick = -1000;
        /// <summary>Unit vector toward the last target, for the attack lunge.</summary>
        public double AimX = 1;
        public double AimY;
        public List<int> IdlePath = new List<int>();
        public double IdleWait;
        /// <summary>Flyers: the node under them, or -1 over water.</summary>
        public int HoverNode = -1;

        public bool IsStructure
        {
            get { return StructureHex >= 0; }
        }
    }

    public sealed class SimProjectile
    {
        public Projectile Kind;
        public CleanupSide Side;
        public int TargetId;
        public double Damage;
        public double FromX;
        public double FromY;
        public double Duration;
        public double Age;
        public double TargetX;
        public double TargetY;
        public double X;
        public double Y;
        public double Px;
        public double Py;
        public bool Done;
    }

    public enum SimEventType
    {
        Hit,
        Death,
        Ferry,
        Razed,
        Cleared,
        Attached,
        Lost,
        /// <summary>A hex of the player's island choked in the border plumes.</summary>
        Poison,
        /// <summary>A hex was destroyed.</summary>
        Crumble,
    }

    public struct SimEvent
    {
        public SimEventType Type;
        public double X;
        public double Y;
        public int Amount;
        public CleanupSide Side;
        /// <summary>The island of a cleared, attached, lost or crumble event.</summary>
        public int Island;
        /// <summary>The hexes that joined, for an attached event.</summary>
        public int Joined;
    }

    public enum CleanupOutcome
    {
        Won,
        Lost,
        Retreated,
        /// <summary>A level with no enemy islands is a calm sea.</summary>
        Calm,
    }

    public sealed class StructureResult
    {
        public string HexId;
        public int Hp;
        public int StartHp;
        public int MaxHp;
    }

    public sealed class CleanupResult
    {
        public CleanupOutcome Outcome;
        public int TotalIslands;
        public int ClearedIslands;
        /// <summary>Cleared islands that joined the player's island.</summary>
        public int AttachedIslands;
        public List<AnnexedHex> Annexed;
        public List<Unit> Survivors;
        public List<Unit> Lost;
        public int Kills;
        /// <summary>Every structure of the player's island after the battle.</summary>
        public List<StructureResult> Structures;
        public int Razed;
        /// <summary>Hexes of the player's island destroyed in battle, by id. They leave the island.</summary>
        public List<string> DestroyedHexIds;
        /// <summary>Hexes of the player's island that the border plumes poisoned, with their new toxicity.</summary>
        public Dictionary<string, int> Poisoned;
        public int ManaSpent;
    }

    /// <summary>
    /// The auto-battle of the cleanup phase (<c>core/cleanup-sim.ts</c>). The
    /// player steers their island; the units on every island find their own fights.
    ///
    /// The sim is deterministic: one seed, a fixed tick and a fixed update order
    /// give the same battle every time. Every moving thing keeps its position from
    /// the previous tick, so the view can interpolate between two ticks.
    ///
    /// All distances are in the prototype's canvas units: one hex is
    /// <see cref="HexMath.HexSize"/> in radius, x is right and y is down.
    /// </summary>
    public sealed class CleanupSim
    {
        public const int TickHz = 30;
        public const double TickSeconds = 1.0 / TickHz;

        private const double PlayerAcceleration = 420;
        private const double PlayerMaxSpeed = 170;
        private const double PlayerDragIdle = 1.8;
        private const double PlayerDragSteering = 0.5;
        private const double DriftMaxSpeed = 16;
        private const double DriftWobble = 60;
        private const double ApproachSpeed = 34;
        private const double ApproachRadius = 1100;
        /// <summary>Islands answer a change of desired velocity at this rate per second.</summary>
        private const double EnemyResponse = 0.8;
        /// <summary>The plumes push an island back inward this hard, per second squared. Stronger than the player's own push.</summary>
        private const double PlumePush = 560;
        /// <summary>The share of an island's outward speed that the plumes eat per second.</summary>
        private const double PlumeDamp = 6;
        /// <summary>The plumes poison the player's island in pulses this many seconds apart.</summary>
        private const double PlumePulseSeconds = 0.5;
        /// <summary>Toxicity, in percent, that one pulse adds to a hex in the plumes.</summary>
        private const int PlumeToxicity = 2;
        /// <summary>Share of max hp that one pulse takes from a unit or a building on such a hex.</summary>
        private const double PlumeUnitDamage = 0.04;
        private const double PlumeStructureDamage = 0.02;

        /// <summary>Melee reach against a flyer, and a flyer's own reach.</summary>
        private const double FlyerReach = 70;
        /// <summary>A flyer counts as over a hex this close to its centre.</summary>
        private const double HoverRadius = 60;
        private const double PlayerFlyerAggro = 480;
        private const double PlayerFlyerLeash = 700;
        private const double EnemyFlyerAggro = 460;
        private const double EnemyFlyerLeash = 900;
        private const double FlyerSeparation = 30;
        private const double FlyerSteering = 6;
        /// <summary>Ground monsters chase no farther than this many hex steps from where they stand.</summary>
        private const int EnemyChaseSteps = 8;
        /// <summary>A foreign island this close pulls idle units to the rim facing it.</summary>
        private const double RallyGap = 420;
        private const int RallyNodes = 5;
        private const double IdleWaitMin = 1.2;
        private const double IdleWaitSpread = 2.4;
        private const double IdlePace = 0.55;
        private const double RegenDelay = 3;
        private const double RegenSharePerSecond = 0.03;
        private const double DamageRoll = 0.3;

        /// <summary>A cleared island waits this long for the player to dock it, then starts to drift off.</summary>
        private const double DriftDelay = 10;
        private const double DriftAwaySpeed = 16;
        /// <summary>Seconds after clearing when an undocked island is gone for good.</summary>
        private const double LostAfter = 24;
        private const double LossDelay = 1.5;
        private const double WinDelay = 1;
        /// <summary>The event queue is drained by the view; this cap guards a stalled one.</summary>
        private const int MaxEvents = 400;

        private static readonly double HexStep = CleanupCollision.HexStep;

        public uint Seed;
        public int Tier;
        public readonly List<SimIsland> Islands = new List<SimIsland>();
        public List<SimUnit> Units = new List<SimUnit>();
        public List<SimProjectile> Projectiles = new List<SimProjectile>();
        public int Tick;
        public SimStatus Status = SimStatus.Running;
        public int Kills;
        /// <summary>Everything the player fielded, dead or alive.</summary>
        public List<Unit> Roster;
        public readonly List<Unit> Lost = new List<Unit>();
        public LevelBounds Bounds;
        /// <summary>The plumes, the rocks and the windows along the map border.</summary>
        public CleanupBorder Border;
        /// <summary>0..1: how far the player's island has sailed through an open window. 1 is out.</summary>
        public double ExitProgress;
        /// <summary>True while the player's island sits in the band of an open window.</summary>
        public bool InWindow;
        /// <summary>The player's mana: the pool at the start minus what the skill spent.</summary>
        public int Mana;
        public int ManaSpent;
        /// <summary>Structures standing when the battle began. Zero means defeat by ruin cannot happen.</summary>
        public int StructuresAtStart;
        /// <summary>Structures razed in this battle.</summary>
        public int Razed;
        /// <summary>Hp of each structure on the player's island, by hex index. -1 where nothing stands.</summary>
        public double[] StructureHpByHex;

        private readonly Rng _rng;
        private readonly List<Body> _bodies = new List<Body>();
        private readonly List<AnnexedHex> _annexed = new List<AnnexedHex>();
        private readonly List<string> _destroyedHexIds = new List<string>();
        private readonly Dictionary<string, int> _startToxicity = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _startHp = new Dictionary<string, int>();
        private readonly List<Bridge> _bridges = new List<Bridge>();
        private readonly List<PairDistance> _distances = new List<PairDistance>();
        private List<SimEvent> _events = new List<SimEvent>();
        private double _inputX;
        private double _inputY;
        /// <summary>Tick at which the end condition was first met, or -1.</summary>
        private int _endingTick = -1;
        private int _nextUnitId = 1;
        private int _skillReadyTick;

        /* The hex graph. Node n is hex NodeHex[n] of island NodeIsland[n]. */
        private int[] _nodeIsland;
        private int[] _nodeHex;
        private double[] _nodeX;
        private double[] _nodeY;
        /// <summary>Nodes of hexes destroyed in battle. They stay in the arrays but are never walked again.</summary>
        private bool[] _nodeGone;
        /// <summary>Neighbours inside one island. They change only when an island joins or a hex is destroyed.</summary>
        private readonly List<List<int>> _innerLinks = new List<List<int>>();
        /// <summary>Neighbours across a bridge, rebuilt every tick.</summary>
        private readonly List<List<int>> _bridgeLinks = new List<List<int>>();
        /// <summary>Smallest gap between each pair of islands, rebuilt every tick.</summary>
        private double[] _gaps;
        /// <summary>Hex steps to the nearest foe, per side, rebuilt every tick. -1 is unreachable.</summary>
        private int[] _flowToEnemy;
        private int[] _flowToPlayer;

        private CleanupSim(uint seed)
        {
            Seed = seed;
            _rng = new Rng(seed);
        }

        public SimIsland PlayerIsland
        {
            get { return Islands.Count > 0 ? Islands[0] : null; }
        }

        public int NodeCount
        {
            get { return _nodeIsland.Length; }
        }

        public static CleanupSim Create(LevelSpec level, uint seed)
        {
            CleanupSim sim = new CleanupSim(seed) { Tier = level.Tier, Roster = level.Roster, Bounds = level.Bounds, Mana = level.Mana };
            int nodeCount = 0;

            for (int index = 0; index < level.Islands.Count; index += 1)
            {
                IslandSpec spec = level.Islands[index];
                double mass = spec.Side == CleanupSide.Player ? Math.Max(12, spec.Hexes.Count) * 1.5 : spec.Hexes.Count;
                Body body = CreateBody(spec.Hexes, spec.X, spec.Y, mass);
                body.Anchored = spec.Side == CleanupSide.Enemy;

                SimIsland island = new SimIsland
                {
                    Index = index,
                    Id = spec.Id,
                    Label = spec.Label,
                    Side = spec.Side,
                    Behavior = spec.Behavior,
                    Hexes = new List<CleanupHex>(spec.Hexes),
                    Body = body,
                    Nodes = Enumerable.Range(nodeCount, spec.Hexes.Count).ToList(),
                    HomeX = spec.X,
                    HomeY = spec.Y,
                    WobblePhase = sim._rng.Next() * Math.PI * 2,
                    GarrisonTotal = spec.Garrison.Count,
                    Px = spec.X,
                    Py = spec.Y,
                };
                UpdateExtent(island);
                sim.Islands.Add(island);
                sim._bodies.Add(body);
                nodeCount += spec.Hexes.Count;
            }

            sim._nodeIsland = new int[nodeCount];
            sim._nodeHex = new int[nodeCount];
            sim._nodeX = new double[nodeCount];
            sim._nodeY = new double[nodeCount];
            sim._nodeGone = new bool[nodeCount];
            sim._flowToEnemy = new int[nodeCount];
            sim._flowToPlayer = new int[nodeCount];
            sim._gaps = new double[sim.Islands.Count * sim.Islands.Count];
            for (int node = 0; node < nodeCount; node += 1)
            {
                sim._innerLinks.Add(new List<int>());
                sim._bridgeLinks.Add(new List<int>());
            }

            foreach (SimIsland island in sim.Islands)
            {
                for (int index = 0; index < island.Hexes.Count; index += 1)
                {
                    sim._nodeIsland[island.Nodes[index]] = island.Index;
                    sim._nodeHex[island.Nodes[index]] = index;
                }

                sim.RebuildInnerLinks(island);
            }

            SimIsland player = sim.PlayerIsland;
            sim.Border = new CleanupBorder(level.Bounds, seed, player != null ? player.Body.Radius : 300);
            sim.StructureHpByHex = new double[player != null ? player.Hexes.Count : 0];

            if (player != null)
            {
                for (int index = 0; index < player.Hexes.Count; index += 1)
                {
                    sim._startToxicity[player.Hexes[index].Id] = player.Hexes[index].Toxicity;
                    sim._startHp[player.Hexes[index].Id] = player.Hexes[index].Hp;
                    sim.StructureHpByHex[index] = -1;
                    sim.AddStructure(player.Hexes[index], index);
                }

                int strongholdHex = player.Hexes.FindIndex(hex => hex.Stronghold);
                List<int> nodes = sim.SpawnNodes(player, strongholdHex);
                for (int index = 0; index < level.Roster.Count; index += 1)
                {
                    sim.AddUnit(level.Roster[index], CleanupSide.Player, 0, nodes[index % nodes.Count], 1);
                }
            }

            foreach (SimIsland island in sim.Islands)
            {
                if (island.Side != CleanupSide.Enemy)
                {
                    continue;
                }

                List<int> nodes = sim.SpawnNodes(island, -1);
                List<Enemy> garrison = level.Islands[island.Index].Garrison;
                for (int index = 0; index < garrison.Count; index += 1)
                {
                    sim.AddUnit(garrison[index], CleanupSide.Enemy, island.Index, nodes[index % nodes.Count], level.Growth);
                }
            }

            sim.RefreshWorld();
            sim.Border.Step(0, TickHz, player != null ? player.Body.Radius : 300);

            return sim;
        }

        /* ---------- what the view and the session read ---------- */

        /// <summary>The island a ground unit stands on, or <c>null</c> for a flyer.</summary>
        public SimIsland IslandOf(SimUnit unit)
        {
            return unit.Node >= 0 ? Islands[_nodeIsland[unit.Node]] : null;
        }

        public double NodeX(int node)
        {
            return _nodeX[node];
        }

        public double NodeY(int node)
        {
            return _nodeY[node];
        }

        /// <summary>The steering of the player's island, each axis in -1..1. Y points down the prototype's plane.</summary>
        public void SetInput(double x, double y)
        {
            _inputX = Math.Max(-1, Math.Min(1, x));
            _inputY = Math.Max(-1, Math.Min(1, y));
        }

        /// <summary>Takes the events queued since the last call.</summary>
        public List<SimEvent> DrainEvents()
        {
            List<SimEvent> events = _events;
            _events = new List<SimEvent>();

            return events;
        }

        public int GarrisonAlive(int island)
        {
            int alive = 0;
            foreach (SimUnit unit in Units)
            {
                if (unit.Alive && unit.Side == CleanupSide.Enemy && unit.HomeIsland == island)
                {
                    alive += 1;
                }
            }

            return alive;
        }

        /// <summary>Whole seconds until an undocked husk is lost, or -1.</summary>
        public int DriftLeft(SimIsland island)
        {
            return island.State == IslandState.Cleared ? (int)Math.Max(0, Math.Ceiling(LostAfter - (Tick - island.StateTick) * TickSeconds)) : -1;
        }

        public int ArmyAlive
        {
            get { return Units.Count(unit => unit.Alive && unit.Side == CleanupSide.Player && !unit.IsStructure); }
        }

        public int StructuresStanding
        {
            get { return Units.Count(unit => unit.Alive && unit.IsStructure); }
        }

        public int WindowsOpen
        {
            get { return Border.Windows.Count(window => window.IsOpen(Tick)); }
        }

        /// <summary>Seconds until the skill is ready. 0 is ready.</summary>
        public double SkillCooldown
        {
            get { return Math.Max(0, (_skillReadyTick - Tick) * TickSeconds); }
        }

        /// <summary>The stronghold's hp in percent, rounded, or -1 when the island has none.</summary>
        public int StrongholdPct
        {
            get
            {
                SimIsland player = PlayerIsland;
                int index = player != null ? player.Hexes.FindIndex(hex => hex.Stronghold) : -1;
                if (index < 0 || player.Hexes[index].MaxHp <= 0)
                {
                    return -1;
                }

                return JsMath.Round(Math.Max(0, StructureHpByHex[index]) / player.Hexes[index].MaxHp * 100);
            }
        }

        /// <summary>What the level ends with.</summary>
        public CleanupResult Result()
        {
            List<SimIsland> enemies = Islands.FindAll(island => island.Side == CleanupSide.Enemy);
            SimIsland player = PlayerIsland;
            List<CleanupHex> hexes = player != null ? player.Hexes : new List<CleanupHex>();
            Dictionary<string, CleanupHex> hexById = hexes.ToDictionary(hex => hex.Id, hex => hex);

            List<StructureResult> structures = new List<StructureResult>();
            Dictionary<string, int> poisoned = new Dictionary<string, int>();
            for (int index = 0; index < hexes.Count; index += 1)
            {
                CleanupHex hex = hexes[index];
                if (hex.MaxHp > 0)
                {
                    structures.Add(new StructureResult
                    {
                        HexId = hex.Id,
                        Hp = JsMath.Round(Math.Max(0, StructureHpByHex[index])),
                        StartHp = _startHp.TryGetValue(hex.Id, out int startHp) ? startHp : hex.Hp,
                        MaxHp = hex.MaxHp,
                    });
                }

                if (_startToxicity.TryGetValue(hex.Id, out int before) && hex.Toxicity > before)
                {
                    poisoned[hex.Id] = hex.Toxicity;
                }
            }

            // A joined hex keeps the toxicity it has now; a joined hex destroyed later is gone.
            List<AnnexedHex> annexed = new List<AnnexedHex>();
            foreach (AnnexedHex gain in _annexed)
            {
                if (hexById.TryGetValue(HexMath.HexId(gain.Q, gain.R), out CleanupHex now))
                {
                    annexed.Add(new AnnexedHex { Q = gain.Q, R = gain.R, Biome = gain.Biome, Toxicity = now.Toxicity });
                }
            }

            CleanupOutcome outcome = Status == SimStatus.Won ? CleanupOutcome.Won : Status == SimStatus.Lost ? CleanupOutcome.Lost : CleanupOutcome.Retreated;
            if (enemies.Count == 0)
            {
                outcome = CleanupOutcome.Calm;
            }

            return new CleanupResult
            {
                Outcome = outcome,
                TotalIslands = enemies.Count,
                ClearedIslands = enemies.Count(island => island.State != IslandState.Active),
                AttachedIslands = enemies.Count(island => island.State == IslandState.Attached),
                // A lost battle claims nothing: the island is in ruins.
                Annexed = Status == SimStatus.Lost ? new List<AnnexedHex>() : annexed,
                Survivors = Units.Where(unit => unit.Alive && unit.Side == CleanupSide.Player && !unit.IsStructure).Select(unit => (Unit)unit.Stats).ToList(),
                Lost = new List<Unit>(Lost),
                Kills = Kills,
                Structures = structures,
                Razed = Razed,
                DestroyedHexIds = new List<string>(_destroyedHexIds),
                Poisoned = poisoned,
                ManaSpent = ManaSpent,
            };
        }

        /// <summary>
        /// Ends a level that is still running as a retreat, at the same cost as
        /// sailing out through the border. Only the turn flow calls it, as a safety
        /// net when the phase moves on without the level having ended.
        /// </summary>
        public void Retreat()
        {
            if (Status == SimStatus.Running)
            {
                LeaveBattle();
            }
        }

        /// <summary>One fixed tick of the whole level. Does nothing once the level has ended.</summary>
        public void Step()
        {
            if (Status != SimStatus.Running)
            {
                return;
            }

            double dt = TickSeconds;
            Tick += 1;

            SteerIslands(dt);
            RefreshWorld();

            foreach (SimUnit unit in Units)
            {
                unit.Px = unit.X;
                unit.Py = unit.Y;
                unit.Cooldown -= dt;
                if (unit.Node < 0)
                {
                    unit.HoverNode = HoverNodeAt(unit.X, unit.Y);
                }
            }

            BuildFlow(CleanupSide.Enemy, _flowToEnemy);
            BuildFlow(CleanupSide.Player, _flowToPlayer);

            foreach (SimUnit unit in Units)
            {
                if (!unit.Alive)
                {
                    continue;
                }

                if (unit.IsStructure)
                {
                    StepStructure(unit);

                    continue;
                }

                if (unit.Node < 0)
                {
                    StepFlyer(unit, dt);
                }
                else
                {
                    StepGroundUnit(unit, dt);
                }

                if (unit.Side == CleanupSide.Player && unit.Hp < unit.MaxHp && (Tick - unit.LastHurtTick) * dt > RegenDelay)
                {
                    unit.Hp = Math.Min(unit.MaxHp, unit.Hp + unit.MaxHp * RegenSharePerSecond * dt);
                }
            }

            StepProjectiles(dt);

            // Ground units ride their island: the world position follows the local one.
            foreach (SimUnit unit in Units)
            {
                if (unit.Node < 0)
                {
                    continue;
                }

                SimIsland island = Islands[_nodeIsland[unit.Node]];
                unit.X = island.Body.X + unit.Lx;
                unit.Y = island.Body.Y + unit.Ly;
            }

            Units.RemoveAll(unit => !unit.Alive);
            StepIslandStates();
            StepStatus();
            Border.Step(Tick, TickHz, PlayerIsland != null ? PlayerIsland.Body.Radius : 300);
            StepPlumePoison();
            StepExit();
        }

        /* ---------- skills ---------- */

        /// <summary>Why the skill cannot be cast right now, or <c>null</c>. The target is checked by the cast itself.</summary>
        public string SkillRefusal()
        {
            if (Status != SimStatus.Running)
            {
                return "Бой окончен";
            }

            if (Tick < _skillReadyTick)
            {
                return "«" + ShatterSkill.Label + "» ещё восстанавливается";
            }

            if (Mana < ShatterSkill.ManaCost)
            {
                return "Не хватает маны: нужно " + ShatterSkill.ManaCost;
            }

            return null;
        }

        /// <summary>
        /// What a cast at the target would destroy: the hex itself and every piece of
        /// its island that loses touch with the core. The core of the player's island
        /// is the piece with the stronghold; the core of any other island is its
        /// largest piece. Returns <c>null</c> and the reason when the cast is refused.
        /// </summary>
        public List<int> ShatterPlan(int islandIndex, int hexIndex, out string refusal)
        {
            refusal = null;
            SimIsland island = islandIndex >= 0 && islandIndex < Islands.Count ? Islands[islandIndex] : null;
            if (island == null || island.IsGone || hexIndex < 0 || hexIndex >= island.Hexes.Count)
            {
                refusal = "Здесь нечего разрушать";

                return null;
            }

            if (island.Hexes[hexIndex].Stronghold)
            {
                refusal = "Гекс твердыни разрушить нельзя";

                return null;
            }

            SimIsland player = PlayerIsland;
            if (player != null && island.Index != 0)
            {
                double reach = ShatterSkill.Reach + player.Body.Radius;
                int node = island.Nodes[hexIndex];
                if (CleanupMath.Hypot(_nodeX[node] - player.CenterX, _nodeY[node] - player.CenterY) > reach)
                {
                    refusal = "Слишком далеко: подведите остров ближе";

                    return null;
                }
            }

            // The pieces of the island without the target hex.
            Dictionary<Axial, int> byCell = new Dictionary<Axial, int>();
            for (int index = 0; index < island.Hexes.Count; index += 1)
            {
                byCell[new Axial(island.Hexes[index].Q, island.Hexes[index].R)] = index;
            }

            int[] pieceOf = Enumerable.Repeat(-1, island.Hexes.Count).ToArray();
            List<List<int>> pieces = new List<List<int>>();

            for (int first = 0; first < island.Hexes.Count; first += 1)
            {
                if (first == hexIndex || pieceOf[first] != -1)
                {
                    continue;
                }

                List<int> piece = new List<int> { first };
                pieceOf[first] = pieces.Count;
                for (int head = 0; head < piece.Count; head += 1)
                {
                    CleanupHex current = island.Hexes[piece[head]];
                    foreach (Axial step in HexMath.Directions)
                    {
                        if (byCell.TryGetValue(new Axial(current.Q + step.Q, current.R + step.R), out int next) && next != hexIndex && pieceOf[next] == -1)
                        {
                            pieceOf[next] = pieces.Count;
                            piece.Add(next);
                        }
                    }
                }

                pieces.Add(piece);
            }

            int strongholdHex = island.Hexes.FindIndex(hex => hex.Stronghold);
            int core = -1;
            if (island.Index == 0 && strongholdHex >= 0)
            {
                core = pieceOf[strongholdHex];
            }
            else
            {
                for (int index = 0; index < pieces.Count; index += 1)
                {
                    if (core < 0 || pieces[index].Count > pieces[core].Count)
                    {
                        core = index;
                    }
                }
            }

            if (island.Index == 0 && core < 0)
            {
                refusal = "Это последний гекс вашего острова";

                return null;
            }

            List<int> removed = new List<int> { hexIndex };
            for (int index = 0; index < pieces.Count; index += 1)
            {
                if (index != core)
                {
                    removed.AddRange(pieces[index]);
                }
            }

            return removed;
        }

        /// <summary>Casts the skill at a hex. Returns <c>null</c> on success, or the reason it was refused.</summary>
        public string CastShatter(int islandIndex, int hexIndex)
        {
            string refusal = SkillRefusal();
            if (refusal != null)
            {
                return refusal;
            }

            List<int> removed = ShatterPlan(islandIndex, hexIndex, out refusal);
            if (removed == null)
            {
                return refusal;
            }

            RemoveHexes(Islands[islandIndex], removed);
            RefreshWorld();
            Mana -= ShatterSkill.ManaCost;
            ManaSpent += ShatterSkill.ManaCost;
            _skillReadyTick = Tick + JsMath.Round(ShatterSkill.CooldownSeconds * TickHz);

            return null;
        }

        /* ---------- setup ---------- */

        private static void HexCenter(CleanupHex hex, out double x, out double y)
        {
            HexMath.HexToPixel(hex.Q, hex.R, HexMath.HexSize, out x, out y);
        }

        private static Body CreateBody(IReadOnlyList<CleanupHex> hexes, double x, double y, double mass)
        {
            Body body = new Body { X = x, Y = y, InvMass = 1 / mass };
            SetBodyShape(body, hexes);

            return body;
        }

        /// <summary>The hex centres, the centroid and the radius of a body, from its hexes.</summary>
        private static void SetBodyShape(Body body, IReadOnlyList<CleanupHex> hexes)
        {
            body.LocalX = new double[hexes.Count];
            body.LocalY = new double[hexes.Count];
            for (int index = 0; index < hexes.Count; index += 1)
            {
                HexCenter(hexes[index], out body.LocalX[index], out body.LocalY[index]);
            }

            if (hexes.Count > 0)
            {
                IslandExtent extent = CleanupLevel.Extent(hexes);
                body.CenterX = extent.Cx;
                body.CenterY = extent.Cy;
                body.Radius = extent.Radius;
            }
        }

        private static void UpdateExtent(SimIsland island)
        {
            Body body = island.Body;
            if (body.LocalX.Length == 0)
            {
                return;
            }

            island.MinX = body.LocalX.Min() - HexMath.HexSize;
            island.MaxX = body.LocalX.Max() + HexMath.HexSize;
            island.MinY = body.LocalY.Min() - HexMath.HexSize;
            island.MaxY = body.LocalY.Max() + HexMath.HexSize;
        }

        /// <summary>The neighbours of every hex of an island, inside the island.</summary>
        private void RebuildInnerLinks(SimIsland island)
        {
            Dictionary<Axial, int> byCell = new Dictionary<Axial, int>();
            for (int index = 0; index < island.Hexes.Count; index += 1)
            {
                byCell[new Axial(island.Hexes[index].Q, island.Hexes[index].R)] = index;
            }

            for (int index = 0; index < island.Hexes.Count; index += 1)
            {
                List<int> links = new List<int>();
                foreach (Axial step in HexMath.Directions)
                {
                    if (byCell.TryGetValue(new Axial(island.Hexes[index].Q + step.Q, island.Hexes[index].R + step.R), out int neighbor))
                    {
                        links.Add(island.Nodes[neighbor]);
                    }
                }

                _innerLinks[island.Nodes[index]] = links;
            }
        }

        /// <summary>The node indices of an island, sorted so that units spread from its middle.</summary>
        private List<int> SpawnNodes(SimIsland island, int preferHex)
        {
            double originX = island.Body.CenterX;
            double originY = island.Body.CenterY;
            if (preferHex >= 0)
            {
                HexCenter(island.Hexes[preferHex], out originX, out originY);
            }

            List<KeyValuePair<int, double>> entries = new List<KeyValuePair<int, double>>();
            for (int index = 0; index < island.Hexes.Count; index += 1)
            {
                HexCenter(island.Hexes[index], out double x, out double y);
                entries.Add(new KeyValuePair<int, double>(island.Nodes[index], CleanupMath.Hypot(x - originX, y - originY) + _rng.Next() * 20));
            }

            return entries.OrderBy(entry => entry.Value).Select(entry => entry.Key).ToList();
        }

        private void AddUnit(Combatant stats, CleanupSide side, int homeIsland, int node, double growth)
        {
            int id = _nextUnitId;
            _nextUnitId += 1;
            // A golden-angle spiral: every unit on a hex gets its own spot.
            double angle = id * 2.39996;
            double radius = 10 + id % 3 * 9;
            SimIsland island = Islands[homeIsland];
            HexCenter(island.Hexes[_nodeHex[node]], out double centerX, out double centerY);
            double slotX = Math.Cos(angle) * radius;
            double slotY = Math.Sin(angle) * radius * 0.7;
            double lx = centerX + slotX;
            double ly = centerY + slotY;
            double x = island.Body.X + lx;
            double y = island.Body.Y + ly;
            int maxHp = JsMath.Round(stats.Hp * growth);

            // The draws keep the prototype's order: the cooldown, then the idle wait.
            double cooldown = _rng.Next() * stats.Cooldown;
            double idleWait = _rng.Next() * IdleWaitSpread;

            Units.Add(new SimUnit
            {
                Id = id,
                Side = side,
                Stats = stats,
                MaxHp = maxHp,
                Damage = stats.Damage * growth,
                HomeIsland = homeIsland,
                SlotX = slotX,
                SlotY = slotY,
                Hp = maxHp,
                Node = stats.Flying ? -1 : node,
                Lx = lx,
                Ly = ly,
                X = x,
                Y = stats.Flying ? y - 10 : y,
                Px = x,
                Py = y,
                Cooldown = cooldown,
                IdleWait = idleWait,
            });
        }

        /// <summary>
        /// A building or the stronghold, as a combatant that never moves. Monsters
        /// attack it like a unit. The stronghold shoots back with bolts.
        /// </summary>
        private void AddStructure(CleanupHex hex, int index)
        {
            if (hex.MaxHp <= 0)
            {
                return;
            }

            StructureHpByHex[index] = hex.Hp;
            if (hex.Hp <= 0)
            {
                return;
            }

            bool defends = hex.Stronghold;
            Combatant stats = new Combatant
            {
                Key = defends ? "stronghold" : Buildings.Get(hex.Building.Value).ArtName,
                Label = defends ? Stronghold.Label : Buildings.Get(hex.Building.Value).Label,
                Icon = defends ? Stronghold.ArtName : Buildings.Get(hex.Building.Value).ArtName,
                Hp = hex.MaxHp,
                Damage = defends ? StructureHp.DefenseDamage : 0,
                Cooldown = StructureHp.DefenseCooldown,
                Range = defends ? StructureHp.DefenseRange : 0,
                Projectile = defends ? Projectile.Bolt : Projectile.None,
            };
            SimIsland island = Islands[0];
            HexCenter(hex, out double centerX, out double centerY);
            int id = _nextUnitId;
            _nextUnitId += 1;
            StructuresAtStart += 1;

            Units.Add(new SimUnit
            {
                Id = id,
                Side = CleanupSide.Player,
                Stats = stats,
                Structure = defends ? StructureKind.Stronghold : StructureHp.ToKind(hex.Building.Value),
                StructureHex = index,
                MaxHp = hex.MaxHp,
                Damage = stats.Damage,
                Hp = hex.Hp,
                Node = island.Nodes[index],
                Lx = centerX,
                Ly = centerY,
                X = island.Body.X + centerX,
                Y = island.Body.Y + centerY,
                Px = island.Body.X + centerX,
                Py = island.Body.Y + centerY,
            });
        }

        private void PushEvent(SimEvent simEvent)
        {
            _events.Add(simEvent);
            if (_events.Count > MaxEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxEvents);
            }
        }

        private double GapBetween(int a, int b)
        {
            return _gaps[a * Islands.Count + b];
        }

        private bool NodeActive(int node)
        {
            if (_nodeGone[node])
            {
                return false;
            }

            IslandState state = Islands[_nodeIsland[node]].State;

            return state == IslandState.Active || state == IslandState.Cleared;
        }

        /* ---------- islands ---------- */

        /// <summary>The nearest enemy island that sails at the player. Only one does at a time, so the player is never boxed in.</summary>
        private int LeadApproacher()
        {
            SimIsland player = PlayerIsland;
            if (player == null)
            {
                return -1;
            }

            int best = -1;
            double bestDistance = ApproachRadius;

            foreach (SimIsland island in Islands)
            {
                if (island.Side != CleanupSide.Enemy || island.State != IslandState.Active || island.Behavior != IslandBehavior.Approach)
                {
                    continue;
                }

                double distance = CleanupMath.Hypot(player.CenterX - island.CenterX, player.CenterY - island.CenterY);
                if (distance < bestDistance)
                {
                    best = island.Index;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// The span of the player's island along a side fits inside an open window
        /// there: the island may sail past the border on that side.
        /// </summary>
        private bool FitsWindow(SimIsland island, int side)
        {
            Body body = island.Body;
            double low = side % 2 == 0 ? body.X + island.MinX : body.Y + island.MinY;
            double high = side % 2 == 0 ? body.X + island.MaxX : body.Y + island.MaxY;
            BorderWindow window = Border.WindowAt(side, (low + high) / 2, Tick);

            return window != null && low >= window.At - window.Half && high <= window.At + window.Half;
        }

        /// <summary>
        /// Keeps every island inside the map. An island that hits the edge stops
        /// there. Enemy islands and husks keep out of the plume band altogether, so
        /// they never stick in it. The player's island stops at the border line,
        /// except where it fits through an open window.
        /// </summary>
        private void ClampToBounds()
        {
            foreach (SimIsland island in Islands)
            {
                if (island.IsGone || island.Hexes.Count == 0)
                {
                    continue;
                }

                bool isPlayer = island.Side == CleanupSide.Player;
                double inset = isPlayer ? 0 : Border.Depth;
                double halfWidth = Bounds.HalfWidth - inset;
                double halfHeight = Bounds.HalfHeight - inset;
                Body body = island.Body;
                double left = body.X + island.MinX;
                double right = body.X + island.MaxX;
                double top = body.Y + island.MinY;
                double bottom = body.Y + island.MaxY;

                if (left < -halfWidth && !(isPlayer && FitsWindow(island, 3)))
                {
                    body.X += -halfWidth - left;
                    body.Vx = Math.Max(0, body.Vx);
                }
                else if (right > halfWidth && !(isPlayer && FitsWindow(island, 1)))
                {
                    body.X -= right - halfWidth;
                    body.Vx = Math.Min(0, body.Vx);
                }

                if (top < -halfHeight && !(isPlayer && FitsWindow(island, 0)))
                {
                    body.Y += -halfHeight - top;
                    body.Vy = Math.Max(0, body.Vy);
                }
                else if (bottom > halfHeight && !(isPlayer && FitsWindow(island, 2)))
                {
                    body.Y -= bottom - halfHeight;
                    body.Vy = Math.Min(0, body.Vy);
                }
            }
        }

        /// <summary>The rocks of the border are solid: an island that runs into one is pushed back out.</summary>
        private void SolveRocks()
        {
            foreach (SimIsland island in Islands)
            {
                if (island.IsGone || !island.Body.Solid || island.Hexes.Count == 0)
                {
                    continue;
                }

                Body body = island.Body;

                foreach (BorderRock rock in Border.Rocks)
                {
                    if (CleanupMath.Hypot(rock.X - (body.X + body.CenterX), rock.Y - (body.Y + body.CenterY)) > body.Radius + rock.Radius)
                    {
                        continue;
                    }

                    double depth = 0;
                    double normalX = 0;
                    double normalY = 0;

                    for (int index = 0; index < body.LocalX.Length; index += 1)
                    {
                        double dx = body.X + body.LocalX[index] - rock.X;
                        double dy = body.Y + body.LocalY[index] - rock.Y;
                        double distance = CleanupMath.Hypot(dx, dy);
                        if (distance == 0)
                        {
                            distance = 1;
                        }

                        double overlap = rock.Radius + HexMath.HexSize * 0.85 - distance;
                        if (overlap > depth)
                        {
                            depth = overlap;
                            normalX = dx / distance;
                            normalY = dy / distance;
                        }
                    }

                    if (depth <= 0)
                    {
                        continue;
                    }

                    body.X += normalX * depth;
                    body.Y += normalY * depth;
                    double closing = body.Vx * normalX + body.Vy * normalY;
                    if (closing < 0)
                    {
                        body.Vx -= closing * normalX;
                        body.Vy -= closing * normalY;
                    }
                }
            }
        }

        /// <summary>The hexes of an island that sit in the plumes, with the side whose plumes cover each.</summary>
        private List<KeyValuePair<int, int>> HexesInPlumes(SimIsland island)
        {
            List<KeyValuePair<int, int>> found = new List<KeyValuePair<int, int>>();
            Body body = island.Body;
            double depth = Border.Depth + HexMath.HexSize;

            // Most of the time the island is far from every edge.
            if (body.X + island.MinX > -Bounds.HalfWidth + depth
                && body.X + island.MaxX < Bounds.HalfWidth - depth
                && body.Y + island.MinY > -Bounds.HalfHeight + depth
                && body.Y + island.MaxY < Bounds.HalfHeight - depth)
            {
                return found;
            }

            for (int index = 0; index < body.LocalX.Length; index += 1)
            {
                int side = Border.PlumeSideAt(body.X + body.LocalX[index], body.Y + body.LocalY[index], Tick, HexMath.HexSize * 0.8);
                if (side >= 0)
                {
                    found.Add(new KeyValuePair<int, int>(index, side));
                }
            }

            return found;
        }

        /// <summary>The plumes push an island back inward and eat its outward speed.</summary>
        private void PushOutOfPlumes(double dt)
        {
            foreach (SimIsland island in Islands)
            {
                if (island.IsGone || island.Hexes.Count == 0)
                {
                    continue;
                }

                List<KeyValuePair<int, int>> inside = HexesInPlumes(island);
                if (inside.Count == 0)
                {
                    continue;
                }

                double pushX = 0;
                double pushY = 0;
                foreach (KeyValuePair<int, int> entry in inside)
                {
                    CleanupBorder.InwardNormal(entry.Value, out double normalX, out double normalY);
                    pushX += normalX;
                    pushY += normalY;
                }

                double length = CleanupMath.Hypot(pushX, pushY);
                if (length == 0)
                {
                    length = 1;
                }

                double nx = pushX / length;
                double ny = pushY / length;
                Body body = island.Body;
                double along = body.Vx * nx + body.Vy * ny;
                if (along < 0)
                {
                    double damp = Math.Min(1, PlumeDamp * dt);
                    body.Vx -= along * nx * damp;
                    body.Vy -= along * ny * damp;
                }

                body.Vx += nx * PlumePush * dt;
                body.Vy += ny * PlumePush * dt;
            }
        }

        private void SteerIslands(double dt)
        {
            SimIsland player = PlayerIsland;
            int approacher = LeadApproacher();

            foreach (SimIsland island in Islands)
            {
                Body body = island.Body;
                island.Px = body.X;
                island.Py = body.Y;

                if (island.IsGone)
                {
                    continue;
                }

                if (island.Side == CleanupSide.Player)
                {
                    double length = CleanupMath.Hypot(_inputX, _inputY);
                    bool steering = length > 0.01;
                    if (steering)
                    {
                        body.Vx += _inputX / length * PlayerAcceleration * dt;
                        body.Vy += _inputY / length * PlayerAcceleration * dt;
                    }

                    double drag = Math.Exp(-(steering ? PlayerDragSteering : PlayerDragIdle) * dt);
                    body.Vx *= drag;
                    body.Vy *= drag;
                    double speed = CleanupMath.Hypot(body.Vx, body.Vy);
                    if (speed > PlayerMaxSpeed)
                    {
                        body.Vx *= PlayerMaxSpeed / speed;
                        body.Vy *= PlayerMaxSpeed / speed;
                    }
                }
                else
                {
                    double desiredX = 0;
                    double desiredY = 0;

                    // A docked island holds still, so the fight on the bridge stays put.
                    bool docked = GapBetween(0, island.Index) < HexStep * 0.2;

                    if (island.State == IslandState.Active && !docked)
                    {
                        double time = Tick * TickSeconds * 0.12 + island.WobblePhase;
                        double wobbleX = island.HomeX + Math.Cos(time) * DriftWobble;
                        double wobbleY = island.HomeY + Math.Sin(time * 1.3) * DriftWobble;
                        desiredX = (wobbleX - body.X) * 0.2;
                        desiredY = (wobbleY - body.Y) * 0.2;
                        double drift = CleanupMath.Hypot(desiredX, desiredY);
                        if (drift > DriftMaxSpeed)
                        {
                            desiredX *= DriftMaxSpeed / drift;
                            desiredY *= DriftMaxSpeed / drift;
                        }

                        if (island.Index == approacher && player != null)
                        {
                            double dx = player.CenterX - island.CenterX;
                            double dy = player.CenterY - island.CenterY;
                            double distance = CleanupMath.Hypot(dx, dy);
                            if (distance < ApproachRadius && distance > 0)
                            {
                                desiredX = dx / distance * ApproachSpeed;
                                desiredY = dy / distance * ApproachSpeed;
                            }
                        }
                    }

                    // An undocked husk drifts away from the player after a while.
                    double sinceCleared = (Tick - island.StateTick) * TickSeconds;
                    if (island.State == IslandState.Cleared && sinceCleared > DriftDelay && player != null)
                    {
                        double awayX = island.CenterX - player.CenterX;
                        double awayY = island.CenterY - player.CenterY;
                        double away = CleanupMath.Hypot(awayX, awayY);
                        if (away == 0)
                        {
                            away = 1;
                        }

                        desiredX = awayX / away * DriftAwaySpeed;
                        desiredY = awayY / away * DriftAwaySpeed;
                    }

                    double response = Math.Min(1, EnemyResponse * dt);
                    body.Vx += (desiredX - body.Vx) * response;
                    body.Vy += (desiredY - body.Vy) * response;
                }

                body.X += body.Vx * dt;
                body.Y += body.Vy * dt;
            }

            PushOutOfPlumes(dt);
            ClampToBounds();
            CleanupCollision.Solve(_bodies);
            SolveRocks();
            // The solver may push an island back out; the border wins.
            ClampToBounds();
        }

        /// <summary>Node positions, bridges and island gaps.</summary>
        private void RefreshWorld()
        {
            for (int node = 0; node < _nodeIsland.Length; node += 1)
            {
                _bridgeLinks[node].Clear();
                if (_nodeGone[node])
                {
                    continue;
                }

                Body body = Islands[_nodeIsland[node]].Body;
                _nodeX[node] = body.X + body.LocalX[_nodeHex[node]];
                _nodeY[node] = body.Y + body.LocalY[_nodeHex[node]];
            }

            CleanupCollision.FindBridges(_bodies, _bridges, _distances);
            for (int index = 0; index < _gaps.Length; index += 1)
            {
                _gaps[index] = double.PositiveInfinity;
            }

            foreach (PairDistance pair in _distances)
            {
                _gaps[pair.A * Islands.Count + pair.B] = pair.Gap;
                _gaps[pair.B * Islands.Count + pair.A] = pair.Gap;
            }

            foreach (Bridge bridge in _bridges)
            {
                int nodeA = Islands[bridge.A].Nodes[bridge.HexA];
                int nodeB = Islands[bridge.B].Nodes[bridge.HexB];
                _bridgeLinks[nodeA].Add(nodeB);
                _bridgeLinks[nodeB].Add(nodeA);
            }
        }

        private List<int> LinksOf(int node)
        {
            List<int> inner = _innerLinks[node];
            List<int> bridged = _bridgeLinks[node];
            if (bridged.Count == 0)
            {
                return inner;
            }

            List<int> all = new List<int>(inner);
            all.AddRange(bridged);

            return all;
        }

        private bool AreLinked(int a, int b)
        {
            return a == b || _innerLinks[a].Contains(b) || _bridgeLinks[a].Contains(b);
        }

        /// <summary>Multi-source BFS from every node that holds a unit of <paramref name="goalSide"/>.</summary>
        private void BuildFlow(CleanupSide goalSide, int[] flow)
        {
            for (int index = 0; index < flow.Length; index += 1)
            {
                flow[index] = -1;
            }

            List<int> queue = new List<int>();

            foreach (SimUnit unit in Units)
            {
                if (!unit.Alive || unit.Side != goalSide)
                {
                    continue;
                }

                int node = unit.Node >= 0 ? unit.Node : unit.HoverNode;
                if (node >= 0 && flow[node] == -1 && NodeActive(node))
                {
                    flow[node] = 0;
                    queue.Add(node);
                }
            }

            for (int head = 0; head < queue.Count; head += 1)
            {
                int node = queue[head];
                int next = flow[node] + 1;

                foreach (int neighbor in LinksOf(node))
                {
                    if (flow[neighbor] == -1 && NodeActive(neighbor))
                    {
                        flow[neighbor] = next;
                        queue.Add(neighbor);
                    }
                }
            }
        }

        /* ---------- combat ---------- */

        private void Hurt(SimUnit target, double amount)
        {
            if (!target.Alive)
            {
                return;
            }

            target.Hp -= amount;
            target.LastHurtTick = Tick;
            PushEvent(new SimEvent { Type = SimEventType.Hit, X = target.X, Y = target.Y, Amount = Math.Max(1, JsMath.Round(amount)), Side = target.Side });

            if (target.IsStructure)
            {
                StructureHpByHex[target.StructureHex] = Math.Max(0, target.Hp);
            }

            if (target.Hp > 0)
            {
                return;
            }

            target.Alive = false;
            target.Hp = 0;

            if (target.IsStructure)
            {
                Razed += 1;
                PushEvent(new SimEvent { Type = SimEventType.Razed, X = target.X, Y = target.Y });

                return;
            }

            PushEvent(new SimEvent { Type = SimEventType.Death, X = target.X, Y = target.Y, Side = target.Side });

            if (target.Side == CleanupSide.Player)
            {
                Lost.Add((Unit)target.Stats);
            }
            else
            {
                Kills += 1;
            }
        }

        private void Attack(SimUnit unit, SimUnit target)
        {
            double dx = target.X - unit.X;
            double dy = target.Y - unit.Y;
            double distance = CleanupMath.Hypot(dx, dy);
            if (distance == 0)
            {
                distance = 1;
            }

            unit.AimX = dx / distance;
            unit.AimY = dy / distance;
            unit.LastAttackTick = Tick;
            unit.Cooldown = unit.Stats.Cooldown;
            double damage = unit.Damage * (1 - DamageRoll / 2 + _rng.Next() * DamageRoll);

            if (unit.Stats.Projectile != Projectile.None)
            {
                Projectiles.Add(new SimProjectile
                {
                    Kind = unit.Stats.Projectile,
                    Side = unit.Side,
                    TargetId = target.Id,
                    Damage = damage,
                    FromX = unit.X,
                    FromY = unit.Y,
                    Duration = Math.Max(TickSeconds * 2, distance / ProjectileSpeed(unit.Stats.Projectile)),
                    TargetX = target.X,
                    TargetY = target.Y,
                    X = unit.X,
                    Y = unit.Y,
                    Px = unit.X,
                    Py = unit.Y,
                });

                return;
            }

            Hurt(target, damage);
        }

        private static double ProjectileSpeed(Projectile kind)
        {
            switch (kind)
            {
                case Projectile.Stone: return 420;
                case Projectile.Arrow: return 560;
                case Projectile.Bullet: return 1100;
                case Projectile.Hex: return 320;
                default: return 760;
            }
        }

        /// <summary>The nearest living foe that <paramref name="accept"/> allows, by straight distance.</summary>
        private SimUnit NearestFoe(SimUnit unit, double maxDistance, Func<SimUnit, bool> accept)
        {
            SimUnit best = null;
            double bestDistance = maxDistance;

            foreach (SimUnit other in Units)
            {
                if (!other.Alive || other.Side == unit.Side)
                {
                    continue;
                }

                double distance = CleanupMath.Hypot(other.X - unit.X, other.Y - unit.Y);
                if (distance <= bestDistance && accept(other))
                {
                    best = other;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// A foe a ground unit can hit from where it stands. Monsters hit units in
        /// reach first and turn on buildings only when no unit is in reach.
        /// </summary>
        private SimUnit TargetForGround(SimUnit unit)
        {
            bool isRanged = unit.Stats.Range > 1;

            bool InReach(SimUnit foe)
            {
                if (isRanged)
                {
                    return true;
                }

                if (foe.Node < 0)
                {
                    return CleanupMath.Hypot(foe.X - unit.X, foe.Y - unit.Y) <= FlyerReach;
                }

                return AreLinked(unit.Node, foe.Node);
            }

            double reach = isRanged ? unit.Stats.Range * HexStep : HexStep * 1.9;
            SimUnit fighter = NearestFoe(unit, reach, foe => !foe.IsStructure && InReach(foe));

            return fighter ?? NearestFoe(unit, reach, foe => foe.IsStructure && InReach(foe));
        }

        /// <summary>The stronghold shoots the nearest monster in range. Other buildings only stand.</summary>
        private void StepStructure(SimUnit unit)
        {
            if (unit.Damage <= 0 || unit.Cooldown > 0)
            {
                return;
            }

            SimUnit target = NearestFoe(unit, unit.Stats.Range * HexStep, foe => true);
            if (target != null)
            {
                Attack(unit, target);
            }
        }

        /* ---------- ground movement ---------- */

        private void Walk(SimUnit unit, double pace, double dt)
        {
            // A bridge that breaks under a walking unit sends it back to its own hex.
            if (unit.NextNode >= 0 && (!AreLinked(unit.Node, unit.NextNode) || !NodeActive(unit.NextNode)))
            {
                unit.NextNode = -1;
                unit.IdlePath.Clear();
            }

            int goalNode = unit.NextNode >= 0 ? unit.NextNode : unit.Node;
            // Where the unit stands on the goal node, in the frame of its own island.
            Body frame = Islands[_nodeIsland[unit.Node]].Body;
            double goalX = _nodeX[goalNode] + unit.SlotX - frame.X;
            double goalY = _nodeY[goalNode] + unit.SlotY - frame.Y;
            double dx = goalX - unit.Lx;
            double dy = goalY - unit.Ly;
            double distance = CleanupMath.Hypot(dx, dy);
            double step = unit.Stats.Speed * HexStep * pace * dt;

            if (distance <= step)
            {
                unit.Lx = goalX;
                unit.Ly = goalY;

                if (unit.NextNode >= 0)
                {
                    int arrived = unit.NextNode;
                    Body body = Islands[_nodeIsland[arrived]].Body;
                    unit.Node = arrived;
                    unit.NextNode = -1;
                    unit.Lx = _nodeX[arrived] + unit.SlotX - body.X;
                    unit.Ly = _nodeY[arrived] + unit.SlotY - body.Y;
                }

                return;
            }

            unit.Lx += dx / distance * step;
            unit.Ly += dy / distance * step;
        }

        /// <summary>BFS path from <paramref name="from"/> to the first node that <paramref name="isGoal"/> accepts.</summary>
        private List<int> PathTo(int from, Func<int, bool> isGoal, bool crossBridges)
        {
            Dictionary<int, int> previous = new Dictionary<int, int> { { from, -1 } };
            List<int> queue = new List<int> { from };

            for (int head = 0; head < queue.Count; head += 1)
            {
                int node = queue[head];
                if (node != from && isGoal(node))
                {
                    List<int> path = new List<int>();
                    int cursor = node;
                    while (cursor != from)
                    {
                        path.Insert(0, cursor);
                        cursor = previous[cursor];
                    }

                    return path;
                }

                List<int> links = crossBridges ? LinksOf(node) : _innerLinks[node];
                foreach (int neighbor in links)
                {
                    if (!previous.ContainsKey(neighbor) && NodeActive(neighbor))
                    {
                        previous[neighbor] = node;
                        queue.Add(neighbor);
                    }
                }
            }

            return new List<int>();
        }

        /// <summary>The foreign island this island's idle units should gather to face.</summary>
        private int RallyIsland(SimIsland island)
        {
            int best = -1;
            double bestGap = RallyGap;

            foreach (SimIsland other in Islands)
            {
                if (other.Side == island.Side || other.State != IslandState.Active)
                {
                    continue;
                }

                double gap = GapBetween(island.Index, other.Index);
                if (gap < bestGap)
                {
                    best = other.Index;
                    bestGap = gap;
                }
            }

            return best;
        }

        /// <summary>Wandering, gathering on the rim facing a foe, or walking home.</summary>
        private void Idle(SimUnit unit, double dt)
        {
            if (unit.NextNode < 0 && unit.IdlePath.Count > 0)
            {
                unit.NextNode = unit.IdlePath[0];
                unit.IdlePath.RemoveAt(0);
            }

            if (unit.NextNode >= 0)
            {
                Walk(unit, IdlePace, dt);

                return;
            }

            Walk(unit, IdlePace, dt);
            unit.IdleWait -= dt;
            if (unit.IdleWait > 0)
            {
                return;
            }

            unit.IdleWait = IdleWaitMin + _rng.Next() * IdleWaitSpread;
            int hereIsland = _nodeIsland[unit.Node];
            int home = unit.Side == CleanupSide.Player ? 0 : unit.HomeIsland;

            if (hereIsland != home)
            {
                IslandState homeState = Islands[home].State;
                if (homeState == IslandState.Active || homeState == IslandState.Cleared)
                {
                    unit.IdlePath = PathTo(unit.Node, node => _nodeIsland[node] == home, true);
                    unit.IdleWait = 0.3;
                }

                if (unit.IdlePath.Count > 0)
                {
                    return;
                }
            }

            SimIsland island = Islands[hereIsland];
            int rally = RallyIsland(island);

            if (rally >= 0)
            {
                double targetX = Islands[rally].CenterX;
                double targetY = Islands[rally].CenterY;
                // OrderBy is stable, as the prototype's sort is.
                List<int> facing = island.Nodes
                    .OrderBy(node => CleanupMath.Hypot(_nodeX[node] - targetX, _nodeY[node] - targetY))
                    .Take(RallyNodes)
                    .ToList();
                int goal = facing[_rng.NextIndex(facing.Count)];
                if (goal != unit.Node)
                {
                    unit.IdlePath = PathTo(unit.Node, node => node == goal, false);
                }

                unit.IdleWait *= 0.5;

                return;
            }

            // A short stroll: one or two hexes in a random direction.
            List<int> links = _innerLinks[unit.Node];
            if (links.Count > 0 && _rng.Next() < 0.7)
            {
                unit.IdlePath = new List<int> { links[_rng.NextIndex(links.Count)] };
            }
        }

        private void StepGroundUnit(SimUnit unit, double dt)
        {
            bool onBridge = unit.NextNode >= 0 && _nodeIsland[unit.NextNode] != _nodeIsland[unit.Node];
            SimUnit target = onBridge ? null : TargetForGround(unit);

            if (target != null)
            {
                // A unit finishes the step it is in before it swings.
                bool walking = unit.NextNode >= 0;
                Walk(unit, 1, dt);
                if (!walking && unit.Cooldown <= 0)
                {
                    Attack(unit, target);
                }

                unit.IdlePath.Clear();

                return;
            }

            if (unit.NextNode < 0)
            {
                int[] flow = unit.Side == CleanupSide.Player ? _flowToEnemy : _flowToPlayer;
                int here = flow[unit.Node];
                bool chases = here > 0 && (unit.Side == CleanupSide.Player || here <= EnemyChaseSteps);

                if (chases)
                {
                    List<int> options = LinksOf(unit.Node).FindAll(node => flow[node] == here - 1 && NodeActive(node));
                    if (options.Count > 0)
                    {
                        unit.NextNode = options[(unit.Id + Tick / TickHz) % options.Count];
                        unit.IdlePath.Clear();
                    }
                }
            }

            if (unit.NextNode >= 0 && unit.IdlePath.Count == 0)
            {
                Walk(unit, 1, dt);

                return;
            }

            Idle(unit, dt);
        }

        /* ---------- flyers ---------- */

        private int HoverNodeAt(double x, double y)
        {
            int best = -1;
            double bestDistance = HoverRadius;

            foreach (SimIsland island in Islands)
            {
                if (island.State != IslandState.Active && island.State != IslandState.Cleared)
                {
                    continue;
                }

                if (CleanupMath.Hypot(island.CenterX - x, island.CenterY - y) > island.Body.Radius + HoverRadius)
                {
                    continue;
                }

                foreach (int node in island.Nodes)
                {
                    double distance = CleanupMath.Hypot(_nodeX[node] - x, _nodeY[node] - y);
                    if (distance < bestDistance)
                    {
                        best = node;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private SimUnit FlyerTarget(SimUnit unit)
        {
            if (unit.Side == CleanupSide.Player)
            {
                SimIsland own = Islands[0];
                double ownX = own.CenterX;
                double ownY = own.CenterY;

                return NearestFoe(unit, PlayerFlyerAggro, foe => CleanupMath.Hypot(foe.X - ownX, foe.Y - ownY) < PlayerFlyerLeash);
            }

            SimIsland home = Islands[unit.HomeIsland];
            double homeX = home.CenterX;
            double homeY = home.CenterY;

            bool Allowed(SimUnit foe)
            {
                bool nearFlyer = CleanupMath.Hypot(foe.X - unit.X, foe.Y - unit.Y) < EnemyFlyerAggro;

                return nearFlyer && CleanupMath.Hypot(foe.X - homeX, foe.Y - homeY) < EnemyFlyerLeash;
            }

            // A flyer in reach of a unit fights it; otherwise it raids the nearest building.
            SimUnit fighter = NearestFoe(unit, FlyerReach * 1.5, foe => !foe.IsStructure && Allowed(foe));

            return fighter ?? NearestFoe(unit, EnemyFlyerAggro * 2, Allowed);
        }

        private void StepFlyer(SimUnit unit, double dt)
        {
            SimUnit target = FlyerTarget(unit);
            double goalX;
            double goalY;
            double pace = 1;

            if (target != null)
            {
                double toX = target.X - unit.X;
                double toY = target.Y - unit.Y;
                double range = CleanupMath.Hypot(toX, toY);
                if (range == 0)
                {
                    range = 1;
                }

                if (range <= FlyerReach && unit.Cooldown <= 0)
                {
                    Attack(unit, target);
                }

                // It hangs just short of the target instead of sitting on it.
                double standoff = FlyerReach * 0.7;
                goalX = target.X - toX / range * standoff;
                goalY = target.Y - toY / range * standoff;
            }
            else
            {
                SimIsland homeIsland = Islands[unit.Side == CleanupSide.Player ? 0 : unit.HomeIsland];
                SimIsland home = homeIsland.IsGone ? Islands[0] : homeIsland;
                double orbit = 50 + unit.Id % 4 * 18;
                double angle = Tick * TickSeconds * 0.6 + unit.Id;
                goalX = home.CenterX + Math.Cos(angle) * orbit;
                goalY = home.CenterY + Math.Sin(angle) * orbit * 0.7;
                pace = 0.6;
            }

            double dx = goalX - unit.X;
            double dy = goalY - unit.Y;
            double distance = CleanupMath.Hypot(dx, dy);
            double maxSpeed = unit.Stats.Speed * HexStep * pace;
            double desiredX = distance > 1 ? dx / distance * Math.Min(maxSpeed, distance * 3) : 0;
            double desiredY = distance > 1 ? dy / distance * Math.Min(maxSpeed, distance * 3) : 0;

            foreach (SimUnit other in Units)
            {
                if (other == unit || !other.Alive || other.Node >= 0)
                {
                    continue;
                }

                double ox = unit.X - other.X;
                double oy = unit.Y - other.Y;
                double gap = CleanupMath.Hypot(ox, oy);
                if (gap > 0 && gap < FlyerSeparation)
                {
                    desiredX += ox / gap * maxSpeed * 0.5;
                    desiredY += oy / gap * maxSpeed * 0.5;
                }
            }

            double response = Math.Min(1, FlyerSteering * dt);
            unit.Vx += (desiredX - unit.Vx) * response;
            unit.Vy += (desiredY - unit.Vy) * response;
            unit.X += unit.Vx * dt;
            unit.Y += unit.Vy * dt;
        }

        /* ---------- the tick ---------- */

        private void StepProjectiles(double dt)
        {
            if (Projectiles.Count == 0)
            {
                return;
            }

            Dictionary<int, SimUnit> byId = new Dictionary<int, SimUnit>();
            foreach (SimUnit unit in Units)
            {
                byId[unit.Id] = unit;
            }

            foreach (SimProjectile shot in Projectiles)
            {
                shot.Px = shot.X;
                shot.Py = shot.Y;
                byId.TryGetValue(shot.TargetId, out SimUnit target);
                if (target != null && target.Alive)
                {
                    shot.TargetX = target.X;
                    shot.TargetY = target.Y;
                }

                shot.Age += dt;
                double t = Math.Min(1, shot.Age / shot.Duration);
                shot.X = shot.FromX + (shot.TargetX - shot.FromX) * t;
                shot.Y = shot.FromY + (shot.TargetY - shot.FromY) * t;

                if (t >= 1)
                {
                    shot.Done = true;
                    if (target != null && target.Alive)
                    {
                        Hurt(target, shot.Damage);
                    }
                }
            }

            Projectiles.RemoveAll(shot => shot.Done);
        }

        /// <summary>Carries a unit to a home node, as when a lost island leaves it behind.</summary>
        private void FerryHome(SimUnit unit, int node)
        {
            SimIsland player = Islands[0];
            PushEvent(new SimEvent { Type = SimEventType.Ferry, X = unit.X, Y = unit.Y });
            unit.Node = node;
            unit.NextNode = -1;
            unit.IdlePath.Clear();
            unit.Lx = _nodeX[node] + unit.SlotX - player.Body.X;
            unit.Ly = _nodeY[node] + unit.SlotY - player.Body.Y;
            unit.X = player.Body.X + unit.Lx;
            unit.Y = player.Body.Y + unit.Ly;
            unit.Px = unit.X;
            unit.Py = unit.Y;
            PushEvent(new SimEvent { Type = SimEventType.Ferry, X = unit.X, Y = unit.Y });
        }

        private void GrowNodes(int count)
        {
            int before = _nodeIsland.Length;
            Array.Resize(ref _nodeIsland, count);
            Array.Resize(ref _nodeHex, count);
            Array.Resize(ref _nodeX, count);
            Array.Resize(ref _nodeY, count);
            Array.Resize(ref _nodeGone, count);
            Array.Resize(ref _flowToEnemy, count);
            Array.Resize(ref _flowToPlayer, count);
            for (int node = before; node < count; node += 1)
            {
                _flowToEnemy[node] = -1;
                _flowToPlayer[node] = -1;
                _innerLinks.Add(new List<int>());
                _bridgeLinks.Add(new List<int>());
            }
        }

        /// <summary>
        /// Joins a cleared island to the player's island. Its hexes snap to the
        /// player's lattice at the docking offset and become hexes of the player's
        /// island, in the same pattern. Units on them come along. The merged island
        /// is one rigid body from now on. Returns false when no hex would connect.
        /// </summary>
        private bool AttachIsland(SimIsland island)
        {
            SimIsland player = PlayerIsland;
            if (player == null)
            {
                return false;
            }

            double dx = island.Body.X - player.Body.X;
            double dy = island.Body.Y - player.Body.Y;
            AttachPlan plan = CleanupAttach.Plan(player.Hexes, island.Hexes, dx, dy);
            if (plan == null)
            {
                return false;
            }

            HexMath.HexToPixel(plan.OffsetQ, plan.OffsetR, HexMath.HexSize, out double offsetX, out double offsetY);
            int firstNew = _nodeIsland.Length;
            Dictionary<int, int> newNodeOf = new Dictionary<int, int>();

            GrowNodes(firstNew + plan.Kept.Count);
            int structuresBefore = StructureHpByHex.Length;
            Array.Resize(ref StructureHpByHex, player.Hexes.Count + plan.Kept.Count);
            for (int index = structuresBefore; index < StructureHpByHex.Length; index += 1)
            {
                StructureHpByHex[index] = -1;
            }

            for (int order = 0; order < plan.Kept.Count; order += 1)
            {
                CleanupHex hex = island.Hexes[plan.Kept[order]];
                int q = hex.Q + plan.OffsetQ;
                int r = hex.R + plan.OffsetR;
                int node = firstNew + order;
                newNodeOf[island.Nodes[plan.Kept[order]]] = node;
                _nodeIsland[node] = 0;
                _nodeHex[node] = player.Hexes.Count;
                player.Nodes.Add(node);
                _annexed.Add(new AnnexedHex { Q = q, R = r, Biome = hex.Biome, Toxicity = hex.Toxicity });
                // A wild island brings no buildings: only its ground joins.
                CleanupHex joined = hex.Clone();
                joined.Id = HexMath.HexId(q, r);
                joined.Q = q;
                joined.R = r;
                joined.Building = null;
                joined.Stronghold = false;
                joined.Hp = 0;
                joined.MaxHp = 0;
                player.Hexes.Add(joined);
            }

            // The neighbours of every player hex, old and new.
            RebuildInnerLinks(player);

            // One rigid body: the union of hexes, the summed mass and momentum.
            Body body = player.Body;
            double playerMass = 1 / body.InvMass;
            double joinedMass = plan.Kept.Count * 1.5;
            body.Vx = (body.Vx * playerMass + island.Body.Vx * joinedMass) / (playerMass + joinedMass);
            body.Vy = (body.Vy * playerMass + island.Body.Vy * joinedMass) / (playerMass + joinedMass);
            body.InvMass = 1 / (playerMass + joinedMass);
            SetBodyShape(body, player.Hexes);
            UpdateExtent(player);
            player.Shape += 1;
            for (int index = 0; index < player.Nodes.Count; index += 1)
            {
                _nodeX[player.Nodes[index]] = body.X + body.LocalX[index];
                _nodeY[player.Nodes[index]] = body.Y + body.LocalY[index];
            }

            // Units on the joining island now stand on the player's island.
            List<int> homeNodes = SpawnNodes(player, -1);
            int spare = 0;
            foreach (SimUnit unit in Units)
            {
                if (unit.NextNode >= 0 && _nodeIsland[unit.NextNode] == island.Index)
                {
                    unit.NextNode = newNodeOf.TryGetValue(unit.NextNode, out int nextNode) ? nextNode : -1;
                }

                if (!unit.Alive || unit.Node < 0 || _nodeIsland[unit.Node] != island.Index)
                {
                    continue;
                }

                unit.IdlePath.Clear();
                if (!newNodeOf.TryGetValue(unit.Node, out int node))
                {
                    // Its hex clashed and was dropped: the unit is carried onto the island.
                    FerryHome(unit, homeNodes[spare % homeNodes.Count]);
                    spare += 1;

                    continue;
                }

                unit.Node = node;
                unit.Lx += offsetX;
                unit.Ly += offsetY;
            }

            island.State = IslandState.Attached;
            island.StateTick = Tick;
            island.Body.Solid = false;
            island.Joined = plan.Kept.Count;
            PushEvent(new SimEvent { Type = SimEventType.Attached, Island = island.Index, Joined = plan.Kept.Count });

            return true;
        }

        /// <summary>
        /// The life of an enemy island after its last monster: it floats free as a
        /// husk, joins the player's island the moment the two touch, or drifts off
        /// and is lost if nobody docks it in time.
        /// </summary>
        private void StepIslandStates()
        {
            foreach (SimIsland island in Islands)
            {
                if (island.Side != CleanupSide.Enemy)
                {
                    continue;
                }

                if (island.State == IslandState.Active && GarrisonAlive(island.Index) == 0)
                {
                    island.State = IslandState.Cleared;
                    island.StateTick = Tick;
                    // A cleared island is a husk: the player's island may shove it aside.
                    island.Body.Anchored = false;
                    PushEvent(new SimEvent { Type = SimEventType.Cleared, Island = island.Index });
                }

                if (island.State != IslandState.Cleared)
                {
                    continue;
                }

                // Touching the player's island: it joins at once.
                if (GapBetween(0, island.Index) < CleanupCollision.BridgeGap && AttachIsland(island))
                {
                    continue;
                }

                if ((Tick - island.StateTick) * TickSeconds < LostAfter)
                {
                    continue;
                }

                // Gone for good. Player units still on it are carried home; monsters go with it.
                SimIsland player = PlayerIsland;
                List<int> homeNodes = player != null ? SpawnNodes(player, -1) : new List<int>();
                int slot = 0;
                foreach (SimUnit unit in Units)
                {
                    if (!unit.Alive || unit.Node < 0 || _nodeIsland[unit.Node] != island.Index)
                    {
                        continue;
                    }

                    if (unit.Side == CleanupSide.Player && homeNodes.Count > 0)
                    {
                        FerryHome(unit, homeNodes[slot % homeNodes.Count]);
                        slot += 1;
                    }
                    else
                    {
                        unit.Alive = false;
                    }
                }

                island.State = IslandState.Lost;
                island.StateTick = Tick;
                island.Body.Solid = false;
                PushEvent(new SimEvent { Type = SimEventType.Lost, Island = island.Index });
            }
        }

        private void StepStatus()
        {
            if (Status != SimStatus.Running)
            {
                return;
            }

            // A level with no enemy islands ends at once: there is nothing to clear.
            bool allSunk = Islands.All(island => island.Side != CleanupSide.Enemy || island.IsGone);
            // Defeat is the last building or the stronghold falling. A wiped army alone
            // does not end the battle: the stronghold keeps shooting.
            bool islandRuined = StructuresAtStart > 0 && !Units.Exists(unit => unit.Alive && unit.IsStructure);

            if (!allSunk && !islandRuined)
            {
                _endingTick = -1;

                return;
            }

            if (_endingTick < 0)
            {
                _endingTick = Tick;
            }

            double waited = (Tick - _endingTick) * TickSeconds;
            if (islandRuined && waited >= LossDelay)
            {
                Status = SimStatus.Lost;
            }
            else if (allSunk && !islandRuined && waited >= WinDelay)
            {
                Status = SimStatus.Won;
            }
        }

        /// <summary>
        /// The retreat costs the units that are not home: a ground unit standing on
        /// another island stays behind and is lost. Flyers keep up with the island.
        /// </summary>
        private void LeaveBattle()
        {
            foreach (SimUnit unit in Units)
            {
                if (!unit.Alive || unit.Side != CleanupSide.Player || unit.Node < 0)
                {
                    continue;
                }

                if (_nodeIsland[unit.Node] != 0)
                {
                    unit.Alive = false;
                    unit.Hp = 0;
                    Lost.Add((Unit)unit.Stats);
                    PushEvent(new SimEvent { Type = SimEventType.Death, X = unit.X, Y = unit.Y, Side = unit.Side });
                }
            }

            Units.RemoveAll(unit => !unit.Alive);
            Status = SimStatus.Retreated;
        }

        /// <summary>
        /// The way out. The player's island leaves once its centre has sailed past
        /// the border line, which only an open window lets it do.
        /// <see cref="ExitProgress"/> shows how far through the window band it has come.
        /// </summary>
        private void StepExit()
        {
            SimIsland player = PlayerIsland;
            if (player == null || player.Hexes.Count == 0)
            {
                return;
            }

            double cx = player.CenterX;
            double cy = player.CenterY;
            double progress = 0;
            bool inWindow = false;
            bool isOut = false;

            for (int side = 0; side < 4; side += 1)
            {
                double depth = CleanupBorder.DepthFrom(Bounds, side, cx, cy);
                if (depth < 0)
                {
                    isOut = true;
                }

                if (depth >= Border.Depth || Border.WindowAt(side, CleanupBorder.AlongOf(side, cx, cy), Tick) == null)
                {
                    continue;
                }

                inWindow = true;
                progress = Math.Max(progress, 1 - Math.Max(0, depth) / Border.Depth);
            }

            InWindow = inWindow;
            ExitProgress = isOut ? 1 : progress;

            if (isOut && Status == SimStatus.Running)
            {
                LeaveBattle();
            }
        }

        /// <summary>
        /// The plumes poison the player's island in pulses: each hex in them gains
        /// toxicity, and units and buildings on it lose a share of their hp. The
        /// island is pushed back out long before this kills anyone.
        /// </summary>
        private void StepPlumePoison()
        {
            int pulse = JsMath.Round(PlumePulseSeconds * TickHz);
            SimIsland player = PlayerIsland;
            if (player == null || Tick % pulse != 0)
            {
                return;
            }

            List<KeyValuePair<int, int>> inside = HexesInPlumes(player);
            if (inside.Count == 0)
            {
                return;
            }

            HashSet<int> poisoned = new HashSet<int>();
            foreach (KeyValuePair<int, int> entry in inside)
            {
                CleanupHex hex = player.Hexes[entry.Key];
                hex.Toxicity = Math.Min(100, hex.Toxicity + PlumeToxicity);
                hex.Dead = hex.Dead || hex.Toxicity >= 100;
                int node = player.Nodes[entry.Key];
                poisoned.Add(node);
                PushEvent(new SimEvent { Type = SimEventType.Poison, X = _nodeX[node], Y = _nodeY[node] });
            }

            foreach (SimUnit unit in Units)
            {
                if (!unit.Alive || unit.Side != CleanupSide.Player || !poisoned.Contains(unit.Node))
                {
                    continue;
                }

                Hurt(unit, unit.MaxHp * (unit.IsStructure ? PlumeStructureDamage : PlumeUnitDamage));
            }
        }

        /// <summary>
        /// Takes hexes out of an island. Their nodes are marked gone; the rest of the
        /// island keeps its nodes, renumbered by hex. Monsters on a lost hex fall
        /// with it; the player's units are carried home, as from a lost island. A
        /// building on a lost hex of the player's island is razed with it.
        /// </summary>
        private void RemoveHexes(SimIsland island, List<int> removedList)
        {
            HashSet<int> removed = new HashSet<int>(removedList);
            List<int> keep = Enumerable.Range(0, island.Hexes.Count).Where(index => !removed.Contains(index)).ToList();
            Dictionary<int, int> newIndex = new Dictionary<int, int>();
            for (int index = 0; index < keep.Count; index += 1)
            {
                newIndex[keep[index]] = index;
            }

            HashSet<int> goneNodes = new HashSet<int>(removedList.Select(index => island.Nodes[index]));

            foreach (int index in removedList)
            {
                int node = island.Nodes[index];
                _nodeGone[node] = true;
                PushEvent(new SimEvent { Type = SimEventType.Crumble, Island = island.Index, X = _nodeX[node], Y = _nodeY[node] });

                if (island.Index == 0)
                {
                    _destroyedHexIds.Add(island.Hexes[index].Id);
                }
            }

            foreach (int node in island.Nodes)
            {
                if (goneNodes.Contains(node))
                {
                    _innerLinks[node] = new List<int>();
                }
                else
                {
                    _innerLinks[node].RemoveAll(goneNodes.Contains);
                }
            }

            if (island.Index == 0)
            {
                StructureHpByHex = keep.Select(old => StructureHpByHex[old]).ToArray();
            }

            island.Hexes = keep.Select(index => island.Hexes[index]).ToList();
            island.Nodes = keep.Select(index => island.Nodes[index]).ToList();
            for (int index = 0; index < island.Nodes.Count; index += 1)
            {
                _nodeHex[island.Nodes[index]] = index;
            }

            island.Shape += 1;
            SetBodyShape(island.Body, island.Hexes);
            UpdateExtent(island);

            SimIsland home = PlayerIsland;
            List<int> homeNodes = home != null && home.Hexes.Count > 0 ? SpawnNodes(home, -1) : new List<int>();
            int slot = 0;

            foreach (SimUnit unit in Units)
            {
                if (!unit.Alive)
                {
                    continue;
                }

                if (unit.IsStructure && island.Index == 0)
                {
                    if (newIndex.TryGetValue(unit.StructureHex, out int moved))
                    {
                        unit.StructureHex = moved;
                    }
                    else
                    {
                        unit.Alive = false;
                        unit.Hp = 0;
                        Razed += 1;
                        PushEvent(new SimEvent { Type = SimEventType.Razed, X = unit.X, Y = unit.Y });
                    }

                    continue;
                }

                if (unit.NextNode >= 0 && goneNodes.Contains(unit.NextNode))
                {
                    unit.NextNode = -1;
                }

                if (unit.IdlePath.Exists(goneNodes.Contains))
                {
                    unit.IdlePath.Clear();
                }

                if (unit.Node < 0 || !goneNodes.Contains(unit.Node))
                {
                    continue;
                }

                if (unit.Side == CleanupSide.Player && homeNodes.Count > 0)
                {
                    FerryHome(unit, homeNodes[slot % homeNodes.Count]);
                    slot += 1;

                    continue;
                }

                unit.Alive = false;
                unit.Hp = 0;
                PushEvent(new SimEvent { Type = SimEventType.Death, X = unit.X, Y = unit.Y, Side = unit.Side });
                if (unit.Side == CleanupSide.Player)
                {
                    Lost.Add((Unit)unit.Stats);
                }
                else
                {
                    Kills += 1;
                }
            }

            Units.RemoveAll(unit => !unit.Alive);

            if (island.Hexes.Count == 0)
            {
                island.State = IslandState.Lost;
                island.StateTick = Tick;
                island.Body.Solid = false;
                PushEvent(new SimEvent { Type = SimEventType.Lost, Island = island.Index });
            }
        }
    }
}
