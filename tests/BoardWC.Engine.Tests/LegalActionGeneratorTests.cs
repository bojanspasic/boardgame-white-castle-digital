using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for LegalActionGenerator — one test per conditional branch.
/// All tests call LegalActionGenerator.Generate() directly with a constructed GameState.
/// </summary>
public class LegalActionGeneratorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State) MakeState(
        Phase phase = Phase.WorkerPlacement,
        Action<Player>? setupAlice = null)
    {
        var alice = new Player { Name = "Alice" };
        setupAlice?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = phase;
        return (alice, bob, state);
    }

    private static void GiveDie(Player player, BridgeColor color, int value) =>
        player.DiceInHand.Add(new Die(value, color));

    // ── Phase.Setup ───────────────────────────────────────────────────────────

    [Fact]
    public void Setup_ReturnsStartGameAction()
    {
        var (alice, _, state) = MakeState(Phase.Setup);
        var actions = LegalActionGenerator.Generate(alice.Id, state);
        var action  = Assert.Single(actions);
        Assert.IsType<StartGameAction>(action);
    }

    // ── Non-WorkerPlacement / non-Setup / non-SeedCard phases ─────────────────

    [Fact]
    public void EndOfRound_ReturnsEmptyList()
    {
        var (alice, _, state) = MakeState(Phase.EndOfRound);
        var actions = LegalActionGenerator.Generate(alice.Id, state);
        Assert.Empty(actions);
    }

    [Fact]
    public void GameOver_ReturnsEmptyList()
    {
        var (alice, _, state) = MakeState(Phase.GameOver);
        var actions = LegalActionGenerator.Generate(alice.Id, state);
        Assert.Empty(actions);
    }

    // ── Phase.SeedCardSelection ───────────────────────────────────────────────

    [Fact]
    public void SeedCardSelection_ActivePlayer_ReturnsSeedPairChoices()
    {
        var (alice, _, state) = MakeState(Phase.SeedCardSelection);
        state.SeedCardPairs.Add(new SeedCardPair(
            new SeedActionCard { Id = "a", ActionType = SeedActionType.PlayCastle,
                Back = new LanternChainGain(CardGainType.Food, 1) },
            new SeedResourceCard { Id = "b", Gains = Array.Empty<SeedResourceGain>().AsReadOnly(),
                Back = new LanternChainGain(CardGainType.Food, 1) }));

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        var action = Assert.Single(actions);
        Assert.IsType<ChooseSeedPairAction>(action);
    }

    [Fact]
    public void SeedCardSelection_InactivePlayer_ReturnsEmpty()
    {
        var (_, bob, state) = MakeState(Phase.SeedCardSelection);
        // Bob is at index 1; Alice (index 0) is active
        var actions = LegalActionGenerator.Generate(bob.Id, state);
        Assert.Empty(actions);
    }

    [Fact]
    public void SeedCardSelection_UnknownPlayer_ReturnsEmpty()
    {
        var (_, _, state) = MakeState(Phase.SeedCardSelection);
        var actions = LegalActionGenerator.Generate(Guid.NewGuid(), state);
        Assert.Empty(actions);
    }

    [Fact]
    public void SeedCardSelection_PendingAnyResource_ReturnsChooseResourceActions()
    {
        var (alice, _, state) = MakeState(Phase.SeedCardSelection,
            p => p.PendingAnyResourceChoices = 1);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Equal(3, actions.Count);
        Assert.All(actions, a => Assert.IsType<ChooseResourceAction>(a));
    }

    // ── WorkerPlacement early exits ───────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_UnknownPlayer_ReturnsEmpty()
    {
        var (_, _, state) = MakeState();
        var actions = LegalActionGenerator.Generate(Guid.NewGuid(), state);
        Assert.Empty(actions);
    }

    [Fact]
    public void WorkerPlacement_InactivePlayer_ReturnsEmpty()
    {
        var (_, bob, state) = MakeState();
        // Bob is index 1 — not active
        var actions = LegalActionGenerator.Generate(bob.Id, state);
        Assert.Empty(actions);
    }

    // ── PendingInfluenceGain ──────────────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingInfluenceGain_ReturnsInfluencePayChoices()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingInfluenceGain = 1);
        var actions = LegalActionGenerator.Generate(alice.Id, state);
        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.IsType<ChooseInfluencePayAction>(a));
    }

    // ── PendingAnyResourceChoices ─────────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingAnyResource_ReturnsChooseResourceActions()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingAnyResourceChoices = 1);
        var actions = LegalActionGenerator.Generate(alice.Id, state);
        Assert.Equal(3, actions.Count);
        Assert.All(actions, a => Assert.IsType<ChooseResourceAction>(a));
    }

    // ── PendingOutsideActivationSlot ──────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingOutsideSlot0_ReturnsFarmAndCastle()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingOutsideActivationSlot = 0);
        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Equal(2, actions.Count);
        var activations = actions.Cast<ChooseOutsideActivationAction>()
                                 .Select(a => a.Choice).ToList();
        Assert.Contains(OutsideActivation.Farm,   activations);
        Assert.Contains(OutsideActivation.Castle, activations);
    }

    [Fact]
    public void WorkerPlacement_PendingOutsideSlot1_ReturnsTGAndCastle()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingOutsideActivationSlot = 1);
        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Equal(2, actions.Count);
        var activations = actions.Cast<ChooseOutsideActivationAction>()
                                 .Select(a => a.Choice).ToList();
        Assert.Contains(OutsideActivation.TrainingGrounds, activations);
        Assert.Contains(OutsideActivation.Castle,          activations);
    }

    // ── PendingFarmActions ────────────────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingFarm_WithFarmersAndFood_OffersSkipAndPlaceFarmer()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingFarmActions = 1;
            p.FarmersAvailable   = 5;
            p.Resources          = new ResourceBag(Food: 10);
        });
        state.Board.SetupFarmingLands(state.Rng);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is FarmSkipAction);
        Assert.Contains(actions, a => a is PlaceFarmerAction);
    }

    [Fact]
    public void WorkerPlacement_PendingFarm_NoFarmers_OnlySkip()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingFarmActions = 1;
            p.FarmersAvailable   = 0;
        });
        state.Board.SetupFarmingLands(state.Rng);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        var action = Assert.Single(actions);
        Assert.IsType<FarmSkipAction>(action);
    }

    // ── PendingTrainingGroundsActions ─────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingTG_WithSoldiersAndIron_OffersSkipAndSoldierPlaces()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingTrainingGroundsActions = 1;
            p.SoldiersAvailable             = 5;
            p.Resources                     = new ResourceBag(Iron: 10);
        });
        state.Board.SetupTrainingGrounds(state.Rng);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is TrainingGroundsSkipAction);
        Assert.Contains(actions, a => a is TrainingGroundsPlaceSoldierAction);
    }

    [Fact]
    public void WorkerPlacement_PendingTG_NoSoldiers_OnlySkip()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingTrainingGroundsActions = 1;
            p.SoldiersAvailable             = 0;
        });
        state.Board.SetupTrainingGrounds(state.Rng);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        var action = Assert.Single(actions);
        Assert.IsType<TrainingGroundsSkipAction>(action);
    }

    // ── Castle pending ────────────────────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_CastlePlace_CourtiersAndCoins_OffersPlaceAndSkip()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastlePlaceRemaining = 1;
            p.CourtiersAvailable   = 1;
            p.Coins                = 5;
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is CastleSkipAction);
        Assert.Contains(actions, a => a is CastlePlaceCourtierAction);
    }

    [Fact]
    public void WorkerPlacement_CastlePlace_NoCourtiersOrCoins_OnlySkip()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastlePlaceRemaining = 1;
            p.CourtiersAvailable   = 0;
            p.Coins                = 0;
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is CastleSkipAction);
        Assert.DoesNotContain(actions, a => a is CastlePlaceCourtierAction);
    }

    // ── ValidAdvances ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidAdvances_Gate_Vi2_OffersGatePlus1Only()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersAtGate        = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 2); // >= 2 but < 5
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        // Gate+1 → 3 ground-floor room choices
        Assert.Equal(3, advances.Count);
        Assert.All(advances, a => Assert.Equal(CourtierPosition.Gate, a.From));
        Assert.All(advances, a => Assert.Equal(1, a.Levels));
    }

    [Fact]
    public void ValidAdvances_Gate_Vi5_OffersGatePlus1AndGatePlus2()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersAtGate        = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 5); // >= 5
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        // Gate+1 (3 rooms) + Gate+2 (2 rooms) = 5
        Assert.Equal(5, advances.Count);
        Assert.Equal(3, advances.Count(a => a.From == CourtierPosition.Gate && a.Levels == 1));
        Assert.Equal(2, advances.Count(a => a.From == CourtierPosition.Gate && a.Levels == 2));
    }

    [Fact]
    public void ValidAdvances_Gate_Vi0_OffersNoAdvances()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersAtGate        = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 0); // < 2
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Empty(advances);
    }

    [Fact]
    public void ValidAdvances_StewardFloor_Vi2_OffersGFPlus1()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersOnStewardFloor = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 2);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        // GF+1 → 2 mid-floor room choices
        Assert.Equal(2, advances.Count);
        Assert.All(advances, a => Assert.Equal(CourtierPosition.StewardFloor, a.From));
        Assert.All(advances, a => Assert.Equal(1, a.Levels));
    }

    [Fact]
    public void ValidAdvances_StewardFloor_Vi5_OffersGFPlus1AndGFPlus2()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersOnStewardFloor = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 5);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        // GF+1 (2 rooms) + GF+2 (top, room index -1) = 3
        Assert.Equal(3, advances.Count);
        Assert.Equal(2, advances.Count(a => a.Levels == 1));
        Assert.Equal(1, advances.Count(a => a.Levels == 2));
    }

    [Fact]
    public void ValidAdvances_DiplomatFloor_Vi2_OffersMFPlus1()
    {
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersOnDiplomatFloor    = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 2);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        var advance = Assert.Single(advances);
        Assert.Equal(CourtierPosition.DiplomatFloor, advance.From);
        Assert.Equal(1, advance.Levels);
    }

    // ── Die in hand: castle rooms ─────────────────────────────────────────────

    [Fact]
    public void DieInHand_CastleRoom_ColorMatch_OffersPlacement()
    {
        var (alice, _, state) = MakeState();
        GiveDie(alice, BridgeColor.Red, 6);
        state.Board.CastleFloors[0][0].AddToken(
            new Token(BridgeColor.Red, TokenResource.Food, false));

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a =>
            a is PlaceDieAction p && p.Target is CastleRoomTarget r && r.Floor == 0 && r.RoomIndex == 0);
    }

    [Fact]
    public void DieInHand_CastleRoom_NoToken_ExcludesRoom()
    {
        var (alice, _, state) = MakeState();
        GiveDie(alice, BridgeColor.Red, 6);
        // No tokens added → no castle room offered

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is PlaceDieAction p && p.Target is CastleRoomTarget);
    }

    [Fact]
    public void DieInHand_CastleRoom_DeltaNegative_CoinsAfford_OffersPlacement()
    {
        // Die value 1, room value 3 → delta = -2; 2 coins covers it
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 2);
        GiveDie(alice, BridgeColor.Red, 1);
        state.Board.CastleFloors[0][0].AddToken(
            new Token(BridgeColor.Red, TokenResource.Food, false));

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is CastleRoomTarget);
    }

    [Fact]
    public void DieInHand_CastleRoom_DeltaNegative_NoCoins_ExcludesRoom()
    {
        // Die value 1, room value 3 → delta = -2; 0 coins — can't afford
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        GiveDie(alice, BridgeColor.Red, 1);
        state.Board.CastleFloors[0][0].AddToken(
            new Token(BridgeColor.Red, TokenResource.Food, false));

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is PlaceDieAction p && p.Target is CastleRoomTarget);
    }

    // ── Die in hand: well ─────────────────────────────────────────────────────

    [Fact]
    public void DieInHand_Well_DeltaNonNegative_OffersWell()
    {
        // Well BaseValue = 1; die value 6 → delta = 5 >= 0
        var (alice, _, state) = MakeState();
        GiveDie(alice, BridgeColor.Red, 6);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is WellTarget);
    }

    [Fact]
    public void DieInHand_Well_DeltaNegative_CoinsAfford_OffersWell()
    {
        // Die value 0, well value 1 → delta = -1; 1 coin covers it
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 1);
        GiveDie(alice, BridgeColor.Red, 0);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is WellTarget);
    }

    [Fact]
    public void DieInHand_Well_DeltaNegative_NoCoins_ExcludesWell()
    {
        // Die value 0, well value 1 → delta = -1; 0 coins — can't afford
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        GiveDie(alice, BridgeColor.Red, 0);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is PlaceDieAction p && p.Target is WellTarget);
    }

    // ── Die in hand: outside slots ────────────────────────────────────────────

    [Fact]
    public void DieInHand_OutsideSlot_DeltaNonNegative_OffersSlot()
    {
        // OutsideSlot BaseValue = 5; die value 6 → delta = 1 >= 0
        var (alice, _, state) = MakeState();
        GiveDie(alice, BridgeColor.Red, 6);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is OutsideSlotTarget);
    }

    [Fact]
    public void DieInHand_OutsideSlot_DeltaNegative_CoinsAfford_OffersSlot()
    {
        // Die value 3, outside value 5 → delta = -2; 2 coins covers it
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 2);
        GiveDie(alice, BridgeColor.Red, 3);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is OutsideSlotTarget);
    }

    [Fact]
    public void DieInHand_OutsideSlot_DeltaNegative_NoCoins_ExcludesSlot()
    {
        // Die value 1, outside value 5 → delta = -4; 0 coins — can't afford
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        GiveDie(alice, BridgeColor.Red, 1);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is PlaceDieAction p && p.Target is OutsideSlotTarget);
    }

    // ── Die in hand: personal domain rows ─────────────────────────────────────

    private static PersonalDomainRow[] LoadPdRows() =>
        PersonalDomainRowConfig.Load()
            .Select(c => new PersonalDomainRow(c))
            .ToArray();

    [Fact]
    public void DieInHand_PersonalDomain_ColorMatch_OffersPlacement()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 10);
        alice.PersonalDomainRows = LoadPdRows();
        var row = alice.PersonalDomainRows[0];
        GiveDie(alice, row.Config.DieColor, 6);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is PersonalDomainTarget);
    }

    [Fact]
    public void DieInHand_PersonalDomain_AlreadyPlaced_ExcludesRow()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 10);
        alice.PersonalDomainRows = LoadPdRows();
        var row = alice.PersonalDomainRows[0];
        // Mark row as already occupied this round
        row.PlacedDie = new Die(6, row.Config.DieColor);
        GiveDie(alice, row.Config.DieColor, 5);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        // Row index 0 must not be offered (die already placed)
        Assert.DoesNotContain(actions, a =>
            a is PlaceDieAction p && p.Target is PersonalDomainTarget t && t.RowIndex == 0);
    }

    [Fact]
    public void DieInHand_PersonalDomain_ColorMismatch_ExcludesRow()
    {
        // Rows are (Red, Black, White) one each. Give a die whose color only
        // matches one row — the other two rows are excluded by color mismatch.
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 10);
        alice.PersonalDomainRows = LoadPdRows();

        // Give a Red die → only the Red row matches; Black/White rows hit the color-mismatch branch
        GiveDie(alice, BridgeColor.Red, 6);

        var actions = LegalActionGenerator.Generate(alice.Id, state);
        var pdActions = actions.OfType<PlaceDieAction>()
                               .Where(a => a.Target is PersonalDomainTarget)
                               .ToList();

        // Only rows whose DieColor == Red should appear
        foreach (var pa in pdActions)
        {
            var idx = ((PersonalDomainTarget)pa.Target).RowIndex;
            Assert.Equal(BridgeColor.Red, alice.PersonalDomainRows[idx].Config.DieColor);
        }
    }

    [Fact]
    public void DieInHand_PersonalDomain_DeltaNegative_CoinsAfford_OffersRow()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 20);
        alice.PersonalDomainRows = LoadPdRows();
        var row = alice.PersonalDomainRows[0];
        // Die value 0 → delta = 0 - compareValue < 0; many coins cover it
        GiveDie(alice, row.Config.DieColor, 0);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is PersonalDomainTarget);
    }

    [Fact]
    public void DieInHand_PersonalDomain_DeltaNegative_NoCoins_ExcludesRow()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        alice.PersonalDomainRows = LoadPdRows();
        var row = alice.PersonalDomainRows[0];
        // Die value 0 → delta = -compareValue; 0 coins — can't afford
        GiveDie(alice, row.Config.DieColor, 0);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is PlaceDieAction p && p.Target is PersonalDomainTarget);
    }

    // ── Bridge takes ──────────────────────────────────────────────────────────

    [Fact]
    public void BridgeTakes_RolledDice_ReturnsBridgeTakeActions()
    {
        var (alice, _, state) = MakeState();
        state.Board.RollAllDice(2, state.Rng);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is TakeDieFromBridgeAction);
    }

    [Fact]
    public void BridgeTakes_AlwaysIncludesPass()
    {
        var (alice, _, state) = MakeState();

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PassAction);
    }

    [Fact]
    public void BridgeTakes_NoDice_OnlyPass()
    {
        // Raw state: bridges have no dice (RollAllDice not called) → only Pass offered
        var (alice, _, state) = MakeState();

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        var action = Assert.Single(actions);
        Assert.IsType<PassAction>(action);
    }

    // ── PendingPersonalDomainRowChoice ────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingPersonalDomainRowChoice_OffersRowChoices()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingPersonalDomainRowChoice = true;
            p.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Equal(3, actions.Count); // one per personal domain row
        Assert.All(actions, a => Assert.IsType<ChoosePersonalDomainRowAction>(a));
    }

    [Fact]
    public void WorkerPlacement_PendingPersonalDomainRowChoice_ColorsMatchRows()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingPersonalDomainRowChoice = true;
            p.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);
        var colors  = actions.Cast<ChoosePersonalDomainRowAction>().Select(a => a.RowColor).ToList();

        Assert.Contains(BridgeColor.Red,   colors);
        Assert.Contains(BridgeColor.White, colors);
        Assert.Contains(BridgeColor.Black, colors);
    }

    // ── PendingNewCardActivation ──────────────────────────────────────────────

    [Fact]
    public void WorkerPlacement_PendingNewCardActivation_OffersSkipAndAffordableFields()
    {
        var gains   = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var afCost  = new[] { new CardCostItem(CardCostType.Coin, 2) }.AsReadOnly();
        var fields  = new CardField[]
        {
            new GainCardField(gains),                   // always affordable (gain, no cost)
            new ActionCardField("Play castle", afCost), // costs 2 coins
        };
        var card = new RoomCard("pending-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingNewCardActivation = card;
            p.Coins = 5; // can afford the 2-coin cost
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        // Skip (fi=-1) + both fields
        Assert.Contains(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == -1);
        Assert.Contains(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == 0);
        Assert.Contains(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == 1);
    }

    [Fact]
    public void WorkerPlacement_PendingNewCardActivation_UnaffordableField_Excluded()
    {
        var afCost = new[] { new CardCostItem(CardCostType.Coin, 5) }.AsReadOnly();
        var fields = new CardField[]
        {
            new ActionCardField("Play castle", afCost), // costs 5 coins
        };
        var card = new RoomCard("pending-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingNewCardActivation = card;
            p.Coins = 0; // cannot afford
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        // Only skip offered
        Assert.Contains(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == -1);
        Assert.DoesNotContain(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == 0);
    }

    [Fact]
    public void WorkerPlacement_PendingNewCardActivation_DaimyoSealCost_Affordable_Offered()
    {
        var sealCost = new[] { new CardCostItem(CardCostType.DaimyoSeal, 2) }.AsReadOnly();
        var fields   = new CardField[] { new ActionCardField("Play castle", sealCost) };
        var card     = new RoomCard("seal-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingNewCardActivation = card;
            p.DaimyoSeals = 3; // >= 2 required
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == 0);
    }

    [Fact]
    public void WorkerPlacement_PendingNewCardActivation_DaimyoSealCost_Unaffordable_Excluded()
    {
        var sealCost = new[] { new CardCostItem(CardCostType.DaimyoSeal, 3) }.AsReadOnly();
        var fields   = new CardField[] { new ActionCardField("Play castle", sealCost) };
        var card     = new RoomCard("seal-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingNewCardActivation = card;
            p.DaimyoSeals = 1; // < 3 required
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is ChooseNewCardFieldAction cn && cn.FieldIndex == 0);
    }

    // ── PendingCastleCardFieldFilter ──────────────────────────────────────────

    private static void SetupCastleWithCard(GameState state, int floor, int room, RoomCard card, BridgeColor tokenColor)
    {
        var placeholder = state.Board.GetCastleRoom(floor, room);
        placeholder.AddToken(new Token(tokenColor, TokenResource.Food));
        placeholder.SetCard(card);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_Red_OffersRedRoomsOnly()
    {
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var card   = new RoomCard("red-card", new CardField[] { new GainCardField(gains) }.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "Red");
        SetupCastleWithCard(state, 0, 0, card, BridgeColor.Red);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.RoomIndex == 0);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_Red_ExcludesNonRedRooms()
    {
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var card   = new RoomCard("black-card", new CardField[] { new GainCardField(gains) }.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "Red");
        SetupCastleWithCard(state, 0, 1, card, BridgeColor.Black); // Black token, not Red

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.RoomIndex == 1);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_Black_OffersBlackRooms()
    {
        var gains = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var card  = new RoomCard("blk-card", new CardField[] { new GainCardField(gains) }.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "Black");
        SetupCastleWithCard(state, 0, 0, card, BridgeColor.Black);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.RoomIndex == 0);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_White_OffersWhiteRooms()
    {
        var gains = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var card  = new RoomCard("wht-card", new CardField[] { new GainCardField(gains) }.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "White");
        SetupCastleWithCard(state, 0, 0, card, BridgeColor.White);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.RoomIndex == 0);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_GainOnly_ExcludesActionFields()
    {
        var afCost  = new[] { new CardCostItem(CardCostType.Coin, 0) }.AsReadOnly();
        var fields  = new CardField[]
        {
            new GainCardField(new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly()),
            new ActionCardField("Play castle", afCost),
        };
        var card = new RoomCard("mixed-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingCastleCardFieldFilter = "GainOnly";
            p.Coins = 5;
        });
        SetupCastleWithCard(state, 0, 0, card, BridgeColor.Red);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        // Field 0 (GainCardField) should be offered
        Assert.Contains(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.FieldIndex == 0);
        // Field 1 (ActionCardField) should be excluded by GainOnly filter
        Assert.DoesNotContain(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == 0 && cc.FieldIndex == 1);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_AlwaysOffersSkip()
    {
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "Red");

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is ChooseCastleCardFieldAction cc && cc.Floor == -1);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_RoomWithNoCard_SkipsRoom()
    {
        // Room has a Red token but no card set
        var (alice, _, state) = MakeState(setupAlice: p => p.PendingCastleCardFieldFilter = "Red");
        state.Board.GetCastleRoom(0, 0).AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        // No SetCard call → card is null → room is skipped

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        // Only skip offered (no room-specific choices since card is null)
        Assert.DoesNotContain(actions, a => a is ChooseCastleCardFieldAction cc
            && cc.Floor == 0 && cc.RoomIndex == 0 && cc.FieldIndex >= 0);
    }

    [Fact]
    public void PendingCastleCardFieldFilter_UnaffordableField_Excluded()
    {
        var afCost = new[] { new CardCostItem(CardCostType.Coin, 10) }.AsReadOnly();
        var fields = new CardField[] { new ActionCardField("Play castle", afCost) };
        var card   = new RoomCard("costly-card", fields.AsReadOnly());

        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.PendingCastleCardFieldFilter = "Red";
            p.Coins = 0; // cannot afford 10-coin action
        });
        SetupCastleWithCard(state, 0, 0, card, BridgeColor.Red);

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is ChooseCastleCardFieldAction cc
            && cc.Floor == 0 && cc.FieldIndex == 0);
    }

    // ── ValidAdvances — vi=1 boundary (kills vi>=1 mutation) ─────────────────

    [Fact]
    public void ValidAdvances_Gate_Vi1_OffersNoAdvances()
    {
        // vi=1 < 2 → no advances from Gate (distinguishes vi>=2 from vi>=1)
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersAtGate        = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 1);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Empty(advances);
    }

    [Fact]
    public void ValidAdvances_StewardFloor_Vi1_OffersNoAdvances()
    {
        // vi=1 < 2 → no advances from StewardFloor
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining  = 1;
            p.CourtiersOnStewardFloor = 1;
            p.Resources               = new ResourceBag(MotherOfPearls: 1);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Empty(advances);
    }

    [Fact]
    public void ValidAdvances_DiplomatFloor_Vi1_OffersNoAdvances()
    {
        // vi=1 < 2 → no advances from DiplomatFloor
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining   = 1;
            p.CourtiersOnDiplomatFloor = 1;
            p.Resources                = new ResourceBag(MotherOfPearls: 1);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Empty(advances);
    }

    // ── ValidAdvances — vi=4 boundary (kills vi>=4 mutation for the >=5 check) ─

    [Fact]
    public void ValidAdvances_Gate_Vi4_OffersGatePlus1Only()
    {
        // vi=4: >= 2 (Gate+1 offered), < 5 (Gate+2 NOT offered)
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining = 1;
            p.CourtiersAtGate        = 1;
            p.Resources              = new ResourceBag(MotherOfPearls: 4);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Equal(3, advances.Count); // Gate+1 only (3 ground rooms)
        Assert.All(advances, a => Assert.Equal(1, a.Levels));
    }

    [Fact]
    public void ValidAdvances_StewardFloor_Vi4_OffersSFPlus1Only()
    {
        // vi=4: >= 2 (SF+1 offered), < 5 (SF+2 NOT offered)
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastleAdvanceRemaining  = 1;
            p.CourtiersOnStewardFloor = 1;
            p.Resources               = new ResourceBag(MotherOfPearls: 4);
        });

        var advances = LegalActionGenerator.Generate(alice.Id, state)
            .OfType<CastleAdvanceCourtierAction>().ToList();

        Assert.Equal(2, advances.Count); // SF+1 only (2 mid rooms)
        Assert.All(advances, a => Assert.Equal(1, a.Levels));
    }

    // ── CastlePlaceCourtier — Coins=2 boundary (coins >= 2) ──────────────────

    [Fact]
    public void WorkerPlacement_CastlePlace_Coins1_DoesNotOfferPlace()
    {
        // Coins=1 < 2 → CastlePlaceCourtierAction not offered
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastlePlaceRemaining = 1;
            p.CourtiersAvailable   = 1;
            p.Coins                = 1;
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.DoesNotContain(actions, a => a is CastlePlaceCourtierAction);
    }

    [Fact]
    public void WorkerPlacement_CastlePlace_Coins2_OffersPlace()
    {
        // Coins=2 == 2 → exactly on boundary → CastlePlaceCourtierAction offered
        var (alice, _, state) = MakeState(setupAlice: p =>
        {
            p.CastlePlaceRemaining = 1;
            p.CourtiersAvailable   = 1;
            p.Coins                = 2;
        });

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is CastlePlaceCourtierAction);
    }

    // ── Die in hand: personal domain — delta=0 with 0 coins ──────────────────

    [Fact]
    public void DieInHand_PersonalDomain_DeltaZero_CoinsZero_OffersRow()
    {
        // die == compareValue → delta=0 → row offered even with 0 coins
        // This distinguishes "delta >= 0" from "delta > 0" mutations
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        alice.PersonalDomainRows = LoadPdRows();
        var row = alice.PersonalDomainRows[0];
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue); // delta = 0

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is PersonalDomainTarget t && t.RowIndex == 0);
    }

    // ── Die in hand: well — delta=0 exact boundary ───────────────────────────

    [Fact]
    public void DieInHand_Well_DeltaZero_CoinsZero_OffersWell()
    {
        // Well baseValue=1; die=1 → delta=0 → well offered even with 0 coins
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 0);
        GiveDie(alice, BridgeColor.Red, 1); // delta = 1-1 = 0

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is WellTarget);
    }

    // ── Die in hand: outside slot — exact coin boundary ───────────────────────

    [Fact]
    public void DieInHand_OutsideSlot_DeltaNegative_ExactCoins_OffersSlot()
    {
        // Outside baseValue=5; die=4 → delta=-1; coins=1 → exactly enough
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 1);
        GiveDie(alice, BridgeColor.Red, 4); // delta = 4-5 = -1

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is OutsideSlotTarget);
    }

    [Fact]
    public void DieInHand_CastleRoom_DeltaNegative_ExactCoins_OffersPlacement()
    {
        // Castle floor 0 baseValue=3; die=2 → delta=-1; coins=1 → exactly enough
        var (alice, _, state) = MakeState(setupAlice: p => p.Coins = 1);
        GiveDie(alice, BridgeColor.Red, 2); // delta = 2-3 = -1
        state.Board.CastleFloors[0][0].AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        var actions = LegalActionGenerator.Generate(alice.Id, state);

        Assert.Contains(actions, a => a is PlaceDieAction p && p.Target is CastleRoomTarget);
    }
}
