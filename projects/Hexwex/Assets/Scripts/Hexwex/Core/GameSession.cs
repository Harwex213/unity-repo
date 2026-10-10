using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>
    /// One game of Toxic Island. It stands where the prototype's <c>store/</c> and
    /// <c>domain/</c> stood: it owns the only mutable state, and every action goes
    /// through it. The view reads the state, calls the actions and listens to
    /// <see cref="Changed"/> and <see cref="Notice"/>; it never writes.
    ///
    /// The prototype paces the rivals with timers and plays animations between
    /// steps. Neither changes a result, because every roll is seeded by the world
    /// seed, the turn and the player. Here the rivals play their phase the moment
    /// the player ends it.
    ///
    /// A turn is build, tax, scout, clear. The clear phase is a real-time battle:
    /// the view asks for its ticks through <see cref="StepBattle"/>. A turn whose
    /// cell holds no wild islands skips it.
    /// </summary>
    public sealed class GameSession
    {
        public const string DefaultNickname = "Mom010";

        /// <summary>The nickname is the world seed: the same name always grows the same islands.</summary>
        public string Nickname { get; private set; }
        public List<Player> Players { get; private set; }
        public GameStage Stage { get; private set; }
        public int Turn { get; private set; }
        public Phase Phase { get; private set; }
        public List<TechId> Researched { get; private set; }
        /// <summary><c>null</c> while the game goes on.</summary>
        public GameOutcome Outcome { get; private set; }
        /// <summary>Every player's rolled dice. Set only while the tax phase is open.</summary>
        public List<TaxPlan> TaxPlans { get; private set; }
        /// <summary>What the player's last tax phase paid, for the view to animate.</summary>
        public List<TaxPayout> LastPayouts { get; private set; }
        /// <summary>The player's spin at the end of the last tax phase, or <c>null</c> when the slot stayed off.</summary>
        public SlotSpin LastSlotSpin { get; private set; }
        /// <summary>The global map: the sphere of cells the islands fly over.</summary>
        public World World { get; private set; }
        /// <summary>Staying in a cell dumps toxicity into it, so a flight has to be remembered.</summary>
        public bool MovedThisTurn { get; private set; }
        /// <summary>The event the trail threw when the last scout phase closed, or <c>null</c>.</summary>
        public TrailEvent LastTrailEvent { get; private set; }
        /// <summary>The running level of the clear phase, or <c>null</c> outside it.</summary>
        public CleanupSim Battle { get; private set; }
        /// <summary>Set when the level ends, while the clear phase is still open. The results screen shows it.</summary>
        public CleanupResult BattleResult { get; private set; }
        /// <summary>What the last battle handed back to the island.</summary>
        public CleanupResult LastBattleResult { get; private set; }

        private int _soilCleansedTurn;

        /// <summary>Raised after any change of the state.</summary>
        public event Action Changed;
        /// <summary>A short message for the player: why an action was refused, or what a rival did.</summary>
        public event Action<string> Notice;

        public GameSession(string nickname)
        {
            string trimmed = (nickname ?? "").Trim();

            Nickname = trimmed.Length > 0 ? trimmed : DefaultNickname;
            Players = IslandGen.CreatePlayers(Nickname);
            World = WorldGen.Create(Nickname, Players.ConvertAll(player => player.Id), out Dictionary<string, string> placement);
            foreach (Player player in Players)
            {
                player.CellId = placement.TryGetValue(player.Id, out string cellId) ? cellId : "";
            }

            Researched = new List<TechId>();
            Turn = 1;
            Phase = Phase.Build;
            Stage = GameStage.Setup;

            // The prototype lets the rivals place their strongholds a second apart.
            // The hex is seeded per rival, so the moment does not change it.
            foreach (Player player in Players)
            {
                if (player.IsHuman)
                {
                    continue;
                }

                string hexId = Stronghold.PickHex(player, Rng.FromText(Nickname + ":stronghold:" + player.Id));
                if (hexId != null)
                {
                    Stronghold.Place(player, hexId);
                }
            }
        }

        public Player Human
        {
            get { return Players.Find(player => player.Id == IslandGen.HumanPlayerId); }
        }

        public TechEffects Effects
        {
            get { return Techs.Effects(Researched); }
        }

        public TaxPlan HumanTaxPlan
        {
            get { return TaxPlans?.Find(plan => plan.PlayerId == IslandGen.HumanPlayerId); }
        }

        public bool IsBuildingAllowed
        {
            get { return Stage == GameStage.Play && Phase == Phase.Build && Outcome == null; }
        }

        public bool IsTaxOpen
        {
            get { return Stage == GameStage.Play && Phase == Phase.Tax && Outcome == null && TaxPlans != null; }
        }

        /// <summary>
        /// A click on a hex during setup. The first click places the stronghold, and
        /// a later click moves it. The choice is final once the game has started.
        /// </summary>
        public bool PlaceStronghold(string hexId)
        {
            if (Stage != GameStage.Setup)
            {
                return false;
            }

            if (!Stronghold.CanPlace(Human, hexId))
            {
                return Refuse("Твердыню можно поставить только на свободный гекс своего острова");
            }

            Stronghold.Place(Human, hexId);
            RaiseChanged();

            return true;
        }

        /// <summary>"Начать". It is refused until the player's stronghold stands.</summary>
        public bool StartGame()
        {
            if (Stage != GameStage.Setup)
            {
                return false;
            }

            if (Human.StrongholdHexId == null)
            {
                return Refuse("Сначала поставьте твердыню");
            }

            Stage = GameStage.Play;
            RaiseChanged();

            return true;
        }

        public bool Build(string hexId, BuildingId buildingId)
        {
            Player player = Human;
            HexTile hex = player.FindHex(hexId);
            if (!IsBuildingAllowed || hex == null)
            {
                return false;
            }

            Building building = Buildings.Get(buildingId);
            int discount = Effects.StoneDiscount;
            string refusal = BuildRules.BuildRefusal(player, hex, building, discount);
            if (refusal != null)
            {
                return Refuse(refusal);
            }

            Pay(player, Buildings.EffectiveCost(building, discount));
            hex.Building = building.Id;
            RaiseChanged();

            return true;
        }

        public bool Demolish(string hexId)
        {
            Player player = Human;
            HexTile hex = player.FindHex(hexId);
            if (!IsBuildingAllowed || hex == null)
            {
                return false;
            }

            string refusal = BuildRules.DemolishRefusal(player, hex);
            if (refusal != null)
            {
                return Refuse(refusal);
            }

            BuildRules.Demolish(hex);
            RaiseChanged();

            return true;
        }

        /// <summary>Why the stronghold cannot cleanse soil right now, or <c>null</c> when it can.</summary>
        public string SoilCleanseBlock()
        {
            if (!IsBuildingAllowed)
            {
                return "Очистка почвы доступна только в фазе строительства";
            }

            return BuildRules.SoilCleanseRefusal(Human, _soilCleansedTurn == Turn);
        }

        /// <summary>
        /// The stronghold's soil cleansing: one hex leaves the island, and the
        /// toxicity of another drops to 0. It costs no resources and works once per turn.
        /// </summary>
        public bool CleanseSoil(string sacrificeHexId, string purifyHexId)
        {
            string block = SoilCleanseBlock();
            if (block != null)
            {
                return Refuse(block);
            }

            Player player = Human;
            HexTile sacrifice = player.FindHex(sacrificeHexId);
            HexTile target = player.FindHex(purifyHexId);
            if (sacrifice == null || target == null)
            {
                return false;
            }

            string refusal = BuildRules.SacrificeRefusal(player, sacrifice) ?? BuildRules.PurifyRefusal(target, sacrificeHexId);
            if (refusal != null)
            {
                return Refuse(refusal);
            }

            BuildRules.CleanseSoil(player, sacrificeHexId, purifyHexId);
            _soilCleansedTurn = Turn;
            RaiseChanged();

            return true;
        }

        /// <summary>
        /// Makes a face the one the building pays. Nothing is paid and no power
        /// leaves the pool here: the plan remembers the choice until the phase ends.
        /// </summary>
        public bool PickTaxFace(string hexId, int faceIndex)
        {
            TaxPlan plan = HumanTaxPlan;
            if (!IsTaxOpen || plan == null)
            {
                return false;
            }

            string refusal = plan.PickRefusal(Human, hexId, faceIndex);
            if (refusal != null)
            {
                return Refuse(refusal);
            }

            plan.Pick(hexId, faceIndex);
            RaiseChanged();

            return true;
        }

        /// <summary>Science buys technologies. Nothing else spends it.</summary>
        public bool Research(TechId techId)
        {
            if (Outcome != null)
            {
                return false;
            }

            Player player = Human;
            Tech tech = Techs.Get(techId);
            if (!Techs.IsAvailable(tech, Researched))
            {
                return Refuse("«" + tech.Label + "» пока недоступна");
            }

            if (player.Resources[ResourceId.Science] < tech.Cost)
            {
                return Refuse("Не хватает науки на «" + tech.Label + "»: нужно " + tech.Cost + " науки");
            }

            Researched.Add(tech.Id);
            player.Techs += 1;
            player.Resources[ResourceId.Science] -= tech.Cost;
            RaiseChanged();

            return true;
        }

        /// <summary>
        /// The end-turn button. Ending the build phase opens the tax phase with
        /// every die rolled. Ending the tax phase pays every player, spins the
        /// slots and starts the next turn.
        /// </summary>
        public void EndPhase()
        {
            if (Stage != GameStage.Play || Outcome != null)
            {
                return;
            }

            if (Phase == Phase.Build)
            {
                foreach (Player rival in ActiveRivals())
                {
                    PlayRivalBuild(rival);
                }

                // A converter built this turn wins at once.
                if (!ConcludeIfGameOver())
                {
                    Phase = Phase.Tax;
                    StartTaxPhase();
                }

                RaiseChanged();

                return;
            }

            if (Phase == Phase.Tax)
            {
                CollectTaxes();
                // The scout phase opens: nothing is spent yet, and no flight has been made.
                Phase = Phase.Scout;
                MovedThisTurn = false;
                LastTrailEvent = null;
                RaiseChanged();

                return;
            }

            HashSet<string> ruinedNow = null;

            if (Phase == Phase.Scout)
            {
                foreach (Player rival in ActiveRivals())
                {
                    PlayRivalScout(rival);
                }

                // An island that did not fly leaves its toxicity in the cell it sat in.
                SettleExploration();
                Phase = Phase.Clear;

                foreach (Player rival in ActiveRivals())
                {
                    PlayRivalClear(rival);
                }

                // A cell with no wild islands has nothing to clear: the turn ends at once.
                if (WorldRules.PendingIslands(World.Get(Human.CellId), Human) > 0)
                {
                    EnterClearing();
                    RaiseChanged();

                    return;
                }
            }
            else if (Phase == Phase.Clear)
            {
                // The level has to end before the turn can: the way out is a window in the plumes.
                if (Battle != null && Battle.Status == SimStatus.Running)
                {
                    Refuse("Бой ещё идёт: зачистите острова или уведите остров через окно в облаках");

                    return;
                }

                ruinedNow = FinishClearing();
            }

            FinishTurn(ruinedNow);
            RaiseChanged();
        }

        /// <summary>Builds the level from the cell the island is over and levies the army.</summary>
        private void EnterClearing()
        {
            Player player = Human;
            WorldCell cell = World.Get(player.CellId);
            Rng rng = Rng.FromText(Nickname + ":cleanup:" + Turn);
            List<Unit> roster = Units.BuildRoster(player.Resources[ResourceId.Population], Effects.UnlockedUnits, rng);
            LevelSpec level = CleanupLevel.Create(
                new LevelSetup
                {
                    Player = player,
                    Turn = Turn,
                    IslandCount = WorldRules.PendingIslands(cell, player),
                    CellBiome = cell.Biome,
                    ToxicTrail = cell.ToxicTrail,
                    Roster = roster,
                    Boss = cell.Boss,
                },
                rng);

            BattleResult = null;
            Battle = CleanupSim.Create(level, Rng.HashSeed(Nickname + ":cleanup-sim:" + Turn));
        }

        /// <summary>
        /// Runs whole ticks of the battle. The view owns the frame loop and asks for
        /// them. The result is taken the moment the level ends.
        /// </summary>
        public void StepBattle(int ticks)
        {
            if (Battle == null)
            {
                return;
            }

            for (int index = 0; index < ticks && Battle.Status == SimStatus.Running; index += 1)
            {
                Battle.Step();
            }

            if (Battle.Status != SimStatus.Running && BattleResult == null)
            {
                BattleResult = Battle.Result();
                RaiseChanged();
            }
        }

        /// <summary>Casts the battle's skill at a hex. A refused cast says why.</summary>
        public bool CastShatter(int islandIndex, int hexIndex)
        {
            if (Battle == null)
            {
                return false;
            }

            string refusal = Battle.CastShatter(islandIndex, hexIndex);

            return refusal == null || Refuse(refusal);
        }

        /// <summary>
        /// Hands the result back to the island. The dead cost their people, the
        /// battle's damage stays on the buildings, the annexed hexes join the rim
        /// with the toxicity they carried, and the cell keeps only the islands that
        /// still stand. Returns the strongholds ruined in this battle, which skip
        /// this turn's repair.
        /// </summary>
        private HashSet<string> FinishClearing()
        {
            HashSet<string> ruinedNow = new HashSet<string>();
            if (Battle == null)
            {
                return ruinedNow;
            }

            Battle.Retreat();
            CleanupResult result = BattleResult ?? Battle.Result();
            Player player = Human;

            // A razed building leaves an empty hex; a razed stronghold stays as ruins at 0 hp.
            foreach (StructureResult entry in result.Structures)
            {
                HexTile hex = player.FindHex(entry.HexId);
                if (hex == null || !StructureHp.TryGet(player, hex, out StructureKind kind, out _, out _))
                {
                    continue;
                }

                if (entry.Hp <= 0 && kind != StructureKind.Stronghold)
                {
                    hex.Building = null;
                    hex.DamagedKind = null;
                    hex.DamagedHp = 0;

                    continue;
                }

                if (entry.Hp <= 0 && entry.StartHp > 0)
                {
                    ruinedNow.Add(hex.Id);
                }

                StructureHp.Set(hex, kind, entry.Hp);
            }

            // Hexes destroyed in battle leave the island; the plumes' poison stays on the rest.
            player.Hexes.RemoveAll(hex => result.DestroyedHexIds.Contains(hex.Id));
            foreach (HexTile hex in player.Hexes)
            {
                if (result.Poisoned.TryGetValue(hex.Id, out int toxicity))
                {
                    hex.Toxicity = Math.Max(hex.Toxicity, toxicity);
                }
            }

            // The islands that joined in battle keep their exact battle coordinates.
            CleanupLevel.JoinAnnexed(player, result.Annexed);

            int peopleLost = 0;
            foreach (Unit unit in result.Lost)
            {
                peopleLost += unit.Upkeep;
            }

            player.Army = result.Survivors.Count;
            player.Resources[ResourceId.Population] = Math.Max(0, player.Resources[ResourceId.Population] - peopleLost);
            player.Resources[ResourceId.Mana] = Math.Max(0, player.Resources[ResourceId.Mana] - result.ManaSpent);

            WorldCell cell = World.Get(player.CellId);

            // The lair never clears: the boss waits for every other player. Its
            // defeat gives this player the trophy technology instead.
            if (cell != null && cell.Boss)
            {
                if (result.TotalIslands > 0 && result.ClearedIslands >= result.TotalIslands)
                {
                    if (!player.BossSlain)
                    {
                        player.BossSlain = true;
                        player.Techs += 1;
                    }

                    if (!Researched.Contains(Techs.BossTechId))
                    {
                        Researched.Add(Techs.BossTechId);
                    }

                    Notice?.Invoke("Повелитель Мора повержен! Открыт «Центральный конвертер»");
                }
            }
            else if (cell != null && cell.Kind == CellKind.Island && result.TotalIslands > 0)
            {
                int standing = result.TotalIslands - result.ClearedIslands;
                cell.IslandCount = standing;
                cell.Cleared = standing == 0;
            }

            LastBattleResult = result;
            Battle = null;
            BattleResult = null;

            return ruinedNow;
        }

        public bool IsExplorationOpen
        {
            get { return Stage == GameStage.Play && Phase == Phase.Scout && Outcome == null; }
        }

        /// <summary>Spends scouting on one frontier cell and reveals what is in it.</summary>
        public bool Scout(string cellId)
        {
            if (!IsExplorationOpen)
            {
                return false;
            }

            Player player = Human;
            ScoutCheck check = WorldRules.CheckScout(World, player.CellId, cellId, player.Resources[ResourceId.Scouting]);
            if (!check.Ok)
            {
                return Refuse(check.Refusal);
            }

            World.Get(cellId).Revealed = true;
            player.Resources[ResourceId.Scouting] -= check.Cost;
            RaiseChanged();

            return true;
        }

        /// <summary>One flight per turn, and only to a free cell that touches the current one.</summary>
        public bool MoveIsland(string cellId)
        {
            if (!IsExplorationOpen)
            {
                return false;
            }

            Player player = Human;
            string refusal = WorldRules.MoveRefusal(World, player.CellId, cellId, MovedThisTurn);
            if (refusal != null)
            {
                return Refuse(refusal);
            }

            WorldCell target = World.Get(cellId);
            World.Get(player.CellId).OwnerId = null;
            target.OwnerId = player.Id;
            target.Revealed = true;
            // Flying into an island cell wakes its wild islands for the cleanup.
            target.Activated = target.Activated || target.Kind == CellKind.Island;
            player.CellId = cellId;
            MovedThisTurn = true;
            RaiseChanged();

            return true;
        }

        /// <summary>
        /// Closing the scout phase. An island that did not fly leaves its whole
        /// toxicity in the cell, and the trail may throw an event back at it.
        /// </summary>
        private void SettleExploration()
        {
            Player player = Human;
            WorldCell here = World.Get(player.CellId);
            if (MovedThisTurn || here == null)
            {
                return;
            }

            // The trail takes the island's hex load, not the meter: the meter is the
            // island's own reckoning, the trail is what it leaves in the world.
            here.ToxicTrail += Tax.TotalToxicity(player);

            Rng rng = Rng.FromText(Nickname + ":trail:" + Turn);
            TrailEvent trailEvent = TrailEvents.Roll(here.ToxicTrail, rng);
            if (trailEvent == null)
            {
                return;
            }

            LastTrailEvent = trailEvent;
            ResourcePool pool = player.Resources;

            switch (trailEvent.Id)
            {
                case TrailEventId.Bandits:
                    pool[ResourceId.Stone] = (int)Math.Floor(pool[ResourceId.Stone] * 0.75);
                    pool[ResourceId.Wood] = (int)Math.Floor(pool[ResourceId.Wood] * 0.75);
                    break;
                case TrailEventId.Undead:
                    pool[ResourceId.Population] = Math.Max(0, pool[ResourceId.Population] - 2);
                    break;
                case TrailEventId.Madness:
                    int moved = Math.Min(2, pool[ResourceId.Population]);
                    pool[ResourceId.Population] -= moved;
                    pool[ResourceId.Mad] += moved;
                    break;
                default:
                    // The worm: it eats a building and leaves the hex filthy.
                    List<HexTile> built = player.Hexes.FindAll(hex => hex.Building.HasValue);
                    if (built.Count > 0)
                    {
                        HexTile victim = built[rng.NextIndex(built.Count)];
                        victim.Building = null;
                        victim.Toxicity = Math.Min(100, victim.Toxicity + 30);
                    }

                    break;
            }
        }

        /// <summary>The scout phase of a rival: one flight toward the lair, once the hunt has started.</summary>
        private void PlayRivalScout(Player rival)
        {
            WorldCell lair = World.BossCell;
            WorldCell here = World.Get(rival.CellId);
            if (lair == null || here == null || Turn < RivalAi.HuntStartTurn)
            {
                return;
            }

            // A rival with the trophy leaves the lair free for the others. Before
            // that, it flies toward the lair. The lair may be taken by another
            // island; the rival then waits.
            string nextId = null;
            if (!rival.BossSlain)
            {
                nextId = RivalAi.StepToward(World, rival.CellId, lair.Id);
            }
            else if (here == lair)
            {
                foreach (int neighbor in here.Neighbors)
                {
                    if (World.Cells[neighbor].OwnerId == null)
                    {
                        nextId = World.Cells[neighbor].Id;

                        break;
                    }
                }
            }

            if (nextId == null)
            {
                return;
            }

            here.OwnerId = null;
            World.Get(nextId).OwnerId = rival.Id;
            rival.CellId = nextId;
        }

        /// <summary>The clearing phase of a rival: one in the lair fights the boss.</summary>
        private void PlayRivalClear(Player rival)
        {
            WorldCell lair = World.BossCell;
            if (lair == null || rival.BossSlain || rival.CellId != lair.Id)
            {
                return;
            }

            RivalAi.FightBoss(rival, Rng.FromText(Nickname + ":boss:" + Turn + ":" + rival.Id));
            if (rival.BossSlain)
            {
                Notice?.Invoke(rival.Nickname + " победил Повелителя Мора");
            }
        }

        /// <summary>
        /// Opens the phase: every player's dice are rolled, and every bot makes its
        /// picks at once.
        /// </summary>
        private void StartTaxPhase()
        {
            LastPayouts = null;
            LastSlotSpin = null;
            TaxPlans = new List<TaxPlan>();

            foreach (Player player in Players)
            {
                TaxPlan plan = TaxPlan.Roll(player, Turn, Nickname);
                if (!player.IsHuman)
                {
                    plan.ApplyBotPicks(player, TechEffects.None);
                }

                TaxPlans.Add(plan);
            }
        }

        /// <summary>Every plan is paid, then every toxicity slot spins. A rival's spin is silent.</summary>
        private void CollectTaxes()
        {
            if (TaxPlans == null)
            {
                return;
            }

            foreach (Player rival in ActiveRivals())
            {
                TaxPlan plan = TaxPlans.Find(candidate => candidate.PlayerId == rival.Id);
                if (plan != null)
                {
                    plan.Collect(rival, TechEffects.None);
                    ToxicSlot.Roll(rival, Turn, Nickname);
                }
            }

            Player human = Human;
            TaxPlan humanPlan = HumanTaxPlan;
            if (humanPlan != null)
            {
                LastPayouts = humanPlan.Collect(human, Effects);
                LastSlotSpin = ToxicSlot.Roll(human, Turn, Nickname);
            }

            TaxPlans = null;
        }

        /// <summary>The end of a turn: the free repair, the second end-of-game check, the next build phase.</summary>
        private void FinishTurn(ICollection<string> ruinedNow)
        {
            StructureHp.RepairIsland(Human, ruinedNow);

            if (ConcludeIfGameOver())
            {
                return;
            }

            Turn += 1;
            Phase = Phase.Build;
        }

        /// <summary>
        /// The end-of-game check. It runs twice a turn: when the build phase ends
        /// and when the turn ends. Returns <c>true</c> when the game is over.
        /// </summary>
        private bool ConcludeIfGameOver()
        {
            Outcome = GameOver.Check(Players, IslandGen.HumanPlayerId, Turn);

            return Outcome != null;
        }

        /// <summary>The build phase of a rival: one with the trophy builds the converter once it can pay.</summary>
        private void PlayRivalBuild(Player rival)
        {
            if (!rival.BossSlain || Buildings.HasConverter(rival))
            {
                return;
            }

            // The rivals have no technologies of their own, so no discount either.
            Building converter = Buildings.Get(BuildingId.Converter);
            if (!Buildings.CanAfford(rival.Resources, converter, 0))
            {
                return;
            }

            HexTile hex = rival.FindHex(RivalAi.PickConverterHex(rival));
            if (hex == null)
            {
                return;
            }

            Pay(rival, Buildings.EffectiveCost(converter, 0));
            hex.DamagedKind = null;
            hex.DamagedHp = 0;
            hex.Building = converter.Id;
            Notice?.Invoke(rival.Nickname + " строит «" + converter.Label + "»");
        }

        private IEnumerable<Player> ActiveRivals()
        {
            foreach (Player player in Players)
            {
                if (!player.IsHuman && !player.Eliminated)
                {
                    yield return player;
                }
            }
        }

        private static void Pay(Player player, BuildCost cost)
        {
            player.Resources[ResourceId.Stone] -= cost.Stone;
            player.Resources[ResourceId.Wood] -= cost.Wood;
            player.Resources[ResourceId.Hammers] -= cost.Hammers;
        }

        private bool Refuse(string message)
        {
            Notice?.Invoke(message);

            return false;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
