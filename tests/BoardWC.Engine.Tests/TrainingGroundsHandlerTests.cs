using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for TrainingGroundsHandler — validation paths and ApplyNamedAction cases.
/// </summary>
public class TrainingGroundsHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State, TrainingGroundsHandler Handler)
        MakeState(Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice" };
        setup?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        state.Board.SetupTrainingGrounds(state.Rng); // loads the TG pool and assigns areas for this round
        return (alice, bob, state, new TrainingGroundsHandler());
    }

    /// Build a state whose training grounds Area[areaIndex] has been manually configured
    /// with the given resource gains and/or action description, with the specified iron cost.
    private static (Player Alice, GameState State, TrainingGroundsHandler Handler)
        MakeStateWithCustomArea(
            int areaIndex,
            int ironCost,
            Action<TrainingGroundsArea>? configureArea = null,
            int playerIron = 10)
    {
        var alice = new Player
        {
            Name      = "Alice",
            Resources = new ResourceBag(Iron: playerIron),
        };
        alice.Pending.TrainingGroundsActions = 1;
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        // Load training grounds, then override the specific area
        state.Board.SetupTrainingGrounds(state.Rng);

        // Manually override the area we want to test — reassign resource/action sides
        var area = state.Board.TrainingGrounds.Areas[areaIndex];

        // Reset the area by clearing its current config and applying our own
        // We create a fresh area inline by calling AssignResourceSide/AssignActionSide via reflection —
        // but since these are accessible (internal) just call them directly:
        configureArea?.Invoke(area);

        return (alice, state, new TrainingGroundsHandler());
    }

    // ── Validation failure paths ──────────────────────────────────────────────

    [Fact]
    public void Validate_AreaIndexNegative_Fails()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 1;
            p.Resources = new ResourceBag(Iron: 10);
        });

        var result = handler.Validate(
            new TrainingGroundsPlaceSoldierAction(alice.Id, -1), state);

        Assert.False(result.IsValid);
        Assert.Contains("Area index", result.Reason);
    }

    [Fact]
    public void Validate_AreaIndexTooLarge_Fails()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 1;
            p.Resources = new ResourceBag(Iron: 10);
        });

        var result = handler.Validate(
            new TrainingGroundsPlaceSoldierAction(alice.Id, 3), state);

        Assert.False(result.IsValid);
        Assert.Contains("Area index", result.Reason);
    }

    [Fact]
    public void Validate_NoSoldiersAvailable_Fails()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 1;
            p.SoldiersAvailable             = 0;
            p.Resources                     = new ResourceBag(Iron: 10);
        });

        var result = handler.Validate(
            new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("soldiers", result.Reason);
    }

    [Fact]
    public void Validate_NoPendingAction_Fails()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 0;
        });

        var result = handler.Validate(
            new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("pending training", result.Reason);
    }

    [Fact]
    public void Validate_InsufficientIron_Fails()
    {
        // Area[0] always costs 1 iron. Give the player 0 iron.
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 1;
            p.Resources                     = new ResourceBag(Iron: 0);
        });

        var result = handler.Validate(
            new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("iron", result.Reason);
    }

    // ── Skip action ───────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Skip_ClearsPendingActions()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Pending.TrainingGroundsActions = 1;
        });

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsSkipAction(alice.Id), state, events);

        Assert.Equal(0, alice.Pending.TrainingGroundsActions);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal(state.GameId, evt.GameId);
        Assert.Equal(alice.Id,     evt.PlayerId);
        Assert.Equal(-1,           evt.AreaIndex); // -1 = skipped
        Assert.Equal(0,            evt.IronSpent);
        Assert.Equal(0,            evt.CoinsGained);
        Assert.Equal(0,            evt.SealsGained);
        Assert.Equal(0,            evt.LanternGained);
        Assert.Null(evt.ActionTriggered);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    // ── ApplyNamedAction — resource gain paths ────────────────────────────────

    [Fact]
    public void Apply_Area0_ResourceGain_Food_GrantsFood()
    {
        // Area 0 always has a resource side; override it to Food:1
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 0,
            ironCost:  1,
            area => area.AssignResourceSide(new[] { new TgGainItem("Food", 2) }.AsReadOnly()),
            playerIron: 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        Assert.Equal(2, alice.Resources.Food);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal(0, evt.AreaIndex);
        Assert.Equal(1, evt.IronSpent);
        Assert.Equal(2, evt.ResourcesGained.Food);
        Assert.Equal(0, evt.CoinsGained);
        Assert.Equal(0, evt.SealsGained);
    }

    [Fact]
    public void Apply_Area0_ResourceGain_Iron_GrantsIron()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 0,
            ironCost:  1,
            area => area.AssignResourceSide(new[] { new TgGainItem("Iron", 3) }.AsReadOnly()),
            playerIron: 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        // Started with 5 iron, spent 1 for area, gained 3 → net 7
        Assert.Equal(7, alice.Resources.Iron);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal(3, evt.ResourcesGained.Iron);
    }

    [Fact]
    public void Apply_Area0_ResourceGain_MotherOfPearls_GrantsMotherOfPearls()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 0,
            ironCost:  1,
            area => area.AssignResourceSide(new[] { new TgGainItem("MotherOfPearls", 2) }.AsReadOnly()),
            playerIron: 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        Assert.Equal(2, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void Apply_Area0_ResourceGain_Coin_GrantsCoin()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 0,
            ironCost:  1,
            area => area.AssignResourceSide(new[] { new TgGainItem("Coin", 3) }.AsReadOnly()),
            playerIron: 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        Assert.Equal(3, alice.Coins);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal(3, evt.CoinsGained);
    }

    [Fact]
    public void Apply_Area0_ResourceGain_DaimyoSeal_GrantsSeal()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 0,
            ironCost:  1,
            area => area.AssignResourceSide(new[] { new TgGainItem("DaimyoSeal", 1) }.AsReadOnly()),
            playerIron: 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        Assert.Equal(1, alice.DaimyoSeals);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal(1, evt.SealsGained);
    }

    // ── ApplyNamedAction — all 5 named action cases ───────────────────────────

    [Fact]
    public void Apply_NamedAction_PlayCastle_SetsCastlePending()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 1,
            ironCost:  3,
            area => area.AssignActionSide("Play castle"),
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 1), state, events);

        Assert.Equal(1, alice.Pending.CastlePlaceRemaining);
        Assert.Equal(1, alice.Pending.CastleAdvanceRemaining);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal("Play castle", evt.ActionTriggered);
        Assert.Equal(1,             evt.AreaIndex);
        Assert.Equal(3,             evt.IronSpent);
    }

    [Fact]
    public void Apply_NamedAction_Gain3Coins_GrantsCoins()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 1,
            ironCost:  3,
            area => area.AssignActionSide("Gain 3 coins"),
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 1), state, events);

        Assert.Equal(3, alice.Coins);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal("Gain 3 coins", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_NamedAction_Gain1DaimyoSeal_GrantsSeal()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 1,
            ironCost:  3,
            area => area.AssignActionSide("Gain 1 daimyo seal"),
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 1), state, events);

        Assert.Equal(1, alice.DaimyoSeals);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal("Gain 1 daimyo seal", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_NamedAction_Gain1Lantern_GrantsLantern()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 1,
            ironCost:  3,
            area => area.AssignActionSide("Gain 1 lantern"),
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 1), state, events);

        Assert.Equal(1, alice.LanternScore);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal("Gain 1 lantern", evt.ActionTriggered);
        Assert.Equal(1,                evt.LanternGained);
    }

    [Fact]
    public void Apply_NamedAction_PlayFarm_SetsFarmPending()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 1,
            ironCost:  3,
            area => area.AssignActionSide("Play farm"),
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 1), state, events);

        Assert.Equal(1, alice.Pending.FarmActions);

        var evt = Assert.Single(events.OfType<TrainingGroundsUsedEvent>());
        Assert.Equal("Play farm", evt.ActionTriggered);
    }

    // ── DaimyoSeal cap ────────────────────────────────────────────────────

    [Fact]
    public void Apply_NamedAction_SealCappedAtFive()
    {
        var alice = new Player
        {
            Name        = "Alice",
            Resources   = new ResourceBag(Iron: 10),
            DaimyoSeals = 5, // already at cap
        };
        alice.Pending.TrainingGroundsActions = 1;
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        state.Board.SetupTrainingGrounds(state.Rng);

        var area = state.Board.TrainingGrounds.Areas[0];
        area.AssignResourceSide(new[] { new TgGainItem("DaimyoSeal", 2) }.AsReadOnly());

        var handler = new TrainingGroundsHandler();
        var events  = new List<IDomainEvent>();

        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 0), state, events);

        // Seals capped at 5
        Assert.Equal(5, alice.DaimyoSeals);
    }

    // ── Soldier counter and iron spending ─────────────────────────────────────

    [Fact]
    public void Apply_Area2_ReducesSoldiersAndSpendIron()
    {
        var (alice, state, handler) = MakeStateWithCustomArea(
            areaIndex: 2,
            ironCost:  5,
            area =>
            {
                area.AssignResourceSide(new[] { new TgGainItem("Food", 1) }.AsReadOnly());
                area.AssignActionSide("Play farm");
            },
            playerIron: 10);

        var events = new List<IDomainEvent>();
        handler.Apply(new TrainingGroundsPlaceSoldierAction(alice.Id, 2), state, events);

        Assert.Equal(4,  alice.SoldiersAvailable); // 5 - 1
        Assert.Equal(5,  alice.Resources.Iron);     // 10 - 5
        Assert.Equal(1,  alice.Pending.FarmActions);
        Assert.Equal(1,  alice.Resources.Food);
    }
}
