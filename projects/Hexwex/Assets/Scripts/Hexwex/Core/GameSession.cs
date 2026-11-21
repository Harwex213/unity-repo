using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>
    /// What one human seat has going on in the current turn: the things that are
    /// that person's own and not the island's or the world's.
    /// </summary>
    public sealed class Seat
    {
        public string PlayerId;
        /// <summary>The person has ended the current phase and waits for the others.</summary>
        public bool IsReady;
        /// <summary>What the last tax phase paid, for the view to animate.</summary>
        public List<TaxPayout> LastPayouts;
        /// <summary>The spin at the end of the last tax phase, or <c>null</c> when the slot stayed off.</summary>
        public SlotSpin LastSlotSpin;
        /// <summary>Staying in a cell dumps toxicity into it, so a flight has to be remembered.</summary>
        public bool MovedThisTurn;
        /// <summary>The event the trail threw when the last scout phase closed, or <c>null</c>.</summary>
        public TrailEvent LastTrailEvent;
        /// <summary>The running level of the clear phase, or <c>null</c> outside it.</summary>
        public CleanupSim Battle;
        /// <summary>Set when the level ends, while the clear phase is still open.</summary>
        public CleanupResult BattleResult;
        /// <summary>What the last battle handed back to the island.</summary>
        public CleanupResult LastBattleResult;
        /// <summary>Strongholds ruined in this turn's battle: they skip this turn's repair.</summary>
        public HashSet<string> RuinedNow;
        public int SoilCleansedTurn;
    }

    /// <summary>
    /// One game of Toxic Island. It stands where the prototype's <c>store/</c> and
    /// <c>domain/</c> stood: it owns the only mutable state, and every action goes
    /// through it. The view reads the state, calls the actions and listens to
    /// <see cref="Changed"/> and <see cref="Notice"/>; it never writes.
    ///
    /// A session has one or more human seats and bots in the other slots (see
    /// <see cref="IslandGen.CreatePlayers(string, IReadOnlyList{string})"/>). Every
    /// action names the player it is for. The overloads without a player act for
    /// seat 0, which is all a single-player game has.
    ///
    /// The phases are played together: a person who ends a phase is ready, and the
    /// phase moves on when every person still in the game is ready. The bots play
    /// their part at that moment. With one person the phase moves on at once, as
    /// it always did. Every roll is seeded by the world seed, the turn and the
    /// player, and seat 0 keeps the single player's seeds.
    ///
    /// A turn is build, tax, scout, clear. The clear phase is a real-time battle,
    /// one for each person whose cell holds wild islands: the view asks for its
    /// ticks through <see cref="StepBattle(string, int)"/>.
    /// </summary>
    public sealed class GameSession
    {
        public const string DefaultNickname = "Mom010";

        private const string PopulationPhaseNotice = "Жителей можно перераспределять только в фазе строительства";

        /// <summary>The nickname is the world seed: the same name always grows the same islands.</summary>
        public string Nickname { get; private set; }
        public List<Player> Players { get; private set; }
        public GameStage Stage { get; private set; }
        public int Turn { get; private set; }
        public Phase Phase { get; private set; }
        /// <summary>The end of the game for everyone, or <c>null</c> while it goes on. Its kind and reason are seat 0's; see <see cref="OutcomeFor"/>.</summary>
        public GameOutcome Outcome { get; private set; }
        /// <summary>Every player's rolled dice. Set only while the tax phase is open.</summary>
        public List<TaxPlan> TaxPlans { get; private set; }
        /// <summary>The global map: the sphere of cells the islands fly over.</summary>
        public World World { get; private set; }
        /// <summary>The human seats, in seat order. Seat 0 comes first.</summary>
        public List<string> HumanIds { get; private set; }

        private readonly Dictionary<string, Seat> _seats = new Dictionary<string, Seat>();

        /// <summary>Raised after any change of the state.</summary>
        public event Action Changed;
        /// <summary>A short message for seat 0: why its action was refused, or what a rival did.</summary>
        public event Action<string> Notice;
        /// <summary>
        /// A short message for one person, by player id, or for everyone when the id
        /// is <c>null</c>. A host sends each one where it belongs.
        /// </summary>
        public event Action<string, string> PlayerNotice;

        /// <summary>A single-player game: one person, and the three rivals.</summary>
        public GameSession(string nickname)
            : this(nickname, null)
        {
        }

        /// <summary>
        /// A game with a seat for each of <paramref name="humanNicknames"/>; bots fill
        /// the rest. <paramref name="nickname"/> is the world seed. With no names, seat 0
        /// takes the seed as its name, as a single player does.
        /// </summary>
        public GameSession(string nickname, IReadOnlyList<string> humanNicknames)
        {
            string trimmed = (nickname ?? "").Trim();

            Nickname = trimmed.Length > 0 ? trimmed : DefaultNickname;
            Players = IslandGen.CreatePlayers(Nickname, humanNicknames != null && humanNicknames.Count > 0 ? humanNicknames : new[] { Nickname });
            World = WorldGen.Create(Nickname, Players.ConvertAll(player => player.Id), out Dictionary<string, string> placement);
            HumanIds = new List<string>();
            foreach (Player player in Players)
            {
                player.CellId = placement.TryGetValue(player.Id, out string cellId) ? cellId : "";
                if (player.IsHuman)
                {
                    HumanIds.Add(player.Id);
                    _seats[player.Id] = new Seat { PlayerId = player.Id };
                }
            }

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

        /* ---------- seat 0: what a single-player game reads ---------- */

        public Player Human
        {
            get { return Players.Find(player => player.Id == IslandGen.HumanPlayerId); }
        }

        /// <summary>Seat 0's technologies.</summary>
        public List<TechId> Researched
        {
            get { return Human.Researched; }
        }

        public TechEffects Effects
        {
            get { return EffectsOf(IslandGen.HumanPlayerId); }
        }

        public TaxPlan HumanTaxPlan
        {
            get { return TaxPlanOf(IslandGen.HumanPlayerId); }
        }

        public List<TaxPayout> LastPayouts
        {
            get { return SeatOf(IslandGen.HumanPlayerId).LastPayouts; }
        }

        public SlotSpin LastSlotSpin
        {
            get { return SeatOf(IslandGen.HumanPlayerId).LastSlotSpin; }
        }

        public bool MovedThisTurn
        {
            get { return SeatOf(IslandGen.HumanPlayerId).MovedThisTurn; }
        }

        public TrailEvent LastTrailEvent
        {
            get { return SeatOf(IslandGen.HumanPlayerId).LastTrailEvent; }
        }

        public CleanupSim Battle
        {
            get { return SeatOf(IslandGen.HumanPlayerId).Battle; }
        }

        public CleanupResult BattleResult
        {
            get { return SeatOf(IslandGen.HumanPlayerId).BattleResult; }
        }

        public CleanupResult LastBattleResult
        {
            get { return SeatOf(IslandGen.HumanPlayerId).LastBattleResult; }
        }

        /* ---------- any seat ---------- */

        /// <summary>The seat of that person, or <c>null</c> for a bot or an unknown id.</summary>
        public Seat SeatOf(string playerId)
        {
            return playerId != null && _seats.TryGetValue(playerId, out Seat seat) ? seat : null;
        }

        public Player PlayerOf(string playerId)
        {
            return Players.Find(player => player.Id == playerId);
        }

        public TechEffects EffectsOf(string playerId)
        {
            Player player = PlayerOf(playerId);

            return player != null ? Techs.Effects(player.Researched) : TechEffects.None;
        }

        public TaxPlan TaxPlanOf(string playerId)
        {
            return TaxPlans?.Find(plan => plan.PlayerId == playerId);
        }

        /// <summary>
        /// How the game has ended for that person: the shared end once the game is
        /// over, or their own defeat when they have left a game the others play on.
        /// <c>null</c> while they play.
        /// </summary>
        public GameOutcome OutcomeFor(string playerId)
        {
            if (Outcome != null)
            {
                return Outcome.For(playerId);
            }

            Player player = PlayerOf(playerId);
            if (player != null && player.Eliminated)
            {
                return new GameOutcome
                {
                    Kind = GameOutcomeKind.Defeat,
                    Reason = player.EliminatedBy ?? GameOutcomeReason.Ruined,
                    WinnerIds = new List<string>(),
                    Turn = Turn,
                };
            }

            return null;
        }

        /// <summary>Whether that person has ended the current phase and waits for the others.</summary>
        public bool IsReady(string playerId)
        {
            Seat seat = SeatOf(playerId);

            return seat != null && seat.IsReady;
        }

        /// <summary>The people the current phase still waits for.</summary>
        public List<string> WaitingFor()
        {
            return HumanIds.FindAll(id => IsInPlay(id) && !SeatOf(id).IsReady);
        }

        public bool IsBuildingAllowed
        {
            get { return Stage == GameStage.Play && Phase == Phase.Build && Outcome == null; }
        }

        public bool IsTaxOpen
        {
            get { return Stage == GameStage.Play && Phase == Phase.Tax && Outcome == null && TaxPlans != null; }
        }

        public bool IsExplorationOpen
        {
            get { return Stage == GameStage.Play && Phase == Phase.Scout && Outcome == null; }
        }

        /* ---------- setup ---------- */

        public bool PlaceStronghold(string hexId)
        {
            return PlaceStronghold(IslandGen.HumanPlayerId, hexId);
        }

        /// <summary>
        /// A click on a hex during setup. The first click places the stronghold, and
        /// a later click moves it. The choice is final once the game has started.
        /// </summary>
        public bool PlaceStronghold(string playerId, string hexId)
        {
            Player player = Person(playerId);
            if (Stage != GameStage.Setup || player == null)
            {
                return false;
            }

            if (!Stronghold.CanPlace(player, hexId))
            {
                return Refuse(playerId, "Твердыню можно поставить только на свободный гекс своего острова");
            }

            Stronghold.Place(player, hexId);
            RaiseChanged();

            return true;
        }

        /// <summary>"Начать". It is refused until every person's stronghold stands.</summary>
        public bool StartGame()
        {
            if (Stage != GameStage.Setup)
            {
                return false;
            }

            if (Human.StrongholdHexId == null)
            {
                return Refuse(IslandGen.HumanPlayerId, "Сначала поставьте твердыню");
            }

            List<string> missing = HumanIds.FindAll(id => PlayerOf(id).StrongholdHexId == null);
            if (missing.Count > 0)
            {
                return Refuse(IslandGen.HumanPlayerId, "Ещё не все поставили твердыню: " + string.Join(", ", missing.ConvertAll(id => PlayerOf(id).Nickname)));
            }

            Stage = GameStage.Play;
            RaiseChanged();

            return true;
        }

        /* ---------- the build phase ---------- */

        public bool Build(string hexId, BuildingId buildingId)
        {
            return Build(IslandGen.HumanPlayerId, hexId, buildingId);
        }

        public bool Build(string playerId, string hexId, BuildingId buildingId)
        {
            Player player = PersonInPlay(playerId);
            HexTile hex = player?.FindHex(hexId);
            if (!IsBuildingAllowed || hex == null)
            {
                return false;
            }

            Building building = Buildings.Get(buildingId);
            int discount = EffectsOf(playerId).StoneDiscount;
            string refusal = BuildRules.BuildRefusal(player, hex, building, discount);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            Pay(player, Buildings.EffectiveCost(building, discount));
            hex.Building = building.Id;

            // A new building takes its workers from the idle people, if there are enough.
            hex.Workers = 0;
            bool isStaffed = Population.Of(player).Idle >= Population.WorkersPerBuilding;
            hex.Workers = isStaffed ? Population.WorkersPerBuilding : 0;
            if (!isStaffed)
            {
                Tell(playerId, "«" + building.Label + "» не работает: нет свободных жителей");
            }

            RaiseChanged();

            return true;
        }

        public bool Demolish(string hexId)
        {
            return Demolish(IslandGen.HumanPlayerId, hexId);
        }

        public bool Demolish(string playerId, string hexId)
        {
            Player player = PersonInPlay(playerId);
            HexTile hex = player?.FindHex(hexId);
            if (!IsBuildingAllowed || hex == null)
            {
                return false;
            }

            string refusal = BuildRules.DemolishRefusal(player, hex);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            BuildRules.Demolish(hex);
            RaiseChanged();

            return true;
        }

        public string SoilCleanseBlock()
        {
            return SoilCleanseBlock(IslandGen.HumanPlayerId);
        }

        /// <summary>Why the stronghold cannot cleanse soil right now, or <c>null</c> when it can.</summary>
        public string SoilCleanseBlock(string playerId)
        {
            if (!IsBuildingAllowed)
            {
                return "Очистка почвы доступна только в фазе строительства";
            }

            Player player = PersonInPlay(playerId);
            if (player == null)
            {
                return "Вы выбыли из игры";
            }

            return BuildRules.SoilCleanseRefusal(player, SeatOf(playerId).SoilCleansedTurn == Turn);
        }

        public bool CleanseSoil(string sacrificeHexId, string purifyHexId)
        {
            return CleanseSoil(IslandGen.HumanPlayerId, sacrificeHexId, purifyHexId);
        }

        /// <summary>
        /// The stronghold's soil cleansing: one hex leaves the island, and the
        /// toxicity of another drops to 0. It costs no resources and works once per turn.
        /// </summary>
        public bool CleanseSoil(string playerId, string sacrificeHexId, string purifyHexId)
        {
            string block = SoilCleanseBlock(playerId);
            if (block != null)
            {
                return Refuse(playerId, block);
            }

            Player player = PersonInPlay(playerId);
            HexTile sacrifice = player.FindHex(sacrificeHexId);
            HexTile target = player.FindHex(purifyHexId);
            if (sacrifice == null || target == null)
            {
                return false;
            }

            string refusal = BuildRules.SacrificeRefusal(player, sacrifice) ?? BuildRules.PurifyRefusal(target, sacrificeHexId);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            BuildRules.CleanseSoil(player, sacrificeHexId, purifyHexId);
            SeatOf(playerId).SoilCleansedTurn = Turn;
            RaiseChanged();

            return true;
        }

        public bool SetHexWorkers(string hexId, int count)
        {
            return SetHexWorkers(IslandGen.HumanPlayerId, hexId, count);
        }

        /// <summary>
        /// Puts <paramref name="count"/> workers into the building on the hex, or
        /// takes them out. People move between idle, the army and the buildings only
        /// in the build phase: the tax phase has already rolled the dice of the
        /// working buildings, and the clear phase has already fielded the army.
        /// </summary>
        public bool SetHexWorkers(string playerId, string hexId, int count)
        {
            Player player = PersonInPlay(playerId);
            if (Outcome != null || Stage != GameStage.Play || player == null)
            {
                return false;
            }

            if (!IsBuildingAllowed)
            {
                return Refuse(playerId, PopulationPhaseNotice);
            }

            string refusal = Population.StaffRefusal(player, hexId, count);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            Population.SetStaff(player, hexId, count);
            RaiseChanged();

            return true;
        }

        public bool ChangeArmy(int delta)
        {
            return ChangeArmy(IslandGen.HumanPlayerId, delta);
        }

        /// <summary>Moves <paramref name="delta"/> idle people into the army, or soldiers back to idle when it is negative.</summary>
        public bool ChangeArmy(string playerId, int delta)
        {
            Player player = PersonInPlay(playerId);
            if (Outcome != null || Stage != GameStage.Play || player == null)
            {
                return false;
            }

            if (!IsBuildingAllowed)
            {
                return Refuse(playerId, PopulationPhaseNotice);
            }

            if (delta > 0 && Population.Of(player).Idle <= 0)
            {
                return Refuse(playerId, "Нет свободных жителей");
            }

            Population.SetArmySize(player, player.Army + delta);
            RaiseChanged();

            return true;
        }

        public bool Research(TechId techId)
        {
            return Research(IslandGen.HumanPlayerId, techId);
        }

        /// <summary>Science buys technologies. Nothing else spends it.</summary>
        public bool Research(string playerId, TechId techId)
        {
            Player player = PersonInPlay(playerId);
            if (Outcome != null || player == null)
            {
                return false;
            }

            Tech tech = Techs.Get(techId);
            if (!Techs.IsAvailable(tech, player.Researched))
            {
                return Refuse(playerId, "«" + tech.Label + "» пока недоступна");
            }

            if (player.Resources[ResourceId.Science] < tech.Cost)
            {
                return Refuse(playerId, "Не хватает науки на «" + tech.Label + "»: нужно " + tech.Cost + " науки");
            }

            player.Researched.Add(tech.Id);
            player.Techs += 1;
            player.Resources[ResourceId.Science] -= tech.Cost;
            RaiseChanged();

            return true;
        }

        /* ---------- the tax phase ---------- */

        public bool PickTaxFace(string hexId, int faceIndex)
        {
            return PickTaxFace(IslandGen.HumanPlayerId, hexId, faceIndex);
        }

        /// <summary>
        /// Makes a face the one the building pays. Nothing is paid and no power
        /// leaves the pool here: the plan remembers the choice until the phase ends.
        /// </summary>
        public bool PickTaxFace(string playerId, string hexId, int faceIndex)
        {
            Player player = PersonInPlay(playerId);
            TaxPlan plan = TaxPlanOf(playerId);
            if (!IsTaxOpen || plan == null || player == null)
            {
                return false;
            }

            // A pick of the face already chosen changes nothing.
            if (plan.FindRoll(hexId)?.ChosenIndex == faceIndex)
            {
                return true;
            }

            string refusal = plan.PickRefusal(player, hexId, faceIndex);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            plan.Pick(hexId, faceIndex);
            RaiseChanged();

            return true;
        }

        /* ---------- the scout phase ---------- */

        public bool Scout(string cellId)
        {
            return Scout(IslandGen.HumanPlayerId, cellId);
        }

        /// <summary>Spends scouting on one frontier cell and reveals what is in it.</summary>
        public bool Scout(string playerId, string cellId)
        {
            Player player = PersonInPlay(playerId);
            if (!IsExplorationOpen || player == null)
            {
                return false;
            }

            ScoutCheck check = WorldRules.CheckScout(World, player.CellId, cellId, player.Resources[ResourceId.Scouting]);
            if (!check.Ok)
            {
                return Refuse(playerId, check.Refusal);
            }

            World.Get(cellId).Revealed = true;
            player.Resources[ResourceId.Scouting] -= check.Cost;
            RaiseChanged();

            return true;
        }

        public bool MoveIsland(string cellId)
        {
            return MoveIsland(IslandGen.HumanPlayerId, cellId);
        }

        /// <summary>One flight per turn, and only to a free cell that touches the current one. Two people cannot take the same cell: the first one there has it.</summary>
        public bool MoveIsland(string playerId, string cellId)
        {
            Player player = PersonInPlay(playerId);
            if (!IsExplorationOpen || player == null)
            {
                return false;
            }

            Seat seat = SeatOf(playerId);
            string refusal = WorldRules.MoveRefusal(World, player.CellId, cellId, seat.MovedThisTurn);
            if (refusal != null)
            {
                return Refuse(playerId, refusal);
            }

            WorldCell target = World.Get(cellId);
            World.Get(player.CellId).OwnerId = null;
            target.OwnerId = player.Id;
            target.Revealed = true;
            // Flying into an island cell wakes its wild islands for the cleanup.
            target.Activated = target.Activated || target.Kind == CellKind.Island;
            player.CellId = cellId;
            seat.MovedThisTurn = true;
            RaiseChanged();

            return true;
        }

        /* ---------- the clear phase ---------- */

        public void StepBattle(int ticks)
        {
            StepBattle(IslandGen.HumanPlayerId, ticks);
        }

        /// <summary>
        /// Runs whole ticks of that person's battle. The view owns the frame loop and
        /// asks for them. The result is taken the moment the level ends.
        /// </summary>
        public void StepBattle(string playerId, int ticks)
        {
            Seat seat = SeatOf(playerId);
            if (seat == null || seat.Battle == null)
            {
                return;
            }

            for (int index = 0; index < ticks && seat.Battle.Status == SimStatus.Running; index += 1)
            {
                seat.Battle.Step();
            }

            if (seat.Battle.Status != SimStatus.Running && seat.BattleResult == null)
            {
                seat.BattleResult = seat.Battle.Result();
                RaiseChanged();
            }
        }

        public bool CastShatter(int islandIndex, int hexIndex)
        {
            return CastShatter(IslandGen.HumanPlayerId, islandIndex, hexIndex);
        }

        /// <summary>Casts the battle's skill at a hex. A refused cast says why.</summary>
        public bool CastShatter(string playerId, int islandIndex, int hexIndex)
        {
            Seat seat = SeatOf(playerId);
            if (seat == null || seat.Battle == null)
            {
                return false;
            }

            string refusal = seat.Battle.CastShatter(islandIndex, hexIndex);

            return refusal == null || Refuse(playerId, refusal);
        }

        /// <summary>
        /// Ends that person's battle with a result fought elsewhere: in a game over
        /// the network only the person who fought runs the level, and the others
        /// take its result. The phase then ends for that person as
        /// <see cref="EndPhase(string)"/> ends it.
        /// </summary>
        public bool EndBattle(string playerId, CleanupResult result)
        {
            Seat seat = SeatOf(playerId);
            if (PersonInPlay(playerId) == null || Phase != Phase.Clear || seat.Battle == null || result == null)
            {
                return false;
            }

            seat.BattleResult = result;

            return EndPhase(playerId);
        }

        /// <summary>
        /// That person leaves the game: their island stays where it is, out of play,
        /// and the phases stop waiting for them. Once nobody is left the game ends.
        /// </summary>
        public bool Resign(string playerId)
        {
            Player player = PersonInPlay(playerId);
            if (player == null || Outcome != null)
            {
                return false;
            }

            player.Eliminated = true;
            player.EliminatedBy = GameOutcomeReason.Left;
            Seat seat = SeatOf(playerId);
            seat.Battle = null;
            seat.BattleResult = null;
            seat.IsReady = false;
            Tell(null, player.Nickname + " покидает игру");

            bool anyoneLeft = HumanIds.Exists(IsInPlay);
            if (!anyoneLeft)
            {
                // The last person has gone: the game is over for everyone.
                Outcome = new GameOutcome { Kind = GameOutcomeKind.Defeat, Reason = GameOutcomeReason.Left, WinnerIds = new List<string>(), Turn = Turn };
                foreach (string id in HumanIds)
                {
                    Outcome.Reasons[id] = PlayerOf(id).EliminatedBy ?? GameOutcomeReason.Left;
                }

                Outcome.Reason = Outcome.Reasons[HumanIds[0]];
            }
            else if (Stage == GameStage.Play && WaitingFor().Count == 0)
            {
                Advance();
            }

            RaiseChanged();

            return true;
        }

        /// <summary>
        /// A number for the whole state that a turn can change: every player's island,
        /// pool and standing, the world's cells, the turn and the phase. Two copies of
        /// one game that agree on it are the same game. The peers of a network game
        /// compare it to catch one that has drifted.
        /// </summary>
        public uint Checksum()
        {
            unchecked
            {
                uint hash = 2166136261;

                void Mix(int value)
                {
                    hash = (hash ^ (uint)value) * 16777619;
                }

                void MixText(string text)
                {
                    foreach (char letter in text ?? "")
                    {
                        Mix(letter);
                    }

                    Mix(-1);
                }

                Mix(Turn);
                Mix((int)Phase);
                Mix((int)Stage);
                foreach (Player player in Players)
                {
                    MixText(player.Id);
                    MixText(player.CellId);
                    MixText(player.StrongholdHexId);
                    Mix(player.Army);
                    Mix(player.Techs);
                    Mix(player.ToxicMeter);
                    Mix(player.BossSlain ? 1 : 0);
                    Mix(player.Eliminated ? 1 : 0);
                    foreach (ResourceId id in Enum.GetValues(typeof(ResourceId)))
                    {
                        Mix(player.Resources[id]);
                    }

                    foreach (TechId tech in player.Researched)
                    {
                        Mix((int)tech);
                    }

                    foreach (HexTile hex in player.Hexes)
                    {
                        MixText(hex.Id);
                        Mix((int)hex.Biome);
                        Mix(hex.Building.HasValue ? (int)hex.Building.Value : -1);
                        Mix(hex.Toxicity);
                        Mix(hex.Workers);
                        Mix(hex.DamagedKind.HasValue ? (int)hex.DamagedKind.Value : -1);
                        Mix(hex.DamagedHp);
                    }
                }

                foreach (WorldCell cell in World.Cells)
                {
                    Mix(cell.IslandCount);
                    Mix(cell.ToxicTrail);
                    Mix((cell.Revealed ? 1 : 0) | (cell.Activated ? 2 : 0) | (cell.Cleared ? 4 : 0));
                    MixText(cell.OwnerId);
                }

                return hash;
            }
        }

        /* ---------- the end of a phase ---------- */

        public void EndPhase()
        {
            EndPhase(IslandGen.HumanPlayerId);
        }

        /// <summary>
        /// The end-turn button of that person. It makes them ready; once every
        /// person still in the game is ready the phase moves on. Ending the build
        /// phase opens the tax phase with every die rolled. Ending the tax phase
        /// pays every player and spins the slots. Ending the scout phase settles the
        /// flights and starts the battles; ending a battle hands its result back.
        /// </summary>
        public bool EndPhase(string playerId)
        {
            Player player = PersonInPlay(playerId);
            if (Stage != GameStage.Play || Outcome != null || player == null)
            {
                return false;
            }

            Seat seat = SeatOf(playerId);
            if (Phase == Phase.Clear && seat.Battle != null)
            {
                // The level has to end before the turn can: the way out is through the fog.
                if (seat.Battle.Status == SimStatus.Running && seat.BattleResult == null)
                {
                    return Refuse(playerId, "Бой ещё идёт: зачистите острова или уведите остров в туман");
                }

                seat.RuinedNow = FinishClearing(player);
            }

            seat.IsReady = true;
            if (WaitingFor().Count == 0)
            {
                Advance();
            }

            RaiseChanged();

            return true;
        }

        /// <summary>Every person is ready: the bots play their part and the next phase opens.</summary>
        private void Advance()
        {
            foreach (Seat seat in _seats.Values)
            {
                seat.IsReady = false;
            }

            switch (Phase)
            {
                case Phase.Build:
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

                    return;
                case Phase.Tax:
                    CollectTaxes();
                    // The scout phase opens: nothing is spent yet, and no flight has been made.
                    Phase = Phase.Scout;
                    foreach (Seat seat in _seats.Values)
                    {
                        seat.MovedThisTurn = false;
                        seat.LastTrailEvent = null;
                    }

                    return;
                case Phase.Scout:
                    foreach (Player rival in ActiveRivals())
                    {
                        PlayRivalScout(rival);
                    }

                    // An island that did not fly leaves its toxicity in the cell it sat in.
                    foreach (Player person in PeopleInPlay())
                    {
                        SettleExploration(person);
                    }

                    Phase = Phase.Clear;

                    foreach (Player rival in ActiveRivals())
                    {
                        PlayRivalClear(rival);
                    }

                    // A cell with no wild islands has nothing to clear: that person is done at once.
                    foreach (Player person in PeopleInPlay())
                    {
                        if (WorldRules.PendingIslands(World.Get(person.CellId), person) > 0)
                        {
                            EnterClearing(person);
                        }
                        else
                        {
                            SeatOf(person.Id).IsReady = true;
                        }
                    }

                    if (WaitingFor().Count == 0)
                    {
                        FinishTurn();
                    }

                    return;
                default:
                    FinishTurn();

                    return;
            }
        }

        /// <summary>Builds the level from the cell the island is over and levies the army.</summary>
        private void EnterClearing(Player player)
        {
            Seat seat = SeatOf(player.Id);
            WorldCell cell = World.Get(player.CellId);
            Rng rng = Rng.FromText(SeedOf(player, ":cleanup:" + Turn));
            // Only the people in the army go to battle. The idle and the workers stay home.
            // Every soldier is a militiaman: the stronghold recruits nothing else.
            List<Unit> roster = Units.BuildRoster(player.Army);
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

            seat.BattleResult = null;
            seat.Battle = CleanupSim.Create(level, Rng.HashSeed(SeedOf(player, ":cleanup-sim:" + Turn)));
        }

        /// <summary>
        /// Hands the result back to the island. The dead cost their people, the
        /// battle's damage stays on the buildings, the annexed hexes join the rim
        /// with the toxicity they carried, and the cell keeps only the islands that
        /// still stand. Returns the strongholds ruined in this battle, which skip
        /// this turn's repair.
        /// </summary>
        private HashSet<string> FinishClearing(Player player)
        {
            Seat seat = SeatOf(player.Id);
            HashSet<string> ruinedNow = new HashSet<string>();
            if (seat.Battle == null)
            {
                return ruinedNow;
            }

            seat.Battle.Retreat();
            CleanupResult result = seat.BattleResult ?? seat.Battle.Result();

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

            // Hexes destroyed in battle leave the island.
            player.Hexes.RemoveAll(hex => result.DestroyedHexIds.Contains(hex.Id));

            // The islands that joined in battle keep their exact battle coordinates.
            CleanupLevel.JoinAnnexed(player, result.Annexed);

            int peopleLost = 0;
            foreach (Unit unit in result.Lost)
            {
                peopleLost += unit.Upkeep;
            }

            // The fallen leave the army and the population; the survivors stay in the army.
            Population.LoseSoldiers(player, peopleLost);
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

                    if (!player.Researched.Contains(Techs.BossTechId))
                    {
                        player.Researched.Add(Techs.BossTechId);
                    }

                    Tell(player.Id, "Повелитель Мора повержен! Открыт «Центральный конвертер»");
                }
            }
            else if (cell != null && cell.Kind == CellKind.Island && result.TotalIslands > 0)
            {
                int standing = result.TotalIslands - result.ClearedIslands;
                cell.IslandCount = standing;
                cell.Cleared = standing == 0;
            }

            seat.LastBattleResult = result;
            seat.Battle = null;
            seat.BattleResult = null;

            return ruinedNow;
        }

        /// <summary>
        /// Closing the scout phase. An island that did not fly leaves its whole
        /// toxicity in the cell, and the trail may throw an event back at it.
        /// </summary>
        private void SettleExploration(Player player)
        {
            Seat seat = SeatOf(player.Id);
            WorldCell here = World.Get(player.CellId);
            if (seat.MovedThisTurn || here == null)
            {
                return;
            }

            // The trail takes the island's hex load, not the meter: the meter is the
            // island's own reckoning, the trail is what it leaves in the world.
            here.ToxicTrail += Tax.TotalToxicity(player);

            Rng rng = Rng.FromText(SeedOf(player, ":trail:" + Turn));
            TrailEvent trailEvent = TrailEvents.Roll(here.ToxicTrail, rng);
            if (trailEvent == null)
            {
                return;
            }

            seat.LastTrailEvent = trailEvent;
            ResourcePool pool = player.Resources;

            switch (trailEvent.Id)
            {
                case TrailEventId.Bandits:
                    pool[ResourceId.Stone] = (int)Math.Floor(pool[ResourceId.Stone] * 0.75);
                    pool[ResourceId.Wood] = (int)Math.Floor(pool[ResourceId.Wood] * 0.75);
                    break;
                case TrailEventId.Undead:
                    Population.LosePeople(player, 2);
                    break;
                case TrailEventId.Madness:
                    int moved = Math.Min(2, pool[ResourceId.Population]);
                    pool[ResourceId.Mad] += moved;
                    Population.LosePeople(player, moved);
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
                Tell(null, rival.Nickname + " победил Повелителя Мора");
            }
        }

        /// <summary>
        /// Opens the phase: every player's dice are rolled, and every bot makes its
        /// picks at once.
        /// </summary>
        private void StartTaxPhase()
        {
            foreach (Seat seat in _seats.Values)
            {
                seat.LastPayouts = null;
                seat.LastSlotSpin = null;
            }

            TaxPlans = new List<TaxPlan>();

            // The bots put their idle people to work before the dice roll.
            foreach (Player rival in Players)
            {
                if (!rival.IsHuman)
                {
                    Population.AutoStaff(rival);
                }
            }

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
                TaxPlan plan = TaxPlanOf(rival.Id);
                if (plan != null)
                {
                    plan.Collect(rival, TechEffects.None);
                    ToxicSlot.Roll(rival, Turn, Nickname);
                }
            }

            foreach (Player person in PeopleInPlay())
            {
                TaxPlan plan = TaxPlanOf(person.Id);
                if (plan != null)
                {
                    Seat seat = SeatOf(person.Id);
                    seat.LastPayouts = plan.Collect(person, EffectsOf(person.Id));
                    seat.LastSlotSpin = ToxicSlot.Roll(person, Turn, Nickname);
                }
            }

            TaxPlans = null;
        }

        /// <summary>The end of a turn: the free repair, the second end-of-game check, the next build phase.</summary>
        private void FinishTurn()
        {
            foreach (Player person in PeopleInPlay())
            {
                Seat seat = SeatOf(person.Id);
                StructureHp.RepairIsland(person, (ICollection<string>)seat.RuinedNow ?? new HashSet<string>());
                seat.RuinedNow = null;
                seat.IsReady = false;
            }

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
            Outcome = GameOver.Check(Players, HumanIds, Turn);

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
            Tell(null, rival.Nickname + " строит «" + converter.Label + "»");
        }

        /* ---------- helpers ---------- */

        /// <summary>
        /// The seed of a roll that belongs to one person. Seat 0 keeps the single
        /// player's seeds, so a single-player game rolls as it always did.
        /// </summary>
        private string SeedOf(Player player, string what)
        {
            return player.Id == IslandGen.HumanPlayerId ? Nickname + what : Nickname + what + ":" + player.Id;
        }

        /// <summary>A human seat, or <c>null</c> for a bot or an unknown id.</summary>
        private Player Person(string playerId)
        {
            Player player = PlayerOf(playerId);

            return player != null && player.IsHuman ? player : null;
        }

        /// <summary>A human seat that has not left the game.</summary>
        private Player PersonInPlay(string playerId)
        {
            Player player = Person(playerId);

            return player != null && !player.Eliminated ? player : null;
        }

        private bool IsInPlay(string playerId)
        {
            return PersonInPlay(playerId) != null;
        }

        private IEnumerable<Player> PeopleInPlay()
        {
            foreach (string id in HumanIds)
            {
                Player player = PersonInPlay(id);
                if (player != null)
                {
                    yield return player;
                }
            }
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

        /// <summary>A message for one person, or for everyone when the id is <c>null</c>. Seat 0's go to <see cref="Notice"/> too.</summary>
        private void Tell(string playerId, string message)
        {
            PlayerNotice?.Invoke(playerId, message);
            if (playerId == null || playerId == IslandGen.HumanPlayerId)
            {
                Notice?.Invoke(message);
            }
        }

        private bool Refuse(string playerId, string message)
        {
            Tell(playerId, message);

            return false;
        }

        /// <summary>
        /// Every change of the state ends here, so the population rule holds after
        /// any of them: the army and the workers never outnumber the population,
        /// and a hex that lost its building lost its workers too.
        /// </summary>
        private void RaiseChanged()
        {
            foreach (Player player in Players)
            {
                Population.Settle(player);
            }

            Changed?.Invoke();
        }
    }
}
